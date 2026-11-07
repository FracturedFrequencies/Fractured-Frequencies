using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace FracturedFrequencies.Pattern {
    /// <summary>
    /// A real Chladni figure of a round plate, drawn with particles.
    ///
    /// Physics (clamped circular plate, polar coordinates r = 0..1, theta):
    ///     mode (n, m):  w(r, theta) = J_n(z_nm * r) * cos(n * theta)
    ///     J_n  = Bessel function of the first kind, z_nm = its m-th zero, so the rim (r = 1) is always
    ///     a nodal line -> the figure is closed by a ring.
    ///     The mode's frequency grows with z_nm^2, so every note picks the mode whose z_nm fits its pitch.
    ///     A chord vibrates all its notes' modes at once; the sum gives the chord's figure.
    /// - n = number of nodal diameters, m = number of nodal circles (including the rim).
    /// - Like sand, particles are shaken where the plate moves and slide onto the nodal lines (w = 0).
    ///   On a chord change the modes cross-fade and the sand flows into the new figure.
    ///
    /// Usage: add this script to an empty GameObject and press Play. It builds its own particle system
    /// (child "ChladniParticles"), material and sound at runtime.
    /// The plate lies in the local XZ plane, centred on this transform (rotation X = -90 faces the
    /// default camera of a new scene). Keys 1..9 pick a chord; gameplay code calls SetChord / SetNotes.
    /// </summary>
    public class ChladniField : MonoBehaviour {
        [Serializable]
        public class Chord {
            public string name = "C";
            [Tooltip("MIDI note numbers (60 = C4, 69 = A4 = 440 Hz). The first note is the root.")]
            public int[] notes = { 60, 64, 67 };
            public Color tint = Color.white;

            public Chord() { }
            public Chord(string name, Color tint, params int[] notes) { this.name = name; this.tint = tint; this.notes = notes; }
        }

        [Header("Chords")]
        [SerializeField] private List<Chord> chords = DefaultChords();
        [SerializeField] private int startChord = 0;
        [Tooltip("Seconds to cross-fade from one figure to the next.")]
        [SerializeField] private float morphTime = 1.2f;
        [Tooltip("Number keys 1..9 select chords (testing).")]
        [SerializeField] private bool numberKeysSelectChord = true;
        [SerializeField] private bool playChordSound = true;
        [SerializeField, Range(0f, 1f)] private float chordVolume = 0.35f;

        [Header("Plate")]
        [Tooltip("Radius of the plate in metres.")]
        [SerializeField] private float radius = 4.5f;
        [SerializeField, Range(500, 20000)] private int particleCount = 6000;
        [Tooltip("Note that excites the reference mode. Higher notes pick more complex modes.")]
        [SerializeField] private int referenceNote = 48;
        [Tooltip("Bessel zero z_nm used for the reference note. Larger = more complex figures overall.")]
        [SerializeField, Range(3f, 16f)] private float referenceZ = 8f;
        [Tooltip("Slow rotation of the whole figure (radians per second).")]
        [SerializeField] private float spin = 0.05f;

        [Header("Sand motion (plate radius = 1)")]
        [Tooltip("How fast the sand slides onto the nodal lines.")]
        [SerializeField] private float attraction = 6f;
        [SerializeField] private float maxSpeed = 1.2f;
        [Tooltip("Shaking where the plate vibrates.")]
        [SerializeField] private float shake = 0.7f;
        [Tooltip("Tiny shimmer that remains on the lines.")]
        [SerializeField] private float lineShimmer = 0.012f;
        [Tooltip("Extra shake right after a chord change; decays with rippleDecay.")]
        [SerializeField] private float chordKick = 1f;
        [Tooltip("Fraction of particles re-dropped at random per second, keeps the lines fed.")]
        [SerializeField] private float respawnRate = 0.03f;

        [Header("Line waves")]
        [Tooltip("The nodal lines wobble slightly (plate units).")]
        [SerializeField] private float lineWobble = 0.008f;
        [SerializeField] private float lineWobbleSpeed = 0.8f;
        [Tooltip("Height of the circular wave that runs outward over the plate (metres).")]
        [SerializeField] private float rippleHeight = 0.1f;
        [Tooltip("Extra wave height right after a chord change.")]
        [SerializeField] private float chordRippleHeight = 0.5f;
        [SerializeField] private float rippleDecay = 1.2f;
        [SerializeField] private float rippleWavelength = 2.5f;
        [SerializeField] private float rippleSpeed = 3f;
        [Tooltip("Sand that is not on a line bounces up and down (metres).")]
        [SerializeField] private float sandBounce = 0.12f;

        [Header("Look")]
        [Tooltip("Optional. Leave empty to create a soft-dot URP particle material at runtime.")]
        public Material particleMaterial;
        [SerializeField] private float particleSize = 0.06f;
        [SerializeField] private Color lineColor = new Color(0.85f, 0.95f, 1f, 1f);
        [SerializeField] private Color offLineColor = new Color(0.55f, 0.65f, 1f, 0.06f);
        [Tooltip("Higher = thinner, sharper lines.")]
        [SerializeField] private float lineSharpness = 12f;

        public event Action<int, Chord> ChordChanged;
        public int CurrentChordIndex { get; private set; } = -1;
        public IReadOnlyList<Chord> Chords => chords;

        // ------------------------------------------------------------------ plate modes

        private const int MaxN = 10;          // most nodal diameters
        private const double MaxZ = 22.5;     // highest Bessel zero used
        private const int TableSize = 512;    // radial samples per mode

        private struct ModeDef { public int n, m; public float z; }
        private static List<ModeDef> modeList; // all (n, m) modes sorted by z

        private class Mode {
            public int n, m;
            public float[] R, dR, Rr;          // J_n(z r), d/dr, J_n(z r) / r   (normalised to peak 1)
            public float weight, target;
        }
        private readonly List<Mode> modes = new List<Mode>();
        private static readonly Dictionary<long, Mode> tableCache = new Dictionary<long, Mode>();

        // ------------------------------------------------------------------ runtime state

        private ParticleSystem ps;
        private ParticleSystem.Particle[] parts;
        private float[] px, py, bouncePhase;
        private uint rng = 0x9E3779B9u;
        private float lastChangeTime = -100f;
        private Color tint = Color.white, targetTint = Color.white;
        private AudioSource audioSource;
        private readonly Dictionary<int, AudioClip> chordClips = new Dictionary<int, AudioClip>();

        // per-frame copy of the active modes
        private Mode[] active = new Mode[8];
        private float[] activeW = new float[8];
        private int activeCount, activeMaxN;
        private float invWeight, rotCos = 1f, rotSin;
        private readonly float[] cosN = new float[MaxN + 1], sinN = new float[MaxN + 1];

        // ------------------------------------------------------------------ public API

        /// <summary>Switch to chord number index (wraps around).</summary>
        public void SetChord(int index) {
            if (chords == null || chords.Count == 0) return;
            index = ((index % chords.Count) + chords.Count) % chords.Count;
            var chord = chords[index];
            CurrentChordIndex = index;
            ApplyNotes(chord.notes, chord.tint);
            if (playChordSound) PlayChord(index, chord.notes);
            ChordChanged?.Invoke(index, chord);
        }

        public void NextChord() => SetChord(CurrentChordIndex + 1);

        /// <summary>Show an arbitrary set of notes (MIDI numbers), e.g. from a puzzle.</summary>
        public void SetNotes(Color chordTint, params int[] midiNotes) {
            CurrentChordIndex = -1;
            ApplyNotes(midiNotes, chordTint);
        }

        // ------------------------------------------------------------------ setup

        private void Reset() => chords = DefaultChords();

        private void Awake() {
            BuildModeList();

            // Build the particle system as a child of this (empty) object.
            var child = new GameObject("ChladniParticles");
            child.transform.SetParent(transform, false);
            child.SetActive(false); // configure before it can play
            ps = child.AddComponent<ParticleSystem>();
            if (particleMaterial == null) particleMaterial = CreateDefaultMaterial(CreateDotTexture(64));
            ConfigureParticleSystem(ps, particleCount, particleMaterial);
            child.SetActive(true);

            px = new float[particleCount];
            py = new float[particleCount];
            bouncePhase = new float[particleCount];
            for (int i = 0; i < particleCount; i++) {
                RandomInDisk(out px[i], out py[i]);
                bouncePhase[i] = Rand01() * 6.2832f;
            }

            audioSource = GetComponent<AudioSource>();
            if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 0f;
        }

        private void Start() {
            ps.Play();
            ps.Emit(particleCount);
            parts = new ParticleSystem.Particle[particleCount];
            ps.GetParticles(parts);

            bool sound = playChordSound;
            playChordSound = false; // no sound on scene start
            SetChord(startChord);
            playChordSound = sound;
            foreach (var m in modes) m.weight = m.target; // start fully formed
            tint = targetTint;
            lastChangeTime = -100f;
        }

        private static void ConfigureParticleSystem(ParticleSystem system, int count, Material material) {
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = system.main;
            main.playOnAwake = false;
            main.loop = true;
            main.duration = 1f;
            main.maxParticles = count;
            main.startLifetime = 1e6f;
            main.startSpeed = 0f;
            main.startSize = 0.06f;
            main.gravityModifier = 0f;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;

            var emission = system.emission;
            emission.enabled = false;
            var shape = system.shape;
            shape.enabled = false;

            var renderer = system.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            if (material != null) renderer.sharedMaterial = material;
        }

        // ------------------------------------------------------------------ update

        private void Update() {
            if (!numberKeysSelectChord || Time.timeScale <= 0f || chords == null) return;
            int count = Mathf.Min(9, chords.Count);
            for (int i = 0; i < count; i++) {
#if ENABLE_INPUT_SYSTEM
                var kb = Keyboard.current;
                if (kb != null && kb[(Key)((int)Key.Digit1 + i)].wasPressedThisFrame) SetChord(i);
#else
                if (Input.GetKeyDown(KeyCode.Alpha1 + i)) SetChord(i);
#endif
            }
        }

        private void LateUpdate() {
            if (parts == null) return;
            float dt = Mathf.Min(Time.deltaTime, 1f / 30f);
            float t = Time.time;

            UpdateModes(dt);
            rotCos = Mathf.Cos(spin * t);
            rotSin = Mathf.Sin(spin * t);
            tint = Color.Lerp(tint, targetTint, 1f - Mathf.Exp(-dt * 3f / Mathf.Max(0.05f, morphTime)));

            float decay = Mathf.Exp(-(t - lastChangeTime) / Mathf.Max(0.05f, rippleDecay));
            float rippleAmp = rippleHeight + chordRippleHeight * decay;
            float kick = chordKick * decay;
            float pull = Mathf.Min(1f, attraction * dt);
            float maxStep = maxSpeed * dt;
            float rippleK = 6.2832f / Mathf.Max(0.1f, rippleWavelength);
            float respawnP = respawnRate * dt;
            float wob = lineWobble, wobPhase = t * lineWobbleSpeed;

            Color onCol = lineColor * tint, offCol = offLineColor * tint;
            onCol.a = lineColor.a; offCol.a = offLineColor.a;

            for (int i = 0; i < particleCount; i++) {
                float x = px[i], y = py[i];

                // Field at a slightly warped position, so the lines wobble.
                float wx0 = x + wob * Mathf.Sin(y * 6.2832f + wobPhase);
                float wy0 = y + wob * Mathf.Sin(x * 6.2832f + wobPhase * 1.3f + 1.7f);
                Eval(wx0, wy0, out float f, out float gx, out float gy);

                // Newton step toward f = 0 (the nearest nodal line), clamped.
                float s = -f / (gx * gx + gy * gy + 0.5f) * pull;
                float dx = gx * s, dy = gy * s;
                float len2 = dx * dx + dy * dy;
                if (len2 > maxStep * maxStep) {
                    float sc = maxStep / Mathf.Sqrt(len2);
                    dx *= sc; dy *= sc;
                }

                // Shaking: strong where the plate moves, tiny on the lines.
                float a = Mathf.Min(Mathf.Abs(f) * 2f, 1f);
                float jitter = (lineShimmer + (shake + kick) * a) * dt;
                x += dx + RandSigned() * jitter;
                y += dy + RandSigned() * jitter;

                // Keep inside the plate (reflect at the rim; the rim itself is a nodal ring).
                float rr = x * x + y * y;
                if (rr > 1f) {
                    float rl = Mathf.Sqrt(rr), back = (2f - rl) / rl;
                    x *= back; y *= back;
                }
                if (Rand01() < respawnP) RandomInDisk(out x, out y);
                px[i] = x; py[i] = y;

                // Visuals.
                float onLine = 1f - Mathf.Clamp01(Mathf.Abs(f) * lineSharpness);
                float wx = x * radius, wz = y * radius;
                float dist = Mathf.Sqrt(wx * wx + wz * wz);
                float h = rippleAmp * Mathf.Sin(dist * rippleK - t * rippleSpeed) * (1f - dist / radius)
                        + sandBounce * a * Mathf.Abs(Mathf.Sin(t * 9f + bouncePhase[i]));

                parts[i].position = new Vector3(wx, h, wz);
                parts[i].velocity = Vector3.zero;
                parts[i].startColor = Color.Lerp(offCol, onCol, onLine);
                parts[i].startSize = particleSize * (0.5f + 0.5f * onLine);
                parts[i].remainingLifetime = 1e6f;
            }
            ps.SetParticles(parts, particleCount);
        }

        // ------------------------------------------------------------------ field

        private void ApplyNotes(int[] midiNotes, Color chordTint) {
            foreach (var m in modes) m.target = 0f;
            if (midiNotes != null) {
                for (int i = 0; i < midiNotes.Length; i++) {
                    var def = ModeForNote(midiNotes[i]);
                    var mode = modes.Find(x => x.n == def.n && x.m == def.m);
                    if (mode == null) {
                        mode = GetTables(def);
                        mode = new Mode { n = mode.n, m = mode.m, R = mode.R, dR = mode.dR, Rr = mode.Rr };
                        modes.Add(mode);
                    }
                    mode.target += i == 0 ? 1.2f : 1f; // root a little stronger
                }
            }
            targetTint = chordTint;
            lastChangeTime = Time.time;
        }

        /// <summary>Plate frequency grows with z^2, so one octave (x2) multiplies z by sqrt(2).</summary>
        private ModeDef ModeForNote(int midi) {
            float zTarget = referenceZ * Mathf.Pow(2f, (midi - referenceNote) / 24f);
            var best = modeList[0];
            float bestDiff = float.MaxValue;
            foreach (var d in modeList) {
                float diff = Mathf.Abs(d.z - zTarget);
                if (diff < bestDiff) { bestDiff = diff; best = d; }
            }
            return best;
        }

        private void UpdateModes(float dt) {
            float rate = dt / Mathf.Max(0.05f, morphTime) * 1.5f;
            activeCount = 0; activeMaxN = 0;
            float weightSum = 0f;
            for (int i = modes.Count - 1; i >= 0; i--) {
                var m = modes[i];
                m.weight = Mathf.MoveTowards(m.weight, m.target, rate);
                if (m.weight <= 0f && m.target <= 0f) { modes.RemoveAt(i); continue; }
                if (activeCount == active.Length) {
                    Array.Resize(ref active, activeCount * 2);
                    Array.Resize(ref activeW, activeCount * 2);
                }
                active[activeCount] = m;
                activeW[activeCount] = m.weight;
                activeMaxN = Mathf.Max(activeMaxN, m.n);
                weightSum += m.weight;
                activeCount++;
            }
            invWeight = weightSum > 1e-4f ? 1f / weightSum : 0f;
        }

        /// <summary>Plate displacement f and its gradient at (x, y) in plate units.</summary>
        private void Eval(float x, float y, out float f, out float gx, out float gy) {
            // Into the rotated plate frame.
            float qx = x * rotCos + y * rotSin, qy = -x * rotSin + y * rotCos;
            float r = Mathf.Sqrt(qx * qx + qy * qy);
            float c = r > 1e-5f ? qx / r : 1f, s = r > 1e-5f ? qy / r : 0f;

            // cos(n theta), sin(n theta) by angle addition.
            cosN[0] = 1f; sinN[0] = 0f;
            for (int k = 1; k <= activeMaxN; k++) {
                cosN[k] = cosN[k - 1] * c - sinN[k - 1] * s;
                sinN[k] = sinN[k - 1] * c + cosN[k - 1] * s;
            }

            float fi = Mathf.Min(r, 1f) * (TableSize - 1);
            int i0 = Mathf.Min((int)fi, TableSize - 2);
            float u = fi - i0;

            float fr = 0f, ft = 0f;
            f = 0f;
            for (int k = 0; k < activeCount; k++) {
                var m = active[k];
                float w = activeW[k];
                float R = m.R[i0] + (m.R[i0 + 1] - m.R[i0]) * u;
                float dR = m.dR[i0] + (m.dR[i0 + 1] - m.dR[i0]) * u;
                float Rr = m.Rr[i0] + (m.Rr[i0 + 1] - m.Rr[i0]) * u;
                float cn = cosN[m.n], sn = sinN[m.n];
                f += w * R * cn;
                fr += w * dR * cn;              // df/dr
                ft -= w * Rr * m.n * sn;        // (1/r) df/dtheta
            }
            f *= invWeight; fr *= invWeight; ft *= invWeight;

            // Gradient in the rotated frame, then back to the plate frame.
            float hx = fr * c - ft * s, hy = fr * s + ft * c;
            gx = hx * rotCos - hy * rotSin;
            gy = hx * rotSin + hy * rotCos;
        }

        // ------------------------------------------------------------------ Bessel functions

        private static void BuildModeList() {
            if (modeList != null) return;
            modeList = new List<ModeDef>();
            for (int n = 0; n <= MaxN; n++) {
                int m = 0;
                double prevX = 0.05, prev = BesselJ(n, prevX);
                for (double x = 0.1; x <= MaxZ; x += 0.02) {
                    double v = BesselJ(n, x);
                    if (prev != 0 && Math.Sign(v) != Math.Sign(prev)) {
                        double lo = prevX, hi = x;
                        for (int it = 0; it < 40; it++) {
                            double mid = 0.5 * (lo + hi);
                            if (Math.Sign(BesselJ(n, mid)) == Math.Sign(BesselJ(n, lo))) lo = mid; else hi = mid;
                        }
                        modeList.Add(new ModeDef { n = n, m = ++m, z = (float)(0.5 * (lo + hi)) });
                    }
                    prevX = x; prev = v;
                }
            }
            modeList.Sort((a, b) => a.z.CompareTo(b.z));
        }

        /// <summary>Radial lookup tables for one mode, normalised so the largest |J_n| is 1. Cached.</summary>
        private static Mode GetTables(ModeDef d) {
            long key = d.n * 1000L + d.m;
            if (tableCache.TryGetValue(key, out var cached)) return cached;

            var R = new float[TableSize];
            var dR = new float[TableSize];
            var Rr = new float[TableSize];
            double peak = 1e-9;
            var raw = new double[TableSize];
            var rawD = new double[TableSize];
            for (int i = 0; i < TableSize; i++) {
                double r = i / (double)(TableSize - 1), x = d.z * r;
                raw[i] = BesselJ(d.n, x);
                double jm = d.n == 0 ? -BesselJ(1, x) : BesselJ(d.n - 1, x);
                rawD[i] = d.z * 0.5 * (jm - BesselJ(d.n + 1, x));   // d/dr J_n(z r)
                peak = Math.Max(peak, Math.Abs(raw[i]));
            }
            for (int i = 0; i < TableSize; i++) {
                double r = i / (double)(TableSize - 1);
                R[i] = (float)(raw[i] / peak);
                dR[i] = (float)(rawD[i] / peak);
                // J_n(z r) / r; the limit at r = 0 is z/2 for n = 1 and 0 otherwise
                Rr[i] = i == 0 ? (d.n == 1 ? (float)(d.z * 0.5 / peak) : 0f) : (float)(raw[i] / r / peak);
            }
            var mode = new Mode { n = d.n, m = d.m, R = R, dR = dR, Rr = Rr };
            tableCache[key] = mode;
            return mode;
        }

        /// <summary>J_n(x) by its power series (accurate in double precision for x up to ~25).</summary>
        private static double BesselJ(int n, double x) {
            double half = 0.5 * x;
            double term = 1.0;
            for (int k = 1; k <= n; k++) term *= half / k;   // (x/2)^n / n!
            double sum = term, q = -half * half;
            for (int k = 1; k < 100; k++) {
                term *= q / (k * (double)(k + n));
                sum += term;
                if (k > half && Math.Abs(term) < 1e-16 * Math.Abs(sum) + 1e-300) break;
            }
            return sum;
        }

        // ------------------------------------------------------------------ sound

        private void PlayChord(int index, int[] notes) {
            if (audioSource == null || notes == null || notes.Length == 0) return;
            if (!chordClips.TryGetValue(index, out var clip) || clip == null)
                chordClips[index] = clip = MakeChordClip(notes);
            audioSource.PlayOneShot(clip, chordVolume);
        }

        /// <summary>Soft bell-like chord: each note with an octave partial, fast attack, long decay.</summary>
        private static AudioClip MakeChordClip(int[] notes) {
            const int rate = 44100;
            const float length = 2.5f;
            int n = (int)(rate * length);
            var data = new float[n];
            float norm = 0.6f / notes.Length;
            for (int i = 0; i < n; i++) {
                float t = i / (float)rate;
                float env = Mathf.Min(1f, t * 200f) * Mathf.Exp(-t * 1.6f);
                float sum = 0f;
                for (int j = 0; j < notes.Length; j++) {
                    float hz = 440f * Mathf.Pow(2f, (notes[j] - 69) / 12f);
                    sum += Mathf.Sin(2f * Mathf.PI * hz * t) + 0.25f * Mathf.Sin(4f * Mathf.PI * hz * t) * Mathf.Exp(-t * 3f);
                }
                data[i] = sum * norm * env;
            }
            var clip = AudioClip.Create("Chord", n, 1, rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        // ------------------------------------------------------------------ helpers

        private float Rand01() {
            rng ^= rng << 13; rng ^= rng >> 17; rng ^= rng << 5;
            return (rng & 0xFFFFFF) / 16777216f;
        }

        private float RandSigned() => Rand01() * 2f - 1f;

        private void RandomInDisk(out float x, out float y) {
            float r = Mathf.Sqrt(Rand01()), a = Rand01() * 6.2832f;
            x = r * Mathf.Cos(a);
            y = r * Mathf.Sin(a);
        }

        private static List<Chord> DefaultChords() => new List<Chord>
        {
            new Chord("C major", new Color(1.00f, 0.95f, 0.85f), 60, 64, 67),
            new Chord("A minor", new Color(0.75f, 0.85f, 1.00f), 57, 60, 64),
            new Chord("F major", new Color(0.90f, 1.00f, 0.85f), 53, 57, 60),
            new Chord("G7",      new Color(1.00f, 0.85f, 0.95f), 55, 59, 62, 65),
            new Chord("B dim",   new Color(0.85f, 0.80f, 1.00f), 59, 62, 65),
        };

        private static Texture2D CreateDotTexture(int size) {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = "ChladniDot", wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++) {
                    float dx = (x + 0.5f) / size * 2f - 1f, dy = (y + 0.5f) / size * 2f - 1f;
                    float a = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy));
                    pixels[y * size + x] = new Color(1f, 1f, 1f, a * a * (3f - 2f * a));
                }
            tex.SetPixels(pixels);
            tex.Apply();
            return tex;
        }

        /// <summary>URP Particles/Unlit, transparent alpha blend (switch Blending Mode to Additive for a glow).</summary>
        private static Material CreateDefaultMaterial(Texture2D dot) {
            var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            Material mat;
            if (shader != null) {
                mat = new Material(shader);
                mat.SetFloat("_Surface", 1f);                  // transparent
                mat.SetFloat("_Blend", 0f);                    // alpha
                mat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                mat.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
                mat.SetFloat("_ZWrite", 0f);
                mat.SetOverrideTag("RenderType", "Transparent");
                mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                mat.renderQueue = (int)RenderQueue.Transparent;
                mat.SetTexture("_BaseMap", dot);
                mat.SetColor("_BaseColor", Color.white);
            } else {
                mat = new Material(Shader.Find("Sprites/Default")) { mainTexture = dot };
            }
            mat.name = "ChladniParticles";
            return mat;
        }

        private void OnDrawGizmosSelected() {
            Gizmos.color = new Color(0.6f, 0.8f, 1f, 0.8f);
            Gizmos.matrix = transform.localToWorldMatrix;
            const int seg = 64;
            for (int i = 0; i < seg; i++) {
                float a0 = i * 6.2832f / seg, a1 = (i + 1) * 6.2832f / seg;
                Gizmos.DrawLine(new Vector3(Mathf.Cos(a0), 0f, Mathf.Sin(a0)) * radius,
                                new Vector3(Mathf.Cos(a1), 0f, Mathf.Sin(a1)) * radius);
            }
        }
    }
}

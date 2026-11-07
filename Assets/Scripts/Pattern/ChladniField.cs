using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace FracturedFrequencies.Pattern
{
    /// <summary>
    /// A floating, round Chladni figure made of particles: closed, concentric rings shaped like rosettes.
    ///
    /// How it works (polar coordinates r = 0..1, theta):
    ///       phi(r, theta) = K * pi * r + r * A(theta)          f = cos(phi)
    ///       A(theta)      = sum over notes of  w * cos(S * h * theta + phase)
    /// - The nodal lines (f = 0) are closed rings around the centre; A(theta) bends them into petals.
    /// - The chord's root picks the symmetry S (3..8 petals), the other notes add harmonics (2S, 3S)
    ///   whose phase depends on the interval, so major / minor / 7th chords get different outlines.
    /// - Each chord also sets how many rings there are.
    /// - Like sand on a plate, particles are shaken where |f| is large and slide onto the rings.
    ///   On a chord change everything cross-fades and the particles flow into the new figure.
    ///
    /// Usage: add this script to an empty GameObject and press Play. It builds its own particle system
    /// (child "ChladniParticles"), material and sound at runtime.
    /// The figure lies in the local XZ plane, centred on this transform (rotation X = -90 faces the
    /// default camera of a new scene). Keys 1..9 pick a chord; gameplay code calls SetChord / SetNotes.
    /// </summary>
    public class ChladniField : MonoBehaviour
    {
        [Serializable]
        public class Chord
        {
            public string name = "C";
            [Tooltip("MIDI note numbers (60 = C4, 69 = A4 = 440 Hz). The first note is the root.")]
            public int[] notes = { 60, 64, 67 };
            [Tooltip("Number of closed rings (0 = one per note).")]
            [Range(0, 8)] public int rings = 3;
            public Color tint = Color.white;

            public Chord() { }
            public Chord(string name, int rings, Color tint, params int[] notes)
            { this.name = name; this.rings = rings; this.tint = tint; this.notes = notes; }
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

        [Header("Shape")]
        [Tooltip("Radius of the figure in metres.")]
        [SerializeField] private float radius = 4.5f;
        [SerializeField, Range(500, 20000)] private int particleCount = 6000;
        [Tooltip("How strongly the rings are bent into petals (radians).")]
        [SerializeField, Range(0f, 4f)] private float petalDepth = 2.2f;
        [Tooltip("Slow rotation of the whole figure (radians per second).")]
        [SerializeField] private float spin = 0.08f;
        [Tooltip("The petals drift against each other so the rings keep undulating (radians per second).")]
        [SerializeField] private float petalDrift = 0.35f;

        [Header("Sand motion (figure radius = 1)")]
        [Tooltip("How fast particles slide onto the rings.")]
        [SerializeField] private float attraction = 5f;
        [SerializeField] private float maxSpeed = 1.2f;
        [Tooltip("Shaking where the plate vibrates (between the rings).")]
        [SerializeField] private float shake = 0.7f;
        [Tooltip("Tiny shimmer that remains on the rings.")]
        [SerializeField] private float lineShimmer = 0.012f;
        [Tooltip("Extra shake right after a chord change; decays with rippleDecay.")]
        [SerializeField] private float chordKick = 1f;
        [Tooltip("Fraction of particles re-dropped at random per second, keeps the rings fed.")]
        [SerializeField] private float respawnRate = 0.03f;

        [Header("Height waves")]
        [Tooltip("Height of the circular wave that runs outward over the figure (metres).")]
        [SerializeField] private float rippleHeight = 0.12f;
        [Tooltip("Extra wave height right after a chord change.")]
        [SerializeField] private float chordRippleHeight = 0.6f;
        [SerializeField] private float rippleDecay = 1.2f;
        [SerializeField] private float rippleWavelength = 2.5f;
        [SerializeField] private float rippleSpeed = 3f;
        [Tooltip("Particles between the rings bounce up and down like sand (metres).")]
        [SerializeField] private float sandBounce = 0.15f;

        [Header("Look")]
        [Tooltip("Optional. Leave empty to create a soft-dot URP particle material at runtime.")]
        public Material particleMaterial;
        [SerializeField] private float particleSize = 0.06f;
        [SerializeField] private Color lineColor = new Color(0.85f, 0.95f, 1f, 1f);
        [SerializeField] private Color offLineColor = new Color(0.55f, 0.65f, 1f, 0.08f);
        [Tooltip("Higher = thinner, sharper rings.")]
        [SerializeField] private float lineSharpness = 6f;

        public event Action<int, Chord> ChordChanged;
        public int CurrentChordIndex { get; private set; } = -1;
        public IReadOnlyList<Chord> Chords => chords;

        // ------------------------------------------------------------------ note -> shape tables

        // Petal symmetry chosen by the root's pitch class (C, C#, D ... B).
        private static readonly int[] RootSymmetry = { 5, 7, 4, 6, 3, 8, 6, 5, 7, 4, 6, 3 };
        // Weight of the 1st, 2nd, 3rd, 4th+ note.
        private static readonly float[] NoteWeight = { 1f, 0.45f, 0.35f, 0.3f };

        private class Term { public int n; public float phase, drift, weight, target; }
        private readonly List<Term> terms = new List<Term>();
        private float rings = 3f, targetRings = 3f;

        // ------------------------------------------------------------------ runtime state

        private ParticleSystem ps;
        private ParticleSystem.Particle[] parts;
        private float[] px, py, bouncePhase;
        private uint rng = 0x9E3779B9u;
        private float lastChangeTime = -100f;
        private Color tint = Color.white, targetTint = Color.white;
        private AudioSource audioSource;
        private readonly Dictionary<int, AudioClip> chordClips = new Dictionary<int, AudioClip>();

        // per-frame copy of the active terms
        private int[] tn = new int[8];
        private float[] tph = new float[8], tw = new float[8];
        private int termCount;
        private float k, amp, phiCap;

        // ------------------------------------------------------------------ public API

        /// <summary>Switch to chord number index (wraps around).</summary>
        public void SetChord(int index)
        {
            if (chords == null || chords.Count == 0) return;
            index = ((index % chords.Count) + chords.Count) % chords.Count;
            var chord = chords[index];
            CurrentChordIndex = index;
            ApplyNotes(chord.notes, chord.rings, chord.tint);
            if (playChordSound) PlayChord(index, chord.notes);
            ChordChanged?.Invoke(index, chord);
        }

        public void NextChord() => SetChord(CurrentChordIndex + 1);

        /// <summary>Show an arbitrary set of notes (MIDI numbers, first = root), e.g. from a puzzle.</summary>
        public void SetNotes(Color chordTint, int ringCount, params int[] midiNotes)
        {
            CurrentChordIndex = -1;
            ApplyNotes(midiNotes, ringCount, chordTint);
        }

        // ------------------------------------------------------------------ setup

        private void Reset() => chords = DefaultChords();

        private void Awake()
        {
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
            for (int i = 0; i < particleCount; i++)
            {
                RandomInDisk(out px[i], out py[i]);
                bouncePhase[i] = Rand01() * 6.2832f;
            }

            audioSource = GetComponent<AudioSource>();
            if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 0f;
        }

        private void Start()
        {
            ps.Play();
            ps.Emit(particleCount);
            parts = new ParticleSystem.Particle[particleCount];
            ps.GetParticles(parts);

            bool sound = playChordSound;
            playChordSound = false; // no sound on scene start
            SetChord(startChord);
            playChordSound = sound;
            foreach (var t in terms) t.weight = t.target; // start fully formed
            rings = targetRings;
            tint = targetTint;
            lastChangeTime = -100f;
        }

        private static void ConfigureParticleSystem(ParticleSystem system, int count, Material material)
        {
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

        private void Update()
        {
            if (!numberKeysSelectChord || Time.timeScale <= 0f || chords == null) return;
            int count = Mathf.Min(9, chords.Count);
            for (int i = 0; i < count; i++)
            {
#if ENABLE_INPUT_SYSTEM
                var kb = Keyboard.current;
                if (kb != null && kb[(Key)((int)Key.Digit1 + i)].wasPressedThisFrame) SetChord(i);
#else
                if (Input.GetKeyDown(KeyCode.Alpha1 + i)) SetChord(i);
#endif
            }
        }

        private void LateUpdate()
        {
            if (parts == null) return;
            float dt = Mathf.Min(Time.deltaTime, 1f / 30f);
            float t = Time.time;

            UpdateShape(dt, t);
            tint = Color.Lerp(tint, targetTint, 1f - Mathf.Exp(-dt * 3f / Mathf.Max(0.05f, morphTime)));

            float decay = Mathf.Exp(-(t - lastChangeTime) / Mathf.Max(0.05f, rippleDecay));
            float rippleAmp = rippleHeight + chordRippleHeight * decay;
            float kick = chordKick * decay;
            float pull = Mathf.Min(1f, attraction * dt);
            float maxStep = maxSpeed * dt;
            float rippleK = 6.2832f / Mathf.Max(0.1f, rippleWavelength);
            float respawnP = respawnRate * dt;

            Color onCol = lineColor * tint, offCol = offLineColor * tint;
            onCol.a = lineColor.a; offCol.a = offLineColor.a;

            for (int i = 0; i < particleCount; i++)
            {
                float x = px[i], y = py[i];
                bool outside = Eval(x, y, out float f, out float gx, out float gy);

                float dx, dy;
                if (outside)
                {
                    // Beyond the outermost ring: drift inward until a ring catches the particle.
                    float r = Mathf.Sqrt(x * x + y * y) + 1e-5f;
                    dx = -x / r * maxStep;
                    dy = -y / r * maxStep;
                }
                else
                {
                    // Newton step toward f = 0 (the nearest ring), clamped.
                    float s = -f / (gx * gx + gy * gy + 0.5f) * pull;
                    dx = gx * s; dy = gy * s;
                    float len2 = dx * dx + dy * dy;
                    if (len2 > maxStep * maxStep)
                    {
                        float sc = maxStep / Mathf.Sqrt(len2);
                        dx *= sc; dy *= sc;
                    }
                }

                // Shaking: strong between the rings, tiny on them.
                float a = outside ? 1f : Mathf.Abs(f);
                float jitter = (lineShimmer + (shake + kick) * a) * dt;
                x += dx + RandSigned() * jitter;
                y += dy + RandSigned() * jitter;

                // Keep inside the unit disk (reflect at the rim).
                float rr = x * x + y * y;
                if (rr > 1f)
                {
                    float rl = Mathf.Sqrt(rr), back = (2f - rl) / rl;
                    x *= back; y *= back;
                }
                if (Rand01() < respawnP) RandomInDisk(out x, out y);
                px[i] = x; py[i] = y;

                // Visuals.
                float onLine = 1f - Mathf.Clamp01(a * lineSharpness);
                float wx = x * radius, wz = y * radius;
                float dist = Mathf.Sqrt(wx * wx + wz * wz);
                float h = rippleAmp * Mathf.Sin(dist * rippleK - t * rippleSpeed) * (1f - 0.6f * dist / radius)
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

        private void ApplyNotes(int[] midiNotes, int ringCount, Color chordTint)
        {
            foreach (var term in terms) term.target = 0f;
            if (midiNotes != null && midiNotes.Length > 0)
            {
                int root = midiNotes[0];
                int sym = RootSymmetry[((root % 12) + 12) % 12];
                for (int i = 0; i < midiNotes.Length; i++)
                {
                    int interval = ((midiNotes[i] - root) % 12 + 12) % 12;
                    int harmonic = i == 0 ? 1 : (i < 3 ? 2 : 3);
                    int n = sym * harmonic;
                    float phase = interval * 0.9f;
                    var term = terms.Find(x => x.n == n && Mathf.Approximately(x.phase, phase));
                    if (term == null)
                    {
                        term = new Term { n = n, phase = phase, drift = (i % 2 == 0 ? 1f : -1f) / harmonic };
                        terms.Add(term);
                    }
                    term.target += NoteWeight[Mathf.Min(i, NoteWeight.Length - 1)];
                }
                targetRings = ringCount > 0 ? ringCount : midiNotes.Length;
            }
            targetTint = chordTint;
            lastChangeTime = Time.time;
        }

        private void UpdateShape(float dt, float time)
        {
            float rate = dt / Mathf.Max(0.05f, morphTime) * 1.5f;
            rings = Mathf.MoveTowards(rings, targetRings, rate * 2f);

            termCount = 0;
            float weightSum = 0f;
            for (int i = terms.Count - 1; i >= 0; i--)
            {
                var term = terms[i];
                term.weight = Mathf.MoveTowards(term.weight, term.target, rate);
                if (term.weight <= 0f && term.target <= 0f) { terms.RemoveAt(i); continue; }
                if (termCount == tn.Length)
                {
                    Array.Resize(ref tn, termCount * 2);
                    Array.Resize(ref tph, termCount * 2);
                    Array.Resize(ref tw, termCount * 2);
                }
                tn[termCount] = term.n;
                // spin turns the whole figure; drift moves the harmonics against each other
                tph[termCount] = term.phase - term.n * spin * time + term.drift * petalDrift * time;
                tw[termCount] = term.weight;
                weightSum += term.weight;
                termCount++;
            }

            amp = weightSum > 1e-4f ? petalDepth / weightSum : 0f;
            // Choose K so the outermost ring stays inside the disk even where the petals push outward.
            k = rings / 0.97f + petalDepth / Mathf.PI;
            phiCap = rings * Mathf.PI;
        }

        /// <summary>f = cos(phi) and its gradient at (x, y). Returns true when outside the outermost ring.</summary>
        private bool Eval(float x, float y, out float f, out float gx, out float gy)
        {
            float r = Mathf.Sqrt(x * x + y * y);
            float c = r > 1e-5f ? x / r : 1f, s = r > 1e-5f ? y / r : 0f;
            float theta = Mathf.Atan2(y, x);

            float A = 0f, dA = 0f;
            for (int i = 0; i < termCount; i++)
            {
                float arg = tn[i] * theta + tph[i];
                A += tw[i] * Mathf.Cos(arg);
                dA -= tw[i] * tn[i] * Mathf.Sin(arg);
            }
            A *= amp; dA *= amp;

            float phi = k * Mathf.PI * r + r * A;
            f = Mathf.Cos(phi);
            float sinPhi = Mathf.Sin(phi);
            float fr = -sinPhi * (k * Mathf.PI + A);   // df/dr
            float ft = -sinPhi * dA;                    // (1/r) df/dtheta
            gx = fr * c - ft * s;
            gy = fr * s + ft * c;
            return phi > phiCap;
        }

        // ------------------------------------------------------------------ sound

        private void PlayChord(int index, int[] notes)
        {
            if (audioSource == null || notes == null || notes.Length == 0) return;
            if (!chordClips.TryGetValue(index, out var clip) || clip == null)
                chordClips[index] = clip = MakeChordClip(notes);
            audioSource.PlayOneShot(clip, chordVolume);
        }

        /// <summary>Soft bell-like chord: each note with an octave partial, fast attack, long decay.</summary>
        private static AudioClip MakeChordClip(int[] notes)
        {
            const int rate = 44100;
            const float length = 2.5f;
            int n = (int)(rate * length);
            var data = new float[n];
            float norm = 0.6f / notes.Length;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)rate;
                float env = Mathf.Min(1f, t * 200f) * Mathf.Exp(-t * 1.6f);
                float sum = 0f;
                for (int j = 0; j < notes.Length; j++)
                {
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

        private float Rand01()
        {
            rng ^= rng << 13; rng ^= rng >> 17; rng ^= rng << 5;
            return (rng & 0xFFFFFF) / 16777216f;
        }

        private float RandSigned() => Rand01() * 2f - 1f;

        private void RandomInDisk(out float x, out float y)
        {
            float r = Mathf.Sqrt(Rand01()), a = Rand01() * 6.2832f;
            x = r * Mathf.Cos(a);
            y = r * Mathf.Sin(a);
        }

        private static List<Chord> DefaultChords() => new List<Chord>
        {
            new Chord("C major", 3, new Color(1.00f, 0.95f, 0.85f), 60, 64, 67),
            new Chord("A minor", 4, new Color(0.75f, 0.85f, 1.00f), 57, 60, 64),
            new Chord("F major", 3, new Color(0.90f, 1.00f, 0.85f), 53, 57, 60),
            new Chord("G7",      5, new Color(1.00f, 0.85f, 0.95f), 55, 59, 62, 65),
            new Chord("B dim",   3, new Color(0.85f, 0.80f, 1.00f), 59, 62, 65),
        };

        private static Texture2D CreateDotTexture(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = "ChladniDot", wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = (x + 0.5f) / size * 2f - 1f, dy = (y + 0.5f) / size * 2f - 1f;
                float a = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy));
                pixels[y * size + x] = new Color(1f, 1f, 1f, a * a * (3f - 2f * a));
            }
            tex.SetPixels(pixels);
            tex.Apply();
            return tex;
        }

        /// <summary>URP Particles/Unlit, transparent alpha blend (switch Blending Mode to Additive for a glow).</summary>
        private static Material CreateDefaultMaterial(Texture2D dot)
        {
            var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            Material mat;
            if (shader != null)
            {
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
            }
            else
            {
                mat = new Material(Shader.Find("Sprites/Default")) { mainTexture = dot };
            }
            mat.name = "ChladniParticles";
            return mat;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.6f, 0.8f, 1f, 0.8f);
            Gizmos.matrix = transform.localToWorldMatrix;
            const int seg = 64;
            for (int i = 0; i < seg; i++)
            {
                float a0 = i * 6.2832f / seg, a1 = (i + 1) * 6.2832f / seg;
                Gizmos.DrawLine(new Vector3(Mathf.Cos(a0), 0f, Mathf.Sin(a0)) * radius,
                                new Vector3(Mathf.Cos(a1), 0f, Mathf.Sin(a1)) * radius);
            }
        }
    }
}

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace FracturedFrequencies.Tools
{
    /// <summary>
    /// A floating Chladni plate made of particles.
    ///
    /// How it works:
    /// - Every note (pitch class) is mapped to one vibration mode (n, m) of a square plate:
    ///       f(u,v) = cos(n*pi*u) * cos(m*pi*v)  +/-  cos(m*pi*u) * cos(n*pi*v)      (u, v in 0..1)
    /// - A chord is the sum of the modes of its notes, so every chord gives its own pattern.
    /// - Like sand on a real plate, particles are shaken where the plate moves (|f| large) and slide
    ///   toward the nodal lines (f = 0). When the chord changes, the mode weights cross-fade and the
    ///   particles flow into the new shape while a ripple runs across the lines.
    ///
    /// Usage: add this script to an empty GameObject and press Play. It builds its own particle system
    /// (child "ChladniParticles"), material and sound at runtime; nothing else needs to be set up.
    /// The plate lies in the local XZ plane, centred on this transform. Rotate the object to stand it up
    /// (rotation X = -90 faces the default camera of a new scene).
    /// Keys 1..9 pick a chord (for testing); gameplay code calls SetChord / SetNotes.
    /// </summary>
    public class ChladniField : MonoBehaviour
    {
        [Serializable]
        public class Chord
        {
            public string name = "C";
            [Tooltip("MIDI note numbers (60 = C4, 69 = A4 = 440 Hz).")]
            public int[] notes = { 60, 64, 67 };
            public Color tint = Color.white;

            public Chord() { }
            public Chord(string name, Color tint, params int[] notes) { this.name = name; this.tint = tint; this.notes = notes; }
        }

        [Header("Chords")]
        [SerializeField] private List<Chord> chords = DefaultChords();
        [SerializeField] private int startChord = 0;
        [Tooltip("Seconds to cross-fade from one pattern to the next.")]
        [SerializeField] private float morphTime = 1.2f;
        [Tooltip("Number keys 1..9 select chords (testing).")]
        [SerializeField] private bool numberKeysSelectChord = true;
        [SerializeField] private bool playChordSound = true;
        [SerializeField, Range(0f, 1f)] private float chordVolume = 0.35f;

        [Header("Plate")]
        [Tooltip("Side length of the square plate in metres.")]
        [SerializeField] private float plateSize = 9f;
        [SerializeField, Range(500, 20000)] private int particleCount = 6000;

        [Header("Sand motion (plate units per second, plate = 1)")]
        [Tooltip("How fast particles slide onto the nodal lines.")]
        [SerializeField] private float attraction = 5f;
        [SerializeField] private float maxSpeed = 0.6f;
        [Tooltip("Shaking where the plate vibrates (off the lines).")]
        [SerializeField] private float shake = 0.35f;
        [Tooltip("Tiny shimmer that remains on the lines.")]
        [SerializeField] private float lineShimmer = 0.006f;
        [Tooltip("Extra shake right after a chord change; decays with rippleDecay.")]
        [SerializeField] private float chordKick = 0.5f;
        [Tooltip("Fraction of particles re-dropped at random per second, keeps the lines fed.")]
        [SerializeField] private float respawnRate = 0.03f;

        [Header("Line waves")]
        [Tooltip("The nodal lines wobble slowly (coordinate warp).")]
        [SerializeField] private float lineWobble = 0.012f;
        [SerializeField] private float lineWobbleSpeed = 0.8f;
        [Tooltip("Height of the ring wave that runs over the plate (metres).")]
        [SerializeField] private float rippleHeight = 0.12f;
        [Tooltip("Extra ripple height right after a chord change.")]
        [SerializeField] private float chordRippleHeight = 0.6f;
        [SerializeField] private float rippleDecay = 1.2f;
        [SerializeField] private float rippleWavelength = 2.5f;
        [SerializeField] private float rippleSpeed = 3f;
        [Tooltip("Particles off the lines bounce up and down like sand (metres).")]
        [SerializeField] private float sandBounce = 0.15f;

        [Header("Look")]
        [Tooltip("Optional. Leave empty to create a soft-dot URP particle material at runtime.")]
        public Material particleMaterial;
        [SerializeField] private float particleSize = 0.06f;
        [SerializeField] private Color lineColor = new Color(0.85f, 0.95f, 1f, 1f);
        [SerializeField] private Color offLineColor = new Color(0.55f, 0.65f, 1f, 0.12f);
        [Tooltip("Higher = thinner, sharper lines.")]
        [SerializeField] private float lineSharpness = 6f;

        public event Action<int, Chord> ChordChanged;
        public int CurrentChordIndex { get; private set; } = -1;
        public IReadOnlyList<Chord> Chords => chords;

        // ------------------------------------------------------------------ note -> mode table

        private struct ModeDef { public int n, m; public float sign; public ModeDef(int n, int m, float s) { this.n = n; this.m = m; sign = s; } }

        // One mode per pitch class (C, C#, D ... B). Mixed + and - forms so chords don't all share the diagonal.
        private static readonly ModeDef[] PitchClassModes =
        {
            new ModeDef(2, 3, -1), new ModeDef(1, 4, +1), new ModeDef(3, 4, -1), new ModeDef(2, 5, +1),
            new ModeDef(1, 5, -1), new ModeDef(3, 5, +1), new ModeDef(2, 6, -1), new ModeDef(4, 5, -1),
            new ModeDef(1, 6, +1), new ModeDef(3, 6, +1), new ModeDef(2, 7, -1), new ModeDef(4, 7, +1),
        };

        private const int MaxK = 16;

        private class ModeState { public int n, m; public float sign, weight, target; }
        private readonly List<ModeState> modes = new List<ModeState>();

        // ------------------------------------------------------------------ runtime state

        private ParticleSystem ps;
        private ParticleSystem.Particle[] parts;
        private float[] pu, pv, phase;
        private uint rng = 0x9E3779B9u;
        private float lastChangeTime = -100f;
        private Color tint = Color.white, targetTint = Color.white;
        private AudioSource audioSource;
        private readonly Dictionary<int, AudioClip> chordClips = new Dictionary<int, AudioClip>();

        // per-frame scratch
        private readonly float[] cu = new float[MaxK + 1], su = new float[MaxK + 1], cv = new float[MaxK + 1], sv = new float[MaxK + 1];
        private int[] an = new int[8], am = new int[8];
        private float[] asg = new float[8], aw = new float[8];
        private int activeCount, activeMaxK;
        private float weightSum;

        // ------------------------------------------------------------------ public API

        /// <summary>Switch to chord number index (wraps around).</summary>
        public void SetChord(int index)
        {
            if (chords == null || chords.Count == 0) return;
            index = ((index % chords.Count) + chords.Count) % chords.Count;
            var chord = chords[index];
            CurrentChordIndex = index;
            ApplyNotes(chord.notes, chord.tint);
            if (playChordSound) PlayChord(index, chord.notes);
            ChordChanged?.Invoke(index, chord);
        }

        public void NextChord() => SetChord(CurrentChordIndex + 1);

        /// <summary>Show an arbitrary set of notes (MIDI numbers), e.g. from a puzzle or the tuning fork.</summary>
        public void SetNotes(Color chordTint, params int[] midiNotes)
        {
            CurrentChordIndex = -1;
            ApplyNotes(midiNotes, chordTint);
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

            pu = new float[particleCount];
            pv = new float[particleCount];
            phase = new float[particleCount];
            for (int i = 0; i < particleCount; i++)
            {
                pu[i] = Rand01();
                pv[i] = Rand01();
                phase[i] = Rand01() * 6.2832f;
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
            foreach (var s in modes) s.weight = s.target; // start fully formed
            tint = targetTint;
            lastChangeTime = -100f;
        }

        /// <summary>Puts the ParticleSystem in "driven by script" mode.</summary>
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

            UpdateModeWeights(dt);
            tint = Color.Lerp(tint, targetTint, 1f - Mathf.Exp(-dt * 3f / Mathf.Max(0.05f, morphTime)));

            float sinceChange = t - lastChangeTime;
            float decay = Mathf.Exp(-sinceChange / Mathf.Max(0.05f, rippleDecay));
            float rippleAmp = rippleHeight + chordRippleHeight * decay;
            float kick = chordKick * decay;
            float k = Mathf.Min(1f, attraction * dt);
            float maxStep = maxSpeed * dt;
            float wob = lineWobble, wobPhase = t * lineWobbleSpeed;
            float rippleK = 6.2832f / Mathf.Max(0.1f, rippleWavelength);
            float respawnP = respawnRate * dt;
            float half = plateSize * 0.5f;

            Color onCol = lineColor * tint, offCol = offLineColor * tint;
            onCol.a = lineColor.a; offCol.a = offLineColor.a;

            for (int i = 0; i < particleCount; i++)
            {
                float u = pu[i], v = pv[i];

                // Field at a slightly warped position, so the lines wobble.
                float wu = u + wob * Mathf.Sin(v * 6.2832f + wobPhase);
                float wv = v + wob * Mathf.Sin(u * 6.2832f + wobPhase * 1.3f + 1.7f);
                Eval(wu, wv, out float f, out float gu, out float gv);

                // Newton step toward f = 0 (the nodal line), clamped.
                float s = -f / (gu * gu + gv * gv + 0.5f) * k;
                float du = gu * s, dv = gv * s;
                float len2 = du * du + dv * dv;
                if (len2 > maxStep * maxStep)
                {
                    float sc = maxStep / Mathf.Sqrt(len2);
                    du *= sc; dv *= sc;
                }

                // Shaking: strong where the plate moves, tiny on the lines.
                float a = Mathf.Abs(f);
                float jitter = (lineShimmer + (shake + kick) * Mathf.Min(a, 1f)) * dt;
                du += RandSigned() * jitter;
                dv += RandSigned() * jitter;

                u += du; v += dv;
                if (u < 0f) u = -u; else if (u > 1f) u = 2f - u;
                if (v < 0f) v = -v; else if (v > 1f) v = 2f - v;
                if (Rand01() < respawnP) { u = Rand01(); v = Rand01(); }
                pu[i] = u; pv[i] = v;

                // Visuals.
                float onLine = 1f - Mathf.Clamp01(a * lineSharpness);
                float x = (u - 0.5f) * plateSize, z = (v - 0.5f) * plateSize;
                float r = Mathf.Sqrt(x * x + z * z);
                float y = rippleAmp * Mathf.Sin(r * rippleK - t * rippleSpeed) * (1f - r / (half * 1.5f))
                        + sandBounce * Mathf.Min(a, 1f) * Mathf.Abs(Mathf.Sin(t * 9f + phase[i]));

                parts[i].position = new Vector3(x, y, z);
                parts[i].velocity = Vector3.zero;
                parts[i].startColor = Color.Lerp(offCol, onCol, onLine);
                parts[i].startSize = particleSize * (0.5f + 0.5f * onLine);
                parts[i].remainingLifetime = 1e6f;
            }
            ps.SetParticles(parts, particleCount);
        }

        // ------------------------------------------------------------------ field

        private void ApplyNotes(int[] midiNotes, Color chordTint)
        {
            foreach (var s in modes) s.target = 0f;
            if (midiNotes != null)
            {
                for (int i = 0; i < midiNotes.Length; i++)
                {
                    int note = midiNotes[i];
                    var def = PitchClassModes[((note % 12) + 12) % 12];
                    int octaveShift = Mathf.Clamp(note / 12 - 1 - 4, 0, 4); // notes above octave 4 use higher modes
                    int n = Mathf.Min(def.n + octaveShift, MaxK), m = Mathf.Min(def.m + octaveShift, MaxK);
                    float w = i == 0 ? 1.25f : 1f; // root a little stronger
                    var st = modes.Find(x => x.n == n && x.m == m && x.sign == def.sign);
                    if (st == null) { st = new ModeState { n = n, m = m, sign = def.sign }; modes.Add(st); }
                    st.target += w;
                }
            }
            targetTint = chordTint;
            lastChangeTime = Time.time;
        }

        private void UpdateModeWeights(float dt)
        {
            float rate = dt / Mathf.Max(0.05f, morphTime) * 1.5f;
            activeCount = 0; activeMaxK = 1; weightSum = 0f;
            for (int i = modes.Count - 1; i >= 0; i--)
            {
                var s = modes[i];
                s.weight = Mathf.MoveTowards(s.weight, s.target, rate);
                if (s.weight <= 0f && s.target <= 0f) { modes.RemoveAt(i); continue; }
                if (activeCount == an.Length)
                {
                    Array.Resize(ref an, activeCount * 2); Array.Resize(ref am, activeCount * 2);
                    Array.Resize(ref asg, activeCount * 2); Array.Resize(ref aw, activeCount * 2);
                }
                an[activeCount] = s.n; am[activeCount] = s.m; asg[activeCount] = s.sign; aw[activeCount] = s.weight;
                activeMaxK = Mathf.Max(activeMaxK, Mathf.Max(s.n, s.m));
                weightSum += s.weight;
                activeCount++;
            }
        }

        /// <summary>Normalised plate displacement f and its gradient at (u, v).</summary>
        private void Eval(float u, float v, out float f, out float gu, out float gv)
        {
            // cos/sin(k*pi*u) for k = 0..maxK by angle addition (4 trig calls per particle).
            float c1u = Mathf.Cos(Mathf.PI * u), s1u = Mathf.Sin(Mathf.PI * u);
            float c1v = Mathf.Cos(Mathf.PI * v), s1v = Mathf.Sin(Mathf.PI * v);
            cu[0] = 1f; su[0] = 0f; cv[0] = 1f; sv[0] = 0f;
            for (int k = 1; k <= activeMaxK; k++)
            {
                cu[k] = cu[k - 1] * c1u - su[k - 1] * s1u;
                su[k] = su[k - 1] * c1u + cu[k - 1] * s1u;
                cv[k] = cv[k - 1] * c1v - sv[k - 1] * s1v;
                sv[k] = sv[k - 1] * c1v + cv[k - 1] * s1v;
            }

            f = gu = gv = 0f;
            for (int i = 0; i < activeCount; i++)
            {
                int n = an[i], m = am[i];
                float w = aw[i], sg = asg[i];
                f  += w * (cu[n] * cv[m] + sg * cu[m] * cv[n]);
                gu += w * (-n * su[n] * cv[m] - sg * m * su[m] * cv[n]);
                gv += w * (-m * cu[n] * sv[m] - sg * n * cu[m] * sv[n]);
            }
            float inv = weightSum > 1e-4f ? 1f / weightSum : 0f;
            f *= inv;
            gu *= Mathf.PI * inv;
            gv *= Mathf.PI * inv;
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

        private static List<Chord> DefaultChords() => new List<Chord>
        {
            new Chord("C major", new Color(1.00f, 0.95f, 0.85f), 60, 64, 67),
            new Chord("A minor", new Color(0.75f, 0.85f, 1.00f), 57, 60, 64),
            new Chord("F major", new Color(0.90f, 1.00f, 0.85f), 53, 57, 60),
            new Chord("G7",      new Color(1.00f, 0.85f, 0.95f), 55, 59, 62, 65),
            new Chord("B dim",   new Color(0.85f, 0.80f, 1.00f), 59, 62, 65),
        };

        private static Texture2D CreateDotTexture(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = "ChladniDot", wrapMode = TextureWrapMode.Clamp };
            var px = new Color[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = (x + 0.5f) / size * 2f - 1f, dy = (y + 0.5f) / size * 2f - 1f;
                float a = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy));
                px[y * size + x] = new Color(1f, 1f, 1f, a * a * (3f - 2f * a));
            }
            tex.SetPixels(px);
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
            Gizmos.DrawWireCube(Vector3.zero, new Vector3(plateSize, 0.01f, plateSize));
        }
    }
}

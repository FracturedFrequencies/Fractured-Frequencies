using UnityEngine;

namespace FracturedFrequencies.Tools
{
    /// <summary>
    /// Placeholder sounds generated in code, so the demo has audio feedback without any sound files.
    /// Replace with real recordings later by swapping the clips.
    /// </summary>
    public static class ProceduralAudio
    {
        private const int Rate = 44100;
        private static AudioClip _pulse, _hum, _shatter, _shift;

        public static AudioClip HumClip => _hum != null ? _hum : (_hum = MakeHum());

        public static void PlayPulse(Vector3 at) => Play(_pulse != null ? _pulse : (_pulse = MakePulse()), at, 0.6f);
        public static void PlayShatter(Vector3 at, float pitch) => Play(_shatter != null ? _shatter : (_shatter = MakeShatter()), at, 0.7f, pitch);
        public static void PlayGravityShift(Vector3 at) => Play(_shift != null ? _shift : (_shift = MakeShift()), at, 0.6f);

        private static void Play(AudioClip clip, Vector3 at, float volume, float pitch = 1f)
        {
            var go = new GameObject("OneShot_" + clip.name);
            go.transform.position = at;
            var src = go.AddComponent<AudioSource>();
            src.clip = clip;
            src.volume = volume;
            src.pitch = pitch;
            src.spatialBlend = 0.3f;
            src.Play();
            Object.Destroy(go, clip.length / Mathf.Max(0.1f, pitch) + 0.1f);
        }

        // Low thump with a falling pitch.
        private static AudioClip MakePulse()
        {
            int n = (int)(Rate * 0.35f);
            var d = new float[n];
            double phase = 0;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                float f = Mathf.Lerp(160f, 55f, t / 0.35f);
                phase += 2 * Mathf.PI * f / Rate;
                float env = Mathf.Exp(-t * 12f) * Mathf.Min(1f, t * 400f);
                d[i] = (float)System.Math.Sin(phase) * env;
            }
            return Make("Pulse", d);
        }

        // Tuning-fork tone (fundamental + soft overtone), seamless loop at 440 Hz.
        private static AudioClip MakeHum()
        {
            int n = Rate; // 1 second, 440 whole cycles
            var d = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                d[i] = 0.7f * Mathf.Sin(2 * Mathf.PI * 440f * t) + 0.2f * Mathf.Sin(2 * Mathf.PI * 880f * t);
            }
            return Make("Hum", d);
        }

        // Noise burst with high ringing partials.
        private static AudioClip MakeShatter()
        {
            int n = (int)(Rate * 0.9f);
            var d = new float[n];
            var rnd = new System.Random(3);
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                float noise = (float)(rnd.NextDouble() * 2 - 1) * Mathf.Exp(-t * 9f);
                float ring = (Mathf.Sin(2 * Mathf.PI * 2637f * t) + Mathf.Sin(2 * Mathf.PI * 3951f * t)) * 0.15f * Mathf.Exp(-t * 4f);
                d[i] = (noise * 0.6f + ring);
            }
            return Make("Shatter", d);
        }

        // Deep rising whoosh for gravity changes.
        private static AudioClip MakeShift()
        {
            int n = (int)(Rate * 1.4f);
            var d = new float[n];
            double phase = 0;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                float f = Mathf.Lerp(50f, 140f, t / 1.4f);
                phase += 2 * Mathf.PI * f / Rate;
                float env = Mathf.Sin(Mathf.PI * t / 1.4f);
                d[i] = (float)System.Math.Sin(phase) * env * 0.8f;
            }
            return Make("GravityShift", d);
        }

        private static AudioClip Make(string name, float[] data)
        {
            var clip = AudioClip.Create(name, data.Length, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}

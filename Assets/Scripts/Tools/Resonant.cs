using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using FracturedFrequencies.World;

namespace FracturedFrequencies.Tools
{
    /// <summary>
    /// An object with a "true name" (natural frequency) that can be driven by the tuning fork.
    ///
    /// Simplified driven, damped oscillator:
    /// - While driven, the amplitude grows by (drive / buildUpTime) per second.
    /// - When not driven, it rings down: A *= exp(-dt / ringTime).
    /// - At amplitude 1 a breakable object breaks.
    /// High-Q materials (glass) build up fast and ring for a long time. Low-Q materials (stone) are slow.
    /// Metal is not breakable but passes its vibration on to the connected objects.
    /// </summary>
    public class Resonant : MonoBehaviour, IResettable
    {
        public enum ResonantMaterial { Glass, Stone, Metal, Crystal, Custom }

        [SerializeField] private ResonantMaterial material = ResonantMaterial.Glass;
        [Tooltip("Pitch of the hum while resonating (Hz). Purely audiovisual.")]
        [SerializeField] private float naturalFrequency = 660f;

        [Header("Custom values (used when material = Custom)")]
        [SerializeField] private float buildUpTime = 1f;
        [SerializeField] private float ringTime = 1f;
        [SerializeField] private bool breakable = true;

        [Header("Vibration transfer (metal)")]
        [SerializeField] private List<Resonant> connected = new();
        [SerializeField, Range(0f, 1f)] private float transferFactor = 0.8f;

        [Header("Shake")]
        [Tooltip("Transform that shakes. Defaults to the first child.")]
        [SerializeField] private Transform visual;
        [SerializeField] private float shakeMagnitude = 0.05f;

        [Header("Events")]
        [SerializeField] private UnityEvent onBreak = new();

        /// <summary>Raised once when the object breaks.</summary>
        public event Action<Resonant> Broken;

        public float Amplitude { get; private set; }
        public bool IsBroken { get; private set; }
        public float NaturalFrequency => naturalFrequency;
        public ResonantMaterial Kind => material;
        public bool Breakable => GetPreset().breakable;

        private float _pendingDrive;
        private Vector3 _visualRestPosition;

        private (float buildUp, float ring, bool breakable) GetPreset()
        {
            switch (material)
            {
                case ResonantMaterial.Glass:   return (0.7f, 1.5f, true);   // sharp resonance, breaks almost at once
                case ResonantMaterial.Crystal: return (1.4f, 2.0f, true);
                case ResonantMaterial.Stone:   return (3.0f, 0.4f, true);   // slow, dull
                case ResonantMaterial.Metal:   return (1.0f, 2.5f, false);  // rings long, does not break
                default:                       return (buildUpTime, ringTime, breakable);
            }
        }

        private void Awake()
        {
            if (visual == null && transform.childCount > 0) visual = transform.GetChild(0);
            if (visual != null) _visualRestPosition = visual.localPosition;
        }

        /// <summary>Drive this object at its natural frequency. Call every frame while driving. 1 = full strength.</summary>
        public void Drive(float strength)
        {
            if (IsBroken) return;
            _pendingDrive = Mathf.Max(_pendingDrive, strength);
        }

        private void Update()
        {
            if (IsBroken) return;
            var preset = GetPreset();
            float dt = Time.deltaTime;

            if (_pendingDrive > 0f)
            {
                Amplitude += _pendingDrive * dt / Mathf.Max(0.05f, preset.buildUp);
                // Metal passes the vibration on along its connections.
                for (int i = 0; i < connected.Count; i++)
                {
                    if (connected[i] != null && connected[i] != this) connected[i].Drive(_pendingDrive * transferFactor);
                }
            }
            else
            {
                Amplitude *= Mathf.Exp(-dt / Mathf.Max(0.05f, preset.ring));
            }
            _pendingDrive = 0f;

            if (!preset.breakable) Amplitude = Mathf.Min(Amplitude, 0.95f);

            // Visual shake grows with amplitude squared (energy).
            if (visual != null)
            {
                visual.localPosition = Amplitude > 0.01f
                    ? _visualRestPosition + UnityEngine.Random.insideUnitSphere * shakeMagnitude * Amplitude * Amplitude
                    : _visualRestPosition;
            }

            if (preset.breakable && Amplitude >= 1f) Break();
        }

        /// <summary>Back to unbroken and still (called when the room resets).</summary>
        public void ResetState()
        {
            IsBroken = false;
            Amplitude = 0f;
            _pendingDrive = 0f;
            if (visual != null) visual.localPosition = _visualRestPosition;
        }

        public void Break()
        {
            if (IsBroken) return;
            IsBroken = true;
            Amplitude = 0f;
            if (visual != null) visual.localPosition = _visualRestPosition;
            ProceduralAudio.PlayShatter(transform.position, material == ResonantMaterial.Stone ? 0.5f : 1f);
            Broken?.Invoke(this);
            onBreak?.Invoke();

            if (TryGetComponent(out BoxShatter shatter)) shatter.Shatter();
            else gameObject.SetActive(false);
        }
    }
}

using System.Collections.Generic;
using FracturedFrequencies.Player;
using FracturedFrequencies.World;
using UnityEngine;

namespace FracturedFrequencies.Tools
{
    /// <summary>
    /// The player's only tool.
    ///
    /// Pulse (left click): a low-frequency pressure wave in a cone.
    ///   - Pushes rigidbodies with an impulse, so heavier objects move less (impulse = m * dv, F = ma).
    ///   - The push gets weaker with distance (inverse-square falloff).
    ///   - Pushes the player the opposite way (Newton's third law). Strongest when the fork is close to a
    ///     surface, weak in open air. Aim at the ground to jump, at a wall to fly back, down to brake a fall.
    ///
    /// Resonate (hold right click): drives the Resonant object under the crosshair at its natural frequency.
    ///
    /// Both scale with the room's WorldPhysics.SoundTransmission (vacuum 0, air 1, water 3).
    /// </summary>
    [RequireComponent(typeof(PlayerMotor))]
    public class TuningFork : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("Aiming camera. Defaults to Camera.main.")]
        [SerializeField] private Transform aimCamera;
        [Tooltip("Where pulses and the resonance beam start.")]
        [SerializeField] private Transform forkTip;
        [Tooltip("Per-room physics settings. Without it, normal air is assumed.")]
        [SerializeField] private WorldPhysics worldPhysics;
        [Tooltip("Material for the pulse ring and resonance beam (e.g. Sprites/Default).")]
        [SerializeField] private Material fxMaterial;

        [Header("Pulse")]
        [SerializeField] private float pulseRange = 7f;
        [SerializeField] private float pulseConeAngle = 35f;
        [Tooltip("Impulse given to objects at point-blank range (N*s). A 2 kg crate gets 12.5 m/s, an 80 kg crate 0.3 m/s.")]
        [SerializeField] private float pulseImpulse = 25f;
        [Tooltip("Distance at which the push has dropped to half (inverse-square falloff).")]
        [SerializeField] private float falloffDistance = 3f;
        [Tooltip("Velocity change for the player when pulsing against a nearby surface (m/s).")]
        [SerializeField] private float recoilSpeed = 9f;
        [Tooltip("Recoil fraction when there is no surface in range (pushing against air).")]
        [SerializeField, Range(0f, 1f)] private float airRecoilFactor = 0.3f;
        [Tooltip("How much of the velocity toward the aim direction is cancelled (lets you brake a fall).")]
        [SerializeField, Range(0f, 1f)] private float brakeFactor = 0.75f;
        [SerializeField] private float pulseCooldown = 0.35f;
        [Tooltip("When aiming within this angle of straight down, the recoil goes straight up (a clean jump).")]
        [SerializeField] private float groundAssistAngle = 35f;

        [Header("Resonate")]
        [SerializeField] private float resonateRange = 18f;
        [Tooltip("In a vacuum, Pulse and Resonate only work within this distance (touching).")]
        [SerializeField] private float contactDistance = 1.5f;

        /// <summary>The object currently being resonated, or null.</summary>
        public Resonant CurrentTarget { get; private set; }

        private PlayerMotor _motor;
        private PlayerInputReader _input;
        private Rigidbody _rigidbody;
        private float _cooldown;
        private LineRenderer _beam;
        private AudioSource _humSource;
        private readonly Collider[] _overlap = new Collider[64];
        private readonly HashSet<Rigidbody> _pushed = new();
        private readonly RaycastHit[] _rayHits = new RaycastHit[16];

        private Vector3 Origin => forkTip != null ? forkTip.position : transform.position + _motor.Up * 1.1f;
        private float Transmission => worldPhysics != null ? worldPhysics.SoundTransmission : 1f;

        private void Awake()
        {
            _motor = GetComponent<PlayerMotor>();
            _input = GetComponent<PlayerInputReader>();
            _rigidbody = GetComponent<Rigidbody>();
            if (aimCamera == null && Camera.main != null) aimCamera = Camera.main.transform;
            if (fxMaterial == null)
            {
                var shader = Shader.Find("Sprites/Default");
                if (shader != null) fxMaterial = new Material(shader);
            }

            var beamObject = new GameObject("ResonanceBeam");
            beamObject.transform.SetParent(transform, false);
            _beam = beamObject.AddComponent<LineRenderer>();
            _beam.positionCount = 16;
            _beam.widthMultiplier = 0.04f;
            _beam.useWorldSpace = true;
            _beam.sharedMaterial = fxMaterial;
            _beam.startColor = new Color(0.75f, 0.85f, 1f, 0.9f);
            _beam.endColor = new Color(1f, 0.8f, 0.95f, 0.6f);
            _beam.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _beam.enabled = false;

            _humSource = gameObject.AddComponent<AudioSource>();
            _humSource.clip = ProceduralAudio.HumClip;
            _humSource.loop = true;
            _humSource.volume = 0f;
            _humSource.spatialBlend = 0f;
            _humSource.playOnAwake = false;
        }

        private void Update()
        {
            _cooldown -= Time.deltaTime;
            if (_input.PulsePressed && _cooldown <= 0f)
            {
                Pulse();
                _cooldown = pulseCooldown;
            }
            UpdateResonate();
        }

        // ------------------------------------------------------------------ Pulse

        private void Pulse()
        {
            Vector3 origin = Origin;
            Vector3 direction = AimDirection();
            float transmission = Transmission;

            // 1. Push objects in the cone.
            _pushed.Clear();
            int count = Physics.OverlapSphereNonAlloc(origin, pulseRange, _overlap, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                var body = _overlap[i].attachedRigidbody;
                if (body == null || body == _rigidbody || body.isKinematic || _pushed.Contains(body)) continue;
                Vector3 point = _overlap[i].ClosestPoint(origin);
                Vector3 toPoint = point - origin;
                float distance = toPoint.magnitude;
                if (distance > 0.01f && Vector3.Angle(direction, toPoint) > pulseConeAngle) continue;
                _pushed.Add(body);

                float falloff = 1f / (1f + (distance / falloffDistance) * (distance / falloffDistance));
                float effective = transmission <= 0f ? (distance <= contactDistance ? 1f : 0f) : transmission;
                body.AddForceAtPosition(direction * pulseImpulse * falloff * effective, point, ForceMode.Impulse);

                // A pulse also gives resonant objects a small kick.
                var resonant = body.GetComponentInParent<Resonant>();
                if (resonant != null) resonant.Drive(0.15f);
            }

            // 2. Recoil on the player (Newton's third law).
            float factor;
            if (Raycast(origin, direction, pulseRange, out var hit))
            {
                float proximity = 1f - (hit.distance / pulseRange) * 0.6f;
                factor = transmission <= 0f
                    ? (hit.distance <= contactDistance ? 1f : 0f)
                    : proximity * Mathf.Min(transmission, 1.5f);
            }
            else
            {
                factor = airRecoilFactor * transmission;
            }

            // Aiming roughly down: push straight up, so jumping and climbing feel predictable.
            Vector3 recoilDirection = -direction;
            if (Vector3.Angle(direction, -_motor.Up) < groundAssistAngle) recoilDirection = _motor.Up;

            Vector3 velocity = _rigidbody.linearVelocity;
            float toward = Vector3.Dot(velocity, -recoilDirection);
            if (toward > 0f) velocity += recoilDirection * toward * brakeFactor * Mathf.Clamp01(factor * 1.5f);
            velocity += recoilDirection * recoilSpeed * factor;
            _rigidbody.linearVelocity = velocity;

            PulseRing.Spawn(origin, direction, pulseRange * 0.6f, fxMaterial);
            ProceduralAudio.PlayPulse(origin);
        }

        // ------------------------------------------------------------------ Resonate

        private void UpdateResonate()
        {
            CurrentTarget = null;
            Vector3 hitPoint = Vector3.zero;

            if (_input.ResonateHeld && aimCamera != null &&
                Raycast(aimCamera.position, aimCamera.forward, resonateRange + 6f, out var hit))
            {
                float distanceFromFork = Vector3.Distance(Origin, hit.point);
                bool inRange = Transmission > 0f ? distanceFromFork <= resonateRange : distanceFromFork <= contactDistance;
                var resonant = hit.collider.GetComponentInParent<Resonant>();
                if (resonant != null && inRange && !resonant.IsBroken)
                {
                    resonant.Drive(Mathf.Clamp(Transmission, 0.5f, 2f));
                    CurrentTarget = resonant;
                    hitPoint = hit.point;
                }
            }

            if (CurrentTarget != null)
            {
                _beam.enabled = true;
                Vector3 a = Origin;
                Vector3 side = Vector3.Cross(hitPoint - a, _motor.Up).normalized;
                float amplitude = 0.05f + 0.15f * CurrentTarget.Amplitude;
                for (int i = 0; i < _beam.positionCount; i++)
                {
                    float s = i / (_beam.positionCount - 1f);
                    float wave = Mathf.Sin(s * 20f - Time.time * 40f) * amplitude * Mathf.Sin(s * Mathf.PI);
                    _beam.SetPosition(i, Vector3.Lerp(a, hitPoint, s) + side * wave);
                }
                if (!_humSource.isPlaying) _humSource.Play();
                _humSource.pitch = Mathf.Lerp(0.8f, 1.6f, CurrentTarget.Amplitude) * CurrentTarget.NaturalFrequency / 440f;
                _humSource.volume = Mathf.MoveTowards(_humSource.volume, 0.25f, Time.deltaTime * 2f);
            }
            else
            {
                _beam.enabled = false;
                _humSource.volume = Mathf.MoveTowards(_humSource.volume, 0f, Time.deltaTime * 3f);
                if (_humSource.volume <= 0f && _humSource.isPlaying) _humSource.Stop();
            }
        }

        // ------------------------------------------------------------------ Helpers

        /// <summary>
        /// Pulse direction = the crosshair direction. (Aiming from the fork to the crosshair point
        /// tilts the pulse forward when looking down, which threw the player backwards.)
        /// </summary>
        private Vector3 AimDirection() => aimCamera != null ? aimCamera.forward : transform.forward;

        /// <summary>Raycast that ignores the player's own colliders and triggers.</summary>
        private bool Raycast(Vector3 origin, Vector3 direction, float maxDistance, out RaycastHit closest)
        {
            closest = default;
            int count = Physics.RaycastNonAlloc(origin, direction, _rayHits, maxDistance, ~0, QueryTriggerInteraction.Ignore);
            float best = float.MaxValue;
            bool found = false;
            for (int i = 0; i < count; i++)
            {
                if (_rayHits[i].collider.attachedRigidbody == _rigidbody) continue;
                if (_rayHits[i].distance < best)
                {
                    best = _rayHits[i].distance;
                    closest = _rayHits[i];
                    found = true;
                }
            }
            return found;
        }
    }
}

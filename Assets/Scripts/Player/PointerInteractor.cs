using FracturedFrequencies.Gravity;
using FracturedFrequencies.Tools;
using UnityEngine;

namespace FracturedFrequencies.Player
{
    /// <summary>
    /// Everything the mouse does:
    /// - Left click a gravity crystal: its surface becomes the floor.
    /// - Hold right click on a resonant object (glass): it vibrates more and more until it shatters.
    /// Also reports what is under the cursor so the HUD can show a tooltip.
    /// </summary>
    public class PointerInteractor : MonoBehaviour
    {
        [SerializeField] private Camera viewCamera;
        [SerializeField] private PlayerInputReader input;
        [SerializeField] private PlayerMotor player;
        [SerializeField] private Material beamMaterial;
        [SerializeField] private float maxDistance = 300f;

        public GravitySwitchCrystal HoveredCrystal { get; private set; }
        public Resonant HoveredResonant { get; private set; }
        public ImpactBreakable HoveredImpactBreakable { get; private set; }
        /// <summary>The object being resonated right now, or null.</summary>
        public Resonant ResonateTarget { get; private set; }

        private readonly RaycastHit[] _hits = new RaycastHit[32];
        private LineRenderer _beam;
        private AudioSource _hum;
        private Vector3 _hitPoint;

        private void Awake()
        {
            var beamObject = new GameObject("ResonanceBeam");
            beamObject.transform.SetParent(transform, false);
            _beam = beamObject.AddComponent<LineRenderer>();
            _beam.positionCount = 20;
            _beam.widthMultiplier = 0.06f;
            _beam.useWorldSpace = true;
            _beam.sharedMaterial = beamMaterial;
            _beam.startColor = new Color(0.7f, 0.9f, 1f, 0.95f);
            _beam.endColor = new Color(1f, 0.75f, 0.95f, 0.8f);
            _beam.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _beam.enabled = false;

            _hum = gameObject.AddComponent<AudioSource>();
            _hum.clip = ProceduralAudio.HumClip;
            _hum.loop = true;
            _hum.volume = 0f;
            _hum.playOnAwake = false;
        }

        private void Update()
        {
            FindHovered();

            if (HoveredCrystal != null && input.ClickPressed) HoveredCrystal.Activate();

            ResonateTarget = null;
            if (input.ResonateHeld && HoveredResonant != null && !HoveredResonant.IsBroken)
            {
                HoveredResonant.Drive(1f);
                ResonateTarget = HoveredResonant;
            }
            UpdateBeam();
        }

        private void FindHovered()
        {
            GravitySwitchCrystal crystal = null;
            Resonant resonant = null;
            ImpactBreakable impact = null;
            float crystalDist = float.MaxValue, resonantDist = float.MaxValue, impactDist = float.MaxValue;

            if (viewCamera != null && Time.timeScale > 0f)
            {
                Ray ray = viewCamera.ScreenPointToRay(input.PointerPosition);
                int count = Physics.RaycastNonAlloc(ray, _hits, maxDistance, ~0, QueryTriggerInteraction.Collide);
                for (int i = 0; i < count; i++)
                {
                    var hit = _hits[i];
                    var c = hit.collider.GetComponentInParent<GravitySwitchCrystal>();
                    if (c != null && hit.distance < crystalDist) { crystalDist = hit.distance; crystal = c; continue; }
                    if (hit.collider.isTrigger) continue;
                    var r = hit.collider.GetComponentInParent<Resonant>();
                    if (r != null && !r.IsBroken && hit.distance < resonantDist) { resonantDist = hit.distance; resonant = r; _hitPoint = hit.point; }
                    var b = hit.collider.GetComponentInParent<ImpactBreakable>();
                    if (b != null && hit.distance < impactDist) { impactDist = hit.distance; impact = b; }
                }
            }

            // Crystals win over everything else under the cursor.
            if (crystal != null) { resonant = null; impact = null; }

            if (crystal != HoveredCrystal)
            {
                if (HoveredCrystal != null) HoveredCrystal.SetHovered(false);
                if (crystal != null) crystal.SetHovered(true);
            }
            HoveredCrystal = crystal;
            HoveredResonant = resonant;
            HoveredImpactBreakable = resonant == null ? impact : null;
        }

        private void UpdateBeam()
        {
            if (ResonateTarget == null || player == null)
            {
                _beam.enabled = false;
                _hum.volume = Mathf.MoveTowards(_hum.volume, 0f, Time.deltaTime * 3f);
                if (_hum.volume <= 0f && _hum.isPlaying) _hum.Stop();
                return;
            }

            _beam.enabled = true;
            Vector3 a = player.transform.position + player.Up * 0.6f;
            Vector3 b = _hitPoint;
            Vector3 side = Vector3.Cross(b - a, player.Up).normalized;
            float amplitude = 0.05f + 0.2f * ResonateTarget.Amplitude;
            for (int i = 0; i < _beam.positionCount; i++)
            {
                float s = i / (_beam.positionCount - 1f);
                float wave = Mathf.Sin(s * 24f - Time.time * 40f) * amplitude * Mathf.Sin(s * Mathf.PI);
                _beam.SetPosition(i, Vector3.Lerp(a, b, s) + side * wave);
            }

            if (!_hum.isPlaying) _hum.Play();
            _hum.pitch = Mathf.Lerp(0.8f, 1.7f, ResonateTarget.Amplitude) * ResonateTarget.NaturalFrequency / 440f;
            _hum.volume = Mathf.MoveTowards(_hum.volume, 0.25f, Time.deltaTime * 2f);
        }
    }
}


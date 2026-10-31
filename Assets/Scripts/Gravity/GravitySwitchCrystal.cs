using FracturedFrequencies.Tools;
using UnityEngine;

namespace FracturedFrequencies.Gravity
{
    /// <summary>
    /// A crystal grown into a wall. Clicking it makes that wall the floor: gravity points into the wall.
    /// Crystals can be used any number of times. The crystal that matches the current gravity glows
    /// and spins; hovering one scales it up.
    /// </summary>
    public class GravitySwitchCrystal : MonoBehaviour
    {
        [SerializeField] private DirectionalGravity targetGravity;
        [Tooltip("Gravity direction after clicking: points from the crystal into its wall.")]
        [SerializeField] private Vector3 gravityDirection = Vector3.down;
        [SerializeField] private Transform visual;
        [Tooltip("Ring shown while this crystal is the active one.")]
        [SerializeField] private GameObject activeHalo;
        [SerializeField] private Material fxMaterial;

        public bool IsActive => targetGravity != null &&
                                Vector3.Dot(targetGravity.Direction, gravityDirection.normalized) > 0.99f;
        public Vector3 GravityDirection => gravityDirection.normalized;

        private bool _hovered;
        private Vector3 _baseScale = Vector3.one;

        private void Awake()
        {
            if (visual != null) _baseScale = visual.localScale;
        }

        public void SetHovered(bool hovered) => _hovered = hovered;

        /// <summary>Makes this crystal's wall the floor. Returns false if it already is.</summary>
        public bool Activate()
        {
            if (targetGravity == null || IsActive) return false;
            targetGravity.SetDirection(gravityDirection);
            ProceduralAudio.PlayGravityShift(transform.position);
            PulseRing.Spawn(transform.position, -gravityDirection.normalized, 3f, fxMaterial);
            return true;
        }

        private void Update()
        {
            bool active = IsActive;
            if (activeHalo != null && activeHalo.activeSelf != active) activeHalo.SetActive(active);
            if (visual == null) return;

            float targetScale = _hovered ? 1.35f : 1f;
            float pulse = active ? 1f + Mathf.Sin(Time.time * 4f) * 0.06f : 1f;
            visual.localScale = Vector3.Lerp(visual.localScale, _baseScale * targetScale * pulse, Time.deltaTime * 12f);
            visual.Rotate(0f, (active ? 90f : 25f) * Time.deltaTime, 0f, Space.Self);
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(1f, 0.45f, 0.8f);
            Gizmos.DrawLine(transform.position, transform.position + gravityDirection.normalized * 1.5f);
        }
    }
}

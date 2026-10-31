using UnityEngine;

namespace FracturedFrequencies.Gravity
{
    /// <summary>
    /// Gravity that pulls toward a center and gets weaker with distance squared (Room 4, Floating Rings).
    /// Not used in Room 1, but part of the shared gravity system.
    /// </summary>
    public class PointGravity : GravitySource
    {
        [Tooltip("Gravity at the surface of the planet (m/s^2).")]
        [SerializeField] private float surfaceGravity = 9.81f;
        [SerializeField] private float surfaceRadius = 5f;
        [Tooltip("Beyond this distance the planet has no effect.")]
        [SerializeField] private float range = 25f;

        public override bool Affects(Vector3 position) =>
            (position - transform.position).sqrMagnitude <= range * range;

        public override Vector3 GetGravity(Vector3 position)
        {
            Vector3 toCenter = transform.position - position;
            float d = Mathf.Max(toCenter.magnitude, surfaceRadius * 0.5f);
            // Inverse-square law: g(d) = g_surface * (R / d)^2
            float g = surfaceGravity * (surfaceRadius * surfaceRadius) / (d * d);
            return toCenter.normalized * g;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.4f, 0.8f, 1f, 0.5f);
            Gizmos.DrawWireSphere(transform.position, surfaceRadius);
            Gizmos.color = new Color(0.4f, 0.8f, 1f, 0.15f);
            Gizmos.DrawWireSphere(transform.position, range);
        }
    }
}

using FracturedFrequencies.Core;
using UnityEngine;

namespace FracturedFrequencies.Gravity
{
    /// <summary>
    /// Uniform gravity in one direction (Rooms 1 and 2).
    /// Without a zone it acts everywhere. With a zone collider it only acts inside that collider.
    /// Gravity crystals call SetDirection() to turn a wall into the new floor.
    /// </summary>
    public class DirectionalGravity : GravitySource
    {
        [SerializeField] private Vector3 direction = Vector3.down;
        [SerializeField] private float strength = 9.81f;
        [Tooltip("Optional. If set, gravity only acts inside this collider.")]
        [SerializeField] private Collider zone;

        public Vector3 Direction => direction.normalized;
        public float Strength => strength;

        public override Vector3 GetGravity(Vector3 position) => Direction * strength;

        public override bool Affects(Vector3 position)
        {
            if (zone == null) return true;
            return (zone.ClosestPoint(position) - position).sqrMagnitude < 1e-6f;
        }

        public void SetDirection(Vector3 newDirection)
        {
            if (newDirection.sqrMagnitude < 1e-6f) return;
            Vector3 previous = Direction;
            direction = newDirection.normalized;
            EventBus<GravityChangedEvent>.Raise(new GravityChangedEvent
            {
                PreviousDirection = previous,
                NewDirection = direction
            });
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.cyan;
            Vector3 p = transform.position;
            Gizmos.DrawLine(p, p + Direction * 3f);
            Gizmos.DrawWireSphere(p + Direction * 3f, 0.2f);
        }
    }
}

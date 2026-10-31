using UnityEngine;

namespace FracturedFrequencies.Gravity
{
    /// <summary>Base class for anything that produces gravity. Registers itself with the GravityManager.</summary>
    public abstract class GravitySource : MonoBehaviour
    {
        [Tooltip("Higher priority sources override lower ones where they overlap.")]
        [SerializeField] private int priority = 0;

        public int Priority => priority;

        protected virtual void OnEnable() => GravityManager.Register(this);
        protected virtual void OnDisable() => GravityManager.Unregister(this);

        /// <summary>Gravity acceleration (m/s^2, world space) at a position.</summary>
        public abstract Vector3 GetGravity(Vector3 position);

        /// <summary>Whether this source acts at a position at all.</summary>
        public virtual bool Affects(Vector3 position) => true;
    }
}

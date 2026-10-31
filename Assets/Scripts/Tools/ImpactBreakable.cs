using FracturedFrequencies.Gravity;
using FracturedFrequencies.World;
using UnityEngine;

namespace FracturedFrequencies.Tools
{
    /// <summary>
    /// Breaks when a physics object hits it hard enough. Impact energy E = 1/2 * m * v^2 is compared with
    /// a threshold, so a heavy crate falling far breaks cracked stone, while light debris or a short drop does not.
    /// Only objects with a GravityBody count (not the player).
    /// </summary>
    public class ImpactBreakable : MonoBehaviour, IResettable
    {
        [Tooltip("Kinetic energy (joules) needed to break. 80 kg falling 4 m is about 3100 J.")]
        [SerializeField] private float energyThreshold = 2500f;
        [SerializeField] private string displayName = "Cracked stone";

        public bool IsBroken { get; private set; }
        public string DisplayName => displayName;
        public float EnergyThreshold => energyThreshold;

        private void OnCollisionEnter(Collision collision)
        {
            if (IsBroken) return;
            var body = collision.rigidbody;
            if (body == null || body.GetComponent<GravityBody>() == null) return;

            float speed = collision.relativeVelocity.magnitude;
            float energy = 0.5f * body.mass * speed * speed;
            if (energy >= energyThreshold) Break();
        }

        public void Break()
        {
            if (IsBroken) return;
            IsBroken = true;
            ProceduralAudio.PlayShatter(transform.position, 0.6f);
            if (TryGetComponent(out BoxShatter shatter)) shatter.Shatter();
            else gameObject.SetActive(false);
        }

        public void ResetState() => IsBroken = false;
    }
}

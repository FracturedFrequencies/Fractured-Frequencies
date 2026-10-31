using FracturedFrequencies.Core;
using FracturedFrequencies.Gravity;
using FracturedFrequencies.World;
using UnityEngine;

namespace FracturedFrequencies.Tools
{
    /// <summary>
    /// A crystal grown into a wall. Resonate it until it breaks and gravity turns toward that wall:
    /// the wall becomes the new floor for the player, crates and debris alike.
    /// </summary>
    [RequireComponent(typeof(Resonant))]
    public class GravityCrystal : MonoBehaviour
    {
        [SerializeField] private DirectionalGravity targetGravity;
        [Tooltip("New gravity direction after breaking, usually pointing into the crystal's wall.")]
        [SerializeField] private Vector3 newGravityDirection = Vector3.right;
        [SerializeField, TextArea] private string message = "Gravity changed! Aim down and Pulse to brake your fall.";

        private Resonant _resonant;

        private void Awake() => _resonant = GetComponent<Resonant>();
        private void OnEnable() => _resonant.Broken += OnBroken;
        private void OnDisable() => _resonant.Broken -= OnBroken;

        private void OnBroken(Resonant resonant)
        {
            if (targetGravity != null) targetGravity.SetDirection(newGravityDirection);
            else Debug.LogWarning("[FF] GravityCrystal has no target gravity assigned.", this);

            ProceduralAudio.PlayGravityShift(transform.position);
            EventBus<HudMessagePostedEvent>.Raise(new HudMessagePostedEvent { Text = message, Duration = 4f });
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(1f, 0.6f, 0.9f);
            Gizmos.DrawLine(transform.position, transform.position + newGravityDirection.normalized * 2f);
        }
    }
}

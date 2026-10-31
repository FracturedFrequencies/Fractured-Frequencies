using UnityEngine;

namespace FracturedFrequencies.Gravity
{
    /// <summary>
    /// Makes a Rigidbody follow the custom gravity system instead of Unity's global gravity.
    /// Put it on every physics object (crates, debris). The player handles gravity in PlayerMotor.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class GravityBody : MonoBehaviour
    {
        [SerializeField] private float gravityScale = 1f;

        private Rigidbody _rigidbody;

        private void Awake()
        {
            _rigidbody = GetComponent<Rigidbody>();
            _rigidbody.useGravity = false;
        }

        private void FixedUpdate()
        {
            if (_rigidbody.isKinematic) return;
            // AddForce also wakes sleeping bodies, so objects react when gravity changes direction.
            _rigidbody.AddForce(GravityManager.GetGravity(_rigidbody.worldCenterOfMass) * gravityScale, ForceMode.Acceleration);
        }
    }
}

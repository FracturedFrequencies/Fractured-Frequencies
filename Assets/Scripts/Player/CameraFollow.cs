using UnityEngine;

namespace FracturedFrequencies.Player
{
    /// <summary>
    /// Put on the Main Camera. Follows the target at a fixed offset and smoothly looks at it.
    /// Ported from FF-Demo (CameraFollow + LookAt merged).
    /// </summary>
    public class CameraFollow : MonoBehaviour
    {
        [SerializeField] private Transform target;
        [SerializeField] private Vector3 offset = new Vector3(0f, 12f, -8f);
        [SerializeField] private float followSmoothTime = 0.1f;
        [SerializeField] private float lookSmoothTime = 2f;

        private Vector3 _moveVelocity;
        private Vector3 _lookVelocity;

        private void LateUpdate()
        {
            if (target == null) return;

            // Follow
            transform.position = Vector3.SmoothDamp(transform.position, target.position + offset, ref _moveVelocity, followSmoothTime);

            // Look at
            Vector3 direction = (target.position - transform.position).normalized;
            Vector3 forward = Vector3.SmoothDamp(transform.forward, direction, ref _lookVelocity, lookSmoothTime);
            transform.rotation = Quaternion.LookRotation(forward);
        }
    }
}

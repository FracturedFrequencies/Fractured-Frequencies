using UnityEngine;

namespace FracturedFrequencies.Player
{
    /// <summary>
    /// Third-person orbit camera that follows the player's "up".
    /// When gravity turns, the camera's reference frame is rotated by the same amount each frame,
    /// so the view rolls smoothly with the player instead of snapping.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class PlayerCamera : MonoBehaviour
    {
        [SerializeField] private PlayerMotor target;
        [SerializeField] private PlayerInputReader input;

        [SerializeField] private float distance = 5f;
        [SerializeField] private float pivotHeight = 1.3f;
        [SerializeField] private float minPitch = -40f;
        [SerializeField] private float maxPitch = 80f;
        [SerializeField] private float collisionRadius = 0.25f;
        [SerializeField] private LayerMask collisionMask = ~0;

        private float _yaw;
        private float _pitch = 15f;
        private Quaternion _frame = Quaternion.identity;
        private Vector3 _lastUp = Vector3.up;
        private readonly RaycastHit[] _hits = new RaycastHit[8];

        private void Start()
        {
            if (target == null)
            {
                Debug.LogError("[FF] PlayerCamera has no target assigned.", this);
                enabled = false;
                return;
            }
            if (input == null) input = target.GetComponent<PlayerInputReader>();

            _lastUp = target.Up;
            _frame = Quaternion.FromToRotation(Vector3.up, _lastUp);
            // Start looking the same way the player faces.
            Vector3 f = Quaternion.Inverse(_frame) * target.transform.forward;
            _yaw = Mathf.Atan2(f.x, f.z) * Mathf.Rad2Deg;
        }

        private void LateUpdate()
        {
            // Parallel transport: rotate the camera frame by however much "up" changed.
            Vector3 up = target.Up;
            _frame = Quaternion.FromToRotation(_lastUp, up) * _frame;
            _lastUp = up;

            if (input != null)
            {
                _yaw += input.Look.x;
                _pitch = Mathf.Clamp(_pitch - input.Look.y, minPitch, maxPitch);
            }

            Quaternion rotation = _frame * Quaternion.Euler(_pitch, _yaw, 0f);
            Vector3 pivot = target.transform.position + up * pivotHeight;
            Vector3 back = rotation * Vector3.back;

            // Pull the camera in when something is between it and the player.
            float d = distance;
            int count = Physics.SphereCastNonAlloc(pivot, collisionRadius, back, _hits, distance, collisionMask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                if (_hits[i].collider.attachedRigidbody == target.Body) continue;
                if (_hits[i].distance > 0f && _hits[i].distance - 0.05f < d) d = _hits[i].distance - 0.05f;
            }
            d = Mathf.Max(d, 0.5f);

            transform.SetPositionAndRotation(pivot + back * d, rotation);
        }
    }
}

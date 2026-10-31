using UnityEngine;

namespace FracturedFrequencies.Player
{
    /// <summary>
    /// Fixed-angle 2.5D camera: looks at the XY plane from the front (-Z) and follows the player.
    /// It rolls together with the player's "up", so "down" is always the bottom of the screen
    /// and a gravity change turns the whole room around the player.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class SideCamera : MonoBehaviour
    {
        [SerializeField] private PlayerMotor target;
        [SerializeField] private float distance = 26f;
        [SerializeField] private float followSmoothTime = 0.15f;
        [Tooltip("Looks a little ahead in the direction of movement.")]
        [SerializeField] private float lookAhead = 0.25f;
        [Tooltip("Turn off to keep the camera upright when gravity changes.")]
        [SerializeField] private bool rollWithGravity = true;

        private Vector3 _focus;
        private Vector3 _focusVelocity;

        private void Start()
        {
            if (target == null)
            {
                Debug.LogError("[FF] SideCamera has no target assigned.", this);
                enabled = false;
                return;
            }
            _focus = target.transform.position;
            Apply();
        }

        private void LateUpdate() => Apply();

        private void Apply()
        {
            Vector3 velocity = target.Body != null ? target.Body.linearVelocity : Vector3.zero;
            Vector3 desired = target.transform.position + Vector3.ClampMagnitude(velocity * lookAhead, 3f);
            desired.z = 0f;
            _focus = Vector3.SmoothDamp(_focus, desired, ref _focusVelocity, followSmoothTime);

            float roll = rollWithGravity ? target.UpAngle - 90f : 0f;
            transform.SetPositionAndRotation(
                new Vector3(_focus.x, _focus.y, -distance),
                Quaternion.Euler(0f, 0f, roll));
        }
    }
}

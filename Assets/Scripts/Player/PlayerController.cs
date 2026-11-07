using UnityEngine;

namespace FracturedFrequencies.Player
{
    /// <summary>
    /// Simple CharacterController player: WASD moves along world X/Z, Space jumps.
    /// Input comes from PlayerInputReader. Ported from FF-Demo.
    /// </summary>
    [RequireComponent(typeof(CharacterController), typeof(PlayerInputReader))]
    public class PlayerController : MonoBehaviour
    {
        [SerializeField] private float moveSpeed = 8f;
        [SerializeField] private float speedSmoothing = 0.1f;
        [SerializeField] private float rotationSmoothing = 12f;
        [SerializeField] private float jumpSpeed = 25f;
        [SerializeField] private float gravity = -85f;

        private CharacterController _controller;
        private PlayerInputReader _input;
        private Vector3 _velocity;
        private Vector3 _velocitySmoothing;
        private float _verticalSpeed;

        private void Awake()
        {
            _controller = GetComponent<CharacterController>();
            _input = GetComponent<PlayerInputReader>();
        }

        private void Update()
        {
            // Horizontal movement
            Vector3 move = new Vector3(_input.Move.x, 0f, _input.Move.y);
            _velocity = Vector3.SmoothDamp(_velocity, move * moveSpeed, ref _velocitySmoothing, speedSmoothing);

            // Face the walking direction
            if (move.sqrMagnitude > 0.01f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(move);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, 1f - Mathf.Exp(-rotationSmoothing * Time.deltaTime));
            }

            // Jump and gravity
            if (_controller.isGrounded)
            {
                if (_verticalSpeed < 0f) _verticalSpeed = -2f; // keeps the controller pressed to the ground
                if (_input.JumpPressed) _verticalSpeed = jumpSpeed;
            }
            _verticalSpeed += gravity * Time.deltaTime;

            _controller.Move((_velocity + Vector3.up * _verticalSpeed) * Time.deltaTime);
        }
    }
}

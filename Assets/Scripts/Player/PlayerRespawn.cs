using FracturedFrequencies.Core;
using FracturedFrequencies.Gravity;
using FracturedFrequencies.World;
using UnityEngine;

namespace FracturedFrequencies.Player
{
    /// <summary>
    /// Remembers the last safe spot (standing on static ground for a moment) and puts the player
    /// back there after falling into the void, touching a KillZone, falling for too long, or pressing R.
    /// </summary>
    [RequireComponent(typeof(PlayerMotor))]
    public class PlayerRespawn : MonoBehaviour
    {
        [SerializeField] private float safeAfterSeconds = 0.4f;
        [SerializeField] private float maxAirTime = 6f;

        private PlayerMotor _motor;
        private PlayerInputReader _input;
        private Vector3 _safePosition;
        private float _groundedTime;
        private float _airTime;

        private void Awake()
        {
            _motor = GetComponent<PlayerMotor>();
            _input = GetComponent<PlayerInputReader>();
            _safePosition = transform.position;
        }

        private void Update()
        {
            if (_input != null && _input.RespawnPressed) Respawn();
        }

        private void FixedUpdate()
        {
            // Only static ground counts as safe (not crates, which can move or fall away).
            if (_motor.IsGrounded && _motor.GroundBody == null)
            {
                _groundedTime += Time.fixedDeltaTime;
                _airTime = 0f;
                if (_groundedTime >= safeAfterSeconds) _safePosition = _motor.Body.position;
            }
            else
            {
                _groundedTime = 0f;
                if (!_motor.IsGrounded)
                {
                    _airTime += Time.fixedDeltaTime;
                    if (_airTime > maxAirTime) Respawn();
                }
            }
        }

        public void Respawn()
        {
            _airTime = 0f;
            _groundedTime = 0f;
            // Use the current gravity at the safe spot, in case gravity changed since.
            Vector3 up = GravityManager.GetUp(_safePosition);
            _motor.Teleport(_safePosition + up * 0.05f, up);
            EventBus<HudMessagePostedEvent>.Raise(new HudMessagePostedEvent
            {
                Text = "Back to the last safe spot.",
                Duration = 1.5f
            });
        }
    }
}

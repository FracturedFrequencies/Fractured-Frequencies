using FracturedFrequencies.Gravity;
using UnityEngine;

namespace FracturedFrequencies.Player
{
    /// <summary>
    /// Physics character for the fixed isometric camera.
    /// - Reads gravity from the GravityManager and turns its feet toward "down" (floor or a wall).
    /// - WASD move relative to the screen, projected onto whatever surface is currently the floor,
    ///   so W is always "up on the screen" even while walking on a wall.
    /// - Falling speed is capped (terminal speed).
    /// </summary>
    [RequireComponent(typeof(Rigidbody), typeof(CapsuleCollider), typeof(PlayerInputReader))]
    public class PlayerMotor : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("The isometric camera. Defaults to Camera.main.")]
        [SerializeField] private Transform cameraTransform;

        [Header("Movement")]
        [SerializeField] private float moveSpeed = 5f;
        [SerializeField] private float groundAcceleration = 50f;
        [SerializeField] private float airAcceleration = 20f;
        [SerializeField] private float jumpSpeed = 6f;
        [SerializeField] private float coyoteTime = 0.1f;
        [SerializeField] private float terminalSpeed = 14f;

        [Header("Orientation")]
        [SerializeField] private float uprightSpeed = 8f;

        [Header("Ground check")]
        [SerializeField] private float maxGroundAngle = 50f;
        [SerializeField] private float groundCheckDistance = 0.15f;
        [SerializeField] private LayerMask groundMask = ~0;

        public Vector3 Up { get; private set; } = Vector3.up;
        /// <summary>Angle of "up" in the XY plane (kept for older scripts).</summary>
        public float UpAngle => Mathf.Atan2(Up.y, Up.x) * Mathf.Rad2Deg;
        public bool IsGrounded { get; private set; }
        public Rigidbody GroundBody { get; private set; }
        public Rigidbody Body => _rigidbody;

        private Rigidbody _rigidbody;
        private CapsuleCollider _capsule;
        private PlayerInputReader _input;
        private bool _jumpRequested;
        private float _lastGroundedTime = -1f;
        private Vector3 _facing = Vector3.forward;
        private readonly RaycastHit[] _hits = new RaycastHit[16];

        private void Awake()
        {
            _rigidbody = GetComponent<Rigidbody>();
            _capsule = GetComponent<CapsuleCollider>();
            _input = GetComponent<PlayerInputReader>();

            _rigidbody.useGravity = false;
            _rigidbody.freezeRotation = true;
            _rigidbody.interpolation = RigidbodyInterpolation.Interpolate;
            _rigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

            if (_capsule.sharedMaterial == null)
            {
                _capsule.sharedMaterial = new PhysicsMaterial("PlayerNoFriction")
                {
                    dynamicFriction = 0f,
                    staticFriction = 0f,
                    frictionCombine = PhysicsMaterialCombine.Minimum,
                    bounceCombine = PhysicsMaterialCombine.Minimum
                };
            }

            if (cameraTransform == null && Camera.main != null) cameraTransform = Camera.main.transform;
            Up = GravityManager.GetUp(transform.position);
        }

        private void Update()
        {
            if (_input.JumpPressed) _jumpRequested = true;
        }

        private void FixedUpdate()
        {
            float dt = Time.fixedDeltaTime;
            Vector3 gravity = GravityManager.GetGravity(_rigidbody.worldCenterOfMass);

            // 1. Turn toward the new "up".
            if (gravity.sqrMagnitude > 1e-4f)
                Up = Vector3.Slerp(Up, -gravity.normalized, 1f - Mathf.Exp(-uprightSpeed * dt)).normalized;

            // 2. Gravity and ground check.
            _rigidbody.AddForce(gravity, ForceMode.Acceleration);
            CheckGround();
            if (IsGrounded) _lastGroundedTime = Time.time;

            // 3. Screen-relative movement on the current floor plane.
            Vector3 wish = ScreenToSurface(_input.Move);
            Vector3 velocity = _rigidbody.linearVelocity;
            Vector3 vertical = Vector3.Project(velocity, Up);
            Vector3 planar = velocity - vertical;
            if (IsGrounded || wish.sqrMagnitude > 1e-4f)
            {
                float acceleration = IsGrounded ? groundAcceleration : airAcceleration;
                planar = Vector3.MoveTowards(planar, wish * moveSpeed, acceleration * dt);
            }
            velocity = planar + vertical;

            // 4. Terminal speed.
            if (gravity.sqrMagnitude > 1e-4f)
            {
                Vector3 down = gravity.normalized;
                float fall = Vector3.Dot(velocity, down);
                if (fall > terminalSpeed) velocity -= down * (fall - terminalSpeed);
            }

            // 5. Jump.
            if (_jumpRequested && Time.time - _lastGroundedTime <= coyoteTime)
            {
                velocity += Up * (jumpSpeed - Vector3.Dot(velocity, Up));
                _lastGroundedTime = -1f;
                IsGrounded = false;
            }
            _jumpRequested = false;
            _rigidbody.linearVelocity = velocity;

            // 6. Face the walking direction.
            if (wish.sqrMagnitude > 0.01f) _facing = wish;
            Vector3 facing = Vector3.ProjectOnPlane(_facing, Up);
            if (facing.sqrMagnitude < 1e-4f) facing = Vector3.ProjectOnPlane(transform.forward, Up);
            if (facing.sqrMagnitude > 1e-4f) _rigidbody.MoveRotation(Quaternion.LookRotation(facing.normalized, Up));
        }

        /// <summary>
        /// Converts WASD into a direction on the current floor plane (perpendicular to "up") whose movement
        /// ON SCREEN is exactly screen-right / screen-up. This keeps W = up and D = right on the screen,
        /// also while standing on a wall.
        /// </summary>
        private Vector3 ScreenToSurface(Vector2 move)
        {
            if (move.sqrMagnitude < 1e-4f) return Vector3.zero;
            Vector3 screenRight = cameraTransform != null ? cameraTransform.right : Vector3.right;
            Vector3 screenUp = cameraTransform != null ? cameraTransform.up : Vector3.forward;

            // Orthonormal basis (e1, e2) of the floor plane.
            Vector3 e1 = Vector3.ProjectOnPlane(screenUp, Up);
            if (e1.sqrMagnitude < 1e-4f) e1 = Vector3.ProjectOnPlane(screenRight, Up);
            e1.Normalize();
            Vector3 e2 = Vector3.Cross(Up, e1).normalized;

            // How a step along e1 / e2 moves on screen: 2x2 matrix [a b; c d].
            float a = Vector3.Dot(e1, screenRight), b = Vector3.Dot(e2, screenRight);
            float c = Vector3.Dot(e1, screenUp),    d = Vector3.Dot(e2, screenUp);
            float det = a * d - b * c;

            Vector3 rightDir, upDir;
            if (Mathf.Abs(det) > 0.15f)
            {
                // Solve for the plane directions that appear exactly as screen-right and screen-up.
                rightDir = (e1 * d - e2 * c) / det;   // M^-1 * (1, 0)
                upDir = (-e1 * b + e2 * a) / det;     // M^-1 * (0, 1)
            }
            else
            {
                // The floor is seen almost edge-on: fall back to a simple projection.
                rightDir = Vector3.ProjectOnPlane(screenRight, Up);
                upDir = Vector3.ProjectOnPlane(screenUp + (cameraTransform != null ? cameraTransform.forward : Vector3.zero), Up);
            }
            if (rightDir.sqrMagnitude < 1e-6f || upDir.sqrMagnitude < 1e-6f) return Vector3.zero;

            return Vector3.ClampMagnitude(rightDir.normalized * move.x + upDir.normalized * move.y, 1f);
        }

        private void CheckGround()
        {
            float radius = _capsule.radius * 0.9f;
            Vector3 center = transform.TransformPoint(_capsule.center);
            float castDistance = _capsule.height * 0.5f - _capsule.radius + groundCheckDistance + (_capsule.radius - radius);

            int count = Physics.SphereCastNonAlloc(center, radius, -Up, _hits, castDistance, groundMask, QueryTriggerInteraction.Ignore);
            IsGrounded = false;
            GroundBody = null;
            float best = float.MaxValue;
            for (int i = 0; i < count; i++)
            {
                var hit = _hits[i];
                if (hit.collider.attachedRigidbody == _rigidbody) continue;
                if (hit.distance <= 0f && hit.point == Vector3.zero) continue;
                if (Vector3.Angle(hit.normal, Up) > maxGroundAngle) continue;
                if (hit.distance < best)
                {
                    best = hit.distance;
                    IsGrounded = true;
                    GroundBody = hit.collider.attachedRigidbody;
                }
            }
        }

        /// <summary>Instantly moves the player and snaps "up" (used when entering or resetting a room).</summary>
        public void Teleport(Vector3 position, Vector3 up)
        {
            Up = up.normalized;
            _rigidbody.linearVelocity = Vector3.zero;
            _rigidbody.angularVelocity = Vector3.zero;
            Vector3 facing = Vector3.ProjectOnPlane(_facing, Up);
            if (facing.sqrMagnitude < 1e-4f) facing = Vector3.ProjectOnPlane(Vector3.forward, Up);
            var rotation = Quaternion.LookRotation(facing.normalized, Up);
            _rigidbody.position = position;
            _rigidbody.rotation = rotation;
            transform.SetPositionAndRotation(position, rotation);
        }
    }
}

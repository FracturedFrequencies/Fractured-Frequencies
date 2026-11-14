using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerController : MonoBehaviour
{
    [SerializeField] private float speed = 8f;
    [SerializeField] private float speedSmoothing = 0.1f;
    [SerializeField] private float jumpForce = 30f;
    [SerializeField] private float gravityMultiplier = 10f;
    [SerializeField] private float coyoteTime = 0.1f;
    private float g = -9.81f;
    private float gravity;
    private float lastGroundedTime = float.NegativeInfinity;
    private Vector2 move;
    private Vector3 currentVelocity;
    private Vector3 velocitySmoothing;
    private CharacterController characterController;

    void Awake()
    {
        characterController = GetComponent<CharacterController>();
    }

    void Update()
    {
        ApplyGravity();
        ApplyMovement();
    }

    private void ApplyMovement()
    {
        Vector3 targetVelocity = new Vector3(move.x, 0f, move.y) * speed;
        Vector3 horizontalVelocity = Vector3.SmoothDamp(
            new Vector3(currentVelocity.x, 0f, currentVelocity.z),
            targetVelocity,
            ref velocitySmoothing,
            speedSmoothing);
        currentVelocity = new Vector3(horizontalVelocity.x, gravity, horizontalVelocity.z);
        characterController.Move(currentVelocity * Time.deltaTime);
    }

    private void ApplyGravity()
    {
        if (characterController.isGrounded)
        {
            lastGroundedTime = Time.time;
        }
        if (characterController.isGrounded && gravity < 0f)
        {
            gravity = 0f;
        }
        else
        {
            gravity += g * gravityMultiplier * Time.deltaTime;
        }

        currentVelocity.y = gravity;
    }

    public void OnMove(InputAction.CallbackContext context)
    {
        move = context.ReadValue<Vector2>();
    }

    public void OnJump(InputAction.CallbackContext context)
    {
        if (!context.started) return;
        // Coyote time allows players to jump after leaving the ground for a short period of time
        if (Time.time - lastGroundedTime > coyoteTime) return;
        gravity = jumpForce;
    }
}

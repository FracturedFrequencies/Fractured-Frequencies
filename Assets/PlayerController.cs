using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

[RequireComponent(typeof(Rigidbody))]
public class PlayerController : MonoBehaviour
{
    public float moveSpeed = 6f;
    public float jumpHeight = 1.5f;   // meters
    public float rotateSpeed = 12f;
    public Transform cameraTransform; // optional, defaults to main camera

    private Rigidbody rb;
    private Vector3 moveDir;
    private bool jumpRequested;
    private bool isGrounded;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.freezeRotation = true;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        if (cameraTransform == null && Camera.main != null)
            cameraTransform = Camera.main.transform;
    }

    void Update()
    {
        Vector2 input = Vector2.zero;
        bool jumpPressed = false;

    #if ENABLE_INPUT_SYSTEM
        var kb = Keyboard.current;
        if (kb != null)
        {
            input.x = (kb.dKey.isPressed ? 1f : 0f) - (kb.aKey.isPressed ? 1f : 0f);
            input.y = (kb.wKey.isPressed ? 1f : 0f) - (kb.sKey.isPressed ? 1f : 0f);
            jumpPressed = kb.spaceKey.wasPressedThisFrame;
        }
    #else
        input = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
        jumpPressed = Input.GetKeyDown(KeyCode.Space);
    #endif

        input = Vector2.ClampMagnitude(input, 1f);

        Vector3 fwd = cameraTransform ? cameraTransform.forward : Vector3.forward;
        Vector3 right = cameraTransform ? cameraTransform.right : Vector3.right;
        fwd.y = 0f; right.y = 0f;
        fwd.Normalize(); right.Normalize();

        moveDir = fwd * input.y + right * input.x;

        if (jumpPressed)
            jumpRequested = true;
    }

    void FixedUpdate()
    {
        // Horizontal movement
        Vector3 v = rb.linearVelocity;
        v.x = moveDir.x * moveSpeed;
        v.z = moveDir.z * moveSpeed;
        rb.linearVelocity = v;

        // Face movement direction
        if (moveDir.sqrMagnitude > 0.001f)
        {
            Quaternion target = Quaternion.LookRotation(moveDir);
            rb.MoveRotation(Quaternion.Slerp(rb.rotation, target,
                                             rotateSpeed * Time.fixedDeltaTime));
        }

        // Jump: v = sqrt(2 * g * h)
        if (jumpRequested && isGrounded)
        {
            float jumpVelocity = Mathf.Sqrt(2f * Mathf.Abs(Physics.gravity.y) * jumpHeight);
            v = rb.linearVelocity;
            v.y = 0f;
            rb.linearVelocity = v;
            rb.AddForce(Vector3.up * jumpVelocity, ForceMode.VelocityChange);
        }
        jumpRequested = false;
        isGrounded = false; // re-set by OnCollisionStay before the next step
    }

    void OnCollisionStay(Collision collision)
    {
        foreach (ContactPoint c in collision.contacts)
        {
            if (c.normal.y > 0.7f) { isGrounded = true; return; }
        }
    }
}
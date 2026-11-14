using UnityEngine;

public class Enemy : Entity
{
    [SerializeField] private float speed = 4f;
    [SerializeField] private float speedSmoothing = 0.1f;
    [SerializeField] private float rotationSmoothing = 12f;
    [SerializeField] private float gravityMultiplier = 10f;
    private float g = -9.81f;
    private float gravity;
    private Vector2 move;
    private Vector3 currentVelocity;
    private Vector3 velocitySmoothing;
    private CharacterController characterController;

    void Awake()
    {
        Attributes = new Attributes(100);
        characterController = GetComponent<CharacterController>();
    }

    void Update()
    {
        ApplyGravity();
        ApplyMovement();
        ApplyRotation();
    }

    private void ApplyMovement()
    {
        Vector3 target = PlayerReference.Player.transform.position - transform.position;
        move = new Vector2(target.x, target.z).normalized;

        Vector3 targetVelocity = new Vector3(move.x, 0f, move.y) * speed;
        Vector3 horizontalVelocity = Vector3.SmoothDamp(
            new Vector3(currentVelocity.x, 0f, currentVelocity.z),
            targetVelocity,
            ref velocitySmoothing,
            speedSmoothing);
        currentVelocity = new Vector3(horizontalVelocity.x, gravity, horizontalVelocity.z);
        characterController.Move(currentVelocity * Time.deltaTime);
    }

    private void ApplyRotation()
    {
        if (move.sqrMagnitude > 0)
        {
            Quaternion targetRotation = Quaternion.LookRotation(new Vector3(move.x, 0f, move.y));
            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                targetRotation,
                1f - Mathf.Exp(-rotationSmoothing * Time.deltaTime));
        }
    }

    private void ApplyGravity()
    {
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
}

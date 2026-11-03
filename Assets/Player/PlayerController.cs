using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;
using JetBrains.Annotations;

public class PlayerController : MonoBehaviour
{
    [SerializeField] private float speed = 8f;
    [SerializeField] private float speedSmoothing = 0.1f;
    [SerializeField] private float rotationSmoothing = 12f;
    [SerializeField] private float jumpForce = 30f;
    [SerializeField] private float gravityMultiplier = 10f;
    private float g = -9.81f;
    private float gravity;
    private Vector2 move;
    private Vector3 currentVelocity;
    private Vector3 velocitySmoothing;
    private CharacterController characterController;

    private Coroutine activeEffectCoroutine; 
    public ChladniEffect lastEffect; //maximum of on simultaneous effect
    BaseValues baseValues;
    struct BaseValues //to return to after Effect duration
    {
        public BaseValues(float speed, float jumpForce,float gravity)
        {
            this.speed = speed; this.jumpForce = jumpForce; this.gravityMultiplier = gravity;
        }
        public float speed; public float jumpForce; public float gravityMultiplier;
    }

    void setBaseValues()
    {
        baseValues = new BaseValues(speed,jumpForce,gravityMultiplier);
    }

    void Awake()
    {
        characterController = GetComponent<CharacterController>();
        setBaseValues();
    }

    void Update()
    {
        ApplyGravity();
        ApplyMovement();
        ApplyRotation();
    }

    private void ApplyMovement()
    {
        Vector3 targetVelocity = new Vector3(move.x, 0f, move.y) * speed;
        currentVelocity = Vector3.SmoothDamp(
            currentVelocity,
            targetVelocity,
            ref velocitySmoothing,
            speedSmoothing);
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
            currentVelocity.y = gravity;
        }
    }

    public void OnMove(InputAction.CallbackContext context)
    {
        move = context.ReadValue<Vector2>();
    }

    public void OnJump(InputAction.CallbackContext context)
    {
        if (!context.started) return;
        if (!characterController.isGrounded) return;
        gravity += jumpForce;
    }

    public void ReceiveEffect(ChladniEffect ceffect, float factor, float duration)
    {
        if (activeEffectCoroutine != null)
        {
            ApplyEffectFactor(lastEffect, 1f);
            StopCoroutine(activeEffectCoroutine);
        }
        lastEffect = ceffect;
        activeEffectCoroutine = StartCoroutine(EffectCoroutine(ceffect, factor, duration));
    }

    private IEnumerator EffectCoroutine(ChladniEffect ceffect, float factor, float duration)
    {
        ApplyEffectFactor(ceffect,factor);

        yield return new WaitForSeconds(duration);

        ApplyEffectFactor(ceffect, 1f);
        activeEffectCoroutine = null;
    }

    void ApplyEffectFactor(ChladniEffect ceffect, float factor)
    {
        switch (ceffect)
        {
            case ChladniEffect.BoostJump:
                jumpForce = baseValues.jumpForce * factor;
                break;

            case ChladniEffect.HighGravity:
                gravityMultiplier = baseValues.gravityMultiplier * factor;
                break;
            case ChladniEffect.SpeedBoost:
                speed = baseValues.speed * factor;
                break;
        }
    }
}

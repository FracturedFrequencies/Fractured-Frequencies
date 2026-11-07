using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace FracturedFrequencies.Player
{
    /// <summary>
    /// Reads keyboard and mouse once per frame (new Input System; legacy Input Manager as fallback).
    /// Isometric controls: WASD move, Space jump, Left click a crystal, hold Right click to resonate,
    /// R reset the current room. The cursor stays visible. Input is ignored while paused.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class PlayerInputReader : MonoBehaviour
    {
        /// <summary>x = right/left, y = up/down on screen (-1..1).</summary>
        public Vector2 Move { get; private set; }
        public bool JumpPressed { get; private set; }
        public bool ClickPressed { get; private set; }
        public bool ResonateHeld { get; private set; }
        public bool ResetPressed { get; private set; }
        /// <summary>Mouse position in screen pixels.</summary>
        public Vector2 PointerPosition { get; private set; }

        // Kept for the older 3D prototype scripts; always idle here.
        public Vector2 Look => Vector2.zero;
        public bool PulsePressed => false;
        public bool RespawnPressed => ResetPressed;

        private void Update()
        {
            if (Time.timeScale <= 0f)
            {
                Move = Vector2.zero;
                JumpPressed = ClickPressed = ResonateHeld = ResetPressed = false;
                return;
            }

#if ENABLE_INPUT_SYSTEM
            var keyboard = Keyboard.current;
            var mouse = Mouse.current;
            Vector2 move = Vector2.zero;
            if (keyboard != null)
            {
                if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed) move.y += 1f;
                if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed) move.y -= 1f;
                if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) move.x += 1f;
                if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) move.x -= 1f;
                JumpPressed = keyboard.spaceKey.wasPressedThisFrame;
                ResetPressed = keyboard.rKey.wasPressedThisFrame;
            }
            Move = Vector2.ClampMagnitude(move, 1f);
            if (mouse != null)
            {
                PointerPosition = mouse.position.ReadValue();
                ClickPressed = mouse.leftButton.wasPressedThisFrame;
                ResonateHeld = mouse.rightButton.isPressed;
            }
#else
            Move = Vector2.ClampMagnitude(new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical")), 1f);
            JumpPressed = Input.GetKeyDown(KeyCode.Space);
            ResetPressed = Input.GetKeyDown(KeyCode.R);
            PointerPosition = Input.mousePosition;
            ClickPressed = Input.GetMouseButtonDown(0);
            ResonateHeld = Input.GetMouseButton(1);
#endif
        }
    }
}

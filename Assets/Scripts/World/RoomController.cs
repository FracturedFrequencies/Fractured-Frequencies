using System.Collections;
using FracturedFrequencies.Core;
using FracturedFrequencies.Gravity;
using FracturedFrequencies.Player;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace FracturedFrequencies.World
{
    /// <summary>
    /// First script of a level scene:
    /// - switches the game to Playing and keeps the mouse cursor visible,
    /// - toggles pause with Esc through the GameManager,
    /// - moves the player through portals into the next room,
    /// - resets the current room on R, when the player falls out of it, or when a ResetTrigger asks.
    /// </summary>
    public class RoomController : MonoBehaviour
    {
        [SerializeField] private PlayerMotor player;
        [SerializeField] private DirectionalGravity gravity;
        [SerializeField] private Chamber startChamber;
        [Tooltip("How far outside the current room the player may get before it resets.")]
        [SerializeField] private float outOfBoundsMargin = 4f;

        private GameManager _gameManager;
        private PlayerInputReader _input;
        private Chamber _current;
        private Coroutine _pendingReset;

        private void OnEnable()
        {
            EventBus<ChamberEnteredEvent>.Subscribe(OnChamberEntered);
            EventBus<ChamberResetRequestedEvent>.Subscribe(OnResetRequested);
            EventBus<PortalEnteredEvent>.Subscribe(OnPortalEntered);
        }

        private void OnDisable()
        {
            EventBus<ChamberEnteredEvent>.Unsubscribe(OnChamberEntered);
            EventBus<ChamberResetRequestedEvent>.Unsubscribe(OnResetRequested);
            EventBus<PortalEnteredEvent>.Unsubscribe(OnPortalEntered);
        }

        private void Start()
        {
            if (ServiceLocator.TryGet(out _gameManager)) _gameManager.ChangeState(GameState.Playing);
            else Debug.LogWarning("[FF] GameManager not registered; pause is disabled.", this);

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            _current = startChamber;
            if (player != null) _input = player.GetComponent<PlayerInputReader>();
        }

        private void Update()
        {
            HandlePause();
            if (player == null || _current == null) return;

            if (_input != null && _input.ResetPressed) ResetCurrent("Room reset.");

            Bounds bounds = _current.RoomBounds;
            bounds.Expand(outOfBoundsMargin * 2f);
            if (!bounds.Contains(player.transform.position)) ResetCurrent("Fell out of the room. Back to the start.");
        }

        private void HandlePause()
        {
            if (_gameManager == null) return;
            bool escape, click;
#if ENABLE_INPUT_SYSTEM
            escape = Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;
            click = Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
#else
            escape = Input.GetKeyDown(KeyCode.Escape);
            click = Input.GetMouseButtonDown(0);
#endif
            if (escape)
            {
                if (_gameManager.State == GameState.Playing) _gameManager.Pause();
                else if (_gameManager.State == GameState.Paused) _gameManager.Resume();
            }
            else if (click && _gameManager.State == GameState.Paused)
            {
                _gameManager.Resume();
            }
        }

        private void OnChamberEntered(ChamberEnteredEvent e) => _current = e.Chamber;

        private void OnPortalEntered(PortalEnteredEvent e)
        {
            _current = e.Target;
            e.Target.Restore(player, gravity);
        }

        private void OnResetRequested(ChamberResetRequestedEvent e)
        {
            if (_pendingReset == null) _pendingReset = StartCoroutine(ResetAfter(e.Delay, e.Message));
        }

        private IEnumerator ResetAfter(float delay, string message)
        {
            if (!string.IsNullOrEmpty(message)) PostMessage(message, delay + 1f);
            yield return new WaitForSeconds(delay);
            ResetCurrent(null);
            _pendingReset = null;
        }

        private void ResetCurrent(string message)
        {
            if (_current == null) return;
            _current.Restore(player, gravity);
            if (!string.IsNullOrEmpty(message)) PostMessage(message, 1.5f);
        }

        private static void PostMessage(string text, float duration) =>
            EventBus<HudMessagePostedEvent>.Raise(new HudMessagePostedEvent { Text = text, Duration = duration });
    }
}

using UnityEngine;

namespace FracturedFrequencies.Core
{
    /// <summary>
    /// Owns the high-level game state. Lives on the persistent [Systems] object
    /// created by <see cref="GameBootstrapper"/>.
    /// Resolve it via <c>ServiceLocator.Get&lt;GameManager&gt;()</c>.
    /// </summary>
    public sealed class GameManager : MonoBehaviour
    {
        public GameState State { get; private set; } = GameState.Boot;

        private GameState _stateBeforePause;

        public void ChangeState(GameState newState)
        {
            if (newState == State) return;

            var previous = State;
            State = newState;

            Debug.Log($"[FF] Game state: {previous} -> {newState}");
            EventBus<GameStateChangedEvent>.Raise(new GameStateChangedEvent
            {
                Previous = previous,
                Current = newState
            });
        }

        public void Pause()
        {
            if (State != GameState.Playing) return;

            _stateBeforePause = State;
            Time.timeScale = 0f;
            ChangeState(GameState.Paused);
        }

        public void Resume()
        {
            if (State != GameState.Paused) return;

            Time.timeScale = 1f;
            ChangeState(_stateBeforePause);
        }
    }
}

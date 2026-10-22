namespace FracturedFrequencies.Core
{
    // Engine-level events raised by Core systems.

    public struct GameStateChangedEvent : IGameEvent
    {
        public GameState Previous;
        public GameState Current;
    }
}

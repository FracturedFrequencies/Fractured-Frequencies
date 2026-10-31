using FracturedFrequencies.Core;

namespace FracturedFrequencies.World
{
    /// <summary>Raised when the player enters a HintZone.</summary>
    public struct HintEnteredEvent : IGameEvent
    {
        public int SourceId;
        public string Text;
    }
}

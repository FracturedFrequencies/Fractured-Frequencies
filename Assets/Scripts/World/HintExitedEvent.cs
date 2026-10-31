using FracturedFrequencies.Core;

namespace FracturedFrequencies.World
{
    /// <summary>Raised when the player leaves a HintZone.</summary>
    public struct HintExitedEvent : IGameEvent
    {
        public int SourceId;
    }
}

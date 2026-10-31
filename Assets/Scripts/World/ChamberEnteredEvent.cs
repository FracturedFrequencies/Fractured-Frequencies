using FracturedFrequencies.Core;

namespace FracturedFrequencies.World
{
    /// <summary>Raised when the player enters a chamber (it becomes the current checkpoint).</summary>
    public struct ChamberEnteredEvent : IGameEvent
    {
        public Chamber Chamber;
    }
}

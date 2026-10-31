using FracturedFrequencies.Core;

namespace FracturedFrequencies.World
{
    /// <summary>Raised when the player reaches a room's exit crack.</summary>
    public struct RoomCompletedEvent : IGameEvent
    {
        public string RoomName;
        public string NextSceneName;
    }
}

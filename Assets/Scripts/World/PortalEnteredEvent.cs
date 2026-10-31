using FracturedFrequencies.Core;

namespace FracturedFrequencies.World
{
    /// <summary>Raised when the player steps into a portal crack; the RoomController moves them to Target.</summary>
    public struct PortalEnteredEvent : IGameEvent
    {
        public Chamber Target;
    }
}


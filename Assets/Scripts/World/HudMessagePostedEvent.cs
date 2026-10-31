using FracturedFrequencies.Core;

namespace FracturedFrequencies.World
{
    /// <summary>Raised to show a short message on screen (for example "Gravity changed!").</summary>
    public struct HudMessagePostedEvent : IGameEvent
    {
        public string Text;
        public float Duration;
    }
}

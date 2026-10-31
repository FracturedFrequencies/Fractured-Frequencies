using FracturedFrequencies.Core;

namespace FracturedFrequencies.World
{
    /// <summary>Raised to reset the current chamber, e.g. when the player missed a jump.</summary>
    public struct ChamberResetRequestedEvent : IGameEvent
    {
        public float Delay;
        public string Message;
    }
}

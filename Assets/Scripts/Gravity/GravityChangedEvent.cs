using FracturedFrequencies.Core;
using UnityEngine;

namespace FracturedFrequencies.Gravity
{
    /// <summary>Raised when a DirectionalGravity changes direction (for example after a gravity crystal breaks).</summary>
    public struct GravityChangedEvent : IGameEvent
    {
        public Vector3 PreviousDirection;
        public Vector3 NewDirection;
    }
}

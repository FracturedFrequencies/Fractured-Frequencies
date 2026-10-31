namespace FracturedFrequencies.World
{
    /// <summary>Something that can be put back to its starting state when a room resets (e.g. shattered glass).</summary>
    public interface IResettable
    {
        void ResetState();
    }
}


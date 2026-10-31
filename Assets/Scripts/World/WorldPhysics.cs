using UnityEngine;

namespace FracturedFrequencies.World
{
    /// <summary>
    /// Per-room physics settings. One per scene, referenced in the Inspector by the scripts that need it.
    /// Room 1 uses normal values. Later rooms change them, e.g. Floating Rings sets
    /// soundTransmission = 0 (vacuum) and Deep Corridor sets it to 3 (water).
    /// </summary>
    public class WorldPhysics : MonoBehaviour
    {
        [SerializeField] private string worldName = "Tuning Workshop";
        [Tooltip("How well sound travels: vacuum 0, air 1, water 3. Scales Pulse and Resonate.")]
        [SerializeField] private float soundTransmission = 1f;

        public string WorldName => worldName;
        public float SoundTransmission => soundTransmission;
    }
}

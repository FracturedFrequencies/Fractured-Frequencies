using FracturedFrequencies.Player;
using UnityEngine;

namespace FracturedFrequencies.World
{
    /// <summary>Closes a door behind the player once they are inside this trigger (no way back).</summary>
    [RequireComponent(typeof(BoxCollider))]
    public class Hatch : MonoBehaviour
    {
        [SerializeField] private Door door;

        private void Reset() => GetComponent<BoxCollider>().isTrigger = true;

        private void OnTriggerEnter(Collider other)
        {
            if (door != null && door.IsOpen && other.GetComponentInParent<PlayerMotor>() != null)
                door.SetOpen(false);
        }
    }
}

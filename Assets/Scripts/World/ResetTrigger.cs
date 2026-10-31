using FracturedFrequencies.Core;
using FracturedFrequencies.Player;
using UnityEngine;

namespace FracturedFrequencies.World
{
    /// <summary>When the player lands here (missed a move), the current chamber resets after a short delay.</summary>
    [RequireComponent(typeof(BoxCollider))]
    public class ResetTrigger : MonoBehaviour
    {
        [SerializeField] private float delay = 0.8f;
        [SerializeField] private string message = "Missed! Try again.";

        private void Reset() => GetComponent<BoxCollider>().isTrigger = true;

        private void OnTriggerEnter(Collider other)
        {
            if (other.GetComponentInParent<PlayerMotor>() == null) return;
            EventBus<ChamberResetRequestedEvent>.Raise(new ChamberResetRequestedEvent { Delay = delay, Message = message });
        }
    }
}

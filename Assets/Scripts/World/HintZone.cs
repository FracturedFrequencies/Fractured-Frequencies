using FracturedFrequencies.Core;
using FracturedFrequencies.Player;
using UnityEngine;

namespace FracturedFrequencies.World
{
    /// <summary>Shows a hint on screen while the player is inside this trigger.</summary>
    [RequireComponent(typeof(Collider))]
    public class HintZone : MonoBehaviour
    {
        [SerializeField, TextArea] private string hint = "Hint";

        private void Reset() => GetComponent<Collider>().isTrigger = true;

        private void OnTriggerEnter(Collider other)
        {
            if (other.GetComponentInParent<PlayerMotor>() == null) return;
            EventBus<HintEnteredEvent>.Raise(new HintEnteredEvent { SourceId = GetInstanceID(), Text = hint });
        }

        private void OnTriggerExit(Collider other)
        {
            if (other.GetComponentInParent<PlayerMotor>() == null) return;
            EventBus<HintExitedEvent>.Raise(new HintExitedEvent { SourceId = GetInstanceID() });
        }
    }
}

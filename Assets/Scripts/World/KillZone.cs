using FracturedFrequencies.Player;
using UnityEngine;

namespace FracturedFrequencies.World
{
    /// <summary>Trigger volume (the void). The player respawns, physics objects are removed.</summary>
    [RequireComponent(typeof(Collider))]
    public class KillZone : MonoBehaviour
    {
        private void Reset() => GetComponent<Collider>().isTrigger = true;

        private void OnTriggerEnter(Collider other)
        {
            var respawn = other.GetComponentInParent<PlayerRespawn>();
            if (respawn != null)
            {
                respawn.Respawn();
                return;
            }

            var body = other.attachedRigidbody;
            if (body != null && !body.isKinematic) Destroy(body.gameObject);
        }
    }
}

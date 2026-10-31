using FracturedFrequencies.Core;
using FracturedFrequencies.Player;
using UnityEngine;

namespace FracturedFrequencies.World
{
    /// <summary>A glowing crack that carries the player into the next room.</summary>
    [RequireComponent(typeof(Collider))]
    public class Portal : MonoBehaviour
    {
        [SerializeField] private Chamber target;
        [SerializeField] private Transform glow;

        private Vector3 _glowBaseScale;

        private void Reset() => GetComponent<Collider>().isTrigger = true;

        private void Awake()
        {
            if (glow != null) _glowBaseScale = glow.localScale;
        }

        private void Update()
        {
            if (glow != null) glow.localScale = _glowBaseScale * (1f + Mathf.Sin(Time.time * 3f) * 0.1f);
        }

        private void OnTriggerEnter(Collider other)
        {
            if (target == null || other.GetComponentInParent<PlayerMotor>() == null) return;
            EventBus<PortalEnteredEvent>.Raise(new PortalEnteredEvent { Target = target });
        }
    }
}

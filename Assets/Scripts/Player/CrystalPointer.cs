using FracturedFrequencies.Gravity;
using UnityEngine;

namespace FracturedFrequencies.Player
{
    /// <summary>
    /// Finds the gravity crystal under the mouse cursor and activates it on click.
    /// Crystals use trigger colliders, so they never block movement but can still be clicked.
    /// </summary>
    public class CrystalPointer : MonoBehaviour
    {
        [SerializeField] private Camera viewCamera;
        [SerializeField] private PlayerInputReader input;
        [SerializeField] private float maxDistance = 200f;

        /// <summary>Crystal currently under the cursor, or null.</summary>
        public GravitySwitchCrystal Hovered { get; private set; }

        private readonly RaycastHit[] _hits = new RaycastHit[16];

        private void Update()
        {
            GravitySwitchCrystal found = null;
            if (viewCamera != null && input != null && Time.timeScale > 0f)
            {
                Ray ray = viewCamera.ScreenPointToRay(input.PointerPosition);
                int count = Physics.RaycastNonAlloc(ray, _hits, maxDistance, ~0, QueryTriggerInteraction.Collide);
                float best = float.MaxValue;
                for (int i = 0; i < count; i++)
                {
                    var crystal = _hits[i].collider.GetComponentInParent<GravitySwitchCrystal>();
                    if (crystal != null && _hits[i].distance < best)
                    {
                        best = _hits[i].distance;
                        found = crystal;
                    }
                }
            }

            if (found != Hovered)
            {
                if (Hovered != null) Hovered.SetHovered(false);
                if (found != null) found.SetHovered(true);
                Hovered = found;
            }

            if (Hovered != null && input.ClickPressed) Hovered.Activate();
        }
    }
}

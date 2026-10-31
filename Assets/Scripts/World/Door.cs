using UnityEngine;

namespace FracturedFrequencies.World
{
    /// <summary>
    /// A blocking panel that can open and close. Open = collider off and the panel shrinks into the wall.
    /// Used for the exit door (driven by a PressurePlate) and the hatch behind the player (driven by a Hatch).
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class Door : MonoBehaviour
    {
        [SerializeField] private bool startOpen = false;
        [SerializeField] private Transform visual;
        [Tooltip("Local axis the panel shrinks along when opening.")]
        [SerializeField] private Vector3 shrinkAxis = Vector3.up;
        [SerializeField] private float speed = 6f;

        public bool IsOpen { get; private set; }

        private Collider _collider;
        private Vector3 _closedScale;
        private bool _initialized;

        private void Awake()
        {
            Initialize();
            SetOpen(startOpen, true);
        }

        private void Initialize()
        {
            if (_initialized) return;
            _initialized = true;
            _collider = GetComponent<Collider>();
            if (visual != null) _closedScale = visual.localScale;
        }

        public void SetOpen(bool open) => SetOpen(open, false);

        private void SetOpen(bool open, bool instant)
        {
            Initialize();
            IsOpen = open;
            _collider.enabled = !open;
            if (instant && visual != null) visual.localScale = TargetScale();
        }

        private Vector3 TargetScale()
        {
            if (!IsOpen) return _closedScale;
            Vector3 s = _closedScale;
            Vector3 axis = shrinkAxis.normalized;
            // Shrink to 5% along the axis.
            return new Vector3(
                Mathf.Lerp(s.x, s.x * 0.05f, Mathf.Abs(axis.x)),
                Mathf.Lerp(s.y, s.y * 0.05f, Mathf.Abs(axis.y)),
                Mathf.Lerp(s.z, s.z * 0.05f, Mathf.Abs(axis.z)));
        }

        private void Update()
        {
            if (visual != null) visual.localScale = Vector3.Lerp(visual.localScale, TargetScale(), Time.deltaTime * speed);
        }
    }
}

using FracturedFrequencies.Gravity;
using UnityEngine;

namespace FracturedFrequencies.World
{
    /// <summary>
    /// Opens a door while a physics object (anything with a GravityBody, e.g. the crate) rests on it.
    /// The player does not count, so the crate has to do the job.
    /// Checks with an overlap box every physics step, which also works after objects are teleported.
    /// </summary>
    public class PressurePlate : MonoBehaviour
    {
        [SerializeField] private Door door;
        [Tooltip("Detection box half size, in local space.")]
        [SerializeField] private Vector3 halfExtents = new Vector3(0.5f, 1f, 1f);
        [SerializeField] private Vector3 detectionOffset = Vector3.zero;
        [SerializeField] private Transform visual;
        [Tooltip("Local direction the plate sinks into its wall when pressed.")]
        [SerializeField] private Vector3 pressDirection = Vector3.right;

        public bool IsPressed { get; private set; }

        private readonly Collider[] _overlap = new Collider[16];
        private Vector3 _visualRest;

        private void Awake()
        {
            if (visual != null) _visualRest = visual.localPosition;
        }

        private void FixedUpdate()
        {
            Vector3 center = transform.TransformPoint(detectionOffset);
            int count = Physics.OverlapBoxNonAlloc(center, halfExtents, _overlap, transform.rotation, ~0, QueryTriggerInteraction.Ignore);
            bool pressed = false;
            for (int i = 0; i < count && !pressed; i++)
            {
                var body = _overlap[i].attachedRigidbody;
                if (body != null && body.GetComponent<GravityBody>() != null) pressed = true;
            }

            if (pressed != IsPressed)
            {
                IsPressed = pressed;
                if (door != null) door.SetOpen(pressed);
            }
        }

        private void Update()
        {
            if (visual == null) return;
            Vector3 target = _visualRest + (IsPressed ? pressDirection.normalized * 0.12f : Vector3.zero);
            visual.localPosition = Vector3.Lerp(visual.localPosition, target, Time.deltaTime * 10f);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Gizmos.matrix = Matrix4x4.TRS(transform.TransformPoint(detectionOffset), transform.rotation, Vector3.one);
            Gizmos.DrawWireCube(Vector3.zero, halfExtents * 2f);
        }
    }
}

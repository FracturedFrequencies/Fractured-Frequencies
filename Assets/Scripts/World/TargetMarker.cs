using UnityEngine;

namespace FracturedFrequencies.World
{
    /// <summary>
    /// A pulsing ring drawn around the object the tutorial wants the player to use next.
    /// The ring always faces the camera and sits slightly in front of the target, so walls never hide it.
    /// </summary>
    [RequireComponent(typeof(LineRenderer))]
    public class TargetMarker : MonoBehaviour
    {
        private const int Segments = 48;

        [SerializeField] private Color color = new Color(1f, 0.85f, 0.3f, 1f);
        [SerializeField] private Transform viewCamera;
        [Tooltip("How far the ring is pulled toward the camera.")]
        [SerializeField] private float towardCamera = 3f;

        private LineRenderer _line;
        private Transform _target;
        private float _radius = 1f;

        private void Awake()
        {
            _line = GetComponent<LineRenderer>();
            _line.positionCount = Segments;
            _line.loop = true;
            _line.useWorldSpace = true;
            _line.widthMultiplier = 0.1f;
            _line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _line.enabled = false;
            if (viewCamera == null && Camera.main != null) viewCamera = Camera.main.transform;
        }

        public void SetTarget(Transform target, float radius)
        {
            _target = target;
            _radius = radius;
            if (_line != null) _line.enabled = target != null;
        }

        private void LateUpdate()
        {
            if (_target == null || !_target.gameObject.activeInHierarchy)
            {
                _line.enabled = false;
                return;
            }
            _line.enabled = true;

            Vector3 right = viewCamera != null ? viewCamera.right : Vector3.right;
            Vector3 up = viewCamera != null ? viewCamera.up : Vector3.up;
            Vector3 forward = viewCamera != null ? viewCamera.forward : Vector3.forward;
            Vector3 center = _target.position - forward * towardCamera;

            float radius = _radius * (1f + Mathf.Sin(Time.time * 5f) * 0.12f);
            for (int i = 0; i < Segments; i++)
            {
                float a = i / (float)Segments * Mathf.PI * 2f;
                _line.SetPosition(i, center + (right * Mathf.Cos(a) + up * Mathf.Sin(a)) * radius);
            }
            var c = color;
            c.a = 0.65f + 0.35f * Mathf.Sin(Time.time * 5f);
            _line.startColor = c;
            _line.endColor = c;
        }
    }
}

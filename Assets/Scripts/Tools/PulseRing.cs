using UnityEngine;

namespace FracturedFrequencies.Tools
{
    /// <summary>A short-lived expanding ring that shows where a pulse went. Placeholder until real VFX.</summary>
    public class PulseRing : MonoBehaviour
    {
        private const int Segments = 40;
        private const float Duration = 0.35f;
        private const float ConeAngle = 35f;

        private LineRenderer _line;
        private Vector3 _origin;
        private Vector3 _direction;
        private float _maxDistance;
        private float _age;

        public static void Spawn(Vector3 origin, Vector3 direction, float maxDistance, Material material)
        {
            var go = new GameObject("PulseRing");
            var ring = go.AddComponent<PulseRing>();
            ring._origin = origin;
            ring._direction = direction.normalized;
            ring._maxDistance = maxDistance;

            var line = go.AddComponent<LineRenderer>();
            line.positionCount = Segments;
            line.loop = true;
            line.useWorldSpace = true;
            line.widthMultiplier = 0.06f;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            if (material != null) line.sharedMaterial = material;
            ring._line = line;
            ring.Apply(0f);
        }

        private void Update()
        {
            _age += Time.deltaTime;
            float t = _age / Duration;
            if (t >= 1f)
            {
                Destroy(gameObject);
                return;
            }
            Apply(t);
        }

        private void Apply(float t)
        {
            float ease = 1f - (1f - t) * (1f - t);
            Vector3 center = _origin + _direction * _maxDistance * ease;
            float radius = Mathf.Lerp(0.2f, Mathf.Tan(ConeAngle * Mathf.Deg2Rad) * _maxDistance, ease);

            Vector3 a = Vector3.Cross(_direction, Vector3.up);
            if (a.sqrMagnitude < 1e-3f) a = Vector3.Cross(_direction, Vector3.right);
            a.Normalize();
            Vector3 b = Vector3.Cross(_direction, a);
            for (int i = 0; i < Segments; i++)
            {
                float angle = i / (float)Segments * Mathf.PI * 2f;
                _line.SetPosition(i, center + (a * Mathf.Cos(angle) + b * Mathf.Sin(angle)) * radius);
            }
            Color color = new Color(0.8f, 0.88f, 1f, 1f - t);
            _line.startColor = color;
            _line.endColor = color;
        }
    }
}

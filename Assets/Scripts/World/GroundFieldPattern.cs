using UnityEngine;
using System.Collections.Generic;

public class GroundFieldPattern : MonoBehaviour
{
    private const float HighlightAnimationDuration = 0.3f;

    private Renderer[] renderers;
    private Vector3 center;
    private Quaternion rotation;
    private float size;
    private Color normalColor;
    private int n;
    private int m;
    private float a;
    private float b;
    private float rangeMin;
    private float rangeMax;
    private float nodeLineWidth;
    private bool isHighlighted;
    private bool hasBeenHighlighted;
    private float highlightStartTime;
    private float highlightDirection;

    public PatternFunctionParameters FunctionParameters
    {
        get
        {
            return new PatternFunctionParameters
            {
                N = n,
                M = m,
                A = a,
                B = b
            };
        }
    }

    public Vector3 Center
    {
        get { return center; }
    }

    public void Initialize(
        Vector3 center,
        float size,
        float height,
        float rotationDegrees,
        Color normalColor,
        int n,
        int m,
        float a,
        float b,
        float rangeMin,
        float rangeMax,
        float nodeLineWidth,
        Material patternMaterial)
    {
        this.center = center;
        this.size = size;
        this.rotation = Quaternion.Euler(0f, rotationDegrees, 0f);
        this.normalColor = normalColor;
        this.n = n;
        this.m = m;
        this.a = a;
        this.b = b;
        this.rangeMin = rangeMin;
        this.rangeMax = rangeMax;
        this.nodeLineWidth = nodeLineWidth;

        transform.SetPositionAndRotation(
            center + Vector3.up * (height * 0.5f),
            this.rotation);
        transform.position += Vector3.up * GetHeightVariation(center);
        transform.localScale = new Vector3(size, height, size);

        renderers = GetComponentsInChildren<Renderer>(true);

        if (patternMaterial != null)
        {
            foreach (Renderer renderer in renderers)
            {
                renderer.sharedMaterial = patternMaterial;
            }
        }

        isHighlighted = true;
        SetHighlighted(false);
    }

    public bool Contains(Vector3 point)
    {
        Vector3 localPoint = Quaternion.Inverse(rotation) * (point - center);

        return Mathf.Abs(localPoint.x) <= size * 0.5f
            && Mathf.Abs(localPoint.z) <= size * 0.5f;
    }

    public bool IsInHighlightedArea(Vector3 point)
    {
        if (!Contains(point))
        {
            return false;
        }

        Vector3 localPoint = Quaternion.Inverse(rotation) * (point - center);
        Vector2 coordinates = new Vector2(
            Mathf.Clamp01(localPoint.x / size + 0.5f),
            Mathf.Clamp01(localPoint.z / size + 0.5f));
        float functionValue = a * Mathf.Cos(n * Mathf.PI * coordinates.x)
            * Mathf.Cos(m * Mathf.PI * coordinates.y)
            + b * Mathf.Cos(m * Mathf.PI * coordinates.x)
            * Mathf.Cos(n * Mathf.PI * coordinates.y);

        return IsInRange(functionValue, rangeMin, rangeMax);
    }

    public float SqrDistanceTo(Vector3 point)
    {
        Vector3 horizontalOffset = point - center;
        horizontalOffset.y = 0f;
        return horizontalOffset.sqrMagnitude;
    }

    public List<Entity> GetEntitiesInside()
    {
        Entity[] entities = FindObjectsByType<Entity>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.None);
        List<Entity> entitiesInside = new List<Entity>();

        foreach (Entity entity in entities)
        {
            if (IsInHighlightedArea(entity.transform.position))
            {
                entitiesInside.Add(entity);
            }
        }

        return entitiesInside;
    }

    private static bool IsInRange(float value, float minimum, float maximum)
    {
        if (minimum <= maximum)
        {
            return value >= minimum && value <= maximum;
        }

        return value <= maximum || value >= minimum;
    }

    public void SetHighlighted(bool highlighted)
    {
        if (renderers == null || isHighlighted == highlighted)
        {
            return;
        }

        isHighlighted = highlighted;
        float currentProgress = hasBeenHighlighted
            ? GetHighlightProgress()
            : 0f;
        highlightDirection = highlighted ? 1f : -1f;
        highlightStartTime = Time.time - (
            highlightDirection > 0f
                ? currentProgress
                : 1f - currentProgress) * HighlightAnimationDuration;
        MaterialPropertyBlock propertyBlock = new MaterialPropertyBlock();

        foreach (Renderer renderer in renderers)
        {
            renderer.GetPropertyBlock(propertyBlock);
            propertyBlock.SetColor("_BaseColor", normalColor);
            propertyBlock.SetFloat("_HighlightEnabled", 1f);
            propertyBlock.SetFloat("_HighlightStartTime", highlightStartTime);
            propertyBlock.SetFloat("_HighlightDirection", highlightDirection);
            propertyBlock.SetFloat("_N", n);
            propertyBlock.SetFloat("_M", m);
            propertyBlock.SetFloat("_A", a);
            propertyBlock.SetFloat("_B", b);
            propertyBlock.SetFloat("_RangeMin", rangeMin);
            propertyBlock.SetFloat("_RangeMax", rangeMax);
            propertyBlock.SetFloat("_NodeLineWidth", nodeLineWidth);
            renderer.SetPropertyBlock(propertyBlock);
        }

        if (highlighted)
        {
            hasBeenHighlighted = true;
        }
    }

    private float GetHighlightProgress()
    {
        float elapsed = Mathf.Clamp01(
            (Time.time - highlightStartTime) / HighlightAnimationDuration);
        return highlightDirection > 0f ? elapsed : 1f - elapsed;
    }

    private static float GetHeightVariation(Vector3 worldPosition)
    {
        return Mathf.Sin(
            worldPosition.x * 12.9898f
            + worldPosition.z * 78.233f) * 0.001f;
    }
}

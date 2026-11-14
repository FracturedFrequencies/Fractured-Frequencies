using System.Collections.Generic;
using UnityEngine;

public class GroundField : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private PlayerActions playerActions;
    [SerializeField] private GroundFieldPattern patternPrefab;
    [SerializeField] private Material patternMaterial;

    [Header("Field")]
    [SerializeField] private Vector2 fieldSize = new Vector2(40f, 40f);
    [SerializeField] private float minimumPatternSize = 5f;
    [SerializeField] private float maximumPatternSize = 12f;
    [SerializeField, Range(0f, 0.35f)] private float allowedOverlap = 0.0f;
    [SerializeField] private float patternHeight = 0.1f;
    [SerializeField] private int maximumPatternCount = 400;

    [Header("Placement")]
    [SerializeField] private int seed = 12345;
    [SerializeField, Range(0f, 0.3f)] private float placementJitter = 0.15f;
    [SerializeField] private bool generateOnStart = true;

    [Header("Appearance")]
    [SerializeField] private Color normalColor = new Color(0.0f, 0.0f, 0.0f, 0.35f);
    [SerializeField, Range(0.001f, 0.25f)] private float nodeLineWidth = 0.03f;

    [Header("Chladni Function")]
    [SerializeField] private int n = 3;
    [SerializeField] private int m = 2;
    [SerializeField] private float a = 1f;
    [SerializeField] private float b = 1f;
    [SerializeField] private float rangeMin = -0.3f;
    [SerializeField] private float rangeMax = 0.3f;

    private readonly List<GroundFieldPattern> patterns = new List<GroundFieldPattern>();
    private GroundFieldPattern highlightedPattern;
    private Material runtimePatternMaterial;

    public bool TryGetHighlightedPattern(out GroundFieldPattern pattern)
    {
        pattern = highlightedPattern;
        return pattern != null;
    }

    private void Start()
    {
        if (Camera.main != null)
        {
            Camera.main.depthTextureMode |= DepthTextureMode.Depth;
        }

        if (generateOnStart)
        {
            Generate();
        }
    }

    private void Update()
    {
        if (playerActions == null || !playerActions.TryGetMouseGroundPoint(out Vector3 mousePoint))
        {
            SetHighlightedPattern(null);
            return;
        }

        GroundFieldPattern closestPattern = FindClosestContainingPattern(mousePoint);
        SetHighlightedPattern(closestPattern);
    }

    public void Generate()
    {
        Clear();

        if (!ValidateConfiguration())
        {
            return;
        }

        if (playerActions == null)
        {
            playerActions = FindFirstObjectByType<PlayerActions>();
        }

        if (playerActions == null)
        {
            Debug.LogError("GroundField requires a PlayerActions reference.", this);
            return;
        }

        System.Random random = new System.Random(seed);
        List<PatternPlacement> placements = GeneratePlacements(random);

        foreach (PatternPlacement placement in placements)
        {
            GroundFieldPattern pattern = CreatePattern();
            pattern.Initialize(
                placement.center,
                placement.size,
                patternHeight,
                placement.rotation,
                normalColor,
                n,
                m,
                a,
                b,
                rangeMin,
                rangeMax,
                nodeLineWidth,
                GetPatternMaterial());
            patterns.Add(pattern);
        }
    }

    public void Clear()
    {
        highlightedPattern = null;

        for (int i = patterns.Count - 1; i >= 0; i--)
        {
            if (patterns[i] != null)
            {
                Destroy(patterns[i].gameObject);
            }
        }

        patterns.Clear();
    }

    private List<PatternPlacement> GeneratePlacements(System.Random random)
    {
        List<PatternPlacement> placements = new List<PatternPlacement>();
        float averagePatternSize = (minimumPatternSize + maximumPatternSize) * 0.5f;
        float spacing = averagePatternSize * (1f - allowedOverlap);
        int columns = Mathf.CeilToInt(fieldSize.x / spacing);
        int rows = Mathf.CeilToInt(fieldSize.y / spacing);
        float columnSpacing = fieldSize.x / columns;
        float rowSpacing = fieldSize.y / rows;
        float halfWidth = fieldSize.x * 0.5f;
        float halfDepth = fieldSize.y * 0.5f;

        for (int row = 0; row < rows && placements.Count < maximumPatternCount; row++)
        {
            for (int column = 0;
                 column < columns && placements.Count < maximumPatternCount;
                 column++)
            {
                float jitterX = (float)(random.NextDouble() * 2f - 1f)
                    * columnSpacing * placementJitter;
                float jitterZ = (float)(random.NextDouble() * 2f - 1f)
                    * rowSpacing * placementJitter;
                float x = -halfWidth + columnSpacing * (column + 0.5f) + jitterX;
                float z = -halfDepth + rowSpacing * (row + 0.5f) + jitterZ;
                float size = Mathf.Lerp(
                    minimumPatternSize,
                    maximumPatternSize,
                    (float)random.NextDouble());
                float rotation = (float)random.NextDouble() * 360f;

                placements.Add(new PatternPlacement
                {
                    center = transform.position + new Vector3(x, transform.position.y, z),
                    size = size,
                    rotation = rotation
                });
            }
        }

        return placements;
    }

    private GroundFieldPattern FindClosestContainingPattern(Vector3 point)
    {
        GroundFieldPattern closestPattern = null;
        float closestDistance = float.PositiveInfinity;

        foreach (GroundFieldPattern pattern in patterns)
        {
            if (pattern == null || !pattern.Contains(point))
            {
                continue;
            }

            float distance = pattern.SqrDistanceTo(point);

            if (distance < closestDistance)
            {
                closestDistance = distance;
                closestPattern = pattern;
            }
        }

        return closestPattern;
    }

    private void SetHighlightedPattern(GroundFieldPattern pattern)
    {
        if (highlightedPattern == pattern)
        {
            return;
        }

        if (highlightedPattern != null)
        {
            highlightedPattern.SetHighlighted(false);
        }

        highlightedPattern = pattern;

        if (highlightedPattern != null)
        {
            highlightedPattern.SetHighlighted(true);
        }
    }

    private GroundFieldPattern CreatePattern()
    {
        GroundFieldPattern pattern;

        if (patternPrefab != null)
        {
            pattern = Instantiate(patternPrefab, transform);
        }
        else
        {
            GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = "Ground Field Pattern";
            cube.transform.SetParent(transform);
            Destroy(cube.GetComponent<Collider>());
            pattern = cube.AddComponent<GroundFieldPattern>();
        }

        return pattern;
    }

    private Material GetPatternMaterial()
    {
        if (patternMaterial != null)
        {
            return patternMaterial;
        }

        if (runtimePatternMaterial == null)
        {
            Shader shader = Shader.Find("Custom/GroundFieldPattern");

            if (shader == null)
            {
                Debug.LogError(
                    "GroundField could not find the Custom/GroundFieldPattern shader.",
                    this);
                return null;
            }

            runtimePatternMaterial = new Material(shader);
        }

        return runtimePatternMaterial;
    }

    private void OnDestroy()
    {
        if (runtimePatternMaterial != null)
        {
            Destroy(runtimePatternMaterial);
        }
    }

    private bool ValidateConfiguration()
    {
        if (fieldSize.x <= 0f || fieldSize.y <= 0f)
        {
            Debug.LogError("GroundField dimensions must be positive.", this);
            return false;
        }

        if (minimumPatternSize <= 0f
            || maximumPatternSize < minimumPatternSize
            || patternHeight <= 0f
            || n < 0
            || m < 0
            || nodeLineWidth <= 0f)
        {
            Debug.LogError("GroundField pattern or function settings are invalid.", this);
            return false;
        }

        if (maximumPatternCount < 1)
        {
            Debug.LogError("GroundField maximum pattern count must be positive.", this);
            return false;
        }

        return true;
    }

    private struct PatternPlacement
    {
        public Vector3 center;
        public float size;
        public float rotation;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.1f, 0.8f, 1f, 0.3f);
        Gizmos.DrawWireCube(
            transform.position,
            new Vector3(fieldSize.x, 0.01f, fieldSize.y));
    }
}

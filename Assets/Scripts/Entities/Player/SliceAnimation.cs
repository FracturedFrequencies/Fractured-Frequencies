using UnityEngine;

public class SliceAnimation : MonoBehaviour
{
    [SerializeField] private Renderer sliceRenderer;
    [SerializeField] private float duration = 0.5f;
    [SerializeField] private AnimationCurve progressCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);

    private Material mat;
    private float timer;
    private static readonly int ProgressId = Shader.PropertyToID("_Progress");

    void Awake()
    {
        if (sliceRenderer == null)
            sliceRenderer = GetComponentInChildren<Renderer>();

        mat = sliceRenderer.material;
        mat.SetFloat(ProgressId, 0f);
    }

    void Update()
    {
        timer += Time.deltaTime;
        float t = Mathf.Clamp01(timer / duration);
        mat.SetFloat(ProgressId, progressCurve.Evaluate(t));

        if (t >= 1f)
            Destroy(gameObject);
    }

    void OnDestroy()
    {
        if (mat != null)
            Destroy(mat);
    }
}
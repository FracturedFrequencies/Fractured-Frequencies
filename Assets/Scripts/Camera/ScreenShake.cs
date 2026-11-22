using System.Collections;
using UnityEngine;

public class ScreenShake : MonoBehaviour
{
    public static ScreenShake Instance { get; private set; }

    private Vector3 originalPos;
    private Coroutine routine;

    void Awake()
    {
        Instance = this;
        originalPos = transform.localPosition;
    }

    public void Shake(float duration = 0.15f, float magnitude = 0.15f)
    {
        if (routine != null) StopCoroutine(routine);
        routine = StartCoroutine(ShakeRoutine(duration, magnitude));
    }

    IEnumerator ShakeRoutine(float duration, float magnitude)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;

            // Fade the shake out over time
            float strength = magnitude * (1f - elapsed / duration);
            Vector2 offset = Random.insideUnitCircle * strength;
            transform.localPosition = originalPos + new Vector3(offset.x, offset.y, 0f);

            yield return null;
        }
        transform.localPosition = originalPos;
        routine = null;
    }
}
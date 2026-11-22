using System.Collections;
using UnityEngine;

public class HitPause : MonoBehaviour
{
    public static HitPause Instance { get; private set; }

    private Coroutine routine;

    void Awake()
    {
        Instance = this;
    }

    public void Pause(float duration = 0.05f)
    {
        if (routine != null) StopCoroutine(routine);
        routine = StartCoroutine(PauseRoutine(duration));
    }

    IEnumerator PauseRoutine(float duration)
    {
        Time.timeScale = 0f;
        yield return new WaitForSecondsRealtime(duration);
        Time.timeScale = 1f;
        routine = null;
    }
}
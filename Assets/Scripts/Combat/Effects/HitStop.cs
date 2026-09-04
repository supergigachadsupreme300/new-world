using System.Collections;
using UnityEngine;

/// <summary>
/// Applies a brief "hit stop" — freezing time for a few frames on impact for game feel
/// (Task 3.3). Triggered from hit feedback (e.g. DamageCalculator callbacks).
///
/// Uses a scaled-down Time.timeScale so that non-timeScale-dependent visual effects still
/// feel impactful without pausing the whole world. Restores scale after the freeze.
/// </summary>
public class HitStop : MonoBehaviour
{
    [Header("Freeze")]
    [Tooltip("Duration of the freeze in seconds (real-time).")]
    public float FreezeDuration = 0.06f;
    [Tooltip("Time scale during the freeze (0 = full stop, 0.1 = near stop).")]
    [Range(0f, 1f)]
    public float FreezeScale = 0.02f;

    private bool _freezing;
    private float _previousScale = 1f;

    /// <summary>Trigger a one-shot hit stop.</summary>
    public void Trigger()
    {
        Trigger(FreezeDuration, FreezeScale);
    }

    /// <summary>Trigger a hit stop with the given duration and scale.</summary>
    public void Trigger(float duration, float scale)
    {
        if (_freezing)
        {
            StopAllCoroutines();
            Time.timeScale = _previousScale;
        }
        StartCoroutine(FreezeRoutine(Mathf.Max(duration, 0f), Mathf.Clamp01(scale)));
    }

    private IEnumerator FreezeRoutine(float duration, float scale)
    {
        _freezing = true;
        _previousScale = Time.timeScale;
        Time.timeScale = scale;
        yield return new WaitForSecondsRealtime(duration);
        Time.timeScale = _previousScale;
        _freezing = false;
    }

    private void OnDestroy()
    {
        StopAllCoroutines();
        // Only restore if this instance is the active freezer.
        if (_freezing)
        {
            Time.timeScale = _previousScale;
            _freezing = false;
        }
    }
}

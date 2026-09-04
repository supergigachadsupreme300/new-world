using UnityEngine;

/// <summary>
/// Applies a short, decaying camera shake on impacts (Task 3.3). Attach to the main camera,
/// or drive a dedicated child rig via <see cref="Target"/>.
///
/// NOTE: A third-person follow camera overwrites its own root transform each LateUpdate
/// (e.g. ThirdPersonCamera sets transform.position), so a shake written to the root will be
/// defeated. Point Target at a child "CameraRig" GameObject instead; CombatFeedback does this
/// automatically so shake survives the follow camera.
/// </summary>
public class ScreenShake : MonoBehaviour
{
    [Header("Intensity")]
    public float MaxAmplitude = 0.15f;
    public float MaxFrequency = 1.5f;
    [Tooltip("Seconds the shake lasts before fully settling.")]
    public float Duration = 0.25f;

    [Tooltip("Transform shaken (local position). Defaults to this transform; set to a child rig for follow cameras.")]
    public Transform Target;

    private float _trauma;
    private Vector3 _basePos;
    private float _elapsed;
    private bool _shaking;

    private void Awake()
    {
        if (Target == null)
            Target = transform;
    }

    /// <summary>Apply a shake impulse (0..1), added to current trauma (capped).</summary>
    public void Shake(float amount = 1f)
    {
        if (Target == null)
            Target = transform;

        _trauma = Mathf.Min(_trauma + Mathf.Clamp01(amount), 1f);
        _elapsed = 0f;
        if (!_shaking)
        {
            _basePos = Target.localPosition;
            _shaking = true;
        }
    }

    private void Update()
    {
        if (!_shaking) return;

        _elapsed += Time.deltaTime;
        if (_elapsed >= Duration || _trauma <= 0.001f)
        {
            Target.localPosition = _basePos;
            _trauma = 0f;
            _shaking = false;
            return;
        }

        // Decay trauma over the duration.
        float t = _elapsed / Duration;
        float decay = Mathf.Pow(1f - t, 2f);

        float amp = MaxAmplitude * _trauma * decay;
        float x = Mathf.PerlinNoise(Time.time * MaxFrequency, 0f) * 2f - 1f;
        float y = Mathf.PerlinNoise(0f, Time.time * MaxFrequency) * 2f - 1f;

        Target.localPosition = _basePos + new Vector3(x, y, 0f) * amp;
    }
}
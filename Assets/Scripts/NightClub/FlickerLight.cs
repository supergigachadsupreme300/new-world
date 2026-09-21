using UnityEngine;

/// <summary>
/// Subtle gas-lamp style intensity flicker for club exterior lights.
/// Self-skips while the Light component is disabled (i.e. during the day).
/// </summary>
public class FlickerLight : MonoBehaviour
{
    public float Intensity = 3f;
    private float _phase;
    private Light _light;

    void Start()
    {
        _phase = Random.value * 20f;
        _light = GetComponent<Light>();
    }

    void Update()
    {
        // 1e5: Light component cached once (was GetComponent per frame).
        var l = _light;
        if (l == null || !l.enabled)
            return;
        float n = Mathf.PerlinNoise(Time.time * 1.4f, _phase);
        float dip = Mathf.PerlinNoise(Time.time * 0.55f, _phase * 2f);
        float mult = 0.72f + 0.28f * n;
        if (dip > 0.88f)
            mult *= 0.35f;
        l.intensity = Intensity * mult;
    }
}
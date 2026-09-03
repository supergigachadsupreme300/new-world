using UnityEngine;

/// <summary>
/// Floating damage number (Task 3.3). Rises and fades above the hit point.
///
/// Self-contained: spawns via DamageNumber.Spawn and auto-creates a world-space TextMesh
/// if none is present, so it works without authored prefabs.
/// </summary>
public class DamageNumber : MonoBehaviour
{
    [Header("Motion")]
    public Vector3 RiseVelocity = new Vector3(0f, 1.5f, 0f);
    public float Lifetime = 0.8f;
    public float FadeDelay = 0.5f;

    [Header("Text")]
    public Color Color = Color.white;
    public float FontScale = 0.1f;

    private TextMesh _text;
    private float _lifetime;

    /// <summary>Spawn a floating damage number above a world position.</summary>
    public static void Spawn(Vector3 worldPos, float amount, bool critical = false)
    {
        GameObject go = new GameObject("DamageNumber");
        var dn = go.AddComponent<DamageNumber>();
        dn.Show(worldPos, amount, critical);
    }

    /// <summary>Configure and display this number.</summary>
    public void Show(Vector3 worldPos, float amount, bool critical)
    {
        transform.position = worldPos + Vector3.up * 0.2f;

        _text = GetComponent<TextMesh>();
        if (_text == null)
        {
            _text = gameObject.AddComponent<TextMesh>();
            _text.anchor = TextAnchor.MiddleCenter;
            _text.alignment = TextAlignment.Center;
            Renderer r = _text.GetComponent<Renderer>();
            if (r != null)
            {
                // Guard against stripped builds where neither built-in shader resolves,
                // since `new Material(null)` throws at runtime.
                Shader shader = Shader.Find("GUI/Text Shader") ?? Shader.Find("Unlit/Color");
                if (shader != null)
                    r.material = new Material(shader);
            }
        }

        _text.text = Mathf.RoundToInt(amount).ToString();
        _text.fontSize = critical ? 96 : 64;
        _text.color = critical ? new Color(1f, 0.8f, 0.2f) : Color;
        _text.transform.localScale = Vector3.one * (critical ? FontScale * 1.4f : FontScale);

        _lifetime = 0f;
    }

    private void Update()
    {
        _lifetime += Time.deltaTime;
        transform.position += RiseVelocity * Time.deltaTime;

        // Fade out near the end of life.
        if (_text != null && _lifetime > FadeDelay)
        {
            float alpha = 1f - Mathf.Clamp01((_lifetime - FadeDelay) / Mathf.Max(Lifetime - FadeDelay, 0.01f));
            Color c = _text.color;
            c.a = Mathf.Clamp01(alpha);
            _text.color = c;
        }

        if (_lifetime >= Lifetime)
            Destroy(gameObject);
    }
}

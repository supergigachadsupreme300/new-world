using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Floating damage number (Task 3.3). Rises and fades above the hit point.
///
/// Self-contained: spawns via DamageNumber.Spawn and auto-creates a world-space TextMesh
/// if none is present, so it works without authored prefabs.
///
/// Instances are pooled (see <see cref="PoolCap"/>) so combat popups stop allocating a
/// GameObject + TextMesh per hit; the TextMesh/material are created once per pooled entry.
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

    private static readonly List<DamageNumber> _pool = new List<DamageNumber>();
    private const int PoolCap = 256;

    /// <summary>Spawn a floating damage number above a world position (pooled).</summary>
    public static void Spawn(Vector3 worldPos, float amount, bool critical = false)
    {
        DamageNumber dn = Acquire();
        dn.Show(worldPos, amount, critical);
    }

    /// <summary>Spawn a floating damage number tinted by a damage type (pooled).</summary>
    public static void Spawn(Vector3 worldPos, float amount, DamageType type)
    {
        DamageNumber dn = Acquire();
        dn.Show(worldPos, amount, false, ColorFor(type));
    }

    /// <summary>Pull a dormant entry from the pool, else build a fresh one.</summary>
    private static DamageNumber Acquire()
    {
        for (int i = 0; i < _pool.Count; i++)
        {
            var dn = _pool[i];
            if (dn == null)
            {
                _pool.RemoveAt(i);
                i--;
                continue;
            }
            if (dn.gameObject.activeSelf) continue;
            _pool.RemoveAt(i);
            dn.gameObject.SetActive(true);
            return dn;
        }
        var go = new GameObject("DamageNumber");
        return go.AddComponent<DamageNumber>();
    }

    /// <summary>Return an expired entry to the pool (bounded; excess are destroyed).</summary>
    private void Release()
    {
        _lifetime = 0f;
        if (_text != null)
        {
            Color c = _text.color;
            c.a = 1f;
            _text.color = c;
        }
        gameObject.SetActive(false);
        if (_pool.Count < PoolCap)
            _pool.Add(this);
        else
            Destroy(gameObject);
    }

    /// <summary>Display color for a damage type (used by popups).</summary>
    public static Color ColorFor(DamageType type)
    {
        switch (type)
        {
            case DamageType.Fire: return new Color(1f, 0.5f, 0.2f);
            case DamageType.Ice: return new Color(0.5f, 0.85f, 1f);
            case DamageType.Lightning: return new Color(1f, 0.95f, 0.4f);
            case DamageType.Holy: return new Color(1f, 0.95f, 0.7f);
            case DamageType.Dark: return new Color(0.85f, 0.45f, 1f);
            case DamageType.Wind: return new Color(0.7f, 1f, 0.95f);
            case DamageType.Earth: return new Color(0.78f, 0.62f, 0.42f);
            case DamageType.Water: return new Color(0.4f, 0.65f, 1f);
            case DamageType.Arcane: return new Color(1f, 0.5f, 1f);
            default: return new Color(1f, 0.9f, 0.3f);
        }
    }

    /// <summary>Configure and display this number.</summary>
    public void Show(Vector3 worldPos, float amount, bool critical)
    {
        Show(worldPos, amount, critical, critical ? new Color(1f, 0.8f, 0.2f) : Color);
    }

    /// <summary>Configure and display this number with an explicit color.</summary>
    public void Show(Vector3 worldPos, float amount, bool critical, Color color)
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
        _text.color = color;
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
            Release();
    }
}
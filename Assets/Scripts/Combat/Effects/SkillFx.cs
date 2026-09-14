using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// One-shot, script-built combat FX for skill execution (visible feedback even when no
/// target is in range). Deliberately prefab-free so skills read without authored assets.
/// </summary>
public static class SkillFx
{
    private static Shader _spriteShader;
    private static readonly Dictionary<Color, Material> _spriteMats = new Dictionary<Color, Material>();

    /// <summary>
    /// Cached persistent material shared across FX renderers (zone/storm visuals), so persistent
    /// effects stop allocating a fresh Material per cast. Keyed by color, which caps the pool at
    /// the damage-palette size. Never mutate the returned material's color — fading FX (Faders,
    /// StrikeFlash) must keep their own per-face instance.
    /// </summary>
    public static Material SharedSpriteMaterial(Color color)
    {
        if (_spriteShader == null)
            _spriteShader = Shader.Find("Sprites/Default") ?? Shader.Find("Unlit/Color");
        if (_spriteShader == null) return null;
        if (_spriteMats.TryGetValue(color, out var mat) && mat != null)
            return mat;
        mat = new Material(_spriteShader) { color = color };
        _spriteMats[color] = mat;
        return mat;
    }
    /// <summary>
    /// Spawn a bright, forward-facing slash sheet at the strike origin and shrink it to
    /// nothing over <paramref name="lifetime"/> seconds. No collider, pure visual.
    /// </summary>
    public static void SlashFlash(Vector3 origin, Vector3 forward, float radius, float lifetime, Color color)
    {
        GameObject slice = GameObject.CreatePrimitive(PrimitiveType.Cube);
        slice.name = "SkillSlash";
        slice.transform.position = origin + forward * (radius * 0.5f);
        slice.transform.rotation = Quaternion.LookRotation(forward);
        slice.transform.localScale = new Vector3(radius * 1.3f, radius, 0.1f);

        Collider col = slice.GetComponent<Collider>();
        if (col != null)
            Object.Destroy(col);

        Shader shader = Shader.Find("Unlit/Color") ?? Shader.Find("Standard");
        Renderer renderer = slice.GetComponent<MeshRenderer>();
        if (renderer != null && shader != null)
        {
            var mat = new Material(shader) { color = color };
            renderer.material = mat;
        }

        slice.AddComponent<SlashFader>().Init(lifetime);
    }

    /// <summary>
    /// Spawn an expanding, fading flat ring (zone spells). A primitive cylinder flattened along
    /// <paramref name="upDir"/>, alpha-fading out. No collider, pure visual.
    /// </summary>
    public static void RingFlash(Vector3 worldPos, Vector3 upDir, Color color, float radius, float lifetime)
    {
        GameObject ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        ring.name = "FxRing";
        ring.transform.position = worldPos + upDir.normalized * 0.02f;
        ring.transform.rotation = Quaternion.FromToRotation(Vector3.up, upDir.normalized);

        Collider col = ring.GetComponent<Collider>();
        if (col != null)
            Object.Destroy(col);

        Shader shader = Shader.Find("Sprites/Default") ?? Shader.Find("Unlit/Color");
        Renderer renderer = ring.GetComponent<MeshRenderer>();
        if (renderer != null && shader != null)
            renderer.material = new Material(shader) { color = color };

        ring.AddComponent<RingFader>().Init(radius, lifetime);
    }

    /// <summary>Expands the ring to full radius while fading to transparent, then removes it.</summary>
    private sealed class RingFader : MonoBehaviour
    {
        private float _radius;
        private float _age;
        private float _lifetime = 0.35f;
        private Material _mat;

        public void Init(float radius, float lifetime)
        {
            _radius = radius;
            _lifetime = Mathf.Max(lifetime, 0.05f);
        }

        private void Start()
        {
            var renderer = GetComponent<MeshRenderer>();
            _mat = renderer != null ? renderer.material : null;
        }

        private void Update()
        {
            _age += Time.deltaTime;
            float t = Mathf.Clamp01(_age / _lifetime);
            float s = Mathf.Lerp(0.25f, 1f, Mathf.SmoothStep(0f, 0.45f, t));
            transform.localScale = new Vector3(_radius * 2f * s, 0.05f, _radius * 2f * s);
            if (_mat != null)
            {
                Color c = _mat.color;
                c.a = 1f - t;
                _mat.color = c;
            }
            if (t >= 1f)
                Destroy(gameObject);
        }
    }

    /// <summary>Shrinks the flash slice to zero scale, then removes it.</summary>
    private sealed class SlashFader : MonoBehaviour
    {
        private Vector3 _startScale;
        private float _age;
        private float _lifetime = 0.15f;

        public void Init(float lifetime)
        {
            _lifetime = Mathf.Max(lifetime, 0.05f);
        }

        private void Start()
        {
            _startScale = transform.localScale;
        }

        private void Update()
        {
            _age += Time.deltaTime;
            float t = Mathf.Clamp01(_age / _lifetime);
            transform.localScale = _startScale * (1f - t);
            if (t >= 1f)
                Destroy(gameObject);
        }
    }
}
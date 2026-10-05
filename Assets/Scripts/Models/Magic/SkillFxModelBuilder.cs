using UnityEngine;

/// <summary>
/// The shared skill/spell flash models — the forward slash sheet and the expanding ground ring.
/// <para>Extracted from <c>Magic/Fx/SkillFx.cs</c> in <b>1jd</b>: "slash model" / "ring model" matched
/// nothing under <c>Models/</c> while the model shipped.</para>
/// <para><b>The spawn entry points stayed in <c>SkillFx</c>.</b> <c>SkillFx.RingFlash</c> and
/// <c>SkillFx.SlashFlash</c> have a dozen-plus call sites including six non-spell callers
/// (<c>ClassEffect</c>, <c>RaceEffect</c>, <c>CastingCircle.Burst</c>), so those signatures are public
/// API and did not move. What moved is the geometry underneath them, plus the two faders that
/// animate it — a fader has to travel with the shape it fades, or the piece outlives its animation.</para>
/// <para><b>Both pieces are body-only.</b> Every collider is destroyed and neither is ever queried:
/// damage resolution is the spell's own pipeline.</para>
/// </summary>
public static class SkillFxModelBuilder
{
    /// <summary>Builds a bright, forward-facing slash sheet at the strike origin, with the
    /// <c>SlashFader</c> that shrinks it to nothing over <paramref name="lifetime"/> seconds.
    /// Placed in WORLD space, offset forward by half the radius so the sheet reads as passing through
    /// the target rather than starting behind it.</summary>
    public static void BuildSlashFlash(Vector3 origin, Vector3 forward, float radius, float lifetime,
        Color color)
    {
        GameObject slice = GameObject.CreatePrimitive(PrimitiveType.Cube);
        slice.name = "SkillSlash";
        slice.transform.position = origin + forward * (radius * 0.5f);
        slice.transform.rotation = Quaternion.LookRotation(forward);
        slice.transform.localScale = new Vector3(radius * 1.3f, radius, 0.1f);

        DestroyCollider(slice);

        Shader shader = Shader.Find("Unlit/Color") ?? Shader.Find("Standard");
        Renderer renderer = slice.GetComponent<MeshRenderer>();
        if (renderer != null && shader != null)
        {
            var mat = new Material(shader) { color = color };
            renderer.material = mat;
        }

        slice.AddComponent<SlashFader>().Init(lifetime);
    }

    /// <summary>Builds an expanding, alpha-fading flat ring — a primitive cylinder flattened along
    /// <paramref name="upDir"/> — with the <c>RingFader</c> that expands and fades it.
    /// <para><b>0.02 m of lift off the ground.</b> Not cosmetic: a ring drawn exactly at
    /// <paramref name="worldPos"/> z-fights the terrain it is supposed to be lying on. The lift was
    /// introduced with the ring and moving it changes what the player sees.</para></summary>
    public static void BuildRingFlash(Vector3 worldPos, Vector3 upDir, Color color, float radius,
        float lifetime)
    {
        GameObject ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        ring.name = "FxRing";
        ring.transform.position = worldPos + upDir.normalized * 0.02f;
        ring.transform.rotation = Quaternion.FromToRotation(Vector3.up, upDir.normalized);

        DestroyCollider(ring);

        Shader shader = Shader.Find("Sprites/Default") ?? Shader.Find("Unlit/Color");
        Renderer renderer = ring.GetComponent<MeshRenderer>();
        if (renderer != null && shader != null)
            renderer.material = new Material(shader) { color = color };

        ring.AddComponent<RingFader>().Init(radius, lifetime);
    }

    private static void DestroyCollider(GameObject go)
    {
        Collider col = go.GetComponent<Collider>();
        if (col != null) Object.Destroy(col);
    }

    /// <summary>Expands the ring to full radius while fading to transparent, then removes it.
    /// 1jd: moved here with the ring. <c>Destroy</c> becomes <c>Object.Destroy</c> — the static-class
    /// edit a diff cannot show, because the line is textually identical in both files.</summary>
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
                Object.Destroy(gameObject);
        }
    }

    /// <summary>Shrinks the flash slice to zero scale, then removes it. 1jd: moved here with the
    /// slash sheet.</summary>
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
                Object.Destroy(gameObject);
        }
    }
}
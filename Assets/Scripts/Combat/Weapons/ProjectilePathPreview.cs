using UnityEngine;

/// <summary>
/// World-space flight-path preview for drawn projectile weapons (the bow's pull) and for projectile
/// magic (firebolt/frostbolt). While the player aims, a ribbed cone of translucent rings fans out
/// from the muzzle, filling the possible flight paths; as the draw/charge builds, the cone narrows
/// until it collapses into the thin center ray marking the exact predicted trajectory. The path
/// clips at the first solid hit so the cone and ray read as the real impact point.
///
/// <para><b>1iq: the cone and the ray are now separate claims.</b> Passing <c>spreadDeg = 0</c>
/// collapses every ring to zero width and leaves the ray at full opacity, so a caller can ask for
/// "where does this go" without also claiming "it could go anywhere in a fan". <b>Only the drawn
/// flight (a weapon that consumes ammo) asks for the cone</b> - see
/// <c>PlayerController.UpdatePathPreview</c>, which owns that decision and states why. Projectile
/// magic and thrown weapons pass 0 and keep the ray, because neither has a spread to draw.
/// </para>
///
/// <para><b>The ray is not a promise about accuracy.</b> It is the trajectory line, clipped to the
/// first solid hit. Nothing here feeds the spread the projectile is actually fired with, so treat the
/// cone as an intent readout rather than a guarantee - and note that the narrowing-on-charge is
/// currently a claim the ranged fire path does not honour.</para>
///
/// Driven by <see cref="PlayerController"/> each aim frame. Pure visual: no colliders and nothing
/// blocking gameplay. Prefab-free, built from LineRenderer rings on first access.
/// </summary>
public sealed class ProjectilePathPreview : MonoBehaviour
{
    private static ProjectilePathPreview _instance;

    private const int Rings = 10;
    private const int RingSegments = 18;
    private const float MaxHalfAngleDeg = 45f;

    private LineRenderer[] _rings;
    private Material _ringMat;
    private LineRenderer _ray;
    private Material _rayMat;

    private bool _active;
    private Vector3 _dir;
    private float _length;
    private float _halfAngle;
    private Color _color = Color.white;
    private float _pulse;

    /// <summary>Ensure the singleton exists and returns it (builds on first access).</summary>
    public static ProjectilePathPreview Instance
    {
        get
        {
            if (_instance == null)
            {
                var go = new GameObject("ProjectilePathPreview");
                Object.DontDestroyOnLoad(go);
                _instance = go.AddComponent<ProjectilePathPreview>();
            }
            return _instance;
        }
    }

    /// <summary>True while a path preview is currently visible.</summary>
    public bool IsActive => _active;

    private void Awake()
    {
        if (_instance == null) _instance = this;
        Build();
        gameObject.SetActive(false);
    }

    private void OnDestroy()
    {
        if (_instance == this) _instance = null;
    }

    /// <summary>
    /// Show/refresh the flight-path cone at an origin along a direction. <paramref name="length"/>
    /// is the max flight distance, <paramref name="spreadDeg"/> the current spread half-angle in
    /// degrees (0 collapses the cone to a ray). <paramref name="ignoreRoot"/> (usually the shooter)
    /// is excluded from the impact-clipping probe.
    /// </summary>
    public void Show(Vector3 origin, Vector3 dir, float length, float spreadDeg, Color color,
        Transform ignoreRoot = null)
    {
        if (length <= 0.01f) { Hide(); return; }

        _active = true;
        _dir = dir.sqrMagnitude > 0.0001f ? dir.normalized : Vector3.forward;
        _length = length;
        _halfAngle = Mathf.Clamp(spreadDeg, 0f, MaxHalfAngleDeg) * Mathf.Deg2Rad;
        _color = color;
        transform.position = origin;
        transform.rotation = Quaternion.LookRotation(_dir);

        // Clip the preview at the first solid hit so the cone/ray ends at the real impact point
        // (mirrors the per-frame flight raycast of RangedProjectile/SpellEffect).
        if (Physics.Raycast(origin, _dir, out RaycastHit hit, length, ~0))
        {
            if (ignoreRoot == null || hit.collider.transform.root != ignoreRoot)
                _length = Mathf.Max(hit.distance, 0.1f);
        }

        gameObject.SetActive(true);
        Apply();
    }

    /// <summary>Hide the preview immediately (aim cancelled, shot released, nothing to aim, etc.).</summary>
    public void Hide()
    {
        if (!_active) return;
        _active = false;
        gameObject.SetActive(false);
    }

    private void Update()
    {
        if (!_active) return;
        _pulse += Time.deltaTime * 2.2f;
        Apply();
    }

    private void Build()
    {
        Shader shader = Shader.Find("Sprites/Default") ?? Shader.Find("Unlit/Color");

        // Ribbed cone cross-sections — one looped LineRenderer per ring (a single GameObject
        // permits only one Renderer, so each ring gets its own child, like CastingCircle).
        _rings = new LineRenderer[Rings];
        if (shader != null)
            _ringMat = new Material(shader);
        for (int i = 0; i < Rings; i++)
        {
            var ringGo = new GameObject("Ring" + i);
            ringGo.transform.SetParent(transform, false);
            var ring = ringGo.AddComponent<LineRenderer>();
            ring.useWorldSpace = false;
            ring.loop = true;
            ring.positionCount = RingSegments;
            ring.startWidth = 1f;
            ring.endWidth = 1f;
            ring.widthMultiplier = 0.05f;
            ring.numCapVertices = 2;
            if (_ringMat != null) ring.material = _ringMat;
            _rings[i] = ring;
        }

        // Center ray — the exact predicted trajectory, revealed as the cone collapses.
        var rayGo = new GameObject("Trajectory");
        rayGo.transform.SetParent(transform, false);
        _ray = rayGo.AddComponent<LineRenderer>();
        _ray.useWorldSpace = false;
        _ray.loop = false;
        _ray.positionCount = 2;
        _ray.startWidth = 1f;
        _ray.endWidth = 1f;
        _ray.widthMultiplier = 0.08f;
        _ray.numCapVertices = 2;
        if (shader != null)
        {
            _rayMat = new Material(shader);
            _ray.material = _rayMat;
        }
    }

    private void Apply()
    {
        // How far the cone has focused into the final ray (0 = wide cone, 1 = pure trajectory).
        float narrow = Mathf.Clamp01(1f - _halfAngle / (MaxHalfAngleDeg * Mathf.Deg2Rad));
        float pulse = 1f + 0.04f * Mathf.Sin(_pulse);
        if (_ringMat != null)
            _ringMat.color = new Color(_color.r, _color.g, _color.b,
                Mathf.Lerp(0.55f, 0f, narrow) * pulse);

        for (int i = 0; i < Rings; i++)
        {
            float t = i / (float)(Rings - 1);
            float d = _length * t;
            float radius = Mathf.Tan(_halfAngle) * d;

            // Shrink the rings into nothing as they collapse into the ray.
            _rings[i].widthMultiplier = Mathf.Lerp(0.06f, 0f, narrow) * pulse;
            _rings[i].transform.localPosition = new Vector3(0f, 0f, d);
            for (int s = 0; s < RingSegments; s++)
            {
                float ang = (s / (float)RingSegments) * Mathf.PI * 2f;
                _rings[i].SetPosition(s,
                    new Vector3(Mathf.Cos(ang) * radius, Mathf.Sin(ang) * radius, 0f));
            }
        }

        if (_ray != null)
        {
            _ray.SetPosition(0, Vector3.zero);
            _ray.SetPosition(1, new Vector3(0f, 0f, _length));
            if (_rayMat != null)
                _rayMat.color = new Color(_color.r, _color.g, _color.b,
                    Mathf.Lerp(0.12f, 0.95f, narrow));
        }
    }
}
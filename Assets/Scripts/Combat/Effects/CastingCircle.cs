using UnityEngine;

/// <summary>
/// Halo-style casting circle that wraps around the held magic weapon while a spell is being
/// aimed/charged (driven by <see cref="PlayerController"/>). The ring plane lies perpendicular
/// to the weapon's up axis so it tilts with the weapon like a halo, growing brighter and
/// spinning faster as the charge level (0..1) builds. Released on cast with a quick outward
/// ring burst. Prefab-free, built from a translucent disc + two LineRenderer rings. Pure
/// visual: no colliders and nothing blocking gameplay.
/// </summary>
public sealed class CastingCircle : MonoBehaviour
{
    private static CastingCircle _instance;

    private const int OuterSegments = 48;
    private const int InnerSegments = 36;

    private const float BaseRadius = 0.35f;
    private const float FullRadius = 0.75f;

    private Transform _disc;
    private Renderer _discRenderer;
    private Material _discMat;
    private LineRenderer _outerRing;
    private Material _outerMat;
    private LineRenderer _innerRing;
    private Material _innerMat;

    private bool _active;
    private Transform _anchor;
    private float _charge;
    private Color _color = Color.white;
    private float _spin;
    private float _pulse;

    /// <summary>Ensure the singleton exists and returns it (builds on first access).</summary>
    public static CastingCircle Instance
    {
        get
        {
            if (_instance == null)
            {
                var go = new GameObject("CastingCircle");
                Object.DontDestroyOnLoad(go);
                _instance = go.AddComponent<CastingCircle>();
            }
            return _instance;
        }
    }

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

    /// <summary>Show/refresh the halo around the anchor weapon with a charge level (0..1).</summary>
    public void Show(Transform anchor, float charge, Color color)
    {
        if (anchor == null) { Hide(); return; }
        _active = true;
        _anchor = anchor;
        _charge = Mathf.Clamp01(charge);
        _color = color;
        transform.position = anchor.position + anchor.up * 0.05f;
        transform.rotation = Quaternion.FromToRotation(Vector3.up, anchor.up);
        gameObject.SetActive(true);
        Apply();
    }

    /// <summary>One-shot expanding ring at the current anchor, sized by the charge level.</summary>
    public void Burst(float radius, Color color, Vector3 upDir)
    {
        Vector3 at = _anchor != null ? _anchor.position : transform.position;
        SkillFx.RingFlash(at, upDir, color, Mathf.Max(radius, 0.4f), 0.35f);
    }

    /// <summary>Hide the halo immediately (aim cancelled, cast released, no magic, etc.).</summary>
    public void Hide()
    {
        if (!_active) return;
        _active = false;
        _anchor = null;
        gameObject.SetActive(false);
    }

    private void Update()
    {
        if (!_active) return;
        if (_anchor == null) { Hide(); return; }
        transform.position = _anchor.position + _anchor.up * 0.05f;
        transform.rotation = Quaternion.FromToRotation(Vector3.up, _anchor.up);

        _spin += Time.deltaTime * (18f + _charge * 60f);
        _pulse += Time.deltaTime * 2.4f;
        Apply();
    }

    private void Build()
    {
        Shader shader = Shader.Find("Sprites/Default") ?? Shader.Find("Unlit/Color");

        // Translucent disc filling the inner halo (flat cylinder, no collider).
        var discGo = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        discGo.name = "Disc";
        var dcol = discGo.GetComponent<Collider>();
        if (dcol != null) Destroy(dcol);
        _disc = discGo.transform;
        _disc.SetParent(transform, false);
        _disc.localPosition = Vector3.zero;
        _discRenderer = discGo.GetComponent<MeshRenderer>();
        if (shader != null && _discRenderer != null)
        {
            _discMat = new Material(shader);
            _discRenderer.material = _discMat;
        }
        else if (_discRenderer != null)
        {
            _discRenderer.enabled = false;
        }

        // Crisp outer halo ring.
        _outerRing = gameObject.AddComponent<LineRenderer>();
        _outerRing.useWorldSpace = false;
        _outerRing.loop = true;
        _outerRing.positionCount = OuterSegments;
        _outerRing.startWidth = 0.06f;
        _outerRing.endWidth = 0.06f;
        _outerRing.numCapVertices = 2;
        if (shader != null)
        {
            _outerMat = new Material(shader);
            _outerRing.material = _outerMat;
        }

        // Inner rune ring that spins while charging.
        _innerRing = gameObject.AddComponent<LineRenderer>();
        _innerRing.useWorldSpace = false;
        _innerRing.loop = true;
        _innerRing.positionCount = InnerSegments;
        _innerRing.startWidth = 0.03f;
        _innerRing.endWidth = 0.03f;
        _innerRing.numCapVertices = 2;
        if (shader != null)
        {
            _innerMat = new Material(shader);
            _innerRing.material = _innerMat;
        }
    }

    private void Apply()
    {
        float r = Mathf.Lerp(BaseRadius, FullRadius, _charge);
        float a = Mathf.Lerp(0.35f, 1f, _charge);
        float pulse = 1f + 0.03f * Mathf.Sin(_pulse);

        if (_disc != null)
            _disc.localScale = new Vector3(r * 2f * pulse, 0.02f, r * 2f * pulse);
        if (_discMat != null)
            _discMat.color = new Color(_color.r, _color.g, _color.b, 0.05f + 0.08f * _charge);

        if (_outerRing != null)
            for (int i = 0; i < OuterSegments; i++)
            {
                float ang = (i / (float)OuterSegments) * Mathf.PI * 2f;
                _outerRing.SetPosition(i, new Vector3(Mathf.Cos(ang) * r, 0f, Mathf.Sin(ang) * r));
            }
        if (_outerMat != null)
            _outerMat.color = new Color(_color.r, _color.g, _color.b, a);

        if (_innerRing != null)
        {
            float ir = r * 0.72f * pulse;
            for (int i = 0; i < InnerSegments; i++)
            {
                float ang = _spin * Mathf.Deg2Rad + (i / (float)InnerSegments) * Mathf.PI * 2f;
                _innerRing.SetPosition(i, new Vector3(Mathf.Cos(ang) * ir, 0f, Mathf.Sin(ang) * ir));
            }
        }
        if (_innerMat != null)
            _innerMat.color = new Color(_color.r, _color.g, _color.b, a * 0.8f);
    }
}
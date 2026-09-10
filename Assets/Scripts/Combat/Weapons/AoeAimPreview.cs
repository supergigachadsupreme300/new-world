using UnityEngine;

/// <summary>
/// World-space ground preview for aimable AoE magic (SpellDelivery.Zone / Vortex). While an
/// AoE spell is being aimed (LMB held in <see cref="PlayerController"/>), a translucent disc
/// plus a crisp outline ring and a pulsing beacon mark exactly where the zone will land — sized
/// to the spell's radius (growing with charge) and tinted by the spell's damage type.
///
/// On cast begin the preview locks in place and hides itself once the actual zone/vortex
/// resolves. Pure visual: no colliders and nothing blocking gameplay raycasts. Prefab-free,
/// built from primitives on first access.
/// </summary>
public sealed class AoeAimPreview : MonoBehaviour
{
    private static AoeAimPreview _instance;

    private const int RingSegments = 48;
    private const float GroundRaise = 0.04f;

    private Transform _disc;
    private Renderer _discRenderer;
    private LineRenderer _ring;
    private Transform _beacon;
    private Material _discMat;
    private Material _ringMat;
    private Material _beaconMat;

    private bool _active;
    private bool _locked;
    private SpellCaster _lockedCaster;
    private int _lockedCastCount = -1;
    private float _radius = 2f;
    private Color _color = Color.white;
    private float _pulse;

    /// <summary>Ensure the singleton exists and returns it (builds on first access).</summary>
    public static AoeAimPreview Instance
    {
        get
        {
            if (_instance == null)
            {
                var go = new GameObject("AoeAimPreview");
                Object.DontDestroyOnLoad(go);
                _instance = go.AddComponent<AoeAimPreview>();
            }
            return _instance;
        }
    }

    /// <summary>True while a preview is currently visible or locked.</summary>
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

    private void Update()
    {
        // Locked: keep marking the target until the tracked cast resolves (or never started).
        if (_locked)
        {
            if (_lockedCaster == null || _lockedCaster.CastCount != _lockedCastCount || !_lockedCaster.IsCasting)
            {
                Hide();
                return;
            }
            return;
        }
        if (!_active) return;

        _pulse += Time.deltaTime * 3.2f;
        Apply();
    }

    /// <summary>Show/refresh the preview at a ground point, sized to a radius and tinted by color.</summary>
    public void Show(Vector3 center, float radius, Color color)
    {
        _active = true;
        _locked = false;
        _lockedCaster = null;
        _radius = Mathf.Max(radius, 0.1f);
        _color = color;
        transform.position = center;
        gameObject.SetActive(true);
        Apply();
    }

    /// <summary>Freeze the preview at its current spot and hide when the ongoing cast resolves.</summary>
    public void Lock(SpellCaster caster)
    {
        if (!_active) return;
        _locked = true;
        _lockedCaster = caster;
        _lockedCastCount = caster != null ? caster.CastCount : -1;
    }

    /// <summary>Hide the preview immediately (aim cancelled, non-AoE aim, etc.).</summary>
    public void Hide()
    {
        if (!_active) return;
        _active = false;
        _locked = false;
        _lockedCaster = null;
        gameObject.SetActive(false);
    }

    private void Build()
    {
        Shader shader = Shader.Find("Sprites/Default") ?? Shader.Find("Unlit/Color");

        // Translucent footprint disc (flat cylinder, no collider).
        var discGo = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        discGo.name = "Footprint";
        var dcol = discGo.GetComponent<Collider>();
        if (dcol != null) Destroy(dcol);
        _disc = discGo.transform;
        _disc.SetParent(transform, false);
        _disc.localPosition = new Vector3(0f, GroundRaise, 0f);
        _disc.localScale = new Vector3(1f, 0.01f, 1f);
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

        // Crisp outline ring (LineRenderer circle in local space).
        _ring = gameObject.AddComponent<LineRenderer>();
        _ring.useWorldSpace = false;
        _ring.loop = true;
        _ring.positionCount = RingSegments;
        _ring.startWidth = 0.08f;
        _ring.endWidth = 0.08f;
        _ring.numCapVertices = 2;
        if (shader != null)
        {
            _ringMat = new Material(shader);
            _ring.material = _ringMat;
        }

        // Pulsing centre beacon so the exact target point stays readable.
        var beaconGo = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        beaconGo.name = "Beacon";
        var bcol = beaconGo.GetComponent<Collider>();
        if (bcol != null) Destroy(bcol);
        _beacon = beaconGo.transform;
        _beacon.SetParent(transform, false);
        _beacon.localPosition = new Vector3(0f, 0.55f, 0f);
        _beacon.localScale = new Vector3(0.16f, 0.55f, 0.16f);
        var brenderer = beaconGo.GetComponent<MeshRenderer>();
        if (shader != null && brenderer != null)
        {
            _beaconMat = new Material(shader);
            brenderer.material = _beaconMat;
        }
        else if (brenderer != null)
        {
            brenderer.enabled = false;
        }
    }

    private void Apply()
    {
        float pulse = 1f + 0.04f * Mathf.Sin(_pulse);

        if (_disc != null)
        {
            float d = _radius * 2f * pulse;
            _disc.localScale = new Vector3(d, 0.01f, d);
        }
        if (_discMat != null)
            _discMat.color = new Color(_color.r, _color.g, _color.b, 0.16f);

        if (_ring != null)
        {
            for (int i = 0; i < RingSegments; i++)
            {
                float a = (i / (float)RingSegments) * Mathf.PI * 2f;
                _ring.SetPosition(i, new Vector3(Mathf.Cos(a) * _radius, GroundRaise, Mathf.Sin(a) * _radius));
            }
        }
        if (_ringMat != null)
            _ringMat.color = new Color(_color.r, _color.g, _color.b, 0.85f);

        if (_beacon != null)
            _beacon.localScale = new Vector3(0.16f, 0.55f * pulse, 0.16f);
        if (_beaconMat != null)
            _beaconMat.color = new Color(_color.r, _color.g, _color.b, 0.9f);
    }
}
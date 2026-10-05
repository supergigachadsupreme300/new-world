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

    /// <summary>1jd: the three pieces moved to <see cref="AoeAimPreviewModelBuilder"/> (under
    /// <c>Models/Magic/</c>). This method only unpacks the builder's per-piece record into the fields
    /// <see cref="Apply"/> drives; every handle keeps its exact type, so the per-frame pulse code is
    /// unchanged. <c>Renderer</c> is the base type on the record, so the ring's cast to
    /// <see cref="LineRenderer"/> happens once here rather than every frame.</summary>
    private void Build()
    {
        AoeAimPreviewModelBuilder.Preview preview =
            AoeAimPreviewModelBuilder.Build(transform, RingSegments, GroundRaise);
        _disc = preview.Disc.Transform;
        _discRenderer = preview.Disc.Renderer;
        _discMat = preview.Disc.Material;
        _ring = preview.Ring.Renderer as LineRenderer;
        _ringMat = preview.Ring.Material;
        _beacon = preview.Beacon.Transform;
        _beaconMat = preview.Beacon.Material;
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
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Channeled beam delivery (SpellDelivery.Beam). Spawned by SpellCaster on cast (e.g. wheel-cast
/// release), aimed along the cast direction, and kept alive while the caster holds LMB and can pay
/// the per-second focus upkeep (<see cref="SpellData.ChannelDrainPerSecond"/>). The beam tracks the
/// caster position each frame, ticks the spell's damage to everything inside its capsule, and fades
/// out on input release or when the focus pool runs dry. On devices without a mouse the beam sustains
/// itself for a short fixed window instead.
/// </summary>
public class SpellBeam : MonoBehaviour
{
    /// <summary>Grace window after the sustain input is released (allows re-press before fade).</summary>
    public const float InputGrace = 0.4f;

    public Vector3 Direction;
    public float Length = 12f;
    public float Width = 1.2f;
    public float TickInterval = 0.5f;
    public float ChannelDrainPerSecond = 0f;

    private SpellCaster _caster;
    private SpellData _spell;
    private float _power;
    private Transform _casterRoot;
    private float _tick;
    private float _noInput;
    private float _fixedWindow = 1.6f;
    private bool _fadeOut;
    private float _fadeAge;
    private float _fadeTime = 0.22f;
    private Transform _body;
    private Transform _endOrb;
    private Material _mat;
    private Material _orbMat;
    private Color _baseColor;
    private Vector3 _bodyBaseScale;
    private Vector3 _orbBaseScale;
    private readonly Collider[] _tickBuffer = new Collider[64];

    /// <summary>1ie/1ih: the beam's resolved look, cached at Initialize.</summary>
    private SpellLook _look;

    // 1e5: cached main camera (re-fetch only when the cache goes stale), instead of Camera.main
    // per channeled frame.
    private Camera _mainCam;

    /// <summary>Configure the beam. Length/width multiplied by the charged size scale.</summary>
    public void Initialize(SpellCaster caster, SpellData spell, float power,
        Vector3 dir, float lengthMult = 1f, float widthMult = 1f)
    {
        _caster = caster;
        _spell = spell;
        _power = power;
        _casterRoot = caster != null ? caster.transform.root : null;
        Direction = dir.sqrMagnitude > 0.0001f ? dir.normalized : Vector3.forward;
        Length = Mathf.Max(spell != null ? spell.Range * lengthMult : Length, 1f);
        Width = Mathf.Max(spell != null ? spell.Radius * widthMult : Width, 0.1f);
        if (spell != null && spell.TickInterval > 0f) TickInterval = spell.TickInterval;
        if (spell != null) ChannelDrainPerSecond = Mathf.Max(spell.ChannelDrainPerSecond, 0f);

        Vector3 end = EndPoint();
        // 1ie: per-spell Core colour + scale. Resolved once here rather than per tick, so the beam's
        // channeled frames do no hashing at all.
        _look = spell != null ? SpellLook.Resolve(spell) : SpellLook.Resolve(DamageType.Arcane, ProjectileShape.Auto);
        Color color = spell != null ? _look.Core : Color.white;
        if (spell != null)
            SkillFx.RingFlash(end, Vector3.up, color, Mathf.Max(Width * 1.5f, 0.4f), 0.3f, _look.Scale);
        BuildVisual(color);
    }

    private void Update()
    {
        if (_spell == null)
        {
            Destroy(gameObject);
            return;
        }

        // Origin follows the caster's hand so the beam tracks movement while held.
        if (_caster != null)
            transform.position = _caster.transform.position;

        // Re-aim with the caster's current aim each frame so the beam sweeps as the
        // player turns. Mirrors SpellCaster.Execute's aim derivation (camera forward,
        // caster forward fallback) so the tick capsule tracks the same line as the visual.
        if (_mainCam == null)
            _mainCam = Camera.main;
        Camera cam = _mainCam;
        if (cam != null)
        {
            Vector3 aim = cam.transform.position + cam.transform.forward * Length;
            Vector3 dir = aim - transform.position;
            if (dir.sqrMagnitude > 0.0001f)
                Direction = dir.normalized;
        }
        else if (_caster != null && _caster.transform.forward.sqrMagnitude > 0.0001f)
        {
            Direction = _caster.transform.forward;
        }

        if (_fadeOut)
        {
            _fadeAge += Time.deltaTime;
            float t = Mathf.Clamp01(_fadeAge / _fadeTime);
            if (_mat != null)
            {
                Color c = _baseColor;
                c.a = Mathf.Max(0f, 1f - t);
                _mat.color = c;
            }
            if (_orbMat != null)
            {
                Color c = _baseColor;
                c.a = Mathf.Max(0f, 1f - t);
                _orbMat.color = c;
            }
            if (t >= 1f)
            {
                EndChannel();
                Destroy(gameObject);
            }
            return;
        }

        bool fixedSustain = _caster == null || GameInput.IsMobile || Mouse.current == null;
        if (fixedSustain)
        {
            _fixedWindow -= Time.deltaTime;
            if (_fixedWindow <= 0f)
            {
                _fadeOut = true;
                return;
            }
        }
        else if (Mouse.current.leftButton.isPressed)
        {
            _noInput = 0f;
            if (ChannelDrainPerSecond > 0f)
            {
                float cost = ChannelDrainPerSecond * Time.deltaTime;
                if (cost > 0f && !_caster.TrySpendFocus(cost))
                {
                    // Focus pool exhausted — the channel can no longer be paid for.
                    _fadeOut = true;
                    return;
                }
            }
        }
        else
        {
            _noInput += Time.deltaTime;
            if (_noInput >= InputGrace)
            {
                _fadeOut = true;
                return;
            }
        }

        PulseVisual();

        _tick -= Time.deltaTime;
        if (_tick <= 0f)
        {
            _tick = TickInterval > 0f ? TickInterval : 0.5f;
            Tick();
        }
    }

    private void Tick()
    {
        if (_caster == null || _spell == null) return;

        int count = Physics.OverlapCapsuleNonAlloc(transform.position, EndPoint(), Width, _tickBuffer);
        bool struck = false;
        for (int i = 0; i < count; i++)
        {
            var col = _tickBuffer[i];
            if (col == null) continue;

            Transform root = col.transform.root;
            if (root == _casterRoot) continue;

            // Healing beams mend allies in the line while still searing enemies.
            if (_spell.Heals && root.TryGetComponent<IHealable>(out _))
            {
                _caster.ResolveHeal(_spell, _power, col.gameObject);
                continue;
            }
            if (root.CompareTag("Player") || root.CompareTag("Companion")) continue;
            if (!col.gameObject.TryGetComponent<IDamageable>(out _)) continue;

            _caster.ResolveHitAt(col.gameObject, _spell, _power);
            struck = true;
        }

        // 1ih: ONE flash per tick, not one per target — the loop above is a collider walk, so a
        // beam through six enemies must not spawn six flashes (H39).
        if (struck)
            SpellImpactFx.Spawn(MidPoint(), Vector3.up, _look, Width * 1.2f);
    }

    /// <summary>Halfway along the beam, where the flash is most visible.</summary>
    private Vector3 MidPoint() => (transform.position + EndPoint()) * 0.5f;

    /// <summary>Force-end the channel immediately (new cast, etc.).</summary>
    public void StopChannel()
    {
        _fadeOut = true;
        _fadeTime = Mathf.Min(_fadeTime, 0.1f);
    }

    private void EndChannel()
    {
        if (_caster != null) _caster.ForgetBeam(this);
    }

    private Vector3 EndPoint()
    {
        return transform.position + Direction * Length;
    }

    private void PulseVisual()
    {
        if (_body == null || _endOrb == null) return;
        Vector3 mid = transform.position + Direction * (Length * 0.5f);
        _body.position = mid;
        _body.rotation = Quaternion.FromToRotation(Vector3.up, Direction);
        _endOrb.position = EndPoint();

        float pulse = 1f + 0.12f * Mathf.Sin(Time.time * 23f) + 0.06f * Mathf.Sin(Time.time * 41f);
        _body.localScale = _bodyBaseScale * pulse;
        _endOrb.localScale = _orbBaseScale * (1f + 0.25f * Mathf.Sin(Time.time * 9f));
    }

    private void BuildVisual(Color color)
    {
        Shader shader = Shader.Find("Sprites/Default") ?? Shader.Find("Unlit/Color");
        if (shader == null) return;

        Vector3 mid = transform.position + Direction * (Length * 0.5f);
        Quaternion rot = Quaternion.FromToRotation(Vector3.up, Direction);

        _body = GameObject.CreatePrimitive(PrimitiveType.Cylinder).transform;
        _body.name = "BeamBody";
        DestroyCollider(_body);
        _body.SetParent(transform, false);
        _body.position = mid;
        _body.rotation = rot;
        _body.localScale = new Vector3(Width * 2f, Length * 0.5f, Width * 2f);
        _mat = SetMaterial(_body, shader, color);
        _bodyBaseScale = _body.localScale;

        _endOrb = GameObject.CreatePrimitive(PrimitiveType.Sphere).transform;
        _endOrb.name = "BeamEnd";
        DestroyCollider(_endOrb);
        _endOrb.SetParent(transform, false);
        _endOrb.position = EndPoint();
        _endOrb.localScale = Vector3.one * Mathf.Max(Width * 1.6f, 0.3f);
        _orbMat = SetMaterial(_endOrb, shader, color);
        _orbBaseScale = _endOrb.localScale;
        _baseColor = color;
    }

    private static void DestroyCollider(Transform t)
    {
        Collider col = t.GetComponent<Collider>();
        if (col != null) Destroy(col);
    }

    private static Material SetMaterial(Transform t, Shader shader, Color color)
    {
        var mat = new Material(shader) { color = color };
        var r = t.GetComponent<MeshRenderer>();
        if (r != null) r.material = mat;
        return mat;
    }
}
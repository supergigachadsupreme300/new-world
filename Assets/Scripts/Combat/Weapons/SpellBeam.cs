using System.Collections.Generic;
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

    /// <summary>1ir: rays across a cone beam's mouth. Odd so one ray sits on the aim axis.</summary>
    public const int ConeRays = 7;

    /// <summary>1ir: capsule segments per ray. 2 = an expanding pair of overlapping capsules.</summary>
    public const int ConeSegments = 2;

    /// <summary>1ir: mouth radius as a fraction of the tip radius (Width) — the cone opens outward.
    /// Public so the QA bench draws the same mouth the live beam does instead of restating the
    /// ratio (1f7's BuildRockBody precedent: mount the real geometry, not a copy of it).</summary>
    public const float ConeMouthFraction = 0.35f;

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
    private Color _baseColor;
    private Vector3 _bodyBaseScale;
    private Vector3 _orbBaseScale;
    private readonly Collider[] _tickBuffer = new Collider[64];

    /// <summary>1ie/1ih: the beam's resolved look, cached at Initialize.</summary>
    private SpellLook _look;

    // 1e5: cached main camera (re-fetch only when the cache goes stale), instead of Camera.main
    // per channeled frame.
    private Camera _mainCam;

    // 1ir: cone state. _coneHalfRad is the SWEPT half-angle in radians; 0 means "legacy line", which
    // is what every other Beam spell gets (BeamHalfAngle defaults to 0), so the cone is opt-in only.
    private float _coneHalfRad;
    private float _mouthRadius;
    private Transform[] _coneVisuals;
    private Vector3[] _coneBaseScale;

    /// <summary>1ir: every material that must fade out together (2 for a line, 1 + rays*segs + ring
    /// for a cone). Replaces the old single _mat/_orbMat pair so one fade path serves both modes
    /// instead of the cone needing a parallel copy of the fade block.</summary>
    private readonly List<Material> _fadeMats = new List<Material>();

    /// <summary>1ir: roots already damaged this tick. Seven rays through one enemy would otherwise
    /// hit it seven times in the same tick — the overlap capsules are counted per ray, not per
    /// victim, so without this a beam through a single large target multiplies its damage by
    /// ConeRays * ConeSegments.</summary>
    private readonly HashSet<Transform> _tickSeen = new HashSet<Transform>();

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

        // 1ir: opt-in cone. A half-angle of 0 (or less) leaves _coneHalfRad at 0, which is the
        // legacy line — see Tick(). Width keeps ONE meaning in both modes: the beam's widest point,
        // which for a cone is the tip.
        _coneHalfRad = spell != null && spell.BeamHalfAngle > 0f
            ? spell.BeamHalfAngle * Mathf.Deg2Rad
            : 0f;
        _mouthRadius = Width * ConeMouthFraction;

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
            // 1ir: one loop for both modes. The line contributes 2 materials and the cone 1 + rays*segs,
            // so this replaces the old _mat/_orbMat pair without changing what a line beam fades.
            for (int i = 0; i < _fadeMats.Count; i++)
            {
                Material m = _fadeMats[i];
                if (m == null) continue;
                Color c = _baseColor;
                c.a = Mathf.Max(0f, 1f - t);
                m.color = c;
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

        // 1ir: cone beams take a different walk; the line below is untouched apart from routing its
        // per-collider decision through TickCollider(), which is the same test in the same order.
        if (_coneHalfRad > 0f) { TickCone(); return; }

        int count = Physics.OverlapCapsuleNonAlloc(transform.position, EndPoint(), Width, _tickBuffer);
        bool struck = false;
        for (int i = 0; i < count; i++)
            if (TickCollider(_tickBuffer[i]) == BeamHit.Damaged) struck = true;

        // 1ih: ONE flash per tick, not one per target — the loop above is a collider walk, so a
        // beam through six enemies must not spawn six flashes (H39).
        if (struck)
            SpellImpactFx.Spawn(MidPoint(), Vector3.up, _look, Width * 1.2f);
    }

    /// <summary>1ir: what one collider's tick actually did. Three states, because "did something" and
    /// "was it damage" are different questions: the cone walk must stop re-touching a root after the
    /// FIRST effect of any kind, while only a damage strike may spawn the once-per-tick flash.
    /// <para>An enum rather than a bool because the first version returned a bool and returned
    /// <c>false</c> for the heal branch — so a healing cone never marked the root seen and healed
    /// it once per overlapping capsule (up to ConeRays * ConeSegments times per tick). No shipped
    /// cone heals, but the axis is shared and the bug was free to ship.</para></summary>
    private enum BeamHit
    {
        /// <summary>Nothing happened — skip, but do NOT consume the root.</summary>
        None = 0,
        /// <summary>An ally was mended. Consumes the root; no flash.</summary>
        Healed = 1,
        /// <summary>An enemy took damage. Consumes the root; one flash.</summary>
        Damaged = 2,
    }

    /// <summary>
    /// 1ir: apply one collider's beam tick, reporting which of the three outcomes it was.
    /// Shared by the line and cone walks so the two cannot drift apart — the heal branch, the
    /// self/ally skip and the damageable test are identical in both, and a beam's "who does this
    /// hit" is a single question with a single answer.
    /// </summary>
    private BeamHit TickCollider(Collider col)
    {
        if (col == null) return BeamHit.None;

        Transform root = col.transform.root;
        if (root == _casterRoot) return BeamHit.None;

        // Healing beams mend allies in the line while still searing enemies.
        if (_spell.Heals && root.TryGetComponent<IHealable>(out _))
        {
            _caster.ResolveHeal(_spell, _power, col.gameObject);
            return BeamHit.Healed;
        }
        if (root.CompareTag("Player") || root.CompareTag("Companion")) return BeamHit.None;
        if (!col.gameObject.TryGetComponent<IDamageable>(out _)) return BeamHit.None;

        _caster.ResolveHitAt(col.gameObject, _spell, _power);
        return BeamHit.Damaged;
    }

    /// <summary>
    /// 1ir: cone tick. Fans <see cref="ConeRays"/> rays across the mouth, each covered by
    /// <see cref="ConeSegments"/> overlapping capsules whose radius grows mouth→tip, and takes the
    /// FIRST live owner of each root (see _tickSeen) so one enemy is hit once per tick rather than
    /// once per overlapping capsule. Flashes once per tick, like the line.
    /// </summary>
    private void TickCone()
    {
        Vector3 origin = transform.position;
        bool struck = false;
        _tickSeen.Clear();

        for (int r = 0; r < ConeRays; r++)
        {
            Vector3 dir = RayDirection(r);
            float prev = 0f;
            for (int s = 1; s <= ConeSegments; s++)
            {
                float t = (float)s / ConeSegments;
                float len = Length * t;
                float rad = Mathf.Lerp(_mouthRadius, Width, t);
                Vector3 a = origin + dir * prev;
                Vector3 b = origin + dir * len;

                int count = Physics.OverlapCapsuleNonAlloc(a, b, rad, _tickBuffer,
                    Physics.DefaultRaycastLayers, QueryTriggerInteraction.UseGlobal);
                for (int i = 0; i < count; i++)
                {
                    Collider col = _tickBuffer[i];
                    if (col == null) continue;
                    // Dedupe on the OUTCOME, not on contact: a root is only marked once a collider of
                    // it actually landed the hit. Marking on first contact would let a non-damageable
                    // child (decoration, a limb with no IDamageable) consume the root and make the
                    // whole enemy immune, which the line path never did because it tested every
                    // collider independently.
                    if (_tickSeen.Contains(col.transform.root)) continue;
                    BeamHit hit = TickCollider(col);
                    if (hit == BeamHit.None) continue;
                    // Mark on ANY effect, so a root is consumed exactly once per tick whether it was
                    // seared or mended — but mark only AFTER a real outcome, so a non-damageable
                    // child (decoration, a limb with no IDamageable) cannot consume the root and make
                    // the whole enemy immune, which the line path never did because it tested every
                    // collider independently.
                    _tickSeen.Add(col.transform.root);
                    if (hit == BeamHit.Damaged) struck = true;
                }
                prev = len;
            }
        }

        if (struck)
            SpellImpactFx.Spawn(MidPoint(), Vector3.up, _look, Width * 1.2f);
    }

    /// <summary>1ir: aim-axis-aligned ray for cone index <paramref name="i"/>, evenly fanned so an
    /// odd count always puts one ray dead centre.</summary>
    private Vector3 RayDirection(int i)
    {
        if (ConeRays <= 1) return Direction;
        float a = (i / (float)(ConeRays - 1) * 2f - 1f) * _coneHalfRad;
        return Quaternion.AngleAxis(a * Mathf.Rad2Deg, Vector3.up) * Direction;
    }

    /// <summary>1ir: the cone's swept FULL angle in degrees, which is what the aim preview must draw.
    /// The rays span -half..+half around the axis, so the full angle is TWICE
    /// <see cref="SpellData.BeamHalfAngle"/> (22° half-angle = a 44° mouth). The preview must not
    /// draw the half-angle: that understates the real hit area by exactly half, which is the mirror
    /// of 1iq's overstating cone — an aim readout that lies about how wide the spell is.</summary>
    public static float ConeFullAngleDegrees(SpellData spell)
    {
        return spell == null || spell.BeamHalfAngle <= 0f ? 0f : spell.BeamHalfAngle * 2f;
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
        if (_endOrb == null) return;
        float pulse = 1f + 0.12f * Mathf.Sin(Time.time * 23f) + 0.06f * Mathf.Sin(Time.time * 41f);

        // 1ir: the line keeps its single body; a cone pulses each ray segment instead.
        if (_body != null)
        {
            Vector3 mid = transform.position + Direction * (Length * 0.5f);
            _body.position = mid;
            _body.rotation = Quaternion.FromToRotation(Vector3.up, Direction);
            _body.localScale = _bodyBaseScale * pulse;
        }
        else if (_coneVisuals != null)
        {
            for (int r = 0; r < ConeRays; r++)
            {
                // World-space ray (fanned around world up, like a flashlight), then converted into
                // the parent's local frame so the segments sit on the beam transform — and, on the
                // bench, on the model root's pedestal. Positioning these in world space instead
                // would build the whole cone at the world origin and ignore the parent entirely.
                Vector3 localDir = transform.InverseTransformDirection(RayDirection(r));
                float prev = 0f;
                for (int s = 1; s <= ConeSegments; s++)
                {
                    int idx = r * ConeSegments + (s - 1);
                    Transform seg = _coneVisuals[idx];
                    if (seg == null) continue;
                    float t = (float)s / ConeSegments;
                    float len = Length * t;
                    seg.localPosition = localDir * ((prev + len) * 0.5f);
                    seg.localRotation = Quaternion.FromToRotation(Vector3.up, localDir);
                    seg.localScale = _coneBaseScale[idx] * pulse;
                    prev = len;
                }
            }
        }

        _endOrb.position = EndPoint();
        _endOrb.localScale = _orbBaseScale * (1f + 0.25f * Mathf.Sin(Time.time * 9f));
    }

    private void BuildVisual(Color color)
    {
        Shader shader = Shader.Find("Sprites/Default") ?? Shader.Find("Unlit/Color");
        if (shader == null) return;

        // 1ir: cone and line are two builders, one shared tip orb.
        if (_coneHalfRad > 0f)
        {
            _coneVisuals = BuildConeVisual(transform, Length, _mouthRadius, Width, color, _coneHalfRad);
            _coneBaseScale = new Vector3[_coneVisuals.Length];
            for (int i = 0; i < _coneVisuals.Length; i++)
                if (_coneVisuals[i] != null) _coneBaseScale[i] = _coneVisuals[i].localScale;
            _baseColor = color;
        }
        else
        {
            Vector3 mid = transform.position + Direction * (Length * 0.5f);
            Quaternion rot = Quaternion.FromToRotation(Vector3.up, Direction);

            _body = GameObject.CreatePrimitive(PrimitiveType.Cylinder).transform;
            _body.name = "BeamBody";
            DestroyCollider(_body);
            _body.SetParent(transform, false);
            _body.position = mid;
            _body.rotation = rot;
            _body.localScale = new Vector3(Width * 2f, Length * 0.5f, Width * 2f);
            SetMaterial(_body, shader, color);
            _bodyBaseScale = _body.localScale;
            _baseColor = color;
        }

        _endOrb = GameObject.CreatePrimitive(PrimitiveType.Sphere).transform;
        _endOrb.name = "BeamEnd";
        DestroyCollider(_endOrb);
        _endOrb.SetParent(transform, false);
        _endOrb.position = EndPoint();
        _endOrb.localScale = Vector3.one * Mathf.Max(Width * 1.6f, 0.3f);
        SetMaterial(_endOrb, shader, color);
        _orbBaseScale = _endOrb.localScale;
        CollectFadeMaterials();
    }

    /// <summary>
    /// 1ir: gather every material this beam owns so the one fade loop can drive all of them. Scans
    /// children instead of tracking each primitive by hand, because the cone's segment count is a
    /// constant the visual builder decides — a hand-kept list would be a second place to update when
    /// that constant changes, and would silently leave the cone solid while the line faded.
    /// </summary>
    private void CollectFadeMaterials()
    {
        _fadeMats.Clear();
        var renderers = GetComponentsInChildren<MeshRenderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            Material m = renderers[i].material;
            if (m != null) _fadeMats.Add(m);
        }
    }

    /// <summary>
    /// 1ir: build the cone's <see cref="ConeRays"/> * <see cref="ConeSegments"/> fanned capsule
    /// segments under <paramref name="parent"/>, plus a mouth ring, and return the segments in
    /// ray-major order (index = ray * ConeSegments + segment - 1) so the tick walk and the pulse
    /// walk can address the same geometry without re-deriving either order.
    /// <para>Segments are placed in the parent's LOCAL frame (fanning around its +Z), which is what
    /// lets the QA bench mount this on a pedestal and the live beam on its own transform without two
    /// coordinate conventions. The live PulseVisual then re-places them each frame from the beam's
    /// real world-space ray.</para>
    /// <para>Public and static on purpose: the QA bench mounts THIS, the same geometry the live beam
    /// draws, rather than a proxy cone that could drift from what ships. That is the 1f7
    /// <c>BuildRockBody</c> precedent — a visual that exists only inside a live cast has no
    /// acceptance readout (1ij).</para>
    /// </summary>
    public static Transform[] BuildConeVisual(Transform parent, float length, float mouthRadius,
        float tipRadius, Color color, float halfAngleRad)
    {
        Shader shader = Shader.Find("Sprites/Default") ?? Shader.Find("Unlit/Color");
        if (shader == null) return new Transform[0];

        int total = ConeRays * ConeSegments;
        var segs = new Transform[total];
        float effHalf = Mathf.Max(halfAngleRad, 0.0001f);

        for (int r = 0; r < ConeRays; r++)
        {
            float ang = (r / (float)Mathf.Max(ConeRays - 1, 1) * 2f - 1f) * effHalf;
            Vector3 dir = Quaternion.AngleAxis(ang * Mathf.Rad2Deg, Vector3.up) * Vector3.forward;
            float prev = 0f;
            for (int s = 1; s <= ConeSegments; s++)
            {
                float t = (float)s / ConeSegments;
                float len = length * t;
                float rad = Mathf.Lerp(mouthRadius, tipRadius, t);
                Vector3 segDir = dir;
                float mid = (prev + len) * 0.5f;

                Transform seg = GameObject.CreatePrimitive(PrimitiveType.Cylinder).transform;
                seg.name = "ConeSeg_" + r + "_" + (s - 1);
                seg.SetParent(parent, false);
                seg.localPosition = segDir * mid;
                seg.localRotation = Quaternion.FromToRotation(Vector3.up, segDir);
                seg.localScale = new Vector3(rad * 2f, (len - prev) * 0.5f, rad * 2f);
                DestroyCollider(seg);
                SetMaterial(seg, shader, color);
                segs[r * ConeSegments + (s - 1)] = seg;
                prev = len;
            }
        }

        // Mouth ring so the wide end reads as an opening rather than a flat cut.
        Transform ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder).transform;
        ring.name = "ConeMouth";
        ring.SetParent(parent, false);
        ring.localScale = new Vector3(mouthRadius * 2.1f, 0.02f, mouthRadius * 2.1f);
        DestroyCollider(ring);
        SetMaterial(ring, shader, color);

        return segs;
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
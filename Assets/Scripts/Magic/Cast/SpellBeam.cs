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

    /// <summary>1ir: mouth radius as a fraction of the tip radius (Width) â€” the cone opens outward.
    /// Public so the QA bench draws the same mouth the live beam does instead of restating the
    /// ratio (1f7's BuildRockBody precedent: mount the real geometry, not a copy of it).</summary>
    public const float ConeMouthFraction = 0.35f;

    /// <summary>1is: chunky discs stacked along the beam axis to form the drawn cone body. This is a
    /// COSMETIC count â€” it does NOT change the hitbox, which is ConeRays * ConeSegments analytic
    /// capsules (see TickCone). It deliberately does not scale with Length: the Great Tornado's
    /// silhouette is a fixed-density stack, and a density that changed with the spell's range would
    /// be a second thing to tune and a second way for the funnel to read differently per spell.</summary>
    public const int FunnelChunks = 9;

    /// <summary>1is: orbiting debris chunks around the funnel axis (the Great Tornado's swirl read,
    /// as SpellTornado gets from TornadoBehavior.AddDebrisBlock).</summary>
    public const int FunnelDebris = 3;

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

    /// <summary>1is: the drawn funnel's chunk transforms, in build order (FunnelChunks discs then
    /// FunnelDebris orbiting chunks).
    /// <para>1is replaced 1ir's <c>_coneVisuals</c>/<c>_coneBaseScale</c> ray-fan pair, which the
    /// funnel body made dead: the tick still walks ConeRays/ConeSegments analytically and the drawn
    /// body no longer sits on those rays.</para>
    /// <para>1kb: the chunks hold the scale the builder wrote — the per-frame base-radius readout
    /// <c>_funnelBaseRadius</c> existed only for the body breath and is deleted with it.</para></summary>
    private Transform[] _funnelChunks;

    /// <summary>1ir: every material that must fade out together (2 for a line, 1 + rays*segs + ring
    /// for a cone). Replaces the old single _mat/_orbMat pair so one fade path serves both modes
    /// instead of the cone needing a parallel copy of the fade block.
    /// <para>1is: the cone's count is now FunnelChunks + FunnelDebris + ring (13) rather than
    /// ConeRays * ConeSegments + ring (15) â€” two fewer fade materials, and the two-tone funnel needs
    /// one material per chunk precisely BECAUSE each chunk is tinted differently along the gradient,
    /// which the old single-colour fan never was.</para></summary>
    private readonly List<Material> _fadeMats = new List<Material>();

    /// <summary>1ir: roots already damaged this tick. Seven rays through one enemy would otherwise
    /// hit it seven times in the same tick â€” the overlap capsules are counted per ray, not per
    /// victim, so without this a beam through a single large target multiplies its damage by
    /// ConeRays * ConeSegments. 1is: the ray FAN is gone from the drawing, but this dedupe is
    /// untouched and still load-bearing, because TickCone still walks all 14 analytic capsules.</para></summary>
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
        // legacy line â€” see Tick(). Width keeps ONE meaning in both modes: the beam's widest point,
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

        // Re-aim with the caster's current aim each frame so the beam sweeps as the player turns.
        // 1is: the derivation itself now lives in SpellCaster.CurrentAimDirection, shared with
        // SpellSummon â€” two components needing "which way is the player aiming" is exactly when a
        // second copy starts drifting. The cached camera is still passed in, so the shared helper
        // does not undo 1e5's "no Camera.main per channeled frame".
        if (_mainCam == null) _mainCam = Camera.main;
        Vector3 casterForward = _caster != null ? _caster.transform.forward : Vector3.forward;
        Vector3 aimDir = SpellCaster.CurrentAimDirection(transform.position, Length, casterForward, _mainCam);
        if (aimDir.sqrMagnitude > 0.0001f)
            Direction = aimDir;

        // 1jz: a cone's funnel must re-aim with the NEW Direction each frame, exactly like the tip orb
        // (BeamEnd, placed at EndPoint every frame) and the line body (rotated in PulseVisual). The
        // funnel's discs are laid out in the parent's local +Z once at build time, so a parent that
        // kept its spawn rotation left the WHOLE funnel frozen pointing at the cast-time aim while the
        // tick walked the new Direction — "the flamethrower funnel doesn't rotate with me" while the
        // tip orb swung away from the cone. Re-orienting the parent re-aims every disc, the orbiting
        // debris and the leading ring in one line (since 1kb they HOLD their build scale — only the
        // debris orbits local X/Y — which stays correct because it all now re-aims on the LIVE beam
        // axis). The line beam is untouched:
        // it places its body in world space and never reads the parent's rotation.
        if (_funnelChunks != null && Direction.sqrMagnitude > 0.0001f)
            transform.rotation = Quaternion.FromToRotation(Vector3.forward, Direction);

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
                    // Focus pool exhausted â€” the channel can no longer be paid for.
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

        // 1ih: ONE flash per tick, not one per target â€” the loop above is a collider walk, so a
        // beam through six enemies must not spawn six flashes (H39).
        if (struck)
            SpellImpactFx.Spawn(MidPoint(), Vector3.up, _look, Width * 1.2f);
    }

    /// <summary>1ir: what one collider's tick actually did. Three states, because "did something" and
    /// "was it damage" are different questions: the cone walk must stop re-touching a root after the
    /// FIRST effect of any kind, while only a damage strike may spawn the once-per-tick flash.
    /// <para>An enum rather than a bool because the first version returned a bool and returned
    /// <c>false</c> for the heal branch â€” so a healing cone never marked the root seen and healed
    /// it once per overlapping capsule (up to ConeRays * ConeSegments times per tick). No shipped
    /// cone heals, but the axis is shared and the bug was free to ship.</para></summary>
    private enum BeamHit
    {
        /// <summary>Nothing happened â€” skip, but do NOT consume the root.</summary>
        None = 0,
        /// <summary>An ally was mended. Consumes the root; no flash.</summary>
        Healed = 1,
        /// <summary>An enemy took damage. Consumes the root; one flash.</summary>
        Damaged = 2,
    }

    /// <summary>
    /// 1ir: apply one collider's beam tick, reporting which of the three outcomes it was.
    /// Shared by the line and cone walks so the two cannot drift apart â€” the heal branch, the
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
    /// <see cref="ConeSegments"/> overlapping capsules whose radius grows mouthâ†’tip, and takes the
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
                    // Dedupe on the OUTCOME, not on contact, and mark only AFTER a real outcome. Both
                    // halves matter and they are the same idea: marking on first contact would let a
                    // non-damageable child (decoration, a limb with no IDamageable) consume the root
                    // and make the whole enemy immune â€” which the line path never did, because it
                    // tested every collider independently.
                    if (_tickSeen.Contains(col.transform.root)) continue;
                    BeamHit hit = TickCollider(col);
                    if (hit == BeamHit.None) continue;
                    // Mark on ANY effect, so a root is consumed exactly once per tick whether it was
                    // seared or mended.
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
    /// <see cref="SpellData.BeamHalfAngle"/> (22Â° half-angle = a 44Â° mouth). The preview must not
    /// draw the half-angle: that understates the real hit area by exactly half, which is the mirror
    /// of 1iq's overstating cone â€” an aim readout that lies about how wide the spell is.</summary>
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

        // 1kb (testing.md task 5): the BODY HOLDS its authored size — the shrink-and-enlarge
        // breathing is gone. The line's pulse previously scaled `_bodyBaseScale * pulse` every frame
        // (12% + 6% oscillation at 23/41 Hz); a cone flared every disc's radius by `pulse` and
        // compounded its thickness through `s.y * pulse` (a slow random-walk upward that this freeze
        // also ends). Both now keep the scale the builder wrote; only position/rotation still
        // re-derive per frame, because the beam must track the caster and sweep with the aim.
        // "Holding" is the steady extended beam — the debris keeps orbiting and the tip orb keeps its
        // soft glow throb below, so the beam is held, not frozen in place.
        float tempo = _look != null ? _look.Tempo : 1f;
        float scaleMul = _look != null ? _look.Scale : 1f;

        if (_body != null)
        {
            Vector3 mid = transform.position + Direction * (Length * 0.5f);
            _body.position = mid;
            _body.rotation = Quaternion.FromToRotation(Vector3.up, Direction);
            _body.localScale = _bodyBaseScale;
        }
        else if (_funnelChunks != null)
        {
            // Gentle axial bob + per-disc scale pulse for cone funnel (Flamethrower).
            // Discs keep their authored scale except breathing; debris orbits as before.
            float axialPhase = Time.time * 2.2f * tempo;
            float axial = Mathf.Sin(axialPhase) * 0.05f;
            for (int i = 0; i < FunnelChunks; i++)
            {
                int idx = i;
                if (idx >= _funnelChunks.Length) break;
                Transform t = _funnelChunks[idx];
                if (t == null) continue;
                float lenT = i / (float)Mathf.Max(1, FunnelChunks);
                float discPhase = Time.time * 3.4f * tempo + i * 0.38f;
                float ds = 1f + Mathf.Sin(discPhase) * 0.08f * scaleMul + (1f - lenT) * 0.03f * Mathf.Sin(discPhase * 1.1f);
                Vector3 ls = t.localScale;
                t.localScale = new Vector3(ls.x * ds, ls.y, ls.z * ds);
                Vector3 lpDisc = t.localPosition;
                t.localPosition = new Vector3(lpDisc.x, lpDisc.y, lpDisc.z + axial * lenT);
            }
            for (int d = 0; d < FunnelDebris; d++)
            {
                int idx = FunnelChunks + d;
                Transform chunk = _funnelChunks[idx];
                if (chunk == null) continue;
                float ang = (70f + d * 55f) * Time.deltaTime * tempo * Mathf.Deg2Rad;
                float cos = Mathf.Cos(ang), sin = Mathf.Sin(ang);
                Vector3 lp = chunk.localPosition;
                chunk.localPosition = new Vector3(
                    lp.x * cos - lp.y * sin,
                    lp.x * sin + lp.y * cos,
                    lp.z);
            }
        }

        _endOrb.position = EndPoint();
        _endOrb.localScale = _orbBaseScale * (1f + 0.25f * Mathf.Sin(Time.time * 9f));
    }

    /// <summary>1jd: the drawn body moved to <see cref="SpellBeamModelBuilder"/> (under
    /// <c>Models/Magic/</c>); this method still decides WHICH body, and still owns everything the
    /// beam does with it afterwards (since 1kb: holding its scale, the fade, and the debris orbit +
    /// tip-orb glow in <see cref="PulseVisual"/>). Nothing about the shape changed — the builder
    /// hands back the same pieces under the same names.</summary>
    private void BuildVisual(Color color)
    {
        Shader shader = Shader.Find("Sprites/Default") ?? Shader.Find("Unlit/Color");
        if (shader == null) return;

        // 1ir: cone and line are two builders, one shared tip orb.
        if (_coneHalfRad > 0f)
        {
            // 1is: the funnel's hot end is SpellLook.HotCore, not Edge â€” Edge desaturates Fire's
            // orange toward peach, so it can never supply the yellow this reads as.
            Color hot = SpellLook.HotCore(color);
            _funnelChunks = SpellBeamModelBuilder.BuildFunnelVisual(transform, Length, _mouthRadius, Width, color, hot);
            _baseColor = color;
        }
        else
        {
            SpellBeamModelBuilder.LineBody line = SpellBeamModelBuilder.BuildLineBody(
                transform, transform.position, Direction, Length, Width, color);
            _body = line.Body;
            _bodyBaseScale = line.BodyBaseScale;
            _baseColor = color;
        }

        // 1ir: the tip orb is shared by BOTH beams, so it is mounted here outside the branch rather
        // than inside the line builder. PulseVisual opens with `if (_endOrb == null) return;` and runs
        // for the cone too, so an orb that only the line got would leave the cone's tip inert.
        SpellBeamModelBuilder.TipOrb tip = SpellBeamModelBuilder.BuildTipOrb(
            transform, EndPoint(), Width, color);
        _endOrb = tip.Orb;
        _orbBaseScale = tip.BaseScale;

        CollectFadeMaterials();
    }

    /// <summary>
    /// 1ir: gather every material this beam owns so the one fade loop can drive all of them. Scans
    /// children instead of tracking each primitive by hand, because the cone's segment count is a
    /// constant the visual builder decides â€” a hand-kept list would be a second place to update when
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
}

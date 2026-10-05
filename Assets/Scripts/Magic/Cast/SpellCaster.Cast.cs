using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Partial: cast lifecycle, delivery dispatch, and ground/zone/storm/summon resolution (§3.8).
/// Split from SpellCaster.cs - see the main partial for focus/cooldown state and fields.
/// </summary>
public partial class SpellCaster
{
    private IEnumerator CastRoutine(SpellData spell, Transform origin, MagicWeaponMods mods, float charge, bool fast)
    {
        // Cast time (modulated by weapon CastTimeMod). A fast cast (wheel-cast magic) resolves
        // immediately — both charged casts (wind-up spent on the hold) and tap casts — so the burst
        // ring and the delivery land on the same frame. Only non-fast casts wait.
        if (!fast)
        {
            float castTime = spell.CastTime * Mathf.Max(mods.CastTimeMult, 0.05f);
            if (castTime > 0f && charge <= 0f)
            {
                float t = 0f;
                while (t < castTime)
                {
                    t += Time.deltaTime;
                    yield return null;
                }
            }
        }

        // Execute the spell.
        DamageResult result = Execute(spell, origin, mods, charge);
        OnCastComplete?.Invoke(spell, result);

        // Apply cooldown (modulated by weapon CooldownMod and tree cooldown-reduction perks §3.3).
        // Fast casts skip it — FP is the limiter.
        if (!fast)
            _cooldowns[spell.id] = spell.Cooldown * Mathf.Max(mods.CooldownMult, 0.05f)
                * (Stats != null ? Stats.CooldownReductionMult : 1f);
        _activeCasts = Mathf.Max(0, _activeCasts - 1);
    }

    /// <summary>
    /// 1jm: the direction a <see cref="SpellDelivery.Projectile"/> leaves the caster — the player's LOOK
    /// direction, taken as a direction rather than as a point to converge on.
    /// <para><b>Why not the hand's forward.</b> The cast origin is the magic hand, which hangs off the
    /// weapon rig on the body, and the body's rotation is yaw only (<c>PlayerController.HandleMouseLook</c>
    /// writes <c>Euler(0f, _yaw, 0f)</c> and keeps pitch on the camera pivot). <c>origin.forward</c> is
    /// therefore a flat horizontal shot that cannot aim up or down.</para>
    /// <para><b>Why not a point in front of the camera.</b> That was the old aim: shoot from the hand
    /// toward <c>camera.position + camera.forward * Range</c>. The error in that is the vector from the
    /// hand to the CAMERA, scaled by 1/Range - so it grows with how far the camera sits from the hand.
    /// First person puts the camera at the pivot, near the hand, and so was already close to right;
    /// third person puts it 6.5 m back, which skews the shot by roughly atan(6.5 / Range) and is the
    /// reported "weird trajectory", and 1jl's 0.6 m shoulder offset adds a lateral term on top of it.
    /// Using the direction deletes the term instead of shrinking it, and in first person it changes the
    /// shot by the hand-to-pivot distance only.</para>
    /// <para>Taking a direction also makes the shot independent of where the camera <i>is</i>, which is
    /// what stops the 1jl shoulder offset from introducing a skew of its own.</para>
    /// <para><c>fallbackForward</c> is the caller's own forward, used only when there is no usable camera.
    /// </para>
    /// <para><b>Relation to <see cref="CurrentAimDirection"/>.</b> That is the point-based aim (beam,
    /// summon, ground deliveries) and this is the direction-based one (projectile). Two modes, not two
    /// spellings of one fact - so they are deliberately NOT folded together; the only overlap is the
    /// two-line "fallbackForward, else Vector3.forward" tail, which is defensive boilerplate and not a
    /// fact about the game that could drift. Stated here so the pair does not read as an oversight.
    /// </para>
    /// </summary>
    public static Vector3 StraightFlightDirection(Camera cam, Vector3 fallbackForward)
    {
        if (cam != null)
        {
            Vector3 look = cam.transform.forward;
            if (look.sqrMagnitude > 0.0001f)
                return look.normalized;
        }
        if (fallbackForward.sqrMagnitude > 0.0001f)
            return fallbackForward.normalized;
        return Vector3.forward;
    }

    private DamageResult Execute(SpellData spell, Transform origin, MagicWeaponMods mods, float charge)
    {
        float basePower = spell.BasePower * mods.DamageMult * (1f + charge * ChargeDamageBonus);
        float wisdom = Stats != null ? Stats.MagicAttackPower : 0f;
        float totalPower = basePower + wisdom * 1f;

        Vector3 pos = origin != null ? origin.position : transform.position;
        Vector3 fwd = origin != null ? origin.forward : transform.forward;

        Camera cam = Camera.main;
        if (spell.Delivery == SpellDelivery.Projectile)
        {
            // 1jm: fly straight along the look direction. The old aim below converged on a point
            // Range metres in front of the camera, which in third person (and again since 1jl moved the
            // camera sideways) is not where the player is standing — the flight line skews off to the
            // side. See StraightFlightDirection for why not the hand's forward either.
            fwd = StraightFlightDirection(cam, fwd);
        }
        else if (cam != null)
        {
            // Ground deliveries (Zone/Vortex/Summon/Storm) land where the camera actually points —
            // out to the practical GroundAimMax cap, not the spell's short Range — so AoE magic can
            // be placed anywhere in the open world (§3.8). Instant/beam casts keep their spell range so
            // their aim stays conventional; 1jm left both on the point-based aim deliberately and
            // reported the identical skew in them rather than changing aim the user did not ask about.
            bool groundDelivery = spell.Delivery == SpellDelivery.Zone || spell.Delivery == SpellDelivery.Vortex
                || spell.Delivery == SpellDelivery.Summon || spell.Delivery == SpellDelivery.Storm;
            float aimDist = groundDelivery ? GroundAimMax : Mathf.Max(spell.Range, 5f);
            Vector3 aim = cam.transform.position + cam.transform.forward * aimDist;
            Vector3 dir = aim - pos;
            if (dir.sqrMagnitude > 0.0001f)
                fwd = dir.normalized;
        }

        switch (spell.Delivery)
        {
            case SpellDelivery.Instant:
                return ResolveDirect(totalPower, spell, pos, fwd, spell.Range * mods.RangeMult);
            case SpellDelivery.Projectile:
                return FireProjectile(totalPower, spell, pos, fwd, charge, SizeScale(charge) * mods.RadiusMult);
            case SpellDelivery.Zone:
                return ResolveZone(totalPower, spell, pos, fwd, charge, SizeScale(charge) * mods.RadiusMult, GroundAimMax);
            case SpellDelivery.Vortex:
                return SpawnVortex(totalPower, spell, pos, fwd, charge, SizeScale(charge) * mods.RadiusMult, GroundAimMax);
            case SpellDelivery.Beam:
                return ResolveBeam(totalPower, spell, pos, fwd, charge, SizeScale(charge) * mods.RadiusMult);
            case SpellDelivery.Summon:
                return ResolveSummon(totalPower, spell, pos, fwd, charge, SizeScale(charge) * mods.RadiusMult, GroundAimMax);
            case SpellDelivery.Storm:
                return ResolveStorm(totalPower, spell, pos, fwd, charge, SizeScale(charge) * mods.RadiusMult, GroundAimMax);
            default:
                return new DamageResult();
        }
    }

    /// <summary>Size multiplier applied to deliveries by charge level.</summary>
    /// <para>1ir: made <c>public</c> so the aim preview in PlayerController.Combat READS this ladder
    /// instead of re-deriving it. 1iq declined to re-derive the flight ladder here for exactly this
    /// reason; a preview that computed its own copy would be a second spelling that rots the moment
    /// <see cref="ChargeSizeBonus"/> changes.</para>
    /// <para><b>An INSTANCE method, and it must stay one.</b> <see cref="ChargeSizeBonus"/> is a
    /// public <i>field</i> so it stays inspector-tunable; a <c>static</c> method cannot read an instance
    /// field (CS0120), and the first version of this was written static — with a comment claiming "it
    /// never read instance state", which the very next line contradicts. Every caller inside SpellCaster
    /// is already in an instance context, so this costs those callers nothing.</para>
    /// <para>Note there is an unrelated <c>SkillContext.SizeScale</c> (skill-LEVEL scaling, 2%/level).
    /// Same name, different ladder, different owner — do not merge them.</para></summary>
    public float SizeScale(float charge) => 1f + charge * ChargeSizeBonus;

    /// <summary>
    /// 1ir: duration multiplier for a caster-anchored summon. This is <b>deliberately the same ladder
    /// as <see cref="SizeScale"/></b> (user decision): charging a following familiar grows the area it
    /// covers AND how long it lives, because both are claims about "how much presence you conjured".
    /// It is one expression calling the other, NOT a copy of the formula — so if the two ever need to
    /// diverge, this single line is the only thing to change.
    /// <para>Only affects Summon; a Beam channel lasts as long as Focus holds out and has no Lifetime,
    /// and Zone/Storm scale their own ticks off TickInterval rather than this.</para></summary>
    public float DurationScale(float charge) => SizeScale(charge);

    /// <summary>
    /// 1is: the ONE place the "where am I aiming right now" question is answered. Camera forward,
    /// aimed from <paramref name="from"/> out to <paramref name="range"/>, falling back to
    /// <paramref name="fallbackForward"/> when there is no camera or the camera sits exactly on
    /// <paramref name="from"/>.
    /// <para>Extracted because two components now need it per frame and a second copy would rot:
    /// <see cref="SpellBeam"/> (re-aims the beam as the player turns) and <see cref="SpellSummon"/>
    /// (1is: places its circle behind the player and sprays along this line). When they disagreed,
    /// the familiar would visibly fire across the beam's own spray — a "the model is wrong" report
    /// that is actually two derivations of one fact (rule 8).</para>
    /// <para>Static because it must be callable from the summon, which is not a child of the caster.
    /// It reads NO instance state — the fallback and the camera are both passed in.</para>
    /// <para><paramref name="cam"/> is a CACHED reference, not a convenience: <c>Camera.main</c> is a
    /// tag lookup, and both callers run this every frame they are alive (a channeled beam, a living
    /// familiar), which is why <see cref="SpellBeam"/> has held a cached camera since 1e5. Pass null
    /// and it resolves <c>Camera.main</c> itself; pass your cache and it does not.</para></summary>
    public static Vector3 CurrentAimDirection(Vector3 from, float range, Vector3 fallbackForward,
        Camera cam = null)
    {
        if (cam == null) cam = Camera.main;
        if (cam != null)
        {
            Vector3 dir = (cam.transform.position + cam.transform.forward * range) - from;
            if (dir.sqrMagnitude > 0.0001f) return dir.normalized;
        }
        return fallbackForward.sqrMagnitude > 0.0001f ? fallbackForward.normalized : Vector3.forward;
    }

    /// <summary>Drop the forward aim onto the ground — shared ground-placement for zone/summon/storm.</summary>
    private static Vector3 GroundTarget(Vector3 pos, Vector3 fwd, float range)
    {
        Vector3 at = pos;
        if (Physics.Raycast(pos, fwd, out RaycastHit aimHit, Mathf.Max(range, 0.1f)))
            at = aimHit.point;
        else
            at = pos + fwd * Mathf.Max(range, 0f);
        if (Physics.Raycast(at + Vector3.up * 0.1f, Vector3.down, out RaycastHit groundHit, 30f))
            at = groundHit.point;
        return at;
    }

    /// <summary>
    /// Summon a persistent object at the goal point (SpellDelivery.Summon). Damage summons act as
    /// turrets firing at the nearest enemy; healing summons become a persistent heal aura.
    /// </summary>
    private DamageResult ResolveSummon(float power, SpellData spell, Vector3 pos, Vector3 fwd, float charge, float sizeScale, float range)
    {
        // 1ir: a caster-anchored summon belongs to the caster, so it is created ON them rather than
        // at the ground aim point — Spawn follows from there. GroundTarget would otherwise drop it
        // GroundAimMax metres away, and "follows the player" would visibly snap across the map on
        // the first frame.
        // 1is: and specifically BEHIND them, along the aim line they are about to spray down. The
        // offset is flattened, because this spell sprays forward (SpellSummon._sprayForward) and a
        // circle parked on the aim line would be standing in its own fire. SpellSummon.BackOffset
        // is applied again on every follow frame, so the two must not drift apart.
        Vector3 center;
        if (spell != null && spell.CasterAnchored)
        {
            Vector3 flat = new Vector3(fwd.x, 0f, fwd.z);
            Vector3 back = flat.sqrMagnitude > 0.0001f
                ? flat.normalized * -SpellSummon.BackOffset
                : Vector3.zero;
            center = pos + back;
        }
        else
        {
            center = GroundTarget(pos, fwd, range);
        }

        // Earth summons (the golem line) erupt a small rock field where the construct rises
        // (§3.8). Other schools carry no terrain shape and no-op in TerrainDeformer. A modest
        // radius so the bump reads as the construct breaking the surface, not a wide reshape.
        if (spell.TerrainShape != TerrainShape.None)
            TerrainDeformer.Apply(center, Mathf.Min(spell.Radius * 0.4f, 2.5f), spell.TerrainShape, fwd);

        var go = new GameObject("SpellSummon");
        go.transform.position = center;
        go.AddComponent<SpellSummon>().Initialize(this, spell, power, sizeScale, DurationScale(charge));
        return new DamageResult { HitTargets = true };
    }

    /// <summary>
    /// Summon a storm over the goal point (SpellDelivery.Storm): repeated element-styled strikes
    /// inside the radius for the spell's duration.
    /// </summary>
    private DamageResult ResolveStorm(float power, SpellData spell, Vector3 pos, Vector3 fwd, float charge, float sizeScale, float range)
    {
        Vector3 center = GroundTarget(pos, fwd, range);
        var go = new GameObject("SpellStorm");
        go.transform.position = center;
        go.AddComponent<SpellStorm>().Initialize(this, spell, power, sizeScale);
        return new DamageResult { HitTargets = true };
    }

    /// <summary>
    /// Spawn a tornado at the cast location (SpellDelivery.Vortex). The Great Tornado
    /// (magic_tornado) uses the old environmental tornado model + function — a tall drifting
    /// debris funnel (TornadoBehavior); other Vortex spells use a persistent
    /// <see cref="SpellZone"/>. Both tick the spell's damage over their lifetime and drag
    /// enemies toward the center — winds pulled in like the tornado. Positioned by raycasting
    /// along the cast direction up to <see cref="SpellData.Range"/>, then dropped to the
    /// ground so the funnel sits on terrain.
    /// </summary>
    private DamageResult SpawnVortex(float power, SpellData spell, Vector3 pos, Vector3 fwd, float charge, float sizeScale, float range)
    {
        Vector3 at = pos;
        if (Physics.Raycast(pos, fwd, out RaycastHit hit, Mathf.Max(range, 0.1f)))
            at = hit.point;
        else
            at = pos + fwd * Mathf.Max(range, 0f);

        Vector3 ground = at;
        if (Physics.Raycast(at + Vector3.up * 0.1f, Vector3.down, out RaycastHit groundHit, 30f))
            ground = groundHit.point;

        // The Great Tornado (magic_tornado) rebuilds the old environmental tornado — a tall
        // tapering funnel of debris blocks (MapBuilder.BuildTornado) that drifts and pulls
        // objects around via physics (TornadoBehavior) — scaled down to the spell radius,
        // instead of the small stationary SpellZone funnel used by other Vortex spells.
        if (spell != null && spell.id == GreatTornadoSpellId)
        {
            float radius = Mathf.Max(spell.Radius > 0f ? spell.Radius * sizeScale : 3f, 0.5f);
            float height = Mathf.Max(radius * 3.5f, 10f);
            float widthScale = Mathf.Max((radius * 2.2f) / 35.5f, 0.12f);

            var tornado = MapBuilder.BuildTornado(null, ground, height, widthScale);
            var spellTornado = tornado.AddComponent<SpellTornado>();
            spellTornado.Initialize(this, spell, power, sizeScale, 1f);
            spellTornado.Lifetime = Mathf.Max(spell.Duration > 0f ? spell.Duration : 5f, 1f);
            return new DamageResult { HitTargets = true };
        }

        var go = new GameObject("SpellVortex");
        go.transform.position = ground;
        var zone = go.AddComponent<SpellZone>();
        zone.Initialize(this, spell, power, sizeScale, 1f, 3.5f);
        zone.Lifetime = Mathf.Max(spell.Duration > 0f ? spell.Duration : 5f, 1f);

        return new DamageResult { HitTargets = true };
    }

    private DamageResult ResolveDirect(float power, SpellData spell, Vector3 pos, Vector3 fwd, float range)
    {
        if (spell.SelfBuff)
        {
            // Self-buff: grant the caster (the player) a timed effect — e.g. flight for the
            // Duration. No damage, no target raycast. Applied to whatever owns this caster.
            var pc = transform.root.GetComponent<PlayerController>();
            if (pc != null && spell.Duration > 0f)
            {
                pc.BeginFlight(spell.Duration);
                // 1ie: the caster's self-buff ring takes the per-spell look, so a flight buff reads
                // differently from every other ring in the game.
                var selfLook = SpellLook.Resolve(spell);
                SkillFx.RingFlash(transform.position, Vector3.up, selfLook.Core, 2.5f, 0.5f, selfLook.Scale);
            }
            return new DamageResult { HitTargets = true };
        }
        if (spell.Heals)
        {
            // Instant restoration casts on the caster (the classic "holy touch").
            int healed = ResolveHeal(spell, power, transform.root.gameObject);
            return new DamageResult { TotalDamage = healed, HitTargets = healed > 0 };
        }
        if (Physics.Raycast(pos, fwd, out RaycastHit hit, range))
        {
            return ApplyHit(spell, power, hit.collider.gameObject);
        }
        return new DamageResult();
    }

    private DamageResult ResolveZone(float power, SpellData spell, Vector3 pos, Vector3 fwd, float charge, float sizeScale, float range)
    {
        float radius = spell.Radius * sizeScale;

        // Aim at the ground the player is pointing at, skipping any already-raised terrain (a wall
        // this spell itself reared) so a repeat cast targets the intended ground, not the wall face.
        Vector3 center = TerrainDeformer.ResolveGroundTarget(pos, fwd, Mathf.Max(range, 0.1f));

        // Sky spells (summonFallingRock: Meteor / Asteroid / Earth Meteor) summon a big rock that
        // drops from high above; the burst resolves in ResolveZoneImpact only when the rock lands,
        // so the cast reads as "a meteor fell here" rather than an instant ground flash. The rock
        // itself is pure visual (no collider) — damage/knockback/deform still go through the normal
        // pipeline, on impact, so this never touches the terrain root (1cx). Which rock it is comes
        // from the spell's own resolved look (1f7), so Asteroid drops a swarm and Meteor a boulder.
        if (spell != null && spell.SummonFallingRock)
        {
            float rockScale = Mathf.Max(radius, 1.5f);
            var skyLook = SpellLook.Resolve(spell);
            SkillFx.FallRock(center, rockScale, skyLook.Core, skyLook.SkyRock,
                () =>
                {
                    if (this == null) return;
                    ResolveZoneImpact(spell, power, center, fwd, radius, sizeScale);
                });
            return new DamageResult { HitTargets = true };
        }

        return ResolveZoneImpact(spell, power, center, fwd, radius, sizeScale);
    }

    /// <summary>The zone burst: reshape terrain, then either keep a persistent zone alive or
    /// resolve the instant overlap blast. Called synchronously for normal Zone spells and from the
    /// falling rock's landing callback for sky spells (delayed by the rock's drop).</summary>
    private DamageResult ResolveZoneImpact(SpellData spell, float power, Vector3 center, Vector3 fwd, float radius, float sizeScale)
    {
        // Earth spells reshape the ground at the impact point before damage resolves (§3.8).
        // `fwd` defines the cast axis; the Wall ridge rears ACROSS it (1ga) — a left-right
        // barricade facing the caster — the other terrain shapes are radial.
        //
        // WIDTH is bounded for EVERY shape, DEPTH is not (1cv, 1cw). The deform shape's width is
        // its local delivery dish — the same small bowl a shovel makes — never the full blast
        // splash, and NEVER scaled by the (unbounded) hold-to-overcharge sizeScale. Feeding the
        // blast radius (spell.Radius × charge sizeScale, up to ~13+ tiles) straight into the
        // deformer made a single cast carve/rear every corner of a 30-tile chunk at once, reading
        // as "the whole chunk / whole terrain moving" — for Crater (Meteor, 1cv) AND for the
        // raised shapes (Ring/Spikes/Wall/Pillar — Tremor, Spire Field, Earth Wall, Landslide).
        // Width is therefore capped to a small local dish from the spell's own delivery Radius
        // (spell.Radius, the `deliveryRadius:` catalog arg); the Crater's per-cast DEPTH ratchet
        // (current − s·CraterStep in WorldStreamer.DeformAt) stays deliberately UNBOUNDED — each
        // cast digs a fresh CraterStep deeper with no floor, no cap, no limit. Player wants the
        // game to have no limit.
        float dish = Mathf.Max(spell.Radius > 0f ? spell.Radius : 1.6f, 0.5f);
        float deformRadius = spell.TerrainShape == TerrainShape.Crater ? dish * 0.5f : dish;
        TerrainDeformer.Apply(center, deformRadius, spell.TerrainShape, fwd);

        // Duration > 0 keeps the zone alive: it ticks the spell's damage while it lasts.
        if (spell.Duration > 0f)
        {
            SpawnZoneRing(center, spell, radius);
            var go = new GameObject("SpellZone");
            go.transform.position = center;
            var zone = go.AddComponent<SpellZone>();
            zone.Initialize(this, spell, power, sizeScale, 0.4f, 0f);
            zone.Lifetime = Mathf.Max(spell.Duration, 0.5f);
            return new DamageResult { HitTargets = true };
        }

        SpawnZoneRing(center, spell, radius);

        int count = Physics.OverlapSphereNonAlloc(center, radius, _overlapBuffer);
        bool hitAny = false;
        float total = 0f;
        for (int i = 0; i < count; i++)
        {
            var col = _overlapBuffer[i];
            if (col == null) continue;
            if (col.transform.root == transform.root)
            {
                // Holy bursts heal the caster too when standing inside the light.
                if (spell.Heals)
                {
                    int healed = ResolveHeal(spell, power, col.gameObject);
                    total += healed;
                    hitAny |= healed > 0;
                }
                continue;
            }
            if (spell.Heals && TryFindHealable(col.gameObject, out _))
            {
                int healed = ResolveHeal(spell, power, col.gameObject);
                total += healed;
                hitAny |= healed > 0;
                continue;
            }
            var hit = ApplyHit(spell, power, col.gameObject);
            total += hit.TotalDamage;
            hitAny |= hit.HitTargets;
        }
        return new DamageResult { TotalDamage = total, HitTargets = hitAny };
    }

    /// <summary>Ground ring flash sized to the spell's radius so zone spells read on screen.</summary>
    private static void SpawnZoneRing(Vector3 pos, SpellData spell, float radius)
    {
        if (spell == null || radius <= 0f) return;
        Vector3 ground = pos;
        if (Physics.Raycast(pos + Vector3.up * 0.1f, Vector3.down, out RaycastHit hit, 4f))
            ground = hit.point;
        // 1ie: per-spell Core colour + scale. This is the shared zone-spawn ring used by
        // ResolveZone and the ground-target path, so it is the highest-leverage of the RingFlash
        // seams (77 zone spells reach it).
        var look = SpellLook.Resolve(spell);
        SkillFx.RingFlash(ground, Vector3.up, look.Core, radius, 0.5f, look.Scale);
    }
}
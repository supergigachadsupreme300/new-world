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

    private DamageResult Execute(SpellData spell, Transform origin, MagicWeaponMods mods, float charge)
    {
        float basePower = spell.BasePower * mods.DamageMult * (1f + charge * ChargeDamageBonus);
        float wisdom = Stats != null ? Stats.MagicAttackPower : 0f;
        float totalPower = basePower + wisdom * 1f;

        Vector3 pos = origin != null ? origin.position : transform.position;
        Vector3 fwd = origin != null ? origin.forward : transform.forward;

        Camera cam = Camera.main;
        if (cam != null)
        {
            // Ground deliveries (Zone/Vortex/Summon/Storm) land where the camera actually points —
            // out to the practical GroundAimMax cap, not the spell's short Range — so AoE magic can
            // be placed anywhere in the open world (§3.8). Projectile/instant/beam casts keep their
            // spell range so their aim stays conventional.
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
    private float SizeScale(float charge) => 1f + charge * ChargeSizeBonus;

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
        Vector3 center = GroundTarget(pos, fwd, range);

        // Earth summons (the golem line) erupt a small rock field where the construct rises
        // (§3.8). Other schools carry no terrain shape and no-op in TerrainDeformer. A modest
        // radius so the bump reads as the construct breaking the surface, not a wide reshape.
        if (spell.TerrainShape != TerrainShape.None)
            TerrainDeformer.Apply(center, Mathf.Min(spell.Radius * 0.4f, 2.5f), spell.TerrainShape, fwd);

        var go = new GameObject("SpellSummon");
        go.transform.position = center;
        go.AddComponent<SpellSummon>().Initialize(this, spell, power, sizeScale);
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
        // pipeline, on impact, so this never touches the terrain root (1cx).
        if (spell != null && spell.SummonFallingRock)
        {
            float rockScale = Mathf.Max(radius, 1.5f);
            SkillFx.FallRock(center, rockScale, DamageNumber.ColorFor(spell.Type),
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
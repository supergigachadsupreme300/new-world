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
    /// reported "weird trajectory". The 1jl shoulder offset is orthogonal to all of this: it moves where
    /// the camera IS, and this is a direction, so it can no longer bend the shot. Using the direction
    /// deletes the term instead of shrinking it.</para>
    /// <para>Taking a direction also makes the shot independent of where the camera <i>is</i>, which is
    /// what stops the 1jl shoulder offset from introducing a skew of its own.</para>
    /// <para><b>Why <paramref name="lookPivot"/> and not the camera.</b> 1jo: they hold the SAME direction
    /// (first person copies the pivot's rotation outright; third person looks from a point offset along
    /// <c>-pivot.forward</c> back to a target offset along <c>pivot.right</c>, and the lateral terms cancel,
    /// so the view direction is the pivot's). But they are written at DIFFERENT TIMES: the pivot is set by
    /// <c>HandleMouseLook</c> inside <c>Update</c>, while <c>CameraModeSwitch</c> writes the camera
    /// transform in <c>LateUpdate</c>. Anything reading the camera during <c>Update</c> - which is what the
    /// aim preview does - therefore gets LAST frame's camera, so the ray trailed the crosshair by a frame
    /// whenever the camera moved. The pivot is the look SOURCE, current-frame, so reading it removes the
    /// lag without changing the direction. The camera is kept only as a fallback, for a caster that has a
    /// camera but no player pivot.</para>
    /// <para><c>fallbackForward</c> is the caller's own forward, used only when neither source is usable.
    /// </para>
    /// <para><b>Relation to <see cref="CurrentAimDirection"/>.</b> That is the point-based aim (beam,
    /// summon, ground deliveries) and this is the direction-based one (projectile). Two modes, not two
    /// spellings of one fact - so they are deliberately NOT folded together; the only overlap is the
    /// two-line "fallbackForward, else Vector3.forward" tail, which is defensive boilerplate and not a
    /// fact about the game that could drift. Stated here so the pair does not read as an oversight.
    /// </para>
    /// </summary>
    public static Vector3 StraightFlightDirection(Camera cam, Transform lookPivot, Vector3 fallbackForward)
    {
        if (lookPivot != null)
        {
            Vector3 look = lookPivot.forward;
            if (look.sqrMagnitude > 0.0001f)
                return look.normalized;
        }
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
            // 1jo: the pivot is passed so the aim is the CURRENT frame's look (see the helper).
            var owner = transform.root.GetComponent<PlayerController>();
            fwd = StraightFlightDirection(cam, owner != null ? owner.PlayerCameraPivot : null, fwd);
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
            case SpellDelivery.Chain:
                return ResolveChain(totalPower, spell, pos, fwd, spell.Range * mods.RangeMult);
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
    /// <para>Extracted so "which way is the player aiming" has ONE answer: <see cref="SpellBeam"/>
    /// re-aims the beam as the player turns, and a second copy would rot. <see cref="SpellSummon"/>
    /// was the second caller from 1is until <b>1l6q</b>, when the familiar's spray switched to the
    /// DIRECTION-based <see cref="StraightFlightDirection"/> — the point-based aim here converges on
    /// the reticle from an offset muzzle, which the user reported as "launching at the cursor", so the
    /// spray now flies parallel to the look like a normal projectile.</para>
    /// <para>Static because it reads NO instance state — the fallback and the camera are both passed
    /// in — so its caller (the beam, which is not a child of the caster) needs no caster reference.</para>
    /// <para><paramref name="cam"/> is a CACHED reference, not a convenience: <c>Camera.main</c> is a
    /// tag lookup, and its caller runs this every frame it is alive (a channeled beam), which is why
    /// <see cref="SpellBeam"/> has held a cached camera since 1e5. Pass null
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

    /// <summary>1jt: how high above the probe point the downward ray starts. Must clear the CASTER's own
    /// capsule — a ray started at a standing player's chest hits them before the terrain and parks the
    /// result at chest height. 1ir's value, kept exactly.</summary>
    public const float GroundProbeUp = 4f;

    /// <summary>1jt: how far below the probe point the downward ray reaches. 1ir's value.</summary>
    public const float GroundProbeDown = 40f;

    /// <summary>1jt: how far above the hit the caller is placed, so a flat ground ring is not z-fought
    /// into the terrain it is lying on.</summary>
    public const float GroundLift = 0.02f;

    /// <summary>1l74: how far above the impact center a SkyStrikeBolt delivery's bolt falls FROM. The
    /// only bound is "reads as sky": the bolt is a picture (segments only, no collider), so this is
    /// visual height, never a projectile runway.</summary>
    public const float SkyStrikeHeight = 30f;

    /// <summary>1l76: how far a chain bolt leaps between victims (Chain Lightning). Named and public
    /// so game-design.md and any future reader share ONE number rather than re-deriving a subtree.</summary>
    public const float ChainJumpRadius = 5f;

    /// <summary>1l76: how many leaps after the primary victim a chain makes — 2 means three victims
    /// (primary + two jumps).</summary>
    public const int ChainMaxJumps = 2;

    /// <summary>1l76: the chain bolt's drawn shape/lifetime, shared by every jump in the arc.</summary>
    public const int ChainBoltSegments = 12;
    public const float ChainBoltWidth = 0.18f;
    public const float ChainBoltJitter = 0.55f;
    public const float ChainBoltLife = 0.4f;

    /// <summary>1l78: Leap Bolt's drawn shape — the bolt strung between where the caster was and where
    /// they land — plus how far short of an obstruction the leap stops (so it never lands inside a
    /// wall). Named/public for the docs and the QA bench, like the Chain pair above.</summary>
    public const int LeapBoltSegments = 12;
    public const float LeapBoltWidth = 0.2f;
    public const float LeapBoltJitter = 0.6f;
    public const float LeapBoltLife = 0.5f;
    public const float LeapBoltWallClearance = 1f;

    /// <summary>1jt: snap a world XZ down onto the ground beneath it, skipping <paramref name="skipRoot"/>
    /// and its whole hierarchy. THE ONE downward ground probe for cast placement.
    /// <para><b>Why it lives here and not in each caller.</b> <see cref="SpellSummon.FollowCaster"/>
    /// already had exactly this — same constants, same "skip the caster", same nearest-hit rule — for
    /// the familiar that rides behind the player. 1jt needs the identical behaviour for the casting
    /// halo, in front of and under the same player. Two private copies of a raycast is rule 8's
    /// "second spelling that rots": change one clearance and the halo floats while the familiar is
    /// fine, or the reverse, and neither report names the other.</para>
    /// <para><b>Takes the NEAREST hit, not the first.</b> <c>RaycastNonAlloc</c> returns hits in an
    /// unspecified order, so walking past a building would otherwise park the halo on its roof some
    /// frames and on the street others. A downward ray's nearest hit is the lowest <c>point.y</c>.
    /// (1ir documented this for the summon; the reasoning is the reason, so it travels with it.)</para>
    /// <para><b>No ground found keeps <paramref name="p"/>'s own Y</b> rather than snapping to the
    /// last frame's ground or to 0 — mid-air and over-a-gap must not teleport the halo to sea level.</para>
    /// <para>The <paramref name="buffer"/> is the caller's so this stays allocation-free per frame;
    /// it must be non-empty. Each caller keeps its own because the buffer is mutable shared state and
    /// two owners of one buffer is a reentrancy bug waiting for the first nested call.</para></summary>
    public static Vector3 GroundUnder(Vector3 p, Transform skipRoot, RaycastHit[] buffer)
    {
        if (buffer == null || buffer.Length == 0) return p;
        int n = Physics.RaycastNonAlloc(p + Vector3.up * GroundProbeUp, Vector3.down,
            buffer, GroundProbeDown);
        float best = float.PositiveInfinity;
        for (int i = 0; i < n && i < buffer.Length; i++)
        {
            RaycastHit h = buffer[i];
            if (h.collider == null) continue;
            if (skipRoot != null && h.collider.transform.root == skipRoot) continue;
            if (h.point.y >= best) continue;
            best = h.point.y;
        }
        if (!float.IsPositiveInfinity(best)) p.y = best + GroundLift;
        return p;
    }

    /// <summary>Drop the forward aim onto the ground — shared ground-placement for zone/summon/storm.</summary>
    private static Vector3 GroundTarget(Vector3 pos, Vector3 fwd, float range)    {
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
        // 1ir: a caster-anchored summon belongs to the caster, so it is created ON them rather than at
        // the ground aim point; GroundTarget would otherwise drop it GroundAimMax metres away and
        // "follows the player" would visibly snap across the map on the first frame.
        // 1l6r: its actual station — BEHIND the caster, on the ground, on the body's yaw-only bearing —
        // is set by SpellSummon.FollowCaster, run once at birth inside Initialize and again every follow
        // frame, so this object has exactly ONE placement and cannot disagree with itself. 1jx's "spawn
        // and follow agree by construction" is now literally true (the same function), whereas before
        // the two call sites both applied BackOffset from DIFFERENT points — the cast origin here vs the
        // caster root in FollowCaster — so they did NOT agree, and the familiar was born wherever the
        // magic hand happened to be (the reported "sometimes not created behind the player"). Seeding on
        // the root here is only so the never-rendered instant between creation and Initialize is sane.
        Vector3 center;
        if (spell != null && spell.CasterAnchored)
        {
            Transform root = transform.root;
            center = root != null ? root.position : pos;
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
        if (spell.SelfTeleport)
            return ResolveTeleport(power, spell, fwd, range);
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

    /// <summary>1l78: Leap Bolt (testing.md #9) — a self-movement cast. The player is teleported up to
    /// <paramref name="range"/> along the flat look (stopping <see cref="LeapBoltWallClearance"/> short
    /// of an obstruction) and the ground under that point is found with the shared
    /// <see cref="GroundUnder"/> probe, so the leap never lands inside geometry. A jagged bolt connects
    /// where the player WAS to where they NOW are, and everything within the spell's radius at the
    /// landing takes the hit. This is the ONE delivery that moves the caster; every other branch is a
    /// projection away from them.</summary>
    private DamageResult ResolveTeleport(float power, SpellData spell, Vector3 fwd, float range)
    {
        Transform root = transform.root;
        Vector3 from = root.position;

        // Flatten the aim so a leap never teleports straight up/down; fall back to the body's own
        // forward when the look is near-vertical (a caster staring at their feet should still hop).
        Vector3 flat = new Vector3(fwd.x, 0f, fwd.z);
        if (flat.sqrMagnitude < 0.0001f) flat = new Vector3(root.forward.x, 0f, root.forward.z);
        if (flat.sqrMagnitude < 0.0001f) flat = Vector3.forward;
        flat.Normalize();

        float reach = Mathf.Max(range, 1f);
        Vector3 dest = from + flat * reach;
        // Stop short of a wall so the landing is never inside it; the ground probe below then drops
        // the (now clear) point onto real ground.
        if (Physics.Raycast(from + Vector3.up * 1f, flat, out RaycastHit wall, reach))
            dest = wall.point - flat * LeapBoltWallClearance;
        dest = GroundUnder(dest, root, _groundProbeBuffer);

        var pc = root.GetComponent<PlayerController>();
        if (pc != null) pc.TeleportTo(dest);

        // Lightning connects where the caster WAS to where they NOW are (testing.md #9).
        var look = SpellLook.Resolve(spell);
        Material mat = SkillFx.SharedSpriteMaterial(look.Core);
        SpellBoltModelBuilder.BuildBolt(null, from, dest, mat, LeapBoltLife,
            LeapBoltSegments, LeapBoltWidth, LeapBoltJitter);

        // Landing zap: everything within the spell radius at the destination takes the hit.
        var total = new DamageResult { HitTargets = true };
        float radius = Mathf.Max(spell.Radius, 0.1f);
        int count = Physics.OverlapSphereNonAlloc(dest, radius, _overlapBuffer);
        for (int i = 0; i < count; i++)
        {
            var col = _overlapBuffer[i];
            GameObject victim = col != null ? CombatVictim(col.gameObject) : null;
            if (victim == null) continue;
            var r = ApplyHit(spell, power, victim);
            total.TotalDamage += r.TotalDamage;
            total.HitTargets |= r.HitTargets;
        }
        SkillFx.RingFlash(dest, Vector3.up, look.Core, radius, 0.4f, look.Scale);
        return total;
    }

    /// <summary>1l76: Chain Lightning (testing.md #7). One instant strike on the aimed (or nearest)
    /// combatant, then up to <see cref="ChainMaxJumps"/> leaps outward to the nearest un-hit
    /// combatant within <see cref="ChainJumpRadius"/> of the last victim. Every victim takes FULL
    /// power (the chain is about reach, not a damage ladder), and each leg is drawn as a jagged bolt
    /// (<see cref="SpellBoltModelBuilder"/>) — the first from the caster to the primary, each leap
    /// from the previous victim to the next, so the arc reads as one continuous stroke.</summary>
    private DamageResult ResolveChain(float power, SpellData spell, Vector3 pos, Vector3 fwd, float range)
    {
        GameObject victim = FindChainTarget(pos, fwd, range, out Vector3 point);
        if (victim == null) return new DamageResult();

        var total = ApplyHit(spell, power, victim);
        var look = SpellLook.Resolve(spell);
        Material mat = SkillFx.SharedSpriteMaterial(look.Core);
        SpellBoltModelBuilder.BuildBolt(null, pos, point, mat, ChainBoltLife,
            ChainBoltSegments, ChainBoltWidth, ChainBoltJitter);

        var visited = new HashSet<Transform> { victim.transform };
        Vector3 from = point;
        for (int hop = 0; hop < ChainMaxJumps; hop++)
        {
            victim = NearestCombatVictim(from, ChainJumpRadius, visited, out point);
            if (victim == null) break;

            var step = ApplyHit(spell, power, victim);
            total.TotalDamage += step.TotalDamage;
            total.HitTargets |= step.HitTargets;

            SpellBoltModelBuilder.BuildBolt(null, from, point, mat, ChainBoltLife,
                ChainBoltSegments, ChainBoltWidth, ChainBoltJitter);
            visited.Add(victim.transform);
            from = point;
        }
        return total;
    }

    /// <summary>1l76: the chain's first victim — whatever the aim ray hits, if it is a combatant,
    /// else the nearest combatant within reach (so a chain aimed into a crowd still starts on
    /// someone instead of fizzling when the reticle is between two foes).</summary>
    private GameObject FindChainTarget(Vector3 pos, Vector3 fwd, float range, out Vector3 point)
    {
        point = Vector3.zero;
        if (Physics.Raycast(pos, fwd, out RaycastHit hit, Mathf.Max(range, 0.1f)))
        {
            GameObject victim = CombatVictim(hit.collider.gameObject);
            if (victim != null)
            {
                point = victim.transform.position;
                return victim;
            }
        }
        return NearestCombatVictim(pos, Mathf.Max(range, 0.1f), null, out point);
    }

    /// <summary>1l76: nearest un-visited combatant within <paramref name="radius"/> of
    /// <paramref name="center"/>. <paramref name="visited"/> holds the roots already struck this
    /// cast, so a chain cannot ping-pong between two foes forever.</summary>
    private GameObject NearestCombatVictim(Vector3 center, float radius, HashSet<Transform> visited, out Vector3 point)
    {
        point = Vector3.zero;
        GameObject best = null;
        float bestSq = float.MaxValue;
        int count = Physics.OverlapSphereNonAlloc(center, radius, _overlapBuffer);
        for (int i = 0; i < count; i++)
        {
            var col = _overlapBuffer[i];
            GameObject victim = col != null ? CombatVictim(col.gameObject) : null;
            if (victim == null) continue;
            if (visited != null && visited.Contains(victim.transform)) continue;
            float sq = (victim.transform.position - center).sqrMagnitude;
            if (sq < bestSq)
            {
                bestSq = sq;
                best = victim;
            }
        }
        if (best != null) point = best.transform.position;
        return best;
    }

    /// <summary>1l76/1l78: the combatant a delivery collider resolves to — the object itself if
    /// damageable, else its root — or null for the caster, allies, and non-combatants. Used by both
    /// Chain Lightning's leaps and Leap Bolt's landing zap, so there is ONE friendly-skip predicate.
    /// Mirrors the Beam's skip (Player/Companion tags) and reuses <see cref="IDamageable"/> as the
    /// same "real combatant" test the other deliveries use.</summary>
    private GameObject CombatVictim(GameObject go)
    {
        if (go == null) return null;
        GameObject victim;
        if (go.TryGetComponent<IDamageable>(out _))
            victim = go;
        else if (go.transform.root != null && go.transform.root.TryGetComponent<IDamageable>(out _))
            victim = go.transform.root.gameObject;
        else
            return null;

        if (victim.transform == transform.root) return null;
        if (victim.CompareTag("Player") || victim.CompareTag("Companion")) return null;
        return victim;
    }

    private DamageResult ResolveZone(float power, SpellData spell, Vector3 pos, Vector3 fwd, float charge, float sizeScale, float range)
    {
        float radius = spell.Radius * sizeScale;

        // Aim at the ground the player is pointing at, skipping any already-raised terrain (a wall
        // this spell itself reared) so a repeat cast targets the intended ground, not the wall face.
        Vector3 center = TerrainDeformer.ResolveGroundTarget(pos, fwd, Mathf.Max(range, 0.1f));

        // 1l79: a CenterOnCaster zone (Static Nova) bursts from the CASTER, not the aim point — it is
        // a ring of static that erupts around the player. Snap the center onto the ground under the
        // caster with the shared probe (skipping the player's own colliders), so the nova sits on the
        // floor they stand on regardless of where the camera points.
        if (spell != null && spell.CenterOnCaster)
            center = GroundUnder(transform.root.position, transform.root, _groundProbeBuffer);

        // 1l74: a SkyStrikeBolt delivery (Lightning Strike) is an instant zone blast announced by a
        // single tall jagged bolt from the sky down onto the impact center — the bolt IS the cast,
        // so it renders before the impact spawns its ring and ships with the same resolved tint
        // (the sky-height is a picture, not a runway: nothing collides with the bolt).
        if (spell != null && spell.SkyStrikeBolt)
        {
            var strikeLook = SpellLook.Resolve(spell);
            SpellBoltModelBuilder.BuildBolt(null, center + Vector3.up * SkyStrikeHeight, center,
                SkillFx.SharedSpriteMaterial(strikeLook.Core), 0.5f, segments: 12,
                width: 0.2f, jitterAmplitude: 0.6f);
        }

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
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Partial: projectile SPAWNING only (§3.8). Split from SpellCaster.cs - see the main partial for
/// fields and focus/cooldown state.
///
/// 1jb: the per-shape projectile body VISUALS used to live here too, so this file's name described
/// two unrelated concerns — who fires a projectile and what one looks like. The geometry is now
/// Models/Magic/MagicProjectileModelBuilder.cs. What stayed is the behaviour:
/// <c>FireProjectile</c> (instantiate, attach a visual, launch) and <c>DecorateProjectile</c> (the
/// public door SpellSummon's turret bolt comes through).
/// </summary>
public partial class SpellCaster
{
    private DamageResult FireProjectile(float power, SpellData spell, Vector3 pos, Vector3 fwd, float charge, float sizeScale)
    {
        // Earth projectiles (TerrainShape.Crater) carve their crater where the shard STRIKES —
        // the impact point, not the caster's own footing. The carve lives in
        // SpellEffect.ResolveProjectileImpact (down-probed onto the ground there), so casting
        // never dents the ground under the player.

        // Spawn on the aim line only (no vertical lift) so the trajectory passes through the
        // casting circle's center (the circle is anchored on the same origin as this cast).
        // The small forward muzzle offset mirrors the ranged Muzzle and keeps the bolt clear of
        // the caster's own collider; the SpellEffect's caster-root skip and ground probe handle
        // self/terrain contacts from there.
        pos += fwd * 0.5f;
        GameObject go;
        if (spell.CastEffectPrefab != null)
        {
            go = Instantiate(spell.CastEffectPrefab, pos, Quaternion.LookRotation(fwd));
            if (go.GetComponent<SpellEffect>() == null)
            {
                var fx = go.AddComponent<SpellEffect>();
                fx.Initialize(spell, power, fwd, this, sizeScale);
            }
        }
        else
        {
            go = new GameObject("SpellProjectile");
            go.transform.position = pos;
            go.transform.rotation = Quaternion.LookRotation(fwd);
            // 1ig: the body's shape, colour and size all come from the spell's resolved look, not
            // from the school. A cast with an authored CastEffectPrefab above still bypasses this
            // entirely, so the prefab hook keeps top precedence.
            MagicProjectileModelBuilder.AttachDefaultProjectileVisual(go, SpellLook.Resolve(spell), spell.SummonFallingRock);
            go.AddComponent<SpellEffect>().Initialize(spell, power, fwd, this, sizeScale);
        }

        // Charge scales the whole projectile body (authored prefab or generated visual).
        if (charge > 0f)
            go.transform.localScale *= sizeScale;

        if (go.TryGetComponent<SpellEffect>(out var proj))
            proj.Launch(spell.ProjectileSpeed);

        return new DamageResult();
    }

    /// <summary>1ig: the summoning spell's real identity, for a summoned turret's bolt.</summary>
    /// <para>(1ig: the sibling <c>DecorateProjectile(GameObject, DamageType, ProjectileShape)</c> was
    /// deleted. It had no callers, and the two overloads sat adjacent enough that a future call site
    /// could pick either — silently getting a school stand-in instead of the summoning spell's own
    /// identity, which is the exact confusion 1ib exists to remove. One name, one identity.)</para>
    public void DecorateProjectile(GameObject go, SpellData spell)
    {
        MagicProjectileModelBuilder.AttachDefaultProjectileVisual(go, SpellLook.Resolve(spell));
    }
}
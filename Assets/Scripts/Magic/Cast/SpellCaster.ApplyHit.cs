using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Partial: apply-hit/damage resolution, healing, status effects and knockback (§3.7, §3.8).
/// Split from SpellCaster.cs - see the main partial for fields and focus/cooldown state.
/// </summary>
public partial class SpellCaster
{
    private DamageResult ApplyHit(SpellData spell, float power, GameObject target)
    {
        // Friendly-heal delivery: restore health on allies instead of damaging them.
        if (spell.Heals && TryFindHealable(target, out var healable))
        {
            int healed = Mathf.Max(1, Mathf.RoundToInt(power));
            healable.Heal(healed);
            DamageNumber.Spawn(target.transform.position, healed, spell.Type);
            return new DamageResult { TotalDamage = healed, HitTargets = true };
        }

        // Non-damageable colliders (terrain chunk MeshColliders, props, FX) must never be treated
        // as spell targets: every streamed terrain chunk shares the "Terrain" root, and the old
        // path ran ApplyKnockback on ANY collider caught by the default-mask overlap sphere, so a
        // single zone cast teleported the whole world's root by Knockback per overlapping chunk
        // (1cx). Only real combatants (EnemyController / BossController / SummonedAlly) act as
        // victims — same predicate the other delivery paths (Zone/Storm/Tornado/Beam) already use.
        if (!target.TryGetComponent<IDamageable>(out _))
        {
            Transform root = target.transform.root;
            if (root == null || !root.TryGetComponent<IDamageable>(out _))
                return new DamageResult();
        }

        var ctx = new DamageCalculator.HitContext
        {
            AttackPower = power,
            SkillMultiplier = 1f,
            Defense = 5f,
            DefenseMultiplier = 1f,
            Type = spell.Type,
            Resistance = NeutralResistance.Instance,
            WeaknessMultiplier = 1f,
            CriticalMultiplier = 1f,
        };

        // Wet conduction (§3.7): a soaked foe takes bonus Ice/Lightning spell damage.
        // Melee/ranged attacks ignore the amp — water combos with frost/shock magic only.
        if ((spell.Type == DamageType.Ice || spell.Type == DamageType.Lightning)
            && WetStatus.IsWet(target))
        {
            ctx.WeaknessMultiplier = WetStatus.IceLightningDamageBonus;
        }

        var result = DamageCalculator.Calculate(ctx, false);

        if (target.TryGetComponent<IDamageable>(out var damageable))
            damageable.TakeDamage(Mathf.RoundToInt(result.TotalDamage));
        if (result.TotalDamage > 0f)
            DamageNumber.Spawn(target.transform.position, result.TotalDamage, spell.Type);

        ApplyStatus(spell, power, target);
        ApplyKnockback(spell, target);

        if (spell.ImpactEffectPrefab != null)
            ObjectPooler.SpawnTransient(spell.ImpactEffectPrefab, target.transform.position,
                Quaternion.identity, 3f);

        return new DamageResult
        {
            TotalDamage = result.TotalDamage,
            HitTargets = true
        };
    }

    /// <summary>
    /// Resolve a spell's damage against a specific target (used by SpellEffect / SpellZone on
    /// projectile/zone impact). Accessible so effects can route back through the shared
    /// DamageCalculator pipeline.
    /// </summary>
    public void ResolveHitAt(GameObject target, SpellData spell, float power)
    {
        if (target == null || spell == null) return;
        ApplyHit(spell, power, target);
    }

    /// <summary>Restore health to a friendly target via an <see cref="IHealable"/> (used by
    /// SpawnZone/SelfHeal). Returns the amount healed, or 0 when the target cannot heal.</summary>
    public int ResolveHeal(SpellData spell, float power, GameObject target)
    {
        if (spell == null || target == null) return 0;
        if (!TryFindHealable(target, out var healable)) return 0;
        int healed = Mathf.Max(1, Mathf.RoundToInt(power));
        healable.Heal(healed);
        DamageNumber.Spawn(target.transform.position, healed, spell.Type);
        return healed;
    }

    /// <summary>Friendly target check for healing: the object or its root implements <see cref="IHealable"/>.</summary>
    private static bool TryFindHealable(GameObject target, out IHealable healable)
    {
        healable = null;
        if (target == null) return false;
        if (target.TryGetComponent<IHealable>(out healable)) return true;
        return target.transform.root != null && target.transform.root.TryGetComponent<IHealable>(out healable);
    }

    /// <summary>Apply a spell's status effect on a damage hit. DoT statuses attach a SpellDoT to
    /// the target's root; Chill/Frost slow, Stagger stuns and Blind cloaks the victim in fog via
    /// the enemy controller / <see cref="BlindStatus"/>; Wet soaks the target (WetStatus) so
    /// Ice/Lightning follow-ups hit harder. When the skill declares no explicit status, the
    /// element's signature status (§3.7) is applied automatically.</summary>
    private static void ApplyStatus(SpellData spell, float power, GameObject target)
    {
        if (spell == null) return;

        // Explicit per-skill status wins; otherwise the element's signature status is applied to
        // every magic attack automatically. Wind/Earth/Holy/Physical have no signature status.
        StatusEffectType effect;
        if (spell.AppliesStatus)
        {
            effect = spell.StatusEffect;
        }
        else
        {
            var signature = ElementSignatureStatus.For(spell.Type);
            if (!signature.HasValue) return;
            effect = signature.Value;
        }

        if (spell.StatusProcChance > 0f && UnityEngine.Random.value > spell.StatusProcChance) return;

        Transform root = target != null ? target.transform.root : null;
        switch (effect)
        {
            case StatusEffectType.Bleed:
            case StatusEffectType.Poison:
            case StatusEffectType.Rot:
                SpellDoT.Apply(target, power * 0.12f, 4f, 0.5f, spell.Type);
                break;
            case StatusEffectType.Burn:
                // Fire-vs-water tug of war (§3.7): fire always melts any cold buildup, but a
                // soaked target can't be ignited — burn only takes hold if it isn't wet.
                ChillStatus.Melt(target);
                if (!WetStatus.IsWet(target))
                    SpellDoT.Apply(target, power * 0.12f, 4f, 0.5f, DamageType.Fire);
                break;
            case StatusEffectType.Chill:
                // Chill accumulates on the target's root; Wet conducts (+2 stacks) and reaching
                // the frost threshold converts it into a full Frost freeze (§3.7).
                ChillStatus.Apply(target);
                break;
            case StatusEffectType.Frost:
                if (root != null && root.TryGetComponent<EnemyController>(out var frostEnemy))
                    frostEnemy.ApplySlow(0.5f, 3.5f);
                break;
            case StatusEffectType.Stagger:
                if (root != null && root.TryGetComponent<EnemyController>(out var staggerEnemy))
                    staggerEnemy.ApplyStun(0.35f);
                break;
            case StatusEffectType.Wet:
                WetStatus.Apply(target, 4f);
                break;
            case StatusEffectType.Blind:
                BlindStatus.Apply(target, 4f);
                break;
        }
    }

    /// <summary>Outward shove on a damage hit — transform-driven (enemies hold no rigidbody).</summary>
    private void ApplyKnockback(SpellData spell, GameObject target)
    {
        if (spell == null || spell.Knockback <= 0f || target == null) return;
        Transform root = target.transform.root;
        Vector3 dir = root.position - transform.position;
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.0001f) return;
        root.position += dir.normalized * spell.Knockback;
    }
}
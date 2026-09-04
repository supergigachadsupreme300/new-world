using UnityEngine;

/// <summary>
/// Attribute 2 of a skill — its effect ("what it does"). Composed into a <see cref="Skill"/>
/// via the pluggable <see cref="Skill.Effect"/>. Concrete effects are reused across many
/// skills (composition), so 60 skills are 60 instances sharing a handful of behavior classes.
/// </summary>
public interface IEffect
{
    /// <summary>Execute the effect against the given context.</summary>
    void Execute(SkillContext ctx);
}

/// <summary>
/// Area/melee strike effect: deals <see cref="BasePower"/> of <see cref="Type"/> in a sphere
/// around the origin. Mirrors the weapon-art strike delivery for physical and elemental kinds.
/// </summary>
[System.Serializable]
public sealed class DamageZoneEffect : IEffect
{
    public float Radius = 1f;
    public float BasePower = 20f;
    public DamageType Type = DamageType.Physical;

    public void Execute(SkillContext ctx)
    {
        if (ctx == null) return;
        Vector3 origin = ctx.Origin != null ? ctx.Origin.position + ctx.Origin.forward * (Radius * 0.5f)
            : Vector3.zero;
        Collider[] cols = Physics.OverlapSphere(origin, Radius, ~0);
        foreach (var col in cols)
        {
            if (col.transform.root == (ctx.User != null ? ctx.User.transform.root : null)) continue;
            if (col.TryGetComponent<IDamageable>(out var damageable))
            {
                var result = DamageCalculator.Calculate(new DamageCalculator.HitContext
                {
                    AttackPower = BasePower,
                    SkillMultiplier = 1f,
                    Defense = 5f,
                    DefenseMultiplier = 1f,
                    Type = Type,
                    Resistance = NeutralResistance.Instance,
                    WeaknessMultiplier = 1f,
                    CriticalMultiplier = 1f
                }, false);
                damageable.TakeDamage(Mathf.RoundToInt(result.TotalDamage));
            }
        }
    }
}

/// <summary>
/// Magic cast effect: forwards a <see cref="SpellData"/> to the caster's <see cref="SpellCaster"/>,
/// routing through the shared casting pipeline (FP, cast time, cooldown, delivery). Used by magic
/// castable skills.
/// </summary>
[System.Serializable]
public sealed class SpellCastEffect : IEffect
{
    public SpellData Spell;

    public void Execute(SkillContext ctx)
    {
        if (ctx == null || Spell == null || ctx.Caster == null) return;
        ctx.Caster.BeginCast(Spell, ctx.Origin != null ? ctx.Origin : ctx.User != null ? ctx.User.transform : null);
    }
}

/// <summary>
/// Passive stat effect: adds <see cref="Amount"/> to <see cref="Stat"/> on the user's
/// PlayerStats. Always-on once its skill is learned. Applied additively.
/// </summary>
[System.Serializable]
public sealed class StatBuffEffect : IEffect
{
    public StatType Stat;
    public float Amount;

    public void Execute(SkillContext ctx)
    {
        if (ctx == null || ctx.Stats == null) return;
        ctx.Stats.AddStatPoints(Stat, Amount);
    }
}

/// <summary>
/// Weapon-art effect: triggers the rigged weapon's <see cref="WeaponArtExecutor"/> on its
/// equipped <see cref="WeaponArt"/> (spends FP + cooldown via the art). Used by melee/ranged
/// attack-art skills. If no art executor is resolved, does nothing.
/// </summary>
[System.Serializable]
public sealed class WeaponArtEffect : IEffect
{
    public void Execute(SkillContext ctx)
    {
        if (ctx == null || ctx.ArtExecutor == null) return;
        ctx.ArtExecutor.TryUse();
    }
}
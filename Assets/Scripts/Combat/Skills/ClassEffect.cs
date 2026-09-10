using UnityEngine;

/// <summary>
/// Attribute 2 of a class skill — its behavior ("what it does"), mirroring <see cref="IEffect"/>
/// for the class skill system (game-design §3.2.1). Concrete effects are reused across many class
/// skills (composition), so ~240 class skills share a handful of behavior classes.
/// </summary>
public interface IClassEffect
{
    /// <summary>Execute the effect against the given class-skill context.</summary>
    void Execute(ClassSkillContext ctx);
}

/// <summary>
/// The wiring handed to any <see cref="ClassSkill"/> when it executes. Carries the active class
/// stack (player root) so effects never need to find things themselves.
/// </summary>
public sealed class ClassSkillContext
{
    public GameObject User;
    public Transform Origin;
    public SpellCaster Caster;
    public StaminaSystem Stamina;
    public PlayerStats Stats;
    public PlayerController Controller;
    public ClassUnlocker Unlocker;
    public ClassPassiveManager Passives;
    public float ChargeLevel;
}

/// <summary>
/// Heal effect: restores the player's HP, scaled by HealPower (Faith) and class HealPowerMul.
/// </summary>
[System.Serializable]
public sealed class HealEffect : IClassEffect
{
    public float Amount = 30f;

    public void Execute(ClassSkillContext ctx)
    {
        if (ctx == null || ctx.Controller == null) return;
        float heal = Amount;
        if (ctx.Stats != null) heal *= ctx.Stats.HealPowerMultiplier;
        if (heal <= 0f) return;
        ctx.Controller.Heal(Mathf.Max(1, Mathf.RoundToInt(heal)));
        DamageNumber.Spawn(ctx.Controller.transform.position + Vector3.up * 1.5f, heal, DamageType.Holy);
    }
}

/// <summary>
/// Class spell cast: forwards a <see cref="SpellData"/> through the shared casting pipeline
/// (FP, cast time, cooldown, delivery) — used by Smite, Talisman, and Mage castable class skills.
/// </summary>
[System.Serializable]
public sealed class ClassSpellEffect : IClassEffect
{
    public SpellData Spell;

    public void Execute(ClassSkillContext ctx)
    {
        if (ctx == null || Spell == null || ctx.Caster == null) return;
        ctx.Caster.BeginCast(Spell,
            ctx.Origin != null ? ctx.Origin : ctx.User != null ? ctx.User.transform : null,
            default, ctx.ChargeLevel);
    }
}

/// <summary>
/// Summon effect: spawns a procedural combat ally (<see cref="SummonedAlly"/>) for a limited time.
/// </summary>
[System.Serializable]
public sealed class SummonEffect : IClassEffect
{
    public float Power = 12f;
    public float Duration = 30f;
    public float Range = 5f;

    public void Execute(ClassSkillContext ctx)
    {
        if (ctx == null || ctx.User == null) return;
        SummonedAlly.Spawn(ctx.User.transform, Power, Duration, Range);
    }
}

/// <summary>
/// Aura effect: grants the player (and nearby summoned allies) a timed buff — flat damage
/// reduction + passive HP regen. Magnitude scales with the active class's AuraStrength.
/// </summary>
[System.Serializable]
public sealed class AuraEffect : IClassEffect
{
    public float Seconds = 20f;
    public float DamageReduction = 0.08f;
    public float HpRegenPerSecond = 0.002f;
    public float Radius = 6f;

    public void Execute(ClassSkillContext ctx)
    {
        if (ctx == null || ctx.Controller == null) return;
        float strength = 1f;
        if (ctx.Passives != null) strength = ctx.Passives.AuraStrengthMul;
        float dr = DamageReduction * strength;
        float regen = HpRegenPerSecond * strength;
        ctx.Controller.ApplyClassBuff(Seconds, dr, regen);

        SkillFx.RingFlash(ctx.Controller.transform.position, Vector3.up,
            DamageNumber.ColorFor(DamageType.Holy), Radius, 0.8f);
    }
}

/// <summary>
/// Taunt effect: forces every EnemyController within the radius to focus the player for the
/// duration (their target locks onto the taunter until the effect expires).
/// </summary>
[System.Serializable]
public sealed class TauntEffect : IClassEffect
{
    public float Radius = 6f;
    public float Duration = 4f;

    public void Execute(ClassSkillContext ctx)
    {
        if (ctx == null || ctx.User == null) return;
        Collider[] cols = Physics.OverlapSphere(ctx.User.transform.position, Radius, ~0);
        foreach (var col in cols)
        {
            if (col == null || col.transform == null) continue;
            if (col.transform.root == ctx.User.transform.root) continue;
            if (col.TryGetComponent<EnemyController>(out var enemy))
                enemy.ForceTarget(ctx.User.transform, Duration);
        }
        SkillFx.RingFlash(ctx.User.transform.position, Vector3.up,
            DamageNumber.ColorFor(DamageType.Fire), Radius, 0.5f);
    }
}

/// <summary>
/// CC zone effect: spawns a persistent <see cref="CCZone"/> that slows (and optionally stuns)
/// enemies inside — Enchanter/Brawler/Archer/Alchemist area denial.
/// </summary>
[System.Serializable]
public sealed class CcZoneEffect : IClassEffect
{
    public float Radius = 4f;
    public float Duration = 5f;
    public float SlowFactor = 0.5f;
    public bool Stun;

    public void Execute(ClassSkillContext ctx)
    {
        if (ctx == null) return;
        Vector3 origin = ctx.Origin != null
            ? ctx.Origin.position + ctx.Origin.forward * Mathf.Max(Radius * 0.5f, 1.5f)
            : ctx.User != null ? ctx.User.transform.position : Vector3.zero;
        CCZone.Spawn(origin, Radius, Duration, SlowFactor, Stun, ctx.User);
    }
}

/// <summary>
/// Stealth effect: makes the player invisible to enemies (auto-drops aggro, enemies skip the
/// target) for a duration. Break handled by the controller on expiry.
/// </summary>
[System.Serializable]
public sealed class StealthEffect : IClassEffect
{
    public float Seconds = 8f;

    public void Execute(ClassSkillContext ctx)
    {
        if (ctx == null || ctx.Controller == null) return;
        ctx.Controller.ActivateStealth(Seconds);
        SkillFx.RingFlash(ctx.Controller.transform.position, Vector3.up,
            new Color(0.1f, 0.1f, 0.15f, 0.9f), 2.5f, 0.6f);
    }
}

/// <summary>
/// Lifesteal strike: a melee-area strike that restores a fraction of the damage dealt to the
/// player — Rogue "Vampiric Dagger", Berserker "Bloodlust", Necromancer blood arts.
/// </summary>
[System.Serializable]
public sealed class LifestealStrikeEffect : IClassEffect
{
    public float Radius = 2.2f;
    public float BasePower = 25f;
    public float LifestealFraction = 0.35f;
    public DamageType Type = DamageType.Physical;

    public void Execute(ClassSkillContext ctx)
    {
        if (ctx == null) return;
        Vector3 origin = ctx.Origin != null ? ctx.Origin.position + ctx.Origin.forward * (Radius * 0.5f)
            : ctx.User != null ? ctx.User.transform.position : Vector3.zero;

        if (ctx.User != null)
        {
            Vector3 fwd = ctx.Origin != null ? ctx.Origin.forward : Vector3.forward;
            SkillFx.SlashFlash(origin, fwd, Radius, 0.18f, DamageNumber.ColorFor(Type));
        }

        Collider[] cols = Physics.OverlapSphere(origin, Radius, ~0);
        float healed = 0f;
        foreach (var col in cols)
        {
            if (col == null || col.transform == null) continue;
            if (ctx.User != null && col.transform.root == ctx.User.transform.root) continue;
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
                DamageNumber.Spawn(col.transform.position, result.TotalDamage, Type);
                healed += result.TotalDamage * LifestealFraction;
            }
        }

        if (healed > 0f && ctx.Controller != null)
            ctx.Controller.Heal(Mathf.Max(1, Mathf.RoundToInt(healed)));
    }
}

/// <summary>
/// Class strike: area melee/typed damage (Warrior "Titan Swing"/"Whirlwind", Brawler "Haymaker",
/// Monk fist arts). Mirrors <see cref="DamageZoneEffect"/> for the class tree.
/// </summary>
[System.Serializable]
public sealed class ClassStrikeEffect : IClassEffect
{
    public float Radius = 2f;
    public float BasePower = 24f;
    public DamageType Type = DamageType.Physical;

    public void Execute(ClassSkillContext ctx)
    {
        if (ctx == null) return;
        Vector3 origin = ctx.Origin != null ? ctx.Origin.position + ctx.Origin.forward * (Radius * 0.5f)
            : ctx.User != null ? ctx.User.transform.position : Vector3.zero;

        if (ctx.User != null)
        {
            Vector3 fwd = ctx.Origin != null ? ctx.Origin.forward : Vector3.forward;
            SkillFx.SlashFlash(origin, fwd, Radius, 0.18f, DamageNumber.ColorFor(Type));
        }

        Collider[] cols = Physics.OverlapSphere(origin, Radius, ~0);
        foreach (var col in cols)
        {
            if (col == null || col.transform == null) continue;
            if (ctx.User != null && col.transform.root == ctx.User.transform.root) continue;
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
                DamageNumber.Spawn(col.transform.position, result.TotalDamage, Type);
            }
        }
    }
}
using UnityEngine;

/// <summary>
/// Attribute 2 of a race skill — its behavior ("what it does"), mirroring <see cref="IClassEffect"/>
/// for the race skill system. Race effects are active abilities that enhance what makes each race
/// unique — Draconic roar, Vampire bat swarm, Werewolf transformation, etc.
/// </summary>
public interface IRaceEffect
{
    /// <summary>Execute the effect against the given race-skill context.</summary>
    void Execute(RaceSkillContext ctx);
}

/// <summary>
/// The wiring handed to any <see cref="RaceSkill"/> when it executes. Carries the active race
/// stack (player root) so effects never need to find things themselves.
/// </summary>
public sealed class RaceSkillContext
{
    public GameObject User;
    public Transform Origin;
    public SpellCaster Caster;
    public StaminaSystem Stamina;
    public PlayerStats Stats;
    public PlayerController Controller;
    public RacePassiveManager RacePassives;
    public RaceSkillPassiveManager SkillPassives;
    public float ChargeLevel;
}

/// <summary>
/// Heal effect: restores the player's HP, scaled by HealPower (Faith) and racial HealPowerMul.
/// </summary>
[System.Serializable]
public sealed class RaceHealEffect : IRaceEffect
{
    public float Amount = 30f;

    public void Execute(RaceSkillContext ctx)
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
/// Racial spell cast: forwards a <see cref="SpellData"/> through the shared casting pipeline.
/// Used for racial active abilities like Draconic fire breath, Vampire drain, etc.
/// </summary>
[System.Serializable]
public sealed class RaceSpellEffect : IRaceEffect
{
    public SpellData Spell;

    public void Execute(RaceSkillContext ctx)
    {
        if (ctx == null || Spell == null || ctx.Caster == null) return;
        ctx.Caster.BeginCast(Spell,
            ctx.Origin != null ? ctx.Origin : ctx.User != null ? ctx.User.transform : null,
            default, ctx.ChargeLevel);
    }
}

/// <summary>
/// Racial strike: area melee/typed damage. Used for racial combat abilities like Orc warcry slam,
/// Werewolf claw swipe, Serpent-kin venom strike, etc.
/// </summary>
[System.Serializable]
public sealed class RaceStrikeEffect : IRaceEffect
{
    public float Radius = 2f;
    public float BasePower = 24f;
    public DamageType Type = DamageType.Physical;

    public void Execute(RaceSkillContext ctx)
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

/// <summary>
/// Lifesteal strike: a melee-area strike that restores a fraction of the damage dealt to the
/// player. Vampire blood arts, Werewolf devour, etc.
/// </summary>
[System.Serializable]
public sealed class RaceLifestealStrikeEffect : IRaceEffect
{
    public float Radius = 2.2f;
    public float BasePower = 25f;
    public float LifestealFraction = 0.35f;
    public DamageType Type = DamageType.Physical;

    public void Execute(RaceSkillContext ctx)
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
/// CC zone effect: spawns a persistent <see cref="CCZone"/> that slows (and optionally stuns)
/// enemies inside. Ice Giant freeze, Harpy wind gust, etc.
/// </summary>
[System.Serializable]
public sealed class RaceCcZoneEffect : IRaceEffect
{
    public float Radius = 4f;
    public float Duration = 5f;
    public float SlowFactor = 0.5f;
    public bool Stun;

    public void Execute(RaceSkillContext ctx)
    {
        if (ctx == null) return;
        Vector3 origin = ctx.Origin != null
            ? ctx.Origin.position + ctx.Origin.forward * Mathf.Max(Radius * 0.5f, 1.5f)
            : ctx.User != null ? ctx.User.transform.position : Vector3.zero;
        CCZone.Spawn(origin, Radius, Duration, SlowFactor, Stun, ctx.User);
    }
}

/// <summary>
/// Aura effect: grants the player (and nearby summoned allies) a timed buff — flat damage
/// reduction + passive HP regen. Orc war shield, Celestial radiance, etc.
/// </summary>
[System.Serializable]
public sealed class RaceAuraEffect : IRaceEffect
{
    public float Seconds = 20f;
    public float DamageReduction = 0.08f;
    public float HpRegenPerSecond = 0.002f;
    public float Radius = 6f;

    public void Execute(RaceSkillContext ctx)
    {
        if (ctx == null || ctx.Controller == null) return;
        float dr = DamageReduction;
        float regen = HpRegenPerSecond;
        if (ctx.SkillPassives != null)
        {
            dr *= ctx.SkillPassives.AuraStrengthMul;
            regen *= ctx.SkillPassives.AuraStrengthMul;
        }
        ctx.Controller.ApplyClassBuff(Seconds, dr, regen);

        SkillFx.RingFlash(ctx.Controller.transform.position, Vector3.up,
            DamageNumber.ColorFor(DamageType.Holy), Radius, 0.8f);
    }
}

/// <summary>
/// Taunt effect: forces every EnemyController within the radius to focus the player for the
/// duration. Orc warcry, Draconic roar, Golem challenge, etc.
/// </summary>
[System.Serializable]
public sealed class RaceTauntEffect : IRaceEffect
{
    public float Radius = 6f;
    public float Duration = 4f;

    public void Execute(RaceSkillContext ctx)
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
/// Summon effect: spawns a procedural combat ally (<see cref="SummonedAlly"/>) for a limited time.
/// Necromancer-style racial summons, Goblin sapper, etc.
/// </summary>
[System.Serializable]
public sealed class RaceSummonEffect : IRaceEffect
{
    public float Power = 12f;
    public float Duration = 30f;
    public float Range = 5f;

    public void Execute(RaceSkillContext ctx)
    {
        if (ctx == null || ctx.User == null) return;
        SummonedAlly.Spawn(ctx.User.transform, Power, Duration, Range);
    }
}

/// <summary>
/// Stealth effect: makes the player invisible to enemies for a duration.
/// Wraith fade, Vampire mist, Serpent-kin vanish, etc.
/// </summary>
[System.Serializable]
public sealed class RaceStealthEffect : IRaceEffect
{
    public float Seconds = 8f;

    public void Execute(RaceSkillContext ctx)
    {
        if (ctx == null || ctx.Controller == null) return;
        ctx.Controller.ActivateStealth(Seconds);
        SkillFx.RingFlash(ctx.Controller.transform.position, Vector3.up,
            new Color(0.1f, 0.1f, 0.15f, 0.9f), 2.5f, 0.6f);
    }
}

/// <summary>
/// Racial roar: stagger + taunt + damage in a cone. Draconic fire breath, Orc warcry,
/// Werewolf howl, Ice Giant bellow, etc.
/// </summary>
[System.Serializable]
public sealed class RaceRoarEffect : IRaceEffect
{
    public float Radius = 6f;
    public float Duration = 3f;
    public float StaggerAmount = 15f;
    public DamageType Type = DamageType.Physical;

    public void Execute(RaceSkillContext ctx)
    {
        if (ctx == null || ctx.User == null) return;
        Vector3 pos = ctx.User.transform.position;
        Collider[] cols = Physics.OverlapSphere(pos, Radius, ~0);
        foreach (var col in cols)
        {
            if (col == null || col.transform == null) continue;
            if (col.transform.root == ctx.User.transform.root) continue;
            if (col.TryGetComponent<EnemyController>(out var enemy))
                enemy.ForceTarget(ctx.User.transform, Duration);
            if (col.TryGetComponent<IDamageable>(out var damageable))
            {
                var result = DamageCalculator.Calculate(new DamageCalculator.HitContext
                {
                    AttackPower = StaggerAmount,
                    SkillMultiplier = 1f,
                    Defense = 0f,
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
        SkillFx.RingFlash(pos, Vector3.up, DamageNumber.ColorFor(Type), Radius, 0.6f);
    }
}

/// <summary>
/// Temporary stat buff: grants a flat stat increase for a duration. Werewolf transformation,
/// Golem stone form, Elf nature's blessing, etc.
/// </summary>
[System.Serializable]
public sealed class RaceStatBuffEffect : IRaceEffect
{
    public float Seconds = 15f;
    public StatType Stat = StatType.Strength;
    public float Amount = 10f;
    public float Radius = 0f;

    public void Execute(RaceSkillContext ctx)
    {
        if (ctx == null || ctx.Stats == null) return;
        ctx.Stats.AddTemporaryStatBuff(Stat, Amount, Seconds);
        if (Radius > 0f && ctx.Controller != null)
            SkillFx.RingFlash(ctx.Controller.transform.position, Vector3.up,
                DamageNumber.ColorFor(DamageType.Arcane), Radius, 0.5f);
    }
}

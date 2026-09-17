/// <summary>
/// Passive-perk vocabulary for the skill tree (game-design §3.3). Every learned passive skill grants
/// one of these themed perks (replacing the old flat "+N stat" buffs) via <see cref="PassivePerkEffect"/>.
/// Percent kinds are additive amount stored as a multiplier field (5f = +5%); flat kinds add points
/// directly. Aggregated by <see cref="PassivePerkManager"/> and consumed by <see cref="PlayerStats"/>
/// derived getters plus the combat pipeline.
/// </summary>
public enum PassivePerkType
{
    /// <summary>Percent — maximum health.</summary>
    MaxHealthPercent,

    /// <summary>Percent — maximum stamina.</summary>
    StaminaMaxPercent,

    /// <summary>Percent — maximum focus points.</summary>
    FocusMaxPercent,

    /// <summary>Percent — physical attack power (melee light/heavy hits, shield bashes).</summary>
    AttackPowerPercent,

    /// <summary>Percent — magic attack power.</summary>
    SpellDamagePercent,

    /// <summary>Percent — healing output.</summary>
    HealPowerPercent,

    /// <summary>Percent — attack-speed scale (swing timing).</summary>
    AttackSpeedPercent,

    /// <summary>Flat — added critical-hit chance in % (physical pipeline rolls against it).</summary>
    CritChanceFlat,

    /// <summary>Percent — critical-hit damage multiplier.</summary>
    CritDamagePercent,

    /// <summary>Percent — backstab damage multiplier.</summary>
    BackstabPercent,

    /// <summary>Percent — movement speed.</summary>
    MovementSpeedPercent,

    /// <summary>Percent — stamina regeneration rate.</summary>
    StaminaRegenPercent,

    /// <summary>Percent — focus regeneration rate.</summary>
    FocusRegenPercent,

    /// <summary>Percent — cooldown reduction (shorter skill/spell cooldowns).</summary>
    CooldownReductionPercent,

    /// <summary>Flat — added fraction of max HP regenerated per second (0.003 = +0.3%).</summary>
    HealthRegenPerSecond,

    /// <summary>Flat — added damage reduction as a fraction (0.02 = +2%), additive on top of Defense-derived DR.</summary>
    DamageReductionFlat,

    /// <summary>Percent — block stamina efficiency (less stamina drained per blocked hit).</summary>
    BlockEfficiencyPercent,

    /// <summary>Percent — stagger/knockback resistance.</summary>
    StaggerResistPercent,

    /// <summary>Percent — parry window length.</summary>
    ParryWindowPercent,

    /// <summary>Percent — loot quality.</summary>
    LootLuckPercent,
}
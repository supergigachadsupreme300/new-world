using UnityEngine;

/// <summary>
/// The wiring handed to any <see cref="Skill"/> when it executes (Phase 10). Carries the
/// caster's referencing components so effects never need to find them themselves. Built by
/// <c>SkillProfile</c> from the player root at cast/learn time.
/// </summary>
public sealed class SkillContext
{
    /// <summary>Caster that owns the skill (drives Focus casting + cooldowns).</summary>
    public SpellCaster Caster;

    /// <summary>Stamina resource pool of the user.</summary>
    public StaminaSystem Stamina;

    /// <summary>Player stats (passive application + Wisdom/Str scaling).</summary>
    public PlayerStats Stats;

    /// <summary>The rigged weapon's skill executor, if a weapon is equipped (WeaponSkill skills).</summary>
    public WeaponSkillExecutor SkillExecutor;

    /// <summary>World-space origin for zones / projectiles.</summary>
    public Transform Origin;

    /// <summary>The user's root GameObject (for self-buffs etc.).</summary>
    public GameObject User;

    /// <summary>Charge level (0..1+) of a held cast, forwarded to magic deliveries.</summary>
    public float ChargeLevel;

    /// <summary>Focus already drained in real time during the charge hold; the delivery spends
    /// only the remainder above this when the cast settles.</summary>
    public float PrepaidFocus;

    /// <summary>Learned skill level of the skill being executed (1 = fresh).</summary>
    public int SkillLevel = 1;

    /// <summary>Power multiplier from skill level (level N → 1 + (N-1)×0.02).</summary>
    public float PowerScale => 1f + (SkillLevel - 1) * 0.02f;

    /// <summary>AoE radius / range multiplier from skill level (level N → 1 + (N-1)×0.02).</summary>
    public float SizeScale => 1f + (SkillLevel - 1) * 0.02f;

    /// <summary>Focus cost + cooldown multiplier from skill level (−1% per level, floor 50%).</summary>
    public float EcoScale => Mathf.Max(0.5f, 1f - (SkillLevel - 1) * 0.01f);
}
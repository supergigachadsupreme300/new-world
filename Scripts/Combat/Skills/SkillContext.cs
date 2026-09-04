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

    /// <summary>The rigged weapon's art executor, if a weapon is equipped (WeaponArt skills).</summary>
    public WeaponArtExecutor ArtExecutor;

    /// <summary>World-space origin for zones / projectiles.</summary>
    public Transform Origin;

    /// <summary>The user's root GameObject (for self-buffs etc.).</summary>
    public GameObject User;
}
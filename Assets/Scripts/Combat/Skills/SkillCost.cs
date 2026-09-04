using UnityEngine;

/// <summary>
/// The resource a skill's <see cref="Cost"/> draws from (Phase 10). "None" marks a
/// passive skill — it costs nothing to use and is always active once learned.
/// </summary>
public enum ResourceKind
{
    /// <summary>No resource spent (passive / always-on).</summary>
    None = 0,

    /// <summary>Focus points (mana), drawn from the caster's SpellCaster pool.</summary>
    Focus = 1,

    /// <summary>Stamina, drawn from the caster's StaminaSystem pool.</summary>
    Stamina = 2
}

/// <summary>
/// Attribute 1 of a skill — its cost. Encapsulates the resource, amount, cast time and
/// cooldown, and the affordability check. Composed into <see cref="Skill"/>.
/// </summary>
[System.Serializable]
public struct Cost
{
    public ResourceKind Resource;
    [Tooltip("Amount of the resource spent (0 for passives).")]
    public float Amount;
    [Tooltip("Cast time in seconds before the effect triggers (0 = instant).")]
    public float CastTime;
    [Tooltip("Cooldown in seconds before the skill can be used again.")]
    public float Cooldown;

    /// <summary>True if the user can currently afford this cost (passives always affordable).</summary>
    public bool CanAfford(SkillContext ctx)
    {
        switch (Resource)
        {
            case ResourceKind.Focus:
                return ctx != null && ctx.Caster != null && ctx.Caster.HasFocusPoints(Amount);
            case ResourceKind.Stamina:
                return ctx != null && ctx.Stamina != null && ctx.Stamina.Stamina >= Amount;
            case ResourceKind.None:
            default:
                return true;
        }
    }

    /// <summary>Try to spend the cost. Returns true if fully paid.</summary>
    public bool Spend(SkillContext ctx)
    {
        switch (Resource)
        {
            case ResourceKind.Focus:
                return ctx != null && ctx.Caster != null && ctx.Caster.TrySpendFocus(Amount);
            case ResourceKind.Stamina:
                return ctx != null && ctx.Stamina != null && ctx.Stamina.TrySpend(Amount);
            case ResourceKind.None:
            default:
                return true;
        }
    }
}
using UnityEngine;

/// <summary>
/// Status effects applied on hit (DoT / crowd-control). They are a separate dimension
/// from DamageType (§3.7) and are scaled by Luck (StatusProcLuck, §3.4).
/// </summary>
public enum StatusEffectType
{
    /// <summary>Damage over time (blood loss burst).</summary>
    Bleed = 0,

    /// <summary>Damage over time (nature).</summary>
    Poison = 1,

    /// <summary>Damage over time + slows healing (severe).</summary>
    Rot = 2,

    /// <summary>Slows movement/attack speed, builds toward freeze.</summary>
    Frost = 3,

    /// <summary>Damage over time (fire), may spread.</summary>
    Burn = 4,

    /// <summary>Poise break / crowd-control (interrupts actions).</summary>
    Stagger = 5
}

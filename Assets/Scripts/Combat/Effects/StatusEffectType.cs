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

    /// <summary>Strong freeze: slows movement/attack speed heavily, builds toward full freeze.
    /// Ice's everyday signature is the lighter <see cref="Chill"/>.</summary>
    Frost = 3,

    /// <summary>Damage over time (fire), may spread.</summary>
    Burn = 4,

    /// <summary>Poise break / crowd-control (interrupts actions).</summary>
    Stagger = 5,

    /// <summary>Wetted by water magic: slows slightly and conducts — Ice/Lightning hits a wet
    /// foe deal bonus damage (see <see cref="WetStatus"/>).</summary>
    Wet = 6,

    /// <summary>Light cold (Ice signature, §3.7): a gentle slow. Frost is the heavier, full-cloud
    /// freeze that the literal "freeze" spells use.</summary>
    Chill = 7,

    /// <summary>Darkness (Dark signature, §3.7): black fog engulfs the victim, following it and
    /// cutting its vision (a local player sees the world dim around them).</summary>
    Blind = 8
}

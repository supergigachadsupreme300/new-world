using UnityEngine;

/// <summary>
/// The 10 damage types (single type per weapon/spell/ability). See game-design §3.7.
///
/// Every weapon, spell, and ability declares exactly one DamageType. Armor/gear provides
/// resistance per type (equipment-only rule, §3.4); the DamageCalculator resolves the
/// attacker's type against the target's resistance.
/// </summary>
public enum DamageType
{
    /// <summary>Weapon/kinetic damage (blunt, slash, pierce — aggregated). Reduced by Defense/armor.</summary>
    Physical = 0,

    /// <summary>Heat/burn damage.</summary>
    Fire = 1,

    /// <summary>Frost/cold damage.</summary>
    Ice = 2,

    /// <summary>Electric damage.</summary>
    Lightning = 3,

    /// <summary>Light/divine damage (strong vs undead/dark).</summary>
    Holy = 4,

    /// <summary>Shadow/void damage (strong vs holy).</summary>
    Dark = 5,

    /// <summary>Air/force damage.</summary>
    Wind = 6,

    /// <summary>Stone/ground damage.</summary>
    Earth = 7,

    /// <summary>Water/fluid damage.</summary>
    Water = 8,

    /// <summary>Generic magic/arcane damage — the distinct "magic" damage type.</summary>
    Arcane = 9
}

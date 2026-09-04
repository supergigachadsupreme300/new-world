/// <summary>
/// The 11 core stats (game-design §3.4). The Arcane stat was removed — its functions
/// split into Luck and Wisdom. This enum drives the PlayerStats raw stat array; derived
/// gameplay values (MaxHP, MoveSpeed, …) are computed in PlayerStats.
/// </summary>
public enum StatType : int
{
    /// <summary>Max HP, HP regen, status-effect resistance.</summary>
    Health = 0,

    /// <summary>Movement speed, dodge speed, small attack-speed bonus.</summary>
    Speed = 1,

    /// <summary>Max stamina, equip load.</summary>
    Endurance = 2,

    /// <summary>Melee damage (heavy), stagger power.</summary>
    Strength = 3,

    /// <summary>Light/one-handed melee damage, ranged accuracy, parry, finesse.</summary>
    Dexterity = 4,

    /// <summary>Primary attack-speed source (large per-point effect).</summary>
    AttackSpeed = 5,

    /// <summary>Flat physical damage reduction (armor/equipment-based).</summary>
    Defense = 6,

    /// <summary>Max FP (mana), skill cooldown reduction.</summary>
    Intelligence = 7,

    /// <summary>Magic damage, spell power.</summary>
    Wisdom = 8,

    /// <summary>Miracle/healing power, buff duration.</summary>
    Faith = 9,

    /// <summary>Loot quality, crit chance, crafting luck, status-effect luck.</summary>
    Luck = 10
}
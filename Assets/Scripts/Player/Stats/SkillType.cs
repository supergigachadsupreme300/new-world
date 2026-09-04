/// <summary>
/// Use-based skill categories (game-design §3.3, planning Task 4.2). Players advance any
/// category through how they play; each category has its own XP bar and flat tier rewards
/// at levels 5 / 10 / 15 / 20 / 25.
/// </summary>
public enum SkillType : int
{
    Melee = 0,
    Ranged = 1,
    Magic = 2,

    /// <summary>Sneaking / pickpocket / trap use.</summary>
    Stealth = 3,

    /// <summary>Alchemy / smithing / tailoring / cooking.</summary>
    Crafting = 4,

    /// <summary>Survival / damage-taken resilience (was "Defense").</summary>
    Fortitude = 5
}
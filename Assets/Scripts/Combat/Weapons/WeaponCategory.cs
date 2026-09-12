using UnityEngine;

/// <summary>
/// Weapon category/delivery method (§3.6 Layer 2). Expandable: future values
/// (Thrown, Shield, Summon, Hybrid, …) slot in as new enum entries + one behavior
/// class each (see IWeaponBehavior).
/// </summary>
public enum WeaponCategory
{
    /// <summary>Melee arc sweep (hitbox). Damage from weapon.base + Str/Dex scaling; costs Stamina.</summary>
    Melee = 0,

    /// <summary>Projectile/raycast. Consumes ammo; damage from weapon.base; accuracy from Dexterity.</summary>
    Ranged = 1,

    /// <summary>Routes to the spell pipeline (§3.8). Damage from spell × Wisdom; costs FP.</summary>
    Magic = 2,

    /// <summary>
    /// Off-hand defense: bash = a short hitbox slam; blocking instead of charging on RMB, with
    /// stricter damage absorb and cheaper stamina drain than bare-hand guard (see WeaponData shield mods).
    /// </summary>
    Shield = 3
}

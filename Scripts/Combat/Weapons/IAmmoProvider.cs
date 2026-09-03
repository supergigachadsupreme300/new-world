using UnityEngine;

/// <summary>
/// Supplies/counts projectile ammo (arrows/bolts) for RangedWeaponBehavior.
/// The Inventory/consumables system (later phase) implements this; until then a
/// simple infinite drop-in keeps ranged weapons functional.
/// </summary>
public interface IAmmoProvider
{
    /// <summary>Available ammo for the given ammo item id, or -1 if infinite/unspecified.</summary>
    int Count(string ammoItemId);

    /// <summary>Consume one round of ammo (if finite).</summary>
    void Consume(string ammoItemId);
}

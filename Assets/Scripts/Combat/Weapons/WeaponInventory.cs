using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Runtime inventory of weapons the player owns. Weapons are collected from the world
/// (test-ground weapon rack) and listed in the Character Info > Inventory tab, where they
/// can be dragged onto the L. Hand / R. Hand slots of the Equipment tab to equip them.
/// Tracked by weapon id against <see cref="WeaponCatalog"/>; duplicate pickups are ignored.
/// </summary>
[DisallowMultipleComponent]
public sealed class WeaponInventory : MonoBehaviour
{
    private readonly List<string> _owned = new List<string>();

    /// <summary>Ids of every owned weapon, in collection order.</summary>
    public IReadOnlyList<string> Owned => _owned;

    /// <summary>True if the given weapon id is already owned.</summary>
    public bool Has(string id)
    {
        return !string.IsNullOrEmpty(id) && _owned.Contains(id);
    }

    /// <summary>Add a weapon id if not already owned. Returns true when newly added.</summary>
    public bool Own(string id)
    {
        if (string.IsNullOrEmpty(id) || _owned.Contains(id))
            return false;
        _owned.Add(id);
        return true;
    }

    /// <summary>Ensure a weapon id is owned (idempotent). Convenience for starter grants.</summary>
    public void EnsureOwned(string id)
    {
        Own(id);
    }
}
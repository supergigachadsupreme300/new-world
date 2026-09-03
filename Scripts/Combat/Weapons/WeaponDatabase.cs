using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Registry and resolver (§3.6 Layer 4). Holds the catalog of WeaponData assets and maps
/// each WeaponCategory to the runtime IWeaponBehavior type that handles it.
///
/// Equipped weapons are resolved by id; the behavior itself lives as a component on the
/// equipped weapon GameObject (CombatController asks the database for the correct behavior
/// type, then GetComponent on the resolved weapon object).
/// </summary>
[CreateAssetMenu(fileName = "WeaponDatabase", menuName = "New World/Combat/Weapon Database", order = 3)]
public class WeaponDatabase : ScriptableObject
{
    [Tooltip("All weapon assets in the game.")]
    public List<WeaponData> Weapons = new List<WeaponData>();

    // Category → concrete IWeaponBehavior component type (§3.6 Layer 3).
    private static readonly Dictionary<WeaponCategory, Type> BehaviorTypes =
        new Dictionary<WeaponCategory, Type>();

    static WeaponDatabase()
    {
        BehaviorTypes[WeaponCategory.Melee] = typeof(MeleeWeaponBehavior);
        BehaviorTypes[WeaponCategory.Ranged] = typeof(RangedWeaponBehavior);
        BehaviorTypes[WeaponCategory.Magic] = typeof(MagicWeaponBehavior);
    }

    /// <summary>Look up a weapon by id, or null if not present.</summary>
    public WeaponData GetWeapon(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;
        for (int i = 0; i < Weapons.Count; i++)
            if (Weapons[i] != null && WeapsId(Weapons[i]) == id)
                return Weapons[i];
        return null;
    }

    /// <summary>The IWeaponBehavior component type that handles the given weapon category.</summary>
    public static Type ResolveBehaviorType(WeaponCategory category)
    {
        BehaviorTypes.TryGetValue(category, out Type type);
        return type;
    }

    /// <summary>Find the matching behavior component on the given weapon GameObject, if any.</summary>
    public static IWeaponBehavior ResolveBehavior(GameObject weaponObject, WeaponCategory category)
    {
        if (weaponObject == null) return null;
        Type type = ResolveBehaviorType(category);
        return type != null ? weaponObject.GetComponent(type) as IWeaponBehavior : null;
    }

    private static string WeapsId(WeaponData w)
    {
        return w.id ?? string.Empty;
    }
}

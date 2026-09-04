using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Runtime catalog of the 15 default weapons (Phase 10). Builds <see cref="WeaponData"/>
/// instances in code — no .asset files needed — mirroring the <c>ClassUnlocker.BuildDefaultClasses</c>
/// pattern. Each weapon is authored to represent a class archetype's distinct fighting style
/// (category, wielding, damage type, base damage/speed/reach/weight, scaling) so the archetypes
/// feel different, but NO weapon is class-locked: any player can equip/use any of them.
///
/// This is data-only. To make a weapon combat-active, hand it to <c>WeaponRigBuilder</c>
/// which builds the live weapon GameObject (behavior + hitbox + art executor + visual proxy)
/// and equips it onto the player's <c>CombatController</c> hands.
/// </summary>
public static class WeaponCatalog
{
    /// <summary>The built roster. <see cref="EnsureBuilt"/> populates it once.</summary>
    public static List<WeaponData> All { get; private set; }

    private static bool _built;

    /// <summary>Build the 15-weapon roster on first access (idempotent, append-only).</summary>
    public static void EnsureBuilt()
    {
        if (_built) return;
        _built = true;
        All = BuildDefault();
    }

    /// <summary>Look up a weapon by id, or null if not present.</summary>
    public static WeaponData Find(string id)
    {
        EnsureBuilt();
        if (All == null) return null;
        for (int i = 0; i < All.Count; i++)
            if (All[i] != null && All[i].id == id)
                return All[i];
        return null;
    }

    /// <summary>Convenience default starter weapon id (Wanderer's Iron Sword).</summary>
    public const string StarterWeaponId = "iron_sword";

    private static List<WeaponData> BuildDefault()
    {
        var list = new List<WeaponData>();

        // ── Melee ──────────────────────────────────────────────────────────
        list.Add(Make("iron_sword", "Iron Sword", WeaponCategory.Melee, DamageType.Physical, 10f, 12f, 1f, 1.2f, 1f, WeaponScalingStat.Dexterity, 0.06f, 5f, null));
        list.Add(Make("greatsword", "Greatsword", WeaponCategory.Melee, DamageType.Physical, 18f, 26f, 0.8f, 1.6f, 1.6f, WeaponScalingStat.Strength, 0.10f, 10f, null));
        list.Add(Make("dagger", "Dagger", WeaponCategory.Melee, DamageType.Physical, 5f, 8f, 1.4f, 0.9f, 0.8f, WeaponScalingStat.Dexterity, 0.08f, 2f, null));
        list.Add(Make("katana", "Katana", WeaponCategory.Melee, DamageType.Physical, 13f, 18f, 1.1f, 1.5f, 1.4f, WeaponScalingStat.Dexterity, 0.09f, 6f, null));
        list.Add(Make("greataxe", "Greataxe", WeaponCategory.Melee, DamageType.Physical, 22f, 34f, 0.6f, 1.7f, 1.7f, WeaponScalingStat.Strength, 0.11f, 14f, null));
        list.Add(Make("lance", "Knight's Lance", WeaponCategory.Melee, DamageType.Physical, 16f, 20f, 0.9f, 2.4f, 1.5f, WeaponScalingStat.Strength, 0.08f, 8f, null));
        list.Add(Make("gauntlets", "Gauntlets", WeaponCategory.Melee, DamageType.Physical, 4f, 6f, 1.7f, 0.6f, 0.7f, WeaponScalingStat.Dexterity, 0.06f, 3f, null));
        list.Add(Make("warhammer", "Warhammer", WeaponCategory.Melee, DamageType.Holy, 17f, 24f, 0.8f, 1.4f, 1.5f, WeaponScalingStat.Strength, 0.09f, 12f, null));

        // ── Ranged ─────────────────────────────────────────────────────────
        list.Add(Make("longbow", "Longbow", WeaponCategory.Ranged, DamageType.Physical, 8f, 16f, 1f, 18f, 2f, WeaponScalingStat.Dexterity, 0f, 3f, "arrow"));
        list.Add(Make("throwing_hammer", "Throwing Hammer", WeaponCategory.Ranged, DamageType.Physical, 10f, 18f, 0.95f, 12f, 1.8f, WeaponScalingStat.Strength, 0f, 6f, null));

        // ── Magic ──────────────────────────────────────────────────────────
        list.Add(MakeMagic("staff", "Mage's Staff", WeaponCategory.Magic, DamageType.Arcane, 8f, 14f, 0.9f, 6f, 1.1f, WeaponScalingStat.Wisdom, 0.08f, 2f,
            1.2f, 1f, 1f));
        list.Add(MakeMagic("holy_book", "Holy Book", WeaponCategory.Magic, DamageType.Holy, 7f, 12f, 0.95f, 5f, 1f, WeaponScalingStat.Wisdom, 0.07f, 2f,
            1.1f, 1f, 1f));
        list.Add(MakeMagic("bone_wand", "Bone Wand", WeaponCategory.Magic, DamageType.Dark, 8f, 15f, 0.9f, 7f, 1.1f, WeaponScalingStat.Wisdom, 0.09f, 2f,
            1.25f, 1f, 1f));
        list.Add(MakeMagic("control_orb", "Control Orb", WeaponCategory.Magic, DamageType.Wind, 9f, 13f, 0.85f, 8f, 1.2f, WeaponScalingStat.Intelligence, 0.09f, 2f,
            1.15f, 0.9f, 1f));
        list.Add(MakeMagic("lute", "Bard's Lute", WeaponCategory.Magic, DamageType.Physical, 5f, 8f, 1f, 4f, 1f, WeaponScalingStat.Wisdom, 0.06f, 1f,
            1f, 1.1f, 1f));

        return list;
    }

    private static WeaponData Make(string id, string displayName, WeaponCategory category, DamageType type,
        float weight, float baseDamage, float speed, float reach, float coefficient,
        WeaponScalingStat scaling, float stagger, string ammoId)
    {
        var w = ScriptableObject.CreateInstance<WeaponData>();
        w.name = id;
        w.id = id;
        w.displayName = displayName;
        w.Category = category;
        w.Type = type;
        w.Weight = weight;
        w.BaseDamage = baseDamage;
        w.Speed = speed;
        w.Reach = reach;
        w.ScalingStat = scaling;
        w.ScalingCoefficient = coefficient;
        w.StaggerPower = stagger;
        w.AmmoItemId = ammoId;
        return w;
    }

    private static WeaponData MakeMagic(string id, string displayName, WeaponCategory category, DamageType type,
        float weight, float baseDamage, float speed, float reach, float coefficient,
        WeaponScalingStat scaling, float stagger, float damageMult, float castTimeMod, float cooldownMod)
    {
        var w = Make(id, displayName, category, type, weight, baseDamage, speed, reach, coefficient, scaling, stagger, null);
        w.MagicDamageMult = damageMult;
        w.CastTimeMod = castTimeMod;
        w.CooldownMod = cooldownMod;
        return w;
    }
}
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Runtime catalog of the default gear roster (game-design §5.4). Builds <see cref="GearDef"/>
/// instances in code — mirroring the <c>WeaponCatalog</c> / <c>ClassUnlocker</c> pattern — so the
/// 21-slot equipment system has data to equip without asset files. Every gear piece defines its
/// slot, weight, flat physical DR, per-type elemental/magic resist, and flat stat bonuses.
///
/// This is data-only. Equipping/unequipping is handled by <see cref="EquipmentSystem"/>.
/// </summary>
public static class GearCatalog
{
    /// <summary>The built roster. <see cref="EnsureBuilt"/> populates it once.</summary>
    public static List<GearDef> All { get; private set; }

    private static bool _built;
    private static readonly Dictionary<string, GearDef> _byId = new Dictionary<string, GearDef>();

    /// <summary>Build the roster on first access (idempotent, append-only).</summary>
    public static void EnsureBuilt()
    {
        if (_built) return;
        _built = true;
        All = BuildDefault();
        foreach (var g in All)
            if (g != null && !string.IsNullOrEmpty(g.id) && !_byId.ContainsKey(g.id))
                _byId.Add(g.id, g);
    }

    /// <summary>Look up a gear definition by id, or null if not present.</summary>
    public static GearDef Find(string id)
    {
        EnsureBuilt();
        if (string.IsNullOrEmpty(id)) return null;
        return _byId.TryGetValue(id, out var g) ? g : null;
    }

    /// <summary>True if the given id corresponds to a known gear piece.</summary>
    public static bool IsGear(string id) => Find(id) != null;

    /// <summary>The slot a gear piece goes in, or None if it isn't gear.</summary>
    public static bool TrySlotFor(string id, out EquipSlot slot)
    {
        var g = Find(id);
        if (g == null) { slot = default; return false; }
        slot = g.Slot;
        return true;
    }

    /// <summary>Genre of a gear piece, or Accessory if not gear (safe default).</summary>
    public static EquipGenre GenreFor(string id)
    {
        var g = Find(id);
        return g != null ? g.Genre : EquipGenre.Accessory;
    }

    private static List<GearDef> BuildDefault()
    {
        var list = new List<GearDef>();

        // ── Armor (5 slots) — flat physical DR + elemental/magic resist (§5.4) ──
        list.Add(Armor("cloth_hood", "Cloth Hood", EquipSlot.Head, 0.4f, 1f, 1f, 0f, new[] { (DamageType.Arcane, 1f) }));
        list.Add(Armor("leather_helmet", "Leather Helmet", EquipSlot.Head, 1f, 2.5f, 2.2f, 1f));
        list.Add(Armor("iron_helmet", "Iron Helmet", EquipSlot.Head, 3.2f, 5f, 4.4f, 2f,
            new[] { (DamageType.Fire, 1f), (DamageType.Ice, 1f) }));
        list.Add(Armor("cloth_robe", "Cloth Robe", EquipSlot.Body, 0.8f, 2f, 1.6f, 0f,
            new[] { (DamageType.Arcane, 3f), (DamageType.Holy, 1f) }));
        list.Add(Armor("leather_armor", "Leather Armor", EquipSlot.Body, 2f, 5f, 3.6f, 1.5f));
        list.Add(Armor("iron_plate", "Iron Plate", EquipSlot.Body, 6f, 10f, 9f, 4f,
            new[] { (DamageType.Physical, 2f) }));
        list.Add(Armor("cloth_gloves", "Cloth Gloves", EquipSlot.Glove, 0.2f, 1f, 0.8f, 0f));
        list.Add(Armor("leather_gloves", "Leather Gloves", EquipSlot.Glove, 0.5f, 2f, 1.6f, 0.5f));
        list.Add(Armor("iron_gauntlets", "Iron Gauntlets", EquipSlot.Glove, 1.6f, 4f, 3.6f, 1.5f));
        list.Add(Armor("cloth_leggings", "Cloth Leggings", EquipSlot.Legging, 0.5f, 1.5f, 1.2f, 0f));
        list.Add(Armor("leather_leggings", "Leather Leggings", EquipSlot.Legging, 1.4f, 3.5f, 2.8f, 1f));
        list.Add(Armor("iron_greaves", "Iron Greaves", EquipSlot.Legging, 3.6f, 6f, 5.4f, 2.5f));
        list.Add(Armor("cloth_boots", "Cloth Boots", EquipSlot.Feet, 0.3f, 1f, 0.8f, 0f));
        list.Add(Armor("leather_boots", "Leather Boots", EquipSlot.Feet, 0.8f, 2f, 1.8f, 0.5f));
        list.Add(Armor("iron_sabatons", "Iron Sabatons", EquipSlot.Feet, 2.4f, 4f, 3.8f, 1.5f));

        // ── Accessory (14 slots) — passive stat bonuses (rings/necklace/ears/belt) ──
        list.Add(Accessory("copper_ring", "Copper Ring", EquipSlot.Finger1, 0.2f, (StatType.Strength, 1f)));       // +1 Str
        list.Add(Accessory("iron_ring", "Iron Ring", EquipSlot.Finger2, 0.3f, (StatType.Endurance, 1f), (StatType.Luck, 0.5f)));  // +1 End, +0.5 Lck
        list.Add(Accessory("silver_ring", "Silver Ring", EquipSlot.Finger3, 0.2f, (StatType.Dexterity, 1f)));    // +1 Dex
        list.Add(Accessory("gold_ring", "Gold Ring", EquipSlot.Finger4, 0.3f, (StatType.Luck, 1f)));    // +1 Lck
        list.Add(Accessory("sapphire_ring", "Sapphire Ring", EquipSlot.Finger5, 0.2f, (StatType.Intelligence, 1f)));
        list.Add(Accessory("ruby_ring", "Ruby Ring", EquipSlot.Finger6, 0.2f, (StatType.Strength, 2f)));
        list.Add(Accessory("emerald_ring", "Emerald Ring", EquipSlot.Finger7, 0.2f, (StatType.Speed, 1f)));
        list.Add(Accessory("amethyst_ring", "Amethyst Ring", EquipSlot.Finger8, 0.2f, (StatType.Wisdom, 1f)));
        list.Add(Accessory("topaz_ring", "Topaz Ring", EquipSlot.Finger9, 0.2f, (StatType.Faith, 1f)));
        list.Add(Accessory("diamond_ring", "Diamond Ring", EquipSlot.Finger10, 0.3f, (StatType.Defense, 2f), (StatType.Luck, 2f)));
        list.Add(Accessory("bronze_necklace", "Bronze Necklace", EquipSlot.Necklace, 0.5f, (StatType.Defense, 1f), (StatType.Health, 5f)));
        list.Add(Accessory("amethyst_necklace", "Amethyst Necklace", EquipSlot.Necklace, 0.5f, (StatType.Dexterity, 2f)));
        list.Add(Accessory("earring_copper", "Copper Earring", EquipSlot.Ear1, 0.2f, (StatType.AttackSpeed, 1f)));
        list.Add(Accessory("earring_silver", "Silver Earring", EquipSlot.Ear2, 0.2f, (StatType.Luck, 1f)));
        list.Add(Accessory("leather_belt", "Leather Belt", EquipSlot.Belt, 0.4f, (StatType.Defense, 1f), (StatType.Endurance, 2f)));
        list.Add(Accessory("buckle_belt", "Buckled Belt", EquipSlot.Belt, 0.8f, (StatType.Defense, 2f), (StatType.Strength, 2f)));

        return list;
    }

    private static GearDef Armor(string id, string name, EquipSlot slot, float weight,
        float defense, float physicalDampen, float extraDampen,
        params (DamageType type, float pct)[] resist)
    {
        var g = new GearDef
        {
            id = id,
            displayName = name,
            Slot = slot,
            Genre = EquipGenre.Armor,
            Weight = weight,
            Defense = defense,
        };
        // Armor dampens the physical DR from the Defense stat and adds per-type resist.
        g.Resist[(int)DamageType.Physical] = physicalDampen + extraDampen;
        foreach (var (t, pct) in resist)
            g.Resist[(int)t] += pct;
        return g;
    }

    private static GearDef Accessory(string id, string name, EquipSlot slot, float weight,
        params (StatType stat, float amt)[] bonus)
    {
        var g = new GearDef
        {
            id = id,
            displayName = name,
            Slot = slot,
            Genre = EquipGenre.Accessory,
            Weight = weight,
        };
        foreach (var (stat, amt) in bonus)
            g.StatBonus[(int)stat] += amt;
        return g;
    }
}
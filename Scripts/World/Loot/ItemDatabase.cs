using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Static registry of all items (planning Task 5.2). Programmatic roster mirrors the race /
/// biome-database pattern. Covers the item kinds in the plan — weapons, armor, consumables,
/// materials, and skill books — plus the per-biome resources from game-design §7.1.
/// </summary>
public static class ItemDatabase
{
    private static readonly Dictionary<string, ItemData> _byId = new Dictionary<string, ItemData>();
    private static bool _built;

    private static void EnsureBuilt()
    {
        if (_built) return;
        _built = true;
        Register(BuildAll());
    }

    private static void Register(IEnumerable<ItemData> items)
    {
        foreach (var it in items)
            if (it != null && !string.IsNullOrEmpty(it.id) && !_byId.ContainsKey(it.id))
                _byId.Add(it.id, it);
    }

    /// <summary>Get an item by id, or null if unknown.</summary>
    public static ItemData Get(string id)
    {
        EnsureBuilt();
        if (string.IsNullOrEmpty(id)) return null;
        return _byId.TryGetValue(id, out var it) ? it : null;
    }

    /// <summary>All registered items.</summary>
    public static IEnumerable<ItemData> All { get { EnsureBuilt(); return _byId.Values; } }

    // ---------------------------------------------------------------
    //  PROGRAMMATIC ROSTER
    // ---------------------------------------------------------------

    private static ItemData Item(string id, string display, ItemType type, int value,
        int stack = 99, float weight = 0.5f, Color? tint = null)
    {
        var d = ScriptableObject.CreateInstance<ItemData>();
        d.name = "Item_" + id;
        d.id = id;
        d.displayName = display;
        d.Type = type;
        d.BaseValue = value;
        d.MaxStack = stack;
        d.Weight = weight;
        d.Tint = tint ?? Color.white;
        return d;
    }

    private static ItemData[] BuildAll()
    {
        var items = new List<ItemData>
        {
            // ── Materials (biome resources, game-design §7.1) ──
            Item("crop", "Crops", ItemType.Material, 4),                       // Plains
            Item("herb", "Herbs", ItemType.Material, 5),                       // Plains
            Item("wood", "Wood", ItemType.Material, 3),                        // Forest
            Item("mushroom", "Mushrooms", ItemType.Material, 6),               // Forest
            Item("ore", "Ore", ItemType.Material, 8),                          // Mountains
            Item("gem", "Gems", ItemType.Material, 20),                        // Mountains
            Item("rare_herb", "Rare Herbs", ItemType.Material, 12),            // Swamp
            Item("poison", "Poisons", ItemType.Material, 10),                  // Swamp
            Item("cactus_fruit", "Cactus Fruit", ItemType.Material, 6),        // Desert
            Item("ancient_relic", "Ancient Relics", ItemType.Material, 25),    // Desert
            Item("frost_crystal", "Frost Crystals", ItemType.Material, 18),    // Tundra
            Item("obsidian", "Obsidian", ItemType.Material, 16),               // Volcanic
            Item("fire_essence", "Fire Essence", ItemType.Material, 28),       // Volcanic
            Item("dark_crystal", "Dark Crystals", ItemType.Material, 30),      // Deep
            Item("pearl", "Pearls", ItemType.Material, 22),                    // Ocean
            Item("coral", "Coral", ItemType.Material, 9),                      // Ocean

            // ── Weapon artifacts (drops) ──
            Item("wolf_fang", "Wolf Fang", ItemType.Weapon, 12, 20),
            Item("treant_bark", "Treant Bark", ItemType.Weapon, 15, 20),
            Item("golem_core", "Golem Core", ItemType.Weapon, 35, 5),
            Item("drake_scale", "Drake Scale", ItemType.Weapon, 40, 5),
            Item("scorpion_stinger", "Scorpion Stinger", ItemType.Weapon, 14, 20),
            Item("yeti_claw", "Yeti Claw", ItemType.Weapon, 32, 10),
            Item("demon_horn", "Demon Horn", ItemType.Weapon, 45, 5),

            // ── Consumables ──
            Item("healing_potion", "Healing Potion", ItemType.Consumable, 10, 10),
            Item("focus_potion", "Focus Potion", ItemType.Consumable, 10, 10),
            Item("stamina_potion", "Stamina Potion", ItemType.Consumable, 8, 10),
            Item("ritual_stone", "Ritual Stone", ItemType.Consumable, 100, 1),   // race re-roll
            Item("cooked_meat", "Cooked Meat", ItemType.Consumable, 6, 25),

            // ── Skill books ──
            Item("skill_book_charge", "Skill Book: Charge", ItemType.SkillBook, 50, 1),
            Item("skill_book_burst", "Skill Book: Burst", ItemType.SkillBook, 50, 1),
            Item("skill_book_heal", "Skill Book: Lesser Heal", ItemType.SkillBook, 60, 1),

            // ── Armor / gear drops ──
            Item("leather_scrap", "Leather Scrap", ItemType.Armor, 7, 30),
            Item("iron_ingot", "Iron Ingot", ItemType.Armor, 12, 20),
        };

        return items.ToArray();
    }
}
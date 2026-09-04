using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Static registry of discoverable crafting recipes (planning Task 6.3, game-design §5.3).
/// Recipes craft weapons, armor and potions from <see cref="ItemDatabase"/> materials, feeding
/// the shared item/economy pipeline. <see cref="RecipeDiscovery"/> gates which recipes are known.
/// </summary>
public static class RecipeRegistry
{
    private static readonly Dictionary<string, RecipeData> _byId = new Dictionary<string, RecipeData>();
    private static readonly List<RecipeData> _all = new List<RecipeData>();
    private static bool _built;

    private static void EnsureBuilt()
    {
        if (_built) return;
        _built = true;
        Register(BuildAll());
    }

    private static void Register(IEnumerable<RecipeData> recipes)
    {
        foreach (var r in recipes)
        {
            if (r == null || string.IsNullOrEmpty(r.Id)) continue;
            if (!_byId.ContainsKey(r.Id))
            {
                _byId.Add(r.Id, r);
                _all.Add(r);
            }
        }
    }

    public static List<RecipeData> All
    {
        get { EnsureBuilt(); return new List<RecipeData>(_all); }
    }

    public static List<RecipeData> OfKind(RecipeKind kind)
    {
        EnsureBuilt();
        return _all.FindAll(r => r.Kind == kind);
    }

    public static RecipeData Get(string id)
    {
        EnsureBuilt();
        if (string.IsNullOrEmpty(id)) return null;
        return _byId.TryGetValue(id, out var r) ? r : null;
    }

    // ---------------------------------------------------------------
    //  PROGRAMMATIC ROSTER (weapons, armor, potions, food)
    // ---------------------------------------------------------------

    private static RecipeData Make(string id, string name, RecipeKind kind, string result,
        bool discovery, string skillBook, params IngredientSpec[] ings)
    {
        var r = ScriptableObject.CreateInstance<RecipeData>();
        r.name = "Recipe_" + id;
        r.Id = id;
        r.DisplayName = name;
        r.Kind = kind;
        r.ResultItemId = result;
        r.ResultCount = 1;
        r.RequiresDiscovery = discovery;
        r.DiscoverySkillBookId = skillBook;
        r.Ingredients = ings;
        return r;
    }

    private static IngredientSpec Ing(string itemId, int count = 1)
    {
        return new IngredientSpec { ItemId = itemId, Count = count };
    }

    private static RecipeData[] BuildAll()
    {
        return new[]
        {
            // ── RangedWeapon (weapons crafted from materials) ──
            Make("craft_treant_bow", "Treant Bow", RecipeKind.RangedWeapon, "treant_bark", false, null,
                Ing("wood", 4), Ing("herb", 1)),
            Make("craft_bone_dagger", "Bone Dagger", RecipeKind.RangedWeapon, "wolf_fang", true, "skill_book_charge",
                Ing("wolf_fang", 2), Ing("leather_scrap", 1)),
            Make("craft_iron_blade", "Iron Blade", RecipeKind.RangedWeapon, "golem_core", true, "skill_book_burst",
                Ing("iron_ingot", 3), Ing("ore", 2)),

            // ── Armor (guards from iron/leather) ──
            Make("craft_leather_tunic", "Leather Tunic", RecipeKind.Armor, "leather_scrap", false, null,
                Ing("leather_scrap", 4), Ing("herb", 1)),
            Make("craft_iron_helmet", "Iron Helmet", RecipeKind.Armor, "iron_ingot", true, "skill_book_charge",
                Ing("iron_ingot", 2), Ing("obsidian", 1)),

            // ── Potions (consumables from herbs/essences) ──
            Make("craft_healing_potion", "Healing Potion", RecipeKind.Potion, "healing_potion", false, null,
                Ing("herb", 2), Ing("crop", 1)),
            Make("craft_focus_potion", "Focus Potion", RecipeKind.Potion, "focus_potion", false, null,
                Ing("fire_essence", 1), Ing("rare_herb", 1)),
            Make("craft_stamina_potion", "Stamina Potion", RecipeKind.Potion, "stamina_potion", false, null,
                Ing("cactus_fruit", 1), Ing("herb", 1)),

            // ── Food (cooked from caught/gathered goods) ──
            Make("craft_cooked_meat", "Cooked Meat", RecipeKind.Food, "cooked_meat", false, null,
                Ing("crop", 1), Ing("herb", 1)),
        };
    }
}
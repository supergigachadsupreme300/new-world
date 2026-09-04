using System;
using UnityEngine;

/// <summary>
/// A single discoverable crafting recipe (planning Task 6.3, game-design §5.3). Result and
/// ingredients reference the open-world <see cref="ItemDatabase"/> ids so crafted weapons,
/// armor and potions flow through the shared item economy. A recipe may be gated behind a
/// skill book copy so it is only crafted after discovery.
/// </summary>
[CreateAssetMenu(fileName = "Recipe", menuName = "New World/Crafting/Recipe", order = 80)]
public class RecipeData : ScriptableObject
{
    [Tooltip("Stable id (e.g. 'craft_iron_sword').")]
    public string Id;
    [Tooltip("Display recipe name.")]
    public string DisplayName;

    [Header("Station")]
    [Tooltip("Which crafting station kind crafts this (RangedWeapon/Armor/Potion/Food).")]
    public RecipeKind Kind;

    [Header("Result")]
    [Tooltip("ItemDatabase id granted on craft (weapon/armor/consumable).")]
    public string ResultItemId;
    [Tooltip("Count of the result granted per craft.")]
    public int ResultCount = 1;

    [Header("Ingredients")]
    public IngredientSpec[] Ingredients;

    [Header("Discovery")]
    [Tooltip("If true the recipe is locked until the matching skill book is consumed.")]
    public bool RequiresDiscovery = true;
    [Tooltip("ItemDatabase id of the skill book that unlocks this recipe.")]
    public string DiscoverySkillBookId;
}

public enum RecipeKind
{
    RangedWeapon = 0,
    Armor = 1,
    Potion = 2,
    Food = 3
}

[Serializable]
public class IngredientSpec
{
    public string ItemId;
    public int Count = 1;
}
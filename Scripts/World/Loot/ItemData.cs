using UnityEngine;

/// <summary>
/// Item category taxonomy (planning Task 5.2). Mirrors the inventory/equipment split and
/// the listed item kinds — weapons, armor, consumables, materials, and skill books.
/// </summary>
public enum ItemType
{
    Weapon = 0,
    Armor = 1,
    Consumable = 2,
    Material = 3,
    SkillBook = 4,
    Ammo = 5,
    Quest = 6
}

/// <summary>
/// A single item definition (planning Task 5.2). Data-only ScriptableObject used by the
/// <see cref="ItemDatabase"/> and <see cref="LootTable"/>. Adding an item = create an asset
/// or add a programmatic entry — no game-code changes.
/// </summary>
[CreateAssetMenu(fileName = "Item", menuName = "New World/Loot/Item", order = 70)]
public class ItemData : ScriptableObject
{
    [Tooltip("Stable id used by loot tables, equipment, and vendors (e.g. 'herb').")]
    public string id;
    public string displayName;
    public ItemType Type;
    [TextArea] public string description;

    [Header("Stacking / Value")]
    [Tooltip("Max count per inventory stack.")]
    public int MaxStack = 99;
    [Tooltip("Base vendor value (credits). Luck inflates drops, not value.)")]
    public int BaseValue;
    [Tooltip("Weight in kg for equip-load / carry limits.")]
    public float Weight;

    [Header("Visual")]
    [Tooltip("Icon/stage tint for generated pickups.")]
    public Color Tint = Color.white;
    [Tooltip("Placeholder pickup prefab (real art drops in later).")]
    public GameObject PickupPrefab;

    /// <summary>True if the item is A common stackable resource (materials/consumables).</summary>
    public bool IsStackable => MaxStack > 1;
}
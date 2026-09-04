using System;
using UnityEngine;

/// <summary>
/// Open-world NPC roles (planning Task 6.4, game-design §7.2). Composes the existing
/// interaction contracts: a Vendor opens the shipped <see cref="VendorShopManager"/> in a
/// mode, a QuestGiver feeds the quest tracker, and a Follower toggles a simplified companion
/// follow. Replaces hand-authored CountryLife-only NPC roles with a data-driven one.
/// </summary>
public enum NpcRoleKind
{
    Vendor = 0,
    QuestGiver = 1,
    Follower = 2
}

/// <summary>
/// Data-only definition of an open-world NPC (planning Task 6.4). Holds its role, display
/// name, greeting/dialog lines, the vendor shop mode it opens, and a friendship-gift category
/// resolved through the simplified friendship adapter.
/// </summary>
[CreateAssetMenu(fileName = "Npc", menuName = "New World/NPC/Npc", order = 90)]
public class NpcDefinition : ScriptableObject
{
    [Tooltip("Stable id (e.g. 'plains_vendor_1').")]
    public string Id;
    public string DisplayName;
    public NpcRoleKind Role = NpcRoleKind.Vendor;

    [Header("Vendor / Economy")]
    [Tooltip("Which VendorShopManager mode to open for a Vendor role.")]
    public NpcShopMode ShopMode = NpcShopMode.Tools;
    [Tooltip("Discount multiplier applied through the friendship adapter (1 = none).")]
    [Range(0.5f, 1f)] public float BaseDiscount = 1f;

    [Header("Dialog")]
    public string[] GreetingLines;

    [Header("Quest")]
    [Tooltip("Quest id advanced on interact for a QuestGiver.")]
    public string QuestObjective;

    [Header("Companion")]
    [Tooltip("Optional follower display name; empty = no follow/perk.")]
    public string CompanionPerkDescription;
}

/// <summary>
/// Vendor shop modes the shipped <see cref="VendorShopManager"/> supports. Mirrors its Open*()
/// entry points so a Vendor NPC maps one-to-one onto an existing shop.
/// </summary>
public enum NpcShopMode
{
    Vendor = 0,
    Tools = 1,
    Convenience = 2,
    Grocery = 3,
    Fishing = 4,
    Restaurant = 5,
    Cafe = 6
}
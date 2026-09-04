using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Minimal equipment system (Phase 8 / Task 8.2 "Inventory/Equipment UI"). The repo has no
/// equipment API, so this lightweight component backs the equipment UI: it holds equip slots
/// (Weapon, Armor) keyed by item ids and reports which inventory items are equippable. It does
/// NOT alter live combat — the inventory/equipment UI composes it for display and simple
/// equipping/unequipping. Add to the same object as the player if equipment display is desired.
/// </summary>
public sealed class EquipmentSystem : MonoBehaviour
{
    public enum Slot { Weapon = 0, Armor = 1 }

    private readonly Dictionary<Slot, string> _equipped = new Dictionary<Slot, string>();

    /// <summary>True if the item id is considered equippable in one of the known slots.</summary>
    public static bool IsEquippable(string itemId)
    {
        if (string.IsNullOrEmpty(itemId)) return false;
        switch (itemId)
        {
            case "wolf_fang":
            case "treant_bark":
            case "golem_core":
            case "iron_ingot":
                return true;
            default:
                return false;
        }
    }

    public static Slot SlotFor(string itemId)
    {
        switch (itemId)
        {
            case "treant_bark":
            case "golem_core":
                return Slot.Armor;
            default:
                return Slot.Weapon;
        }
    }

    public bool Equip(string itemId)
    {
        if (!IsEquippable(itemId)) return false;
        _equipped[SlotFor(itemId)] = itemId;
        return true;
    }

    public bool Unequip(Slot slot) => _equipped.Remove(slot);

    public bool IsEquipped(Slot slot, string itemId = null)
    {
        if (!_equipped.TryGetValue(slot, out var cur)) return false;
        return itemId == null || cur == itemId;
    }

    public string Get(Slot slot) => _equipped.TryGetValue(slot, out var cur) ? cur : null;

    public void Clear()
    {
        _equipped.Clear();
    }
}
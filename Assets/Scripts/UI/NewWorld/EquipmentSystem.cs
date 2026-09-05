using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 21-slot equipment manager (game-design §5.4): 5 armor (Head/Body/Glove/Legging/Feet),
/// 2 weapon (Left/Right Hand), and 14 accessory (10 Fingers, Necklace, 2 Ears, Belt). Backs
/// the humanoid Equipment tab with equipped-<see cref="GearDef"/> lookups and aggregate stat
/// surfaces (weight/equip-load, flat physical DR, per-type resist, passive stat bonuses).
///
/// This layer is display/data — it does NOT alter live combat numbers. Weapon slots are meant
/// to mirror the armed hand weapons (see <see cref="WeaponRigBuilder"/>); armor/accessory slots
/// report DR + resist so the UI can show what a loadout provides. Add to the same object as the
/// player if equipment display is desired.
/// </summary>
[DisallowMultipleComponent]
public sealed class EquipmentSystem : MonoBehaviour
{
    public const int SlotCount = 21;

    private readonly EquipSlot[] _order =
    {
        EquipSlot.Head, EquipSlot.Body, EquipSlot.Glove, EquipSlot.Legging, EquipSlot.Feet,
        EquipSlot.LeftHand, EquipSlot.RightHand,
        EquipSlot.Finger1, EquipSlot.Finger2, EquipSlot.Finger3, EquipSlot.Finger4, EquipSlot.Finger5,
        EquipSlot.Finger6, EquipSlot.Finger7, EquipSlot.Finger8, EquipSlot.Finger9, EquipSlot.Finger10,
        EquipSlot.Necklace, EquipSlot.Ear1, EquipSlot.Ear2, EquipSlot.Belt,
    };

    private readonly Dictionary<EquipSlot, string> _equipped = new Dictionary<EquipSlot, string>();

    /// <summary>All 21 slots in canonical order.</summary>
    public IReadOnlyList<EquipSlot> AllSlots => _order;

    /// <summary>Genre of any of the 21 slots.</summary>
    public static EquipGenre GenreOf(EquipSlot slot)
    {
        if (slot >= EquipSlot.LeftHand && slot <= EquipSlot.RightHand) return EquipGenre.Weapon;
        if (slot >= EquipSlot.Finger1) return EquipGenre.Accessory;
        return EquipGenre.Armor;
    }

    /// <summary>Short display label for a slot.</summary>
    public static string SlotLabel(EquipSlot slot)
    {
        switch (slot)
        {
            case EquipSlot.Head: return "Head";
            case EquipSlot.Body: return "Body";
            case EquipSlot.Glove: return "Glove";
            case EquipSlot.Legging: return "Legging";
            case EquipSlot.Feet: return "Feet";
            case EquipSlot.LeftHand: return "L. Hand";
            case EquipSlot.RightHand: return "R. Hand";
            case EquipSlot.Necklace: return "Necklace";
            case EquipSlot.Ear1: return "Ear"; 
            case EquipSlot.Ear2: return "Ear";
            case EquipSlot.Belt: return "Belt";
            default:
            {
                int idx = slot - EquipSlot.Finger1 + 1;
                return "Ring " + idx;
            }
        }
    }

    public bool Equip(string itemId)
    {
        if (!GearCatalog.TrySlotFor(itemId, out var slot)) return false;
        _equipped[slot] = itemId;
        return true;
    }

    public bool Unequip(EquipSlot slot) => _equipped.Remove(slot);

    public bool UnequipItem(string itemId) => _equipped.Remove(GearSlotOf(itemId));

    public bool IsEquipped(EquipSlot slot, string itemId = null)
    {
        if (!_equipped.TryGetValue(slot, out var cur)) return false;
        return itemId == null || cur == itemId;
    }

    public string Get(EquipSlot slot) => _equipped.TryGetValue(slot, out var cur) ? cur : null;

    public GearDef GetDef(EquipSlot slot)
    {
        var id = Get(slot);
        return id != null ? GearCatalog.Find(id) : null;
    }

    public void Clear() => _equipped.Clear();

    public int Count => _equipped.Count;

    /// <summary>The slot a gear item would occupy, or None if not gear.</summary>
    public static EquipSlot GearSlotOf(string itemId)
    {
        if (GearCatalog.TrySlotFor(itemId, out var slot)) return slot;
        return (EquipSlot)(-1);
    }

    // ── Aggregate surfaces (display) ───────────────────────────────────────

    /// <summary>Total equip weight from all worn gear.</summary>
    public float TotalWeight
    {
        get
        {
            float w = 0f;
            foreach (var kv in _equipped)
            {
                var g = GearCatalog.Find(kv.Value);
                if (g != null) w += g.Weight;
            }
            return w;
        }
    }

    /// <summary>Total flat physical damage reduction from worn armor (%).</summary>
    public float TotalPhysicalDR
    {
        get
        {
            float dr = 0f;
            foreach (var kv in _equipped)
            {
                var g = GearCatalog.Find(kv.Value);
                if (g != null) dr += g.Resist[(int)DamageType.Physical];
            }
            return dr;
        }
    }

    /// <summary>Total elemental/magic resistance for a damage type (%).</summary>
    public float Resistance(DamageType type)
    {
        float r = 0f;
        foreach (var kv in _equipped)
        {
            var g = GearCatalog.Find(kv.Value);
            if (g != null) r += g.Resist[(int)type];
        }
        return r;
    }

    /// <summary>Total flat stat bonus granted by worn accessories.</summary>
    public float StatBonus(StatType stat)
    {
        float s = 0f;
        foreach (var kv in _equipped)
        {
            var g = GearCatalog.Find(kv.Value);
            if (g != null) s += g.StatBonus[(int)stat];
        }
        return s;
    }
}
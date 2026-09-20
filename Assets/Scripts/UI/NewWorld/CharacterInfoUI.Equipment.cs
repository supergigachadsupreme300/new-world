/// <summary>Character Info - Inventory tab humanoid 21-slot equipment sheet and hand-slot weapon equip/cycle/unequip logic (drag-drop hooks, rig handling).</summary>
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using TMPro;

public sealed partial class CharacterInfoUI
{

    // ── Weapon equipping (hand slots) ───────────────────────────────────────
    // Weapons are normal inventory items now: drag one from the grid/hotbar onto
    // the LHand/RHand slot (WeaponDropTarget) or click a hand slot to equip/cycle.

    public void EquipWeaponFromDrop(string weaponId, EquipSlot slot)
    {
        if (string.IsNullOrEmpty(weaponId)) return;
        _selectedWeaponId = weaponId;
        EquipOwnedWeapon(weaponId, slot);
    }

    public void OnDragDropEnded()
    {
        RefreshInventoryUi();
    }

    public void OnItemDragEnded()
    {
        RefreshInventoryUi();
    }

    /// <summary>Refresh all inventory-side displays (storage grid, use bar, sheet).</summary>
    public void RefreshInventoryUi()
    {
        RefreshInventory();
        RefreshEquipment();
    }

    private void EquipOwnedWeapon(string weaponId, EquipSlot slot)
    {
        var player = GameManager.Instance?.Player;
        var combat = CombatOf();
        if (player == null || combat == null) return;
        // Fists are innate — never granted/equipped as a bag item.
        if (weaponId == WeaponCatalog.FistWeaponId) return;

        var weapon = WeaponCatalog.Find(weaponId);
        if (weapon == null) return;

        // Dual-wield rule: the same weapon id CAN occupy both hands (one rig = one owned copy), but
        // only when a spare copy still exists in the bag — RemoveItemAmount below consumes it.
        // Without a spare (single owned sword / starter auto-equip), dropping onto the other hand
        // MOVES it there (drop that rig first) so an item is never duplicated onto the body. Already
        // on the drop hand → keep the rig in place.
        bool targetHolds = slot == EquipSlot.LeftHand
            ? RigHolds(combat.LeftHand, weaponId)
            : RigHolds(combat.RightHand, weaponId);
        if (targetHolds)
        {
            _selectedWeaponId = "";
            RefreshInventoryUi();
            return;
        }
        bool otherHolds = slot == EquipSlot.LeftHand
            ? RigHolds(combat.RightHand, weaponId)
            : RigHolds(combat.LeftHand, weaponId);
        if (otherHolds)
        {
            var spareCopies = ToolManager.Instance != null ? ToolManager.Instance.CountItem(weaponId) : 0;
            if (spareCopies < 1)
            {
                if (slot == EquipSlot.LeftHand)
                {
                    Destroy(combat.RightHand);
                    combat.RightHand = null;
                }
                else
                {
                    Destroy(combat.LeftHand);
                    combat.LeftHand = null;
                }
                combat.SetTwoHand(false);
            }
        }

        // Never lose the currently equipped weapon: anything that's about to be cleared by
        // EquipInto goes back into the bag first. If the bag can't hold it, abort the swap so
        // nothing disappears (re-equipping the same id on the other hand skips this).
        string replaced = ReplacedWeaponId(combat, weaponId, slot);
        if (!string.IsNullOrEmpty(replaced))
        {
            var tm = ToolManager.Instance;
            if (tm != null && !tm.CanHoldItem(replaced))
            {
                GameManager.Instance?.UIManager?.ShowMessage(Localization.T("Túi đồ đầy."), 1.5f);
                return;
            }
            tm?.AddItem(replaced, 1);
        }

        var rig = WeaponRigBuilder.EquipInto(player.gameObject, weapon, slot == EquipSlot.LeftHand);
        if (rig == null) return;
        // Match the new weapon's visual pose to the current state (drawn only while fighting,
        // stowed on the body in casual mode — any camera view).
        var pc = player.GetComponent<PlayerController>();
        bool drawn = pc != null && pc.WeaponsDrawn;
        WeaponRigBuilder.ApplyPose(player.gameObject, draw: drawn, instant: true);

        // Equipping takes the weapon out of the bag: it's now on the character. Removing a copy
        // that isn't in the ToolManager inventory (e.g. owned but never picked up as an item, or
        // the starter auto-equip at boot) is a harmless no-op via RemoveItemAmount's false return.
        ToolManager.Instance?.RemoveItemAmount(weaponId, 1);

        _selectedWeaponId = "";
        RefreshInventoryUi();
    }

    /// <summary>The weapon id held on the target hand that a new equip into that slot would clear, if any.</summary>
    private static string ReplacedWeaponId(CombatController combat, string incomingId, EquipSlot slot)
    {
        var rig = slot == EquipSlot.LeftHand ? combat.LeftHand : combat.RightHand;
        var host = rig != null ? rig.GetComponent<WeaponRigHost>() : null;
        // Bare fists are innate (never a bag item) — EquipInto clears them itself.
        if (host != null && host.Data != null && host.Data.id != incomingId && host.Data.id != WeaponCatalog.FistWeaponId)
            return host.Data.id;
        return null;
    }

    /// <summary>
    /// Remove <paramref name="weaponId"/> from the hands (single / two-hand / dual-wield mirrors)
    /// and return it to the backpack so equipment can be dragged back out of a slot.
    /// <paramref name="destSlot"/> is the specific sheet cell it was dropped onto (weapon lands
    /// there instead of the first free slot). <paramref name="sourceHand"/> is the hand slot the
    /// drag started from — only that hand is unequipped (so a second identical weapon on the other
    /// hand stays equipped); when absent, every hand holding the weapon is cleared.
    /// </summary>
    public void UnequipWeapon(string weaponId, int destSlot = -1, EquipSlot sourceHand = (EquipSlot)(-1))
    {
        if (string.IsNullOrEmpty(weaponId)) return;
        // Bare fists can't be unequipped/dropped — they are the empty-hand combat mode itself.
        if (weaponId == WeaponCatalog.FistWeaponId) return;
        var combat = CombatOf();
        if (combat == null) return;

        bool equipped = RigHolds(combat.RightHand, weaponId) || RigHolds(combat.LeftHand, weaponId);
        if (!equipped) return;

        // Bag full → keep the weapon equipped rather than let it vanish.
        var tm = ToolManager.Instance;
        if (tm != null && !tm.CanHoldItem(weaponId))
        {
            GameManager.Instance?.UIManager?.ShowMessage(Localization.T("Túi đồ đầy."), 1.5f);
            return;
        }

        int removed = 0;
        if ((int)sourceHand >= 0)
        {
            var handRig = sourceHand == EquipSlot.LeftHand ? combat.LeftHand : combat.RightHand;
            if (handRig != null && RigHolds(handRig, weaponId))
            {
                Destroy(handRig);
                if (sourceHand == EquipSlot.LeftHand) combat.LeftHand = null;
                else combat.RightHand = null;
                removed++;
            }
        }
        else
        {
            if (combat.RightHand != null && RigHolds(combat.RightHand, weaponId))
            {
                Destroy(combat.RightHand);
                combat.RightHand = null;
                removed++;
            }
            if (combat.LeftHand != null && RigHolds(combat.LeftHand, weaponId))
            {
                Destroy(combat.LeftHand);
                combat.LeftHand = null;
                removed++;
            }
        }
        if (removed <= 0) return;

        combat.SetTwoHand(false);
        if (destSlot >= 0) tm?.PutItem(weaponId, removed, destSlot);
        else tm?.AddItem(weaponId, removed);
        RefreshInventoryUi();
    }

    private static bool RigHolds(GameObject rig, string weaponId)
    {
        var host = rig != null ? rig.GetComponent<WeaponRigHost>() : null;
        return host != null && host.Data != null && host.Data.id == weaponId;
    }

    // ── Humanoid 21-slot equipment sheet (§5.4) ────────────────────────────
    private void BuildEquipmentSheet(Transform parent)
    {
        GearCatalog.EnsureBuilt();
        _equipSlotLabels.Clear();

        SlotButton(parent, "Ear1",        EquipSlot.Ear1,     new Vector2(-200f, -10f));
        SlotButton(parent, "Head",        EquipSlot.Head,     new Vector2(-105f, -10f));
        SlotButton(parent, "Ear2",        EquipSlot.Ear2,     new Vector2(-10f, -10f));
        SlotButton(parent, "Necklace",    EquipSlot.Necklace, new Vector2(-105f, -54f));
        SlotButton(parent, "LHand",       EquipSlot.LeftHand, new Vector2(-200f, -98f));
        SlotButton(parent, "Body",        EquipSlot.Body,     new Vector2(-105f, -98f));
        SlotButton(parent, "RHand",       EquipSlot.RightHand,new Vector2(-10f, -98f));
        SlotButton(parent, "Glove",       EquipSlot.Glove,    new Vector2(-200f, -142f));
        SlotButton(parent, "Belt",        EquipSlot.Belt,     new Vector2(-105f, -142f));
        SlotButton(parent, "Legging",     EquipSlot.Legging,  new Vector2(-105f, -186f));
        SlotButton(parent, "Feet",        EquipSlot.Feet,     new Vector2(-105f, -230f));
        SlotButton(parent, "Finger1",     EquipSlot.Finger1,  new Vector2(-270f, -10f));
        SlotButton(parent, "Finger2",     EquipSlot.Finger2,  new Vector2(-270f, -54f));
        SlotButton(parent, "Finger3",     EquipSlot.Finger3,  new Vector2(-270f, -98f));
        SlotButton(parent, "Finger4",     EquipSlot.Finger4,  new Vector2(-270f, -142f));
        SlotButton(parent, "Finger5",     EquipSlot.Finger5,  new Vector2(-270f, -186f));
        SlotButton(parent, "Finger6",     EquipSlot.Finger6,  new Vector2(62f, -10f));
        SlotButton(parent, "Finger7",     EquipSlot.Finger7,  new Vector2(62f, -54f));
        SlotButton(parent, "Finger8",     EquipSlot.Finger8,  new Vector2(62f, -98f));
        SlotButton(parent, "Finger9",     EquipSlot.Finger9,  new Vector2(62f, -142f));
        SlotButton(parent, "Finger10",    EquipSlot.Finger10, new Vector2(62f, -186f));
    }

    private void SlotButton(Transform parent, string name, EquipSlot slot, Vector2 pos)
    {
        var go = new GameObject(name + "Slot");
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2((pos.x + EquipShiftX) * S, (pos.y + 150f) * S);
        rt.sizeDelta = Sz(62f, 42f);
        var img = go.AddComponent<Image>();
        img.color = new Color(0.14f, 0.16f, 0.2f, 0.95f);
        var btn = go.AddComponent<Button>();
        btn.targetGraphic = img;
        EquipSlot captured = slot;
        btn.onClick.AddListener(() => ToggleEquipSlot(captured));

        if (slot == EquipSlot.LeftHand || slot == EquipSlot.RightHand)
        {
            var drop = go.AddComponent<WeaponDropTarget>();
            drop.Slot = slot;
            var drag = go.AddComponent<WeaponDragHandle>();
            drag.Slot = slot;
        }

        EquipSlot capturedSlot = slot;
        go.AddComponent<TooltipSlot>().Bind(() => EquipSlotItemId(capturedSlot));

        var label = new GameObject("Label");
        label.transform.SetParent(go.transform, false);
        var lr = label.AddComponent<RectTransform>();
        lr.anchorMin = Vector2.zero;
        lr.anchorMax = Vector2.one;
        lr.offsetMin = Vector2.zero;
        lr.offsetMax = Vector2.zero;
        var lt = label.AddComponent<TextMeshProUGUI>();
        GameManager.Instance?.UIManager?.ApplyDefaultFont(lt);
        lt.text = EquipmentSystem.SlotLabel(slot);
        lt.fontSize = Mathf.Max(12f, Screen.height / 80f);
        lt.color = new Color(0.75f, 0.78f, 0.85f, 1f);
        lt.alignment = TextAlignmentOptions.Center;
        _equipSlotLabels[slot] = lt;
    }

    private void ToggleEquipSlot(EquipSlot slot)
    {
        if (slot == EquipSlot.LeftHand || slot == EquipSlot.RightHand)
        {
            if (!string.IsNullOrEmpty(_selectedWeaponId))
                EquipOwnedWeapon(_selectedWeaponId, slot);
            else
                CycleWeapon();
            return;
        }
        var equip = EquipmentOf();
        if (equip == null) return;
        if (GearCatalog.TrySlotFor(equip.Get(slot) ?? "", out _))
        {
            equip.Unequip(slot);
        }
        else
        {
            foreach (var g in GearCatalog.All)
            {
                if (g == null || g.Slot != slot) continue;
                if (equip.Get(EquipmentSystem.GearSlotOf(g.id)) == g.id) continue;
                equip.Equip(g.id);
                break;
            }
        }
        RefreshEquipment();
    }

    private void RefreshEquipment()
    {
        var equip = EquipmentOf();
        if (equip == null)
        {
            _equipSummary.text = Localization.T("No equipment system present.");
            return;
        }

        // Slot buttons: label = slot name, value = equipped gear display name.
        foreach (var slot in equip.AllSlots)
        {
            if (!_equipSlotLabels.TryGetValue(slot, out var label)) continue;
            var id = equip.Get(slot);
            var g = id != null ? GearCatalog.Find(id) : null;
            string item = g != null && !string.IsNullOrEmpty(g.displayName) ? g.displayName : (id ?? "—");
            label.text = EquipmentSystem.SlotLabel(slot) + "\n" + item;
        }

        // Weapons are tracked by CombatController, not the gear sheet.
        var combat = CombatOf();
        _equipSlotLabels.TryGetValue(EquipSlot.LeftHand, out var lh);
        _equipSlotLabels.TryGetValue(EquipSlot.RightHand, out var rh);
        if (lh != null)
            lh.text = EquipmentSystem.SlotLabel(EquipSlot.LeftHand) + "\n" + HandName(combat != null ? combat.LeftHand : null);
        if (rh != null)
            rh.text = EquipmentSystem.SlotLabel(EquipSlot.RightHand) + "\n" + HandName(combat != null ? combat.RightHand : null);

        StringBuilder sb = new StringBuilder();
        sb.Append(Localization.F("Wield: {0}", combat != null ? combat.Wielding.ToString() : "—"));
        sb.Append(Localization.F("   Equipped: {0}/21", equip.Count));
        sb.Append(Localization.F("   Weight: {0:0.0}", equip.TotalWeight)).Append('\n');
        sb.Append(Localization.F("Physical DR: {0:0.#}%", equip.TotalPhysicalDR)).Append("   ");
        string[] resTypes = { "Fire", "Ice", "Lightning", "Holy", "Dark", "Wind", "Earth", "Water", "Arcane" };
        for (int i = 0; i < resTypes.Length; i++)
        {
            float r = equip.Resistance((DamageType)(i + 1));
            if (r > 0.01f)
                sb.Append(resTypes[i]).Append(" ").Append(r.ToString("0.#")).Append("% ");
        }
        _equipSummary.text = sb.ToString();
    }

    private static string HandName(GameObject hand)
    {
        var host = hand != null ? hand.GetComponent<WeaponRigHost>() : null;
        if (host != null && host.Data != null && !string.IsNullOrEmpty(host.Data.displayName))
            return host.Data.displayName;
        return hand != null ? hand.name : "—";
    }

    private void CycleWeapon()
    {
        var player = GameManager.Instance?.Player;
        var combat = CombatOf();
        var inv = player != null ? player.GetComponent<WeaponInventory>() : null;
        if (player == null || combat == null) return;

        WeaponCatalog.EnsureBuilt();
        var all = WeaponCatalog.All;
        if (all == null || all.Count == 0) return;

        var owned = new List<string>();
        if (inv != null) owned.AddRange(inv.Owned);
        if (owned.Count == 0 || !owned.Contains(WeaponCatalog.StarterWeaponId))
            owned.Insert(0, WeaponCatalog.StarterWeaponId);

        int idx = 0;
        var cur = combat.RightHand != null ? combat.RightHand.GetComponent<WeaponRigHost>() : null;
        if (cur != null && cur.Data != null)
            for (int i = 0; i < owned.Count; i++)
            {
                var target = WeaponCatalog.Find(owned[i]);
                if (target != null && target.id == cur.Data.id) { idx = i; break; }
            }
        idx = (idx + 1) % owned.Count;

        var next = WeaponCatalog.Find(owned[idx]);
        if (next != null)
        {
            WeaponRigBuilder.EquipInto(player.gameObject, next);
            var pc = player.GetComponent<PlayerController>();
            bool drawn = pc != null && pc.WeaponsDrawn;
            WeaponRigBuilder.ApplyPose(player.gameObject, draw: drawn, instant: true);
        }
        RefreshEquipment();
    }

    /// <summary>Live item id equipped in a gear slot, or null when empty (for hover tooltips).</summary>
    private string EquipSlotItemId(EquipSlot slot)
    {
        var equip = EquipmentOf();
        return equip != null ? equip.Get(slot) : null;
    }
}
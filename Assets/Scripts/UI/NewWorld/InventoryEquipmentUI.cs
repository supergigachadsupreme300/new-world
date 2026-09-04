using System.Text;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Phase 8 (Task 8.2): Inventory / Equipment UI. Lists the player's 10 inventory slots
/// (via <see cref="ToolManager.PeekSlot"/>), money, and the two equipment slots (Weapon/Armor)
/// exposed by <see cref="EquipmentSystem"/>, allowing simple equipping/unequipping. Composes the
/// existing <see cref="ToolManager"/> / <see cref="PlayerController"/> without rewriting them.
/// </summary>
public sealed class InventoryEquipmentUI : MenuPanelBase
{
    public const int SlotCount = 10;
    private TMP_Text _inventoryLine;
    private TMP_Text _equipLine;
    private TMP_Text _moneyLine;
    private ToolManager _tm;
    private EquipmentSystem _equip;
    private int _selectedSlot;

    private void OnEnable()
    {
        Build(Localization.T("INVENTORY / EQUIPMENT"));
        _inventoryLine = MakeBodyText(BodyRow, "Inventory", new Vector2(-220f, 150f));
        _equipLine = MakeBodyText(BodyRow, "Equipment", new Vector2(60f, 150f));
        _moneyLine = MakeBodyText(BodyRow, "Money", new Vector2(-220f, -150f));
    }

    private TMP_Text MakeBodyText(RectTransform parent, string name, Vector2 pos)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = new Vector2(480f, 240f);
        var tmp = go.AddComponent<TextMeshProUGUI>();
        GameManager.Instance?.UIManager?.ApplyDefaultFont(tmp);
        tmp.fontSize = Mathf.Max(12f, Screen.height / 60f);
        tmp.color = Color.white;
        tmp.alignment = TextAlignmentOptions.Left;
        return tmp;
    }

    protected override void Refresh()
    {
        var gm = GameManager.Instance;
        _tm = ToolManager.Instance;
        var equipHost = FindEquipmentHost();
        _equip = equipHost != null ? equipHost.GetComponent<EquipmentSystem>() : null;
        var player = gm != null ? gm.Player : null;

        // Inventory.
        StringBuilder inv = new StringBuilder();
        for (int i = 0; i < SlotCount; i++)
        {
            var slot = _tm != null ? _tm.PeekSlot(i) : null;
            string label = slot == null || slot.Type == null || slot.Count <= 0
                ? "(trống)"
                : Localization.ItemName(slot.Type) + " x" + slot.Count;
            inv.Append(i + 1).Append(": ").Append(label);
            if (i != SlotCount - 1) inv.Append("\n");
        }
        _inventoryLine.text = inv.ToString();

        // Money.
        _moneyLine.text = Localization.F("Tiền: {0}", player != null ? player.Money : 0L);

        // Equipment.
        StringBuilder eq = new StringBuilder();
        if (_equip != null)
        {
            eq.Append("Weapon: ").Append(NameOrDash(_equip.Get(EquipmentSystem.Slot.Weapon))).Append("\n");
            eq.Append("Armor:  ").Append(NameOrDash(_equip.Get(EquipmentSystem.Slot.Armor)));
        }
        else
        {
            eq.Append(Localization.T("No equipment system present."));
        }
        _equipLine.text = eq.ToString();
    }

    private static string NameOrDash(string id) => string.IsNullOrEmpty(id) ? "—" : Localization.ItemName(id);

    private Transform FindEquipmentHost()
    {
        var gm = GameManager.Instance;
        if (gm != null && gm.Player != null) return gm.Player.transform;
        return null;
    }

    /// <summary>Equip the selected inventory slot's item if equippable.</summary>
    public void EquipSelected()
    {
        if (_tm == null || _equip == null) return;
        var slot = _tm.PeekSlot(_selectedSlot);
        if (slot == null || slot.Type == null) return;
        if (EquipmentSystem.IsEquippable(slot.Type))
        {
            _equip.Equip(slot.Type);
            Refresh();
        }
    }

    /// <summary>Unequip the given slot.</summary>
    public void Unequip(EquipmentSystem.Slot slot)
    {
        if (_equip != null)
        {
            _equip.Unequip(slot);
            Refresh();
        }
    }

    public void SelectSlot(int index)
    {
        _selectedSlot = Mathf.Clamp(index, 0, SlotCount - 1);
    }
}
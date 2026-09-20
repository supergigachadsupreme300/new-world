/// <summary>Character Info - Inventory tab backpack storage grid, mirrored hotbar row, slot selection, inventory refresh, and slot tooltip id helper.</summary>
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using TMPro;

public sealed partial class CharacterInfoUI
{

    // ── Backpack storage grid (Inventory tab, right side) ───────────────────
    private void BuildStorageGrid(Transform parent)
    {
        var headerRoot = MakeBodyText(parent, "StorageHeader", P(120f, 238f), Sz(280f, 28f));
        headerRoot.text = Localization.T("Backpack (storage)");

        for (int i = 0; i < ToolManager.StorageSlotCount; i++)
        {
            int col = i % 5;
            int row = i / 5;
            int slot = ToolManager.StorageStart + i;

            var go = new GameObject("StorageSlot_" + i);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2((120f + col * 62f) * S, (214f - row * 68f) * S);
            rt.sizeDelta = Sz(58f, 64f);
            var img = go.AddComponent<Image>();
            img.color = SlotColor;
            go.AddComponent<ItemDragHandle>().Slot = slot;
            go.AddComponent<ItemDropTarget>().Slot = slot;
            go.AddComponent<TooltipSlot>().Bind(() => SlotItemId(slot));

            var label = new GameObject("Label");
            label.transform.SetParent(go.transform, false);
            var lr = label.AddComponent<RectTransform>();
            lr.anchorMin = Vector2.zero;
            lr.anchorMax = Vector2.one;
            lr.offsetMin = Vector2.zero;
            lr.offsetMax = Vector2.zero;
            var tmp = label.AddComponent<TextMeshProUGUI>();
            GameManager.Instance?.UIManager?.ApplyDefaultFont(tmp);
            tmp.fontSize = Mathf.Max(12f, Screen.height / 80f);
            tmp.color = Color.white;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.text = (i + 1).ToString();

            _storageImgs[i] = img;
            _storageLabels[i] = tmp;
        }
    }

    // ── Hotbar mirror (Inventory tab, bottom-right of the grid) ─────────────
    private void BuildHotbarMirror(Transform parent)
    {
        MakeBodyText(parent, "UseBarHeader", P(40f, -162f), Sz(300f, 24f))
            .text = Localization.T("Use bar (1-0)");

        for (int i = 0; i < ToolManager.HotbarSlotCount; i++)
        {
            var go = new GameObject("HudInvSlot_" + i);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2((40f + i * 42f) * S, -199f * S);
            rt.sizeDelta = Sz(44f, 50f);
            var img = go.AddComponent<Image>();
            img.color = SlotColor;
            var btn = go.AddComponent<Button>();
            btn.targetGraphic = img;
            int captured = i;
            btn.onClick.AddListener(() => SelectInventorySlot(captured));
            go.AddComponent<ItemDragHandle>().Slot = i;
            go.AddComponent<ItemDropTarget>().Slot = i;
            go.AddComponent<TooltipSlot>().Bind(() => SlotItemId(i));

            var label = new GameObject("Label");
            label.transform.SetParent(go.transform, false);
            var lr = label.AddComponent<RectTransform>();
            lr.anchorMin = Vector2.zero;
            lr.anchorMax = Vector2.one;
            lr.offsetMin = Vector2.zero;
            lr.offsetMax = Vector2.zero;
            var tmp = label.AddComponent<TextMeshProUGUI>();
            GameManager.Instance?.UIManager?.ApplyDefaultFont(tmp);
            tmp.fontSize = Mathf.Max(11f, Screen.height / 88f);
            tmp.color = Color.white;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.text = (i % 10 == 0 ? "10" : i.ToString());

            _invImgs[i] = img;
            _invLabels[i] = tmp;
        }
    }

    private void SelectInventorySlot(int index)
    {
        _invSelected = index;
        ToolManager.Instance?.SelectSlot(index);
        RefreshInventory();
    }

    private void RefreshInventory()
    {
        var tm = ToolManager.Instance;
        var player = GameManager.Instance?.Player;
        int selected = tm != null ? tm.SelectedSlotIndex : -1;

        for (int i = 0; i < ToolManager.StorageSlotCount; i++)
        {
            var slot = tm != null ? tm.PeekSlot(ToolManager.StorageStart + i) : null;
            string body = slot == null || slot.Type == null || slot.Count <= 0
                ? ""
                : WeaponCatalog.DisplayName(slot.Type) + " x" + slot.Count;
            if (_storageLabels[i] != null)
                _storageLabels[i].text = string.IsNullOrEmpty(body) ? (i + 1).ToString() : (i + 1) + " " + body;
            if (_storageImgs[i] != null)
                _storageImgs[i].color = SlotColor;
        }

        for (int i = 0; i < ToolManager.HotbarSlotCount; i++)
        {
            var slot = tm != null ? tm.PeekSlot(i) : null;
            string body = slot == null || slot.Type == null || slot.Count <= 0
                ? ""
                : WeaponCatalog.DisplayName(slot.Type) + " x" + slot.Count;
            if (_invLabels[i] != null)
                _invLabels[i].text = string.IsNullOrEmpty(body)
                    ? (i + 1).ToString()
                    : (i + 1) + "\n" + body;
            if (_invImgs[i] != null)
                _invImgs[i].color = selected == i || _invSelected == i
                    ? SlotSelectedColor
                    : SlotColor;
        }
        if (_moneyLine != null)
            _moneyLine.text = Localization.F("Tiền: {0}", player != null ? player.Money : 0L);
    }

    /// <summary>Live item id in a ToolManager slot, or null when empty (for hover tooltips).</summary>
    private string SlotItemId(int slot)
    {
        var slotInfo = ToolManager.Instance?.PeekSlot(slot);
        return slotInfo != null && slotInfo.Count > 0 ? slotInfo.Type : null;
    }
}
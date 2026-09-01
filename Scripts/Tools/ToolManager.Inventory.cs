using System.Collections.Generic;
using UnityEngine;

public partial class ToolManager
{
    public bool AddItem(string itemType, int amount)
    {
        itemType = NormalizeItemType(itemType);
        if (string.IsNullOrEmpty(itemType) || amount <= 0)
            return false;

        var slot = FindSlotFor(itemType);
        if (slot >= 0)
        {
            _inventory[slot].Count += amount;
            UpdateInventoryUI();
            ShowActiveToolModel();
            return true;
        }

        var empty = FindEmptySlot();
        if (empty < 0)
        {
            _uiManager.ShowMessage(Localization.T("Túi đồ đầy."), 1.5f);
            return false;
        }

        _inventory[empty] = new InventorySlot {Type = itemType, Count = amount};
        UpdateInventoryUI();
        ShowActiveToolModel();
        return true;
    }

    public bool CanHoldItem(string itemType)
    {
        itemType = NormalizeItemType(itemType);
        if (string.IsNullOrEmpty(itemType))
            return false;
        if (FindSlotFor(itemType) >= 0)
            return true;
        return FindEmptySlot() >= 0;
    }

    public bool RemoveItem(int slotIndex, int amount)
    {
        if (slotIndex < 0 || slotIndex >= _inventory.Length)
            return false;

        var slot = _inventory[slotIndex];
        if (slot == null)
            return false;

        slot.Count -= amount;
        if (slot.Count <= 0)
            _inventory[slotIndex] = null;
        UpdateInventoryUI();
        ShowActiveToolModel();
        return true;
    }

    public bool RemoveItemAmount(string itemType, int amount)
    {
        itemType = NormalizeItemType(itemType);
        if (amount <= 0) return true;
        int remaining = amount;
        for (int i = 0; i < _inventory.Length && remaining > 0; i++)
        {
            if (_inventory[i] == null || _inventory[i].Type != itemType)
                continue;
            int take = Mathf.Min(_inventory[i].Count, remaining);
            _inventory[i].Count -= take;
            remaining -= take;
            if (_inventory[i].Count <= 0)
                _inventory[i] = null;
        }
        if (remaining > 0)
            return false;
        UpdateInventoryUI();
        ShowActiveToolModel();
        return true;
    }

    public string GetSelectedItemType()
    {
        if (_selectedSlot < 0 || _selectedSlot >= _inventory.Length)
            return null;
        var slot = _inventory[_selectedSlot];
        return slot?.Type;
    }

    public InventorySlotSave[] GetInventorySave()
    {
        var result = new List<InventorySlotSave>();
        for (int i = 0; i < _inventory.Length; i++)
        {
            if (_inventory[i] != null)
                result.Add(new InventorySlotSave {Slot = i, Type = _inventory[i].Type, Count = _inventory[i].Count});
        }
        return result.ToArray();
    }

    public void LoadInventorySave(InventorySlotSave[] data)
    {
        for (int i = 0; i < _inventory.Length; i++)
            _inventory[i] = null;

        if (data == null)
            return;

        foreach (var slot in data)
        {
            if (slot.Slot >= 0 && slot.Slot < _inventory.Length)
                _inventory[slot.Slot] = new InventorySlot {Type = NormalizeItemType(slot.Type), Count = slot.Count};
        }

        UpdateInventoryUI();
        ShowActiveToolModel();
    }

    public void ClearInventory()
    {
        for (int i = 0; i < _inventory.Length; i++)
            _inventory[i] = null;
        _selectedSlot = 0;
        UpdateInventoryUI();
        ShowActiveToolModel();
    }

    public void SortInventory()
    {
        for (int i = 0; i < _inventory.Length; i++)
        {
            var slot = _inventory[i];
            if (slot == null)
                continue;
            for (int j = i + 1; j < _inventory.Length; j++)
            {
                var other = _inventory[j];
                if (other == null || other.Type != slot.Type)
                    continue;
                slot.Count += other.Count;
                _inventory[j] = null;
            }
        }

        var list = new List<InventorySlot>();
        for (int i = 0; i < _inventory.Length; i++)
        {
            if (_inventory[i] != null)
                list.Add(_inventory[i]);
        }
        list.Sort((a, b) =>
            string.CompareOrdinal(Localization.ItemName(a.Type), Localization.ItemName(b.Type)));

        for (int i = 0; i < _inventory.Length; i++)
            _inventory[i] = i < list.Count ? list[i] : null;

        UpdateInventoryUI();
        ShowActiveToolModel();
        _uiManager?.ShowMessage(Localization.T("Đã sắp xếp túi đồ."), 1.5f);
    }

    private int FindSlotFor(string itemType)
    {
        itemType = NormalizeItemType(itemType);
        for (int i = 0; i < _inventory.Length; i++)
        {
            if (_inventory[i] != null && _inventory[i].Type == itemType)
                return i;
        }
        return -1;
    }

    private string NormalizeItemType(string itemType)
    {
        if (string.IsNullOrEmpty(itemType))
            return itemType;

        var normalized = itemType.Trim().ToLowerInvariant().Replace(" ", "_");

        // Map variant names from Python source / user data to canonical internal keys
        if (normalized == "mì_hảo_hảo" || normalized == "mi_hao_hao" || normalized == "mi_hao_hao")
            return "mi_hao_hao";

        return normalized;
    }

    public int FindEmptySlot()
    {
        for (int i = 0; i < _inventory.Length; i++)
        {
            if (_inventory[i] == null)
                return i;
        }
        return -1;
    }

    public int CountItem(string itemType)
    {
        itemType = NormalizeItemType(itemType);
        int total = 0;
        for (int i = 0; i < _inventory.Length; i++)
        {
            if (_inventory[i] != null && _inventory[i].Type == itemType)
                total += _inventory[i].Count;
        }
        return total;
    }

    public void RemoveAllItems(string itemType)
    {
        itemType = NormalizeItemType(itemType);
        for (int i = 0; i < _inventory.Length; i++)
        {
            if (_inventory[i] != null && _inventory[i].Type == itemType)
                _inventory[i] = null;
        }
        UpdateInventoryUI();
        ShowActiveToolModel();
    }
}

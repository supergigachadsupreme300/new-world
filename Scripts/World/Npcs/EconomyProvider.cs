using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// RPG economy bridge (planning Task 6.4 "VendorShopManager adapted for RPG economy").
/// Prices open-world <see cref="ItemDatabase"/> items from their <c>BaseValue</c> (sell/refund)
/// with configured multipliers, and moves inventory/money through the shared contracts
/// (<see cref="ToolManager"/>, <c>PlayerController.Money</c>, <see cref="GameStats"/>,
/// <see cref="QuestManager"/>). This is a thin, additive layer over the legacy vendor UI rather
/// than a rewrite.
/// </summary>
public static class EconomyProvider
{
    /// <summary>Sell multiplier when the player sells to a vendor (lower = merchant cut).</summary>
    public const float SellMultiplier = 0.6f;
    /// <summary>Buy price multiplier (higher = merchant margin).</summary>
    public const float BuyMultiplier = 1.5f;

    public static int SellPrice(string itemId)
    {
        var item = ItemDatabase.Get(itemId);
        return item == null ? 0 : Mathf.Max(1, Mathf.RoundToInt(item.BaseValue * SellMultiplier));
    }

    public static int BuyPrice(string itemId)
    {
        var item = ItemDatabase.Get(itemId);
        return item == null ? 0 : Mathf.Max(1, Mathf.RoundToInt(item.BaseValue * BuyMultiplier));
    }

    /// <summary>Player sells owned copies of an item; returns credits gained (0 if none).</summary>
    public static int SellItem(string itemId, UIManager ui = null)
    {
        var tm = ToolManager.Instance;
        if (tm == null) return 0;
        int owned = tm.CountItem(itemId);
        if (owned <= 0) return 0;
        tm.RemoveAllItems(itemId);
        int price = SellPrice(itemId);
        int earned = owned * price;
        var player = GameManager.Instance?.Player;
        if (player != null) player.Money += earned;
        GameStats.AddMoneyEarned(earned);
        QuestManager.Instance?.AddProgress("money_earned", earned);
        return earned;
    }

    /// <summary>Player buys one copy of an item, spending money; returns true if bought.</summary>
    public static bool BuyItem(string itemId, UIManager ui = null)
    {
        var tm = ToolManager.Instance;
        var player = GameManager.Instance?.Player;
        if (tm == null || player == null) return false;
        int price = BuyPrice(itemId);
        if (player.Money < price) return false;
        if (!tm.CanHoldItem(itemId)) return false;
        player.Money -= price;
        tm.AddItem(itemId, 1);
        return true;
    }
}
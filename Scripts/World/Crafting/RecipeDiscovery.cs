using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Tracks which crafting recipes the player has discovered (planning Task 6.3, game-design
/// §5.3 "recipes discovered through exploration and skill books"). Recipes marked
/// <c>RequiresDiscovery</c> are hidden from a <see cref="CraftingStation"/> until the matching
/// skill book is consumed. Optional, non-duplicating singleton; survives scene reloads
/// when <see cref="LoadOrCreate"/> is called from a boot-time manager.
/// </summary>
public class RecipeDiscovery : MonoBehaviour
{
    public static RecipeDiscovery Instance { get; private set; }

    private readonly HashSet<string> _discovered = new HashSet<string>();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    /// <summary>Load or create the singleton (safe from any Awake/Start).</summary>
    public static RecipeDiscovery LoadOrCreate()
    {
        if (Instance != null) return Instance;
        var go = new GameObject("RecipeDiscovery");
        return go.AddComponent<RecipeDiscovery>();
    }

    public bool IsDiscovered(string recipeId)
    {
        return string.IsNullOrEmpty(recipeId) || _discovered.Contains(recipeId);
    }

    public void Discover(string recipeId)
    {
        if (!string.IsNullOrEmpty(recipeId))
            _discovered.Add(recipeId);
    }

    /// <summary>
    /// Attempt to unlock a recipe by consuming its required skill book from the tool
    /// inventory (or every recipe sharing that book when <paramref name="discoverAll"/>).
    /// Returns true if something was unlocked.
    /// </summary>
    public bool TryUnlockWithSkillBook(string skillBookItemId, bool discoverAll = true)
    {
        var tm = ToolManager.Instance;
        if (tm == null || string.IsNullOrEmpty(skillBookItemId) || tm.CountItem(skillBookItemId) <= 0)
            return false;

        bool any = false;
        foreach (var r in RecipeRegistry.All)
        {
            if (!r.RequiresDiscovery) continue;
            if (string.Compare(r.DiscoverySkillBookId, skillBookItemId, StringComparison.Ordinal) != 0) continue;
            if (_discovered.Contains(r.Id)) continue;
            _discovered.Add(r.Id);
            any = true;
            if (!discoverAll) break;
        }
        if (!any) return false;

        tm.RemoveItemAmount(skillBookItemId, 1);
        return true;
    }

    /// <summary>Number of recipes still locked (for progress displays).</summary>
    public int LockedCount
    {
        get
        {
            int n = 0;
            foreach (var r in RecipeRegistry.All)
                if (r.RequiresDiscovery && !_discovered.Contains(r.Id)) n++;
            return n;
        }
    }

    /// <summary>
    /// Consume every skill book currently in the tool inventory that unlocks a still-locked
    /// recipe. Returns the number of recipes unlocked. Public so player input or skill-book
    /// pickups can drive discovery (§5.3 exploration / skill books).
    /// </summary>
    public int UseAllSkillBooksFromInventory()
    {
        var tm = ToolManager.Instance;
        if (tm == null) return 0;
        int before = LockedCount;
        foreach (var r in RecipeRegistry.All)
        {
            if (!r.RequiresDiscovery) continue;
            if (_discovered.Contains(r.Id)) continue;
            if (string.IsNullOrEmpty(r.DiscoverySkillBookId)) continue;
            TryUnlockWithSkillBook(r.DiscoverySkillBookId, false);
        }
        return before - LockedCount;
    }
}
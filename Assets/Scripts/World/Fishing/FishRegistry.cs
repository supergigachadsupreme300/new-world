using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Static registry of fish species (planning Task 6.2). Programmatic roster mirrors the
/// biome/item database pattern so <see cref="FishingSpot"/> can roll a species by weight.
/// .asset overrides take precedence when assigned in the Inspector.
/// </summary>
public static class FishRegistry
{
    private static readonly List<FishData> _all = new List<FishData>();
    private static readonly Dictionary<FishType, FishData> _byType = new Dictionary<FishType, FishData>();
    private static bool _built;

    private static void EnsureBuilt()
    {
        if (_built) return;
        _built = true;
        Register(BuildAll());
    }

    private static void Register(IEnumerable<FishData> all)
    {
        foreach (var f in all)
        {
            if (f == null) continue;
            if (!_byType.ContainsKey(f.Type))
            {
                _byType.Add(f.Type, f);
                _all.Add(f);
            }
        }
    }

    public static List<FishData> All
    {
        get { EnsureBuilt(); return new List<FishData>(_all); }
    }

    public static FishData Get(FishType type)
    {
        EnsureBuilt();
        return _byType.TryGetValue(type, out var f) ? f : null;
    }

    /// <summary>Roll a species by relative weight (mirrors FishingController.PickFishType).</summary>
    public static FishData RollFish()
    {
        EnsureBuilt();
        float total = 0f;
        foreach (var f in _all) total += f.Weight;
        float roll = UnityEngine.Random.Range(0f, Mathf.Max(0.001f, total));
        float cum = 0f;
        foreach (var f in _all)
        {
            cum += f.Weight;
            if (roll <= cum) return f;
        }
        return _all.Count > 0 ? _all[0] : null;
    }

    // ---------------------------------------------------------------
    //  PROGRAMMATIC ROSTER (game-design §5.2 + legacy minigame species)
    // ---------------------------------------------------------------

    private static FishData Make(FishType type, string display, float weight, float flop,
        float reel, string toolId, int sell, bool consumable = true)
    {
        var f = ScriptableObject.CreateInstance<FishData>();
        f.name = "Fish_" + type;
        f.Type = type;
        f.DisplayName = display;
        f.Weight = weight;
        f.Flop = flop;
        f.ReelFactor = reel;
        f.ToolItemId = toolId;
        f.SellValue = sell;
        f.IsConsumable = consumable;
        return f;
    }

    private static FishData[] BuildAll()
    {
        return new[]
        {
            Make(FishType.Carp, "Carp", 40f, 0.30f, 1.00f, "fish_carp", 6),
            Make(FishType.Salmon, "Salmon", 30f, 0.40f, 0.92f, "fish_salmon", 12),
            Make(FishType.Tuna, "Tuna", 20f, 0.52f, 0.82f, "fish_tuna", 20),
            Make(FishType.Pufferfish, "Pufferfish", 10f, 0.62f, 0.72f, "fish_pufferfish", 26, false),
            Make(FishType.ReefFish, "Reef Fish", 18f, 0.45f, 0.88f, "fish_reef", 15),
            Make(FishType.Eel, "Eel", 14f, 0.50f, 0.85f, "fish_eel", 18, false),
        };
    }
}
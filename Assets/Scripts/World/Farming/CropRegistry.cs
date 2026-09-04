using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Static registry of crops (planning Task 6.1). Programmatic roster mirrors the biome/item
/// database pattern; <see cref="FarmPlot"/>/<see cref="FarmingManager"/> resolve a crop by
/// <see cref="CropType"/>. .asset overrides take precedence when assigned in the Inspector.
/// Harvest item ids match the crop names already used by the cooking recipes.
/// </summary>
public static class CropRegistry
{
    private static readonly Dictionary<CropType, CropData> _byType = new Dictionary<CropType, CropData>();
    private static bool _built;

    private static void EnsureBuilt()
    {
        if (_built) return;
        _built = true;
        Register(BuildAll());
    }

    private static void Register(IEnumerable<CropData> all)
    {
        foreach (var c in all)
            if (c != null && !_byType.ContainsKey(c.Type))
                _byType.Add(c.Type, c);
    }

    public static List<CropData> All
    {
        get { EnsureBuilt(); return new List<CropData>(_byType.Values); }
    }

    public static CropData Get(CropType type)
    {
        EnsureBuilt();
        return _byType.TryGetValue(type, out var c) ? c : null;
    }

    // ---------------------------------------------------------------
    //  PROGRAMMATIC ROSTER (game-design §5.1 crops)
    // ---------------------------------------------------------------

    private static CropData Make(CropType type, string display, float hours, int stages,
        string harvestId, bool needsWater = true)
    {
        var c = ScriptableObject.CreateInstance<CropData>();
        c.name = "Crop_" + type;
        c.Type = type;
        c.DisplayName = display;
        c.BaseGrowHours = hours;
        c.Stages = stages;
        c.NeedsWater = needsWater;
        c.HarvestItemId = harvestId;
        c.HarvestCount = 1;
        return c;
    }

    private static CropData[] BuildAll()
    {
        return new[]
        {
            Make(CropType.Rice, "Rice", 12f, 4, "rice"),
            Make(CropType.Carrot, "Carrot", 10f, 4, "carrot"),
            Make(CropType.Wheat, "Wheat", 12f, 4, "wheat"),
            Make(CropType.Tomato, "Tomato", 15f, 4, "tomato"),
            Make(CropType.Pumpkin, "Pumpkin", 16f, 4, "pumpkin"),
            Make(CropType.Strawberry, "Strawberry", 17f, 4, "strawberry"),
            Make(CropType.Onion, "Onion", 11f, 4, "onion"),
            Make(CropType.Sugarcane, "Sugarcane", 13f, 4, "sugarcane"),
            Make(CropType.Potato, "Potato", 13f, 4, "potato"),
            Make(CropType.Corn, "Corn", 14f, 4, "corn"),
        };
    }
}
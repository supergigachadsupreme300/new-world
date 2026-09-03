using System;
using UnityEngine;

/// <summary>
/// Simplified build/decorate system (planning Task 6.5, game-design §5.3). Adapts the
/// CountryLife blueprint concept into a light placement helper: building a structure object
/// (walls, roof, furniture) costs a few <see cref="ToolManager"/> materials and spawns it at the
/// given plot. Kept deliberately small and additive — it does not touch the sprawling
/// <see cref="WorldBuilder"/> blueprint pipeline.
/// </summary>
public static class HomeBuilder
{
    /// <summary>Blueprint material costs per object type (itemId → cost in wood/materials).</summary>
    public const int WallWoodCost = 4;
    public const int RoofWoodCost = 5;
    public const int DoorWoodCost = 3;
    public const int FurnitureWoodCost = 2;

    public static int CostFor(string objectType)
    {
        switch (objectType)
        {
            case "wall": return WallWoodCost;
            case "roof": return RoofWoodCost;
            case "door": return DoorWoodCost;
            default: return FurnitureWoodCost;
        }
    }

    /// <summary>Try to build an object at the plot; spends tool materials on success.</summary>
    public static bool TryBuild(HousePlot plot, string objectType, Vector3 localPosition, float rotationY = 0f)
    {
        if (plot == null) return false;
        var tm = ToolManager.Instance;
        if (tm == null) return false;

        int woodCost = CostFor(objectType);
        if (tm.CountItem("wood") < woodCost)
        {
            GameManager.Instance?.UIManager?.ShowMessage("Not enough wood.", 1.5f);
            return false;
        }
        tm.RemoveItemAmount("wood", woodCost);

        plot.SpawnObject(objectType, localPosition, rotationY);
        QuestManager.Instance?.AddProgress("build_object", 1);
        return true;
    }
}
using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A designated player-home plot (planning Task 6.5, game-design §5.3 "player home plots at
/// designated locations"). <see cref="BuildHome"/> erects the home shell (foundation/walls/
/// roof placeholders), attaches a home <see cref="CraftingStation"/> (Task 6.3), a
/// <see cref="HomeChest"/> for storage, and a small <see cref="FarmPlot"/> grid for the farming
/// attachment. <see cref="HomeBuilder"/> places additional build/decorate objects onto the plot.
/// </summary>
public class HousePlot : MonoBehaviour
{
    [Header("Plot")]
    public string PlotId = "home_plot";
    [Tooltip("True once the home has been built on this plot.")]
    public bool HomeBuilt;

    [Header("Attachments")]
    public CraftingStation Crafting;
    public HomeChest Chest;
    public readonly List<FarmPlot> FarmPlots = new List<FarmPlot>();

    private readonly List<GameObject> _parts = new List<GameObject>();
    private readonly List<GameObject> _built = new List<GameObject>();

    /// <summary>Erect the basic home on this plot (foundation + walls + roof placeholders).</summary>
    public void BuildHome()
    {
        if (HomeBuilt) return;
        HomeBuilt = true;

        // Foundation slab.
        SpawnShell("Foundation", new Vector3(0f, 0.25f, 0f), new Vector3(8f, 0.5f, 6f),
            new Color(0.55f, 0.5f, 0.42f));

        // Four walls.
        SpawnShell("Wall", new Vector3(0f, 2.5f, 3.15f), new Vector3(8f, 4f, 0.3f),
            new Color(0.78f, 0.55f, 0.35f));
        SpawnShell("Wall", new Vector3(0f, 2.5f, -3.15f), new Vector3(8f, 4f, 0.3f),
            new Color(0.78f, 0.55f, 0.35f));
        SpawnShell("Wall", new Vector3(-3.85f, 2.5f, 0f), new Vector3(0.3f, 4f, 6f),
            new Color(0.78f, 0.55f, 0.35f));
        SpawnShell("Wall", new Vector3(3.85f, 2.5f, 0f), new Vector3(0.3f, 4f, 6f),
            new Color(0.78f, 0.55f, 0.35f));

        // Roof.
        SpawnShell("Roof", new Vector3(0f, 5.0f, 0f), new Vector3(8.5f, 0.4f, 6.5f),
            new Color(0.5f, 0.3f, 0.2f));

        // Crafting station (home crafting, Task 6.3).
        var craftGo = new GameObject("HomeCrafting");
        craftGo.transform.SetParent(transform, false);
        craftGo.transform.localPosition = new Vector3(2.5f, 0f, 0f);
        Crafting = craftGo.AddComponent<CraftingStation>();
        Crafting.StationId = "home_" + PlotId;
        Crafting.Kind = RecipeKind.Armor;
        Crafting.CategoryName = "Home Crafting";
        var craftCol = craftGo.AddComponent<BoxCollider>();
        craftCol.isTrigger = true;
        craftCol.size = new Vector3(1.4f, 1.6f, 1.2f);

        // Chest storage.
        var chestGo = new GameObject("HomeChest");
        chestGo.transform.SetParent(transform, false);
        chestGo.transform.localPosition = new Vector3(-2.5f, 0f, 0f);
        Chest = chestGo.AddComponent<HomeChest>();
        Chest.ChestId = "home_" + PlotId;

        // Farming attachment: a small 2x3 grid of plots out back.
        ArrangeFarming();

        // Spawn a link to the player (fast travel to home plot).
        SpawnHomeSignpost();
    }

    private void ArrangeFarming()
    {
        FarmPlots.Clear();
        for (int x = 0; x < 2; x++)
        {
            for (int z = 0; z < 3; z++)
            {
                var go = new GameObject("FarmPlot_" + x + "_" + z);
                go.transform.SetParent(transform, false);
                go.transform.localPosition = new Vector3(-1.5f + x * 1.5f, 0.05f, 4.5f + z * 1.5f);
                var plot = go.AddComponent<FarmPlot>();
                plot.Tilled = true;
                plot.BuildFieldVisual(new Color(0.45f, 0.32f, 0.16f));
                FarmPlots.Add(plot);
            }
        }
    }

    /// <summary>Spawn a build object (wall/furniture) onto this plot. Called by HomeBuilder.</summary>
    public void SpawnObject(string objectType, Vector3 localPosition, float rotationY)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = "Built_" + objectType;
        go.transform.SetParent(transform, false);
        go.transform.localPosition = localPosition;
        go.transform.localRotation = Quaternion.Euler(0f, rotationY, 0f);
        go.transform.localScale = BuildScale(objectType);
        var r = go.GetComponent<Renderer>();
        if (r != null) r.material.color = BuildColor(objectType);
        _built.Add(go);
    }

    private static Vector3 BuildScale(string objectType)
    {
        switch (objectType)
        {
            case "wall": return new Vector3(4f, 3f, 0.3f);
            case "roof": return new Vector3(5f, 0.3f, 4f);
            case "door": return new Vector3(1f, 3f, 0.3f);
            default: return new Vector3(1.5f, 1.5f, 1.5f);
        }
    }

    private static Color BuildColor(string objectType)
    {
        switch (objectType)
        {
            case "wall": return new Color(0.78f, 0.55f, 0.35f);
            case "roof": return new Color(0.5f, 0.3f, 0.2f);
            case "door": return new Color(0.45f, 0.28f, 0.14f);
            default: return new Color(0.6f, 0.4f, 0.2f);
        }
    }

    private void SpawnShell(string part, Vector3 localPos, Vector3 scale, Color color)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = "Home_" + part;
        go.transform.SetParent(transform, false);
        go.transform.localPosition = localPos;
        go.transform.localScale = scale;
        var r = go.GetComponent<Renderer>();
        if (r != null) r.material.color = color;
        Destroy(go.GetComponent<Collider>());
        _parts.Add(go);
    }

    private void SpawnHomeSignpost()
    {
        var sign = GameObject.CreatePrimitive(PrimitiveType.Cube);
        sign.name = "HomeSign";
        sign.transform.SetParent(transform, false);
        sign.transform.localPosition = new Vector3(0f, 1.0f, 4.2f);
        sign.transform.localScale = new Vector3(0.12f, 1.8f, 0.12f);
        Destroy(sign.GetComponent<Collider>());
        _parts.Add(sign);
    }

    private void OnDestroy()
    {
        foreach (var p in _built) if (p != null) Destroy(p);
        foreach (var p in _parts) if (p != null) Destroy(p);
    }
}
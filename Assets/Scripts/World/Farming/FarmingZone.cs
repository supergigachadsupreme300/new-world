using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Designated fertile farming zones (planning Task 6.1 "Farming plots at designated fertile
/// biome zones"). Spawns a grid of <see cref="FarmPlot"/> tilled plots (plus an optional
/// pre-seeded starter crop) at a zone anchor within fertile biomes (Plains/Forest/Swamp).
/// Composes with <see cref="FarmingManager"/> so the grid is day/night-growth driven.
/// </summary>
public class FarmingZone : MonoBehaviour
{
    [Header("Layout")]
    public Vector2Int GridSize = new Vector2Int(4, 3);
    public float Spacing = 1.8f;
    public CropType StarterCrop = CropType.Carrot;
    public bool PreSeedStarter = true;

    [Header("Zone")]
    public BiomeType FertileBiome = BiomeType.Plains;
    [Tooltip("Sparse marker so the player can find this fertile zone.")]
    public bool SpawnZoneMarker = true;

    private readonly List<FarmPlot> _plots = new List<FarmPlot>();

    public IReadOnlyList<FarmPlot> Plots => _plots;

    public static FarmingZone Build(Transform parent, Vector3 worldPosition, BiomeType biome)
    {
        var root = new GameObject("FarmingZone_" + biome);
        root.transform.SetParent(parent);
        root.transform.position = worldPosition;
        var zone = root.AddComponent<FarmingZone>();
        zone.FertileBiome = biome;
        zone.BuildPlots(root.transform);
        return zone;
    }

    private void BuildPlots(Transform parentOfPlot)
    {
        for (int x = 0; x < GridSize.x; x++)
        {
            for (int y = 0; y < GridSize.y; y++)
            {
                Vector3 offset = new Vector3(
                    (x - (GridSize.x - 1) * 0.5f) * Spacing, 0f,
                    (y - (GridSize.y - 1) * 0.5f) * Spacing);
                var plot = FarmingManager.Instance != null
                    ? FarmingManager.Instance.TillGround(transform.position + offset)
                    : CreateStandalone(offset);
                if (plot == null) continue;
                _plots.Add(plot);
                if (PreSeedStarter)
                    plot.Plant(StarterCrop);
            }
        }

        if (SpawnZoneMarker)
            BuildZoneMarker(parentOfPlot);
    }

    private FarmPlot CreateStandalone(Vector3 offset)
    {
        var go = new GameObject("FarmPlot");
        go.transform.position = transform.position + offset;
        go.transform.SetParent(transform);
        var p = go.AddComponent<FarmPlot>();
        p.Tilled = true;
        p.BuildFieldVisual(new Color(0.45f, 0.28f, 0.12f));
        return p;
    }

    private void BuildZoneMarker(Transform parent)
    {
        var marker = GameObject.CreatePrimitive(PrimitiveType.Cube);
        marker.name = "FarmingZoneSign";
        marker.transform.SetParent(parent, false);
        marker.transform.localPosition = new Vector3(0f, 1.6f, -GridSize.y * Spacing * 0.5f - 1f);
        marker.transform.localScale = new Vector3(1.2f, 0.5f, 0.09f);
        marker.GetComponent<MeshRenderer>().material.color = new Color(0.6f, 0.85f, 0.4f);
        Destroy(marker.GetComponent<Collider>());
    }
}
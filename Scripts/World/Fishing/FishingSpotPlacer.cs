using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Places marked fishing spots in the world (planning Task 6.2 "Fishing spots marked in world
/// (water biome chunks)"). Reads the <see cref="POIRegistry"/> fishing POIs (Task 5.3 §7.2)
/// and drops a <see cref="FishingSpot"/> at each, tagging its biome. Designers can also author
/// spots by hand and skip this.
/// </summary>
public class FishingSpotPlacer : MonoBehaviour
{
    public Vector3 Anchor = Vector3.zero;
    public bool AutoPlaceOnStart = true;

    private readonly List<FishingSpot> _spots = new List<FishingSpot>();

    public IReadOnlyList<FishingSpot> Spots => _spots;

    private void Start()
    {
        if (AutoPlaceOnStart)
            Place();
    }

    /// <summary>Place a spot at a world position; returns it.</summary>
    public FishingSpot PlaceSpot(Vector3 position, BiomeType biome, string id)
    {
        var go = new GameObject("FishingSpot_" + id);
        go.transform.SetParent(transform);
        go.transform.position = position;
        var spot = go.AddComponent<FishingSpot>();
        spot.SpotId = id;
        spot.Biome = biome;
        spot.BuildSpot();
        _spots.Add(spot);
        return spot;
    }

    /// <summary>Place spots for every PoiKind.Fishing POI in the registry.</summary>
    public void Place()
    {
        var fishing = POIRegistry.OfKind(PoiKind.Fishing);
        foreach (var poi in fishing)
        {
            if (poi == null) continue;
            PlaceSpot(Anchor + poi.LocalPosition, poi.Biome, poi.Id);
        }
    }
}
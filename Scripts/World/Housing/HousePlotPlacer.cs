using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Places player-home plots at designated locations (planning Task 6.5). Generates one or more
/// <see cref="HousePlot"/> components and (optionally) auto-builds a starter home. Locations are
/// designer-defined; the placer keeps them discoverable via <see cref="Plots"/>.
/// </summary>
public class HousePlotPlacer : MonoBehaviour
{
    public Vector3 Anchor = Vector3.zero;
    [Tooltip("Pre-build the starter home on every placed plot.")]
    public bool AutoBuildHome = true;

    private readonly List<HousePlot> _plots = new List<HousePlot>();
    public IReadOnlyList<HousePlot> Plots => _plots;

    private void Start()
    {
        if (_plots.Count == 0)
            PlaceDefaults();
    }

    /// <summary>Place a home plot at a world position; returns it.</summary>
    public HousePlot PlacePlot(string id, Vector3 localPosition)
    {
        var go = new GameObject("HousePlot_" + id);
        go.transform.SetParent(transform);
        go.transform.position = Anchor + localPosition;
        var plot = go.AddComponent<HousePlot>();
        plot.PlotId = id;
        if (AutoBuildHome)
            plot.BuildHome();
        _plots.Add(plot);
        return plot;
    }

    /// <summary>Place a small default set of home plots near the spawn/anchor.</summary>
    public void PlaceDefaults()
    {
        PlacePlot("home_1", new Vector3(12f, 0f, 6f));
        PlacePlot("home_2", new Vector3(-14f, 0f, -8f));
    }
}
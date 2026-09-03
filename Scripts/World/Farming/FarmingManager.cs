using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Farming orchestrator (planning Task 6.1). Adapts the legacy <see cref="FieldManager"/>
/// approach (a field-placement preview when the player holds a hoe) for the new open-world
/// terrain: it raycasts any ground instead of matching CountryLife collider names, and drives
/// <see cref="FarmPlot"/> growth from the game day/night cycle (game-design §7.3) instead of
/// raw unscaled timers. Manages the field preview + the list of live plots and zones.
/// </summary>
public class FarmingManager : MonoSingleton<FarmingManager>
{
    [Header("Placement")]
    public float PlotSize = 1.8f;
    public Color TilledTint = new Color(0.45f, 0.28f, 0.12f);
    public Color PreviewTint = new Color(0.6f, 0.42f, 0.2f, 0.5f);

    [Header("Growth Timing")]
    [Tooltip("Game-hours that pass per real-time second when TimeScale is applied.")]
    public float GameHoursPerSecond = 0.5f;

    private readonly List<FarmPlot> _plots = new List<FarmPlot>();
    private GameObject _preview;
    private Camera _cam;
    private float _drawnHours;

    public IReadOnlyList<FarmPlot> Plots => _plots;

    private void Update()
    {
        float dt = Time.deltaTime;
        // Drive growth from the game clock: convert real time to game-hours each frame.
        float gameHours = GameHoursPerSecond * dt;
        bool isNight = IsNight();
        TickPlots(gameHours, isNight);
        UpdatePreviewHoe();
    }

    private void TickPlots(float gameHours, bool isNight)
    {
        for (int i = _plots.Count - 1; i >= 0; i--)
        {
            if (_plots[i] == null) { _plots.RemoveAt(i); continue; }
            _plots[i].TickCycle(gameHours, isNight);
        }
    }

    private static bool IsNight()
    {
        float hour = GameManager.Instance != null ? GameManager.Instance.TimeOfDay : 12f;
        return hour >= 18f || hour < 6f;
    }

    private void UpdatePreviewHoe()
    {
        if (GameManager.Instance == null || !GameManager.Instance.InGame) return;
        if (ToolManager.Instance == null || ToolManager.Instance.GetSelectedItemType() != "hoe")
        {
            if (_preview != null) _preview.SetActive(false);
            return;
        }
        if (_preview == null) CreatePreview();
        if (_preview == null) return;
        UpdatePreview();
    }

    private void CreatePreview()
    {
        _preview = GameObject.CreatePrimitive(PrimitiveType.Quad);
        _preview.name = "FarmPreview";
        _preview.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        _preview.transform.localScale = new Vector3(PlotSize, PlotSize, 1f);
        var r = _preview.GetComponent<MeshRenderer>();
        if (r != null) r.material.color = PreviewTint;
        Destroy(_preview.GetComponent<Collider>());
        _preview.SetActive(false);
    }

    private void UpdatePreview()
    {
        if (_cam == null) _cam = Camera.main;
        var cam = _cam;
        if (cam == null) return;

        var ray = new Ray(cam.transform.position + cam.transform.forward * 0.3f, cam.transform.forward);
        if (Physics.Raycast(ray, out var hit, 10f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide))
        {
            Vector2Int grid = new Vector2Int(
                Mathf.RoundToInt(hit.point.x / PlotSize),
                Mathf.RoundToInt(hit.point.z / PlotSize));
            Vector3 pos = new Vector3(grid.x * PlotSize, 0, grid.y * PlotSize);
            _preview.transform.position = pos;
            _preview.SetActive(true);
            return;
        }
        _preview.SetActive(false);
    }

    /// <summary>Where the placement preview sits, if active (mirrors FieldManager.TryGetPreviewPosition).</summary>
    public bool TryGetPreviewPosition(out Vector3 position)
    {
        position = Vector3.zero;
        if (_preview == null || !_preview.activeSelf) return false;
        position = _preview.transform.position;
        return true;
    }

    /// <summary>Create (or re-till) a plot at a world position and track it.</summary>
    public FarmPlot TillGround(Vector3 position)
    {
        Vector3 rounded = new Vector3(
            Mathf.Round(position.x / PlotSize) * PlotSize, 0f,
            Mathf.Round(position.z / PlotSize) * PlotSize);
        var existing = GetPlotAt(rounded);
        if (existing != null)
        {
            existing.Tilled = true;
            existing.Stage = 0;
            existing.Watered = false;
            existing.Harvested = false;
            return existing;
        }

        var go = new GameObject("FarmPlot");
        go.transform.position = rounded;
        go.transform.SetParent(transform);
        var plot = go.AddComponent<FarmPlot>();
        plot.Tilled = true;
        plot.BuildFieldVisual(TilledTint);
        _plots.Add(plot);
        return plot;
    }

    public FarmPlot GetPlotAt(Vector3 position)
    {
        foreach (var p in _plots)
        {
            if (p == null) continue;
            if (Vector3.Distance(p.transform.position, position) < PlotSize * 0.5f)
                return p;
        }
        return null;
    }

    public bool WaterPlot(Vector3 position) => GetPlotAt(position)?.Water() ?? false;

    public bool HarvestPlot(Vector3 position, out string yieldId)
    {
        var plot = GetPlotAt(position);
        if (plot == null) { yieldId = null; return false; }
        return plot.Harvest(out yieldId);
    }
}
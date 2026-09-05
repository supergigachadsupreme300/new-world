using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Phase 8 (Task 8.1): compass strip + minimap with a chunk-grid overlay.
/// The minimap is a north-up top-down circle centred on the player. A chunk-grid overlay
/// draws the 1x1 world chunk boundaries (<see cref="ChunkData.Size"/>) across the visible
/// footprint, repositioned each frame so chunk lines stay world-aligned while the player
/// moves. The compass shows the current heading cardinal.
/// </summary>
public sealed class CompassMinimapHUD : MonoBehaviour
{
    [Header("Layout")]
    public bool ShowOnInGame = true;
    [Range(0.08f, 0.35f)] public float MinimapRadiusFraction = 0.16f;

    /// <summary>World metres shown across the minimap diameter.</summary>
    public float ViewSize = 40f;

    private Canvas _canvas;
    private TMP_Text _compassLabel;
    private RectTransform _minimapRoot;
    private Transform _gridLineHost;
    private readonly List<RectTransform> _gridLines = new List<RectTransform>();
    private float _radius;
    private float _lastView;

    private void OnEnable()
    {
        _canvas = HudCanvas.CreateOverlay("CompassMinimapCanvas");
        float w = Mathf.Max(Screen.width, 1f);
        float h = Mathf.Max(Screen.height, 1f);

        // Compass strip (top-centre).
        var compassRoot = HudCanvas.CreateBackdrop(_canvas.transform, "CompassStrip",
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0f, -6f), new Vector2(w * 0.5f, 28f));
        _compassLabel = MakeLabel(compassRoot, "N", Vector2.zero, Color.white);

        // Minimap circle + grid host (bottom-right).
        _radius = Mathf.Min(w, h) * MinimapRadiusFraction;
        float d = _radius * 2f;
        _minimapRoot = HudCanvas.CreateBackdrop(_canvas.transform, "Minimap",
            new Vector2(1f, 0f), new Vector2(1f, 0f),
            new Vector2(-_radius - 18f, _radius + 18f), new Vector2(d, d));

        var gridHost = new GameObject("ChunkGrid");
        gridHost.transform.SetParent(_minimapRoot, false);
        var gridRect = gridHost.AddComponent<RectTransform>();
        gridRect.anchorMin = new Vector2(0.5f, 0.5f);
        gridRect.anchorMax = new Vector2(0.5f, 0.5f);
        gridRect.pivot = new Vector2(0.5f, 0.5f);
        gridRect.anchoredPosition = Vector2.zero;
        gridRect.sizeDelta = new Vector2(d, d);
        _gridLineHost = gridGridlineHost(gridRect);
        _lastView = 0f;
    }

    /// <summary>Create a nested host that keeps gridlines centred; sizes to the minimap.</summary>
    private static Transform gridGridlineHost(RectTransform gridRect)
    {
        // A single child keeps the Rects static; gridlines are children of this host.
        var go = new GameObject("Lines");
        go.transform.SetParent(gridRect, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = gridRect.sizeDelta;
        // Gridlines act as world-aligned segments; created lazily in Update.
        return rt;
    }

    private TMP_Text MakeLabel(RectTransform parent, string text, Vector2 pos, Color color)
    {
        var go = new GameObject("Label");
        go.transform.SetParent(parent, false);
        var rect = go.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = pos;
        rect.sizeDelta = new Vector2(112f, 32f);
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.fontSize = Mathf.Max(17f, Screen.height / 48f);
        tmp.color = color;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.text = text;
        return tmp;
    }

    private void Update()
    {
        var gm = GameManager.Instance;
        bool inGame = gm != null && gm.InGame;
        if (_canvas != null)
            _canvas.gameObject.SetActive(ShowOnInGame ? inGame : true);
        if (!inGame) return;

        Transform focus = gm.Player != null ? gm.Player.transform : null;
        if (focus == null) return;

        // Compass heading.
        float yaw;
        var cam = Camera.main;
        yaw = cam != null ? cam.transform.eulerAngles.y : focus.eulerAngles.y;
        if (_compassLabel != null)
            _compassLabel.text = Cardinal(yaw);

        // Chunk-grid overlay: rebuild when the visible world-view changes.
        if (_gridLineHost != null && Mathf.Abs(_lastView - ViewSize) > 1f)
        {
            _lastView = ViewSize;
            RebuildGrid();
        }
        AlignGrid(focus.position);
    }

    private void RebuildGrid()
    {
        foreach (var r in _gridLines) if (r != null) Destroy(r.gameObject);
        _gridLines.Clear();
        float meterPerPx = ViewSize / Mathf.Max(1f, _minimapRoot.rect.width);

        // Draw boundaries for chunks within the view footprint.
        int half = Mathf.Max(1, Mathf.CeilToInt(ViewSize / ChunkData.Size / 2f));
        float lineThick = Mathf.Max(1f, meterPerPx * 0.5f);
        Color lineColor = new Color(0.95f, 0.95f, 0.7f, 0.6f);

        // Vertical lines (constant world X).
        for (int i = -half; i <= half; i++)
        {
            var r = CreateLine(_gridLineHost, lineColor, lineThick);
            r.anchorMin = new Vector2(0.5f, 0f);
            r.anchorMax = new Vector2(0.5f, 1f);
            r.pivot = new Vector2(0.5f, 0.5f);
            r.anchoredPosition = new Vector2(i * ViewSize / (2f * half + 1f), 0f);
            r.sizeDelta = new Vector2(lineThick, _minimapRoot.rect.height);
            _gridLines.Add(r);
        }
        // Horizontal lines (constant world Z).
        for (int i = -half; i <= half; i++)
        {
            var r = CreateLine(_gridLineHost, lineColor, lineThick);
            r.anchorMin = new Vector2(0f, 0.5f);
            r.anchorMax = new Vector2(1f, 0.5f);
            r.pivot = new Vector2(0.5f, 0.5f);
            r.anchoredPosition = new Vector2(0f, i * ViewSize / (2f * half + 1f));
            r.sizeDelta = new Vector2(_minimapRoot.rect.width, lineThick);
            _gridLines.Add(r);
        }
    }

    private RectTransform CreateLine(Transform parent, Color color, float thick)
    {
        var go = new GameObject("GridLine");
        go.transform.SetParent(parent, false);
        var rect = go.AddComponent<RectTransform>();
        var img = go.AddComponent<Image>();
        img.color = color;
        img.raycastTarget = false;
        return rect;
    }

    private void AlignGrid(Vector3 worldPos)
    {
        if (_gridLineHost == null || _gridLines.Count == 0) return;
        // Shift the grid host so a chunk boundary aligns with the minimap centre
        // (north-up: X↔screen X, Z↔screen Y).
        float meterPerPx = ViewSize / Mathf.Max(1f, _minimapRoot.rect.width);
        float ox = Mathf.Repeat(worldPos.x, ChunkData.Size);
        float oz = Mathf.Repeat(worldPos.z, ChunkData.Size);
        _gridLineHost.localPosition = new Vector3(
            (ChunkData.Size * 0.5f - ox) * meterPerPx,
            (ChunkData.Size * 0.5f - oz) * meterPerPx,
            0f);
    }

    private static string Cardinal(float yaw)
    {
        yaw = ((yaw % 360f) + 360f) % 360f;
        if (yaw < 22.5f || yaw >= 337.5f) return "N";
        if (yaw < 67.5f) return "NE";
        if (yaw < 112.5f) return "E";
        if (yaw < 157.5f) return "SE";
        if (yaw < 202.5f) return "S";
        if (yaw < 247.5f) return "SW";
        if (yaw < 292.5f) return "W";
        return "NW";
    }
}
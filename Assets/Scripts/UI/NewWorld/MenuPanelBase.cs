using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Base helper for Phase 8 modal menu panels (Task 8.2). Creates a full-screen dimmed overlay
/// with a centred panel, a title, body region and a close button. Subclasses populate the body
/// and control visibility. Uses the project's <see cref="ColorPalette.UIBackdrop"/> and a
/// default TMP font via <see cref="GameManager"/>.
///
/// Canvas scaling is height-matched on a 1280x720-reference (legacy-proportional) design so the
/// new UI keeps its on-screen footprint; subclasses register their content group via
/// <see cref="RegisterFit"/> and the whole menu re-scales live when the window changes aspect so it
/// never clips on small/non-16:9 displays. <see cref="UiScale"/> is the single knob to bulk-adjust
/// size. Showing any panel unlocks the cursor; shutting the last one re-locks it.
/// </summary>
public abstract class MenuPanelBase : MonoBehaviour
{
    private static readonly List<MenuPanelBase> _open = new List<MenuPanelBase>();

    /// <summary>Number of open MenuPanelBase overlays (drives cursor release).</summary>
    public static int OpenCount => _open.Count;
    public static bool AnyShown => _open.Count > 0;

    /// <summary>Bulk-size knob for the new UI (default 1.2 = ~20% larger than legacy 1280x720).</summary>
    public static float UiScale = 1.2f;

    /// <summary>Close the most recently opened panel overlay.</summary>
    public static void CloseTopmost()
    {
        if (_open.Count == 0) return;
        _open[_open.Count - 1].Close();
    }

    protected RectTransform PanelRect;
    protected RectTransform BodyRow;

    /// <summary>Content groups registered for aspect-fit re-scaling (root + its design-space box).</summary>
    private readonly List<(RectTransform root, Rect designBox)> _fits = new List<(RectTransform, Rect)>();
    private int _lastScreenW = -1, _lastScreenH = -1;
    private RectTransform _titleRect;
    private RectTransform _closeRect;

    /// <summary>When set, no title bar is rendered (subclasses that use their own top band, e.g. tabs).</summary>
    protected bool SuppressTitle;

    /// <summary>When set, no close button is rendered (panels with their own dismiss affordance).</summary>
    protected bool SuppressCloseButton;

    /// <summary>Create the overlay container. Call once from subclass OnEnable.</summary>
    protected void Build(string title)
    {
        // Reference space the layout is authored in (legacy 1280x720 units, ~20% enlarged).
        float refW = 1280f / UiScale;
        float refH = 720f / UiScale;

        var canvasGo = new GameObject(name + "Canvas");
        canvasGo.transform.SetParent(transform, false);
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 40;
        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(refW, refH);
        // Match height: the design height always fills the screen, so a 16:9 window shows the
        // layout 1:1 and other aspects only over/under-fill horizontally. RegisterFit() below
        // re-scales each panel's content group to the visible width so nothing is ever clipped.
        scaler.matchWidthOrHeight = 1f;
        // Without a raycaster the EventSystem never raycasts this canvas, so every
        // button on the menu is unclickable while keyboard shortcuts keep working.
        canvasGo.AddComponent<GraphicRaycaster>();

        // Full-screen dim.
        var overlay = new GameObject("Overlay");
        overlay.transform.SetParent(canvasGo.transform, false);
        var or = overlay.AddComponent<RectTransform>();
        or.anchorMin = Vector2.zero;
        or.anchorMax = Vector2.one;
        or.offsetMin = Vector2.zero;
        or.offsetMax = Vector2.zero;
        var overlayImg = overlay.AddComponent<Image>();
        overlayImg.color = new Color(0f, 0f, 0f, 0.82f);

        // Panel (backdrop + title + close button): spans the whole reference canvas.
        var panel = new GameObject("Panel");
        panel.transform.SetParent(canvasGo.transform, false);
        PanelRect = panel.AddComponent<RectTransform>();
        PanelRect.anchorMin = Vector2.zero;
        PanelRect.anchorMax = Vector2.one;
        PanelRect.offsetMin = Vector2.zero;
        PanelRect.offsetMax = Vector2.zero;
        var panelImg = panel.AddComponent<Image>();
        var menuTex = Resources.Load<Texture2D>("menu");
        if (menuTex != null)
        {
            panelImg.sprite = Sprite.Create(menuTex,
                new Rect(0, 0, menuTex.width, menuTex.height), new Vector2(0.5f, 0.5f));
            panelImg.type = Image.Type.Simple;
            panelImg.preserveAspect = false;
            panelImg.color = Color.white;
        }
        else
        {
            panelImg.color = ColorPalette.UIBackdrop;
        }

        // Title (skipped when the subclass renders its own top band).
        if (SuppressTitle)
        {
            // no title
        }
        else
        {
            var titleGo = new GameObject("Title");
            titleGo.transform.SetParent(panel.transform, false);
            var tr = titleGo.AddComponent<RectTransform>();
            tr.anchorMin = new Vector2(0.5f, 1f);
            tr.anchorMax = new Vector2(0.5f, 1f);
            tr.pivot = new Vector2(0.5f, 1f);
            tr.anchoredPosition = new Vector2(0f, -20f);
            tr.sizeDelta = new Vector2(refW - 24f, 40f);
            _titleRect = tr;
            var tmp = titleGo.AddComponent<TextMeshProUGUI>();
            GameManager.Instance?.UIManager?.ApplyDefaultFont(tmp);
            tmp.text = title;
            tmp.fontSize = Mathf.Max(20f, Screen.height / 40f);
            tmp.color = Color.white;
            tmp.alignment = TextAlignmentOptions.Center;
        }

        // Body row (subclasses add content here). Stretch-anchored to the panel with fixed
        // margins (≈ the old centered sizeDelta at 16:9, but always the full visible panel).
        var body = new GameObject("Body");
        body.transform.SetParent(panel.transform, false);
        BodyRow = body.AddComponent<RectTransform>();
        BodyRow.anchorMin = Vector2.zero;
        BodyRow.anchorMax = Vector2.one;
        BodyRow.pivot = new Vector2(0.5f, 0.5f);
        BodyRow.offsetMin = new Vector2(30f, 60f);
        BodyRow.offsetMax = new Vector2(-30f, -60f);

        // Close button (red ✕ pinned to the visible top-right corner, see ApplyFits).
        if (!SuppressCloseButton)
            _closeRect = MakeRedClose(panel.transform, "Close",
                new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(-24f, -22f), new Vector2(36f, 36f), Close).GetComponent<RectTransform>();

        canvasGo.SetActive(false);
        PaletteCanvas = canvasGo.transform;
        _lastScreenW = Screen.width;
        _lastScreenH = Screen.height;
    }

    /// <summary>
    /// Build a red ✕ close button. Uses the shared 'redx' sprite (falling back to a red square
    /// with a text glyph when the texture isn't present). Shared by the panel close buttons and
    /// the skill-detail close button so every "close" looks consistent.
    /// </summary>
    public static Button MakeRedClose(Transform parent, string name,
        Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 pos, Vector2 size,
        UnityEngine.Events.UnityAction onClick)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.pivot = pivot;
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        var img = go.AddComponent<Image>();
        var tex = Resources.Load<Texture2D>("redx");
        if (tex != null)
        {
            img.sprite = Sprite.Create(tex,
                new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
            img.type = Image.Type.Simple;
            img.preserveAspect = true;
            img.color = Color.white;
        }
        else
        {
            img.color = new Color(0.85f, 0.16f, 0.16f, 0.95f);
        }
        var btn = go.AddComponent<Button>();
        btn.targetGraphic = img;
        btn.onClick.AddListener(onClick);

        if (tex == null)
        {
            var label = new GameObject("Label");
            label.transform.SetParent(go.transform, false);
            var lr = label.AddComponent<RectTransform>();
            lr.anchorMin = Vector2.zero;
            lr.anchorMax = Vector2.one;
            lr.offsetMin = Vector2.zero;
            lr.offsetMax = Vector2.zero;
            var ltmp = label.AddComponent<TextMeshProUGUI>();
            GameManager.Instance?.UIManager?.ApplyDefaultFont(ltmp);
            ltmp.text = "✕";
            ltmp.fontSize = Mathf.Max(20f, Screen.height / 40f);
            ltmp.color = Color.white;
            ltmp.alignment = TextAlignmentOptions.Center;
        }
        return btn;
    }

    protected Transform PaletteCanvas;

    /// <summary>True while the overlay canvas is active (the host component may stay active).</summary>
    public bool IsShown { get { return PaletteCanvas != null && PaletteCanvas.gameObject.activeInHierarchy; } }

    /// <summary>Show the panel overlay.</summary>
    public void Show()
    {
        if (PaletteCanvas == null) return;
        if (!_open.Contains(this)) _open.Add(this);
        GameInput.SetCursorLocked(false);
        GameManager.Instance?.UIManager?.SetCrosshairVisible(false);
        PaletteCanvas.gameObject.SetActive(true);
        // The window may have changed aspect while hidden; re-fit before drawing.
        ApplyFits();
        Refresh();
    }

    /// <summary>Hide the panel overlay.</summary>
    public void Close()
    {
        if (PaletteCanvas == null) return;
        _open.Remove(this);
        if (_open.Count == 0 && GameManager.Instance != null && !GameManager.Instance.GamePaused)
        {
            var player = GameManager.Instance.Player;
            if (player != null && !player.IgnoreInput)
            {
                GameInput.SetCursorLocked(true);
                GameManager.Instance.UIManager?.SetCrosshairVisible(true);
            }
        }
        PaletteCanvas.gameObject.SetActive(false);
    }

    private void Update()
    {
        if (!IsShown) return;
        // Live re-layout: any window resize (aspect) changes how much of the design space is
        // visible, so re-fit each registered content group. The CanvasScaler already handles the
        // pixel re-scale; this keeps the group fit factor in sync.
        if (Screen.width != _lastScreenW || Screen.height != _lastScreenH)
        {
            _lastScreenW = Screen.width;
            _lastScreenH = Screen.height;
            ApplyFits();
        }
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            Close();
    }

    /// <summary>
    /// Register a content group whose design-space box must always fit inside the visible canvas.
    /// The group is uniformly scaled down (around its centre) whenever the window gets narrower or
    /// shorter than the design, so fixed-position layouts (see <see cref="CharacterInfoUI"/>) never
    /// clip on small / non-16:9 windows, while 16:9 shows the design at 1:1.
    /// </summary>
    public void RegisterFit(RectTransform root, Rect designBox)
    {
        if (root == null || designBox.width <= 0f || designBox.height <= 0f) return;
        _fits.RemoveAll(f => f.root == root);
        _fits.Add((root, designBox));
        ApplyFits();
    }

    /// <summary>Re-scale all registered content groups against the current window size.</summary>
    public void ApplyFits()
    {
        if (_fits.Count == 0) return;
        float refH = 720f / UiScale;
        if (refH <= 0f || Screen.height <= 0f) return;

        // Height-matched canvas: design height always = refH; the visible design width is
        // screen pixels divided by the height-driven scale factor.
        float scale = Screen.height / refH;
        float availW = Screen.width / scale;
        float availH = Screen.height / scale;

        foreach (var (root, box) in _fits)
        {
            float fit = Mathf.Min(1f, availW / box.width, availH / box.height);
            if (fit < 0.25f) fit = 0.25f;   // never shrink to illegibility on extreme windows
            root.localScale = Vector3.one * fit;
        }

        // Keep the title and close button inside the visible area: the canvas can overhang the
        // sides on narrow windows (height-matched width), so nudge them to the safe edge.
        float refW = 1280f / UiScale;
        if (_closeRect != null)
            _closeRect.anchoredPosition = new Vector2((availW - refW) * 0.5f - 24f, -22f);
        if (_titleRect != null)
            _titleRect.sizeDelta = new Vector2(Mathf.Max(120f, availW - 24f), 40f);

        OnLayoutFitted(availW, availH);
    }

    /// <summary>
    /// Called after every aspect-fit pass with the currently visible design-space size. Subclasses
    /// whose own chrome (tab bars, fixed corner widgets) lives outside a registered content group
    /// use this to keep that chrome inside the visible area while the window resizes.
    /// </summary>
    protected virtual void OnLayoutFitted(float availW, float availH) { }

    protected abstract void Refresh();
}
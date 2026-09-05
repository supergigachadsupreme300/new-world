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
/// Canvas scaling is legacy-proportional (1280x720 reference) so the new UI matches the old
/// pixel-based UI's on-screen footprint; <see cref="UiScale"/> is the single knob to bulk-adjust
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

    /// <summary>When set, no title bar is rendered (subclasses that use their own top band, e.g. tabs).</summary>
    protected bool SuppressTitle;

    /// <summary>Create the overlay container. Call once from subclass OnEnable.</summary>
    protected void Build(string title)
    {
        var canvasGo = new GameObject(name + "Canvas");
        canvasGo.transform.SetParent(transform, false);
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 40;
        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280f / UiScale, 720f / UiScale);
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

        // Centred panel.
        var panel = new GameObject("Panel");
        panel.transform.SetParent(canvasGo.transform, false);
        PanelRect = panel.AddComponent<RectTransform>();
        PanelRect.anchorMin = new Vector2(0.5f, 0.5f);
        PanelRect.anchorMax = new Vector2(0.5f, 0.5f);
        PanelRect.pivot = new Vector2(0.5f, 0.5f);
        PanelRect.anchoredPosition = Vector2.zero;
        var w = Mathf.Min(Screen.width * 0.8f, 640f);
        var h = Mathf.Min(Screen.height * 0.8f, 486f);
        PanelRect.sizeDelta = new Vector2(w, h);
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
            tr.sizeDelta = new Vector2(w - 24f, 40f);
            var tmp = titleGo.AddComponent<TextMeshProUGUI>();
            GameManager.Instance?.UIManager?.ApplyDefaultFont(tmp);
            tmp.text = title;
            tmp.fontSize = Mathf.Max(20f, Screen.height / 40f);
            tmp.color = Color.white;
            tmp.alignment = TextAlignmentOptions.Center;
        }

        // Body row (subclasses add content here).
        var body = new GameObject("Body");
        body.transform.SetParent(panel.transform, false);
        BodyRow = body.AddComponent<RectTransform>();
        BodyRow.anchorMin = new Vector2(0.5f, 0.5f);
        BodyRow.anchorMax = new Vector2(0.5f, 0.5f);
        BodyRow.pivot = new Vector2(0.5f, 0.5f);
        BodyRow.anchoredPosition = Vector2.zero;
        BodyRow.sizeDelta = new Vector2(w - 40f, h - 110f);

        // Close button.
        var closeGo = new GameObject("Close");
        closeGo.transform.SetParent(panel.transform, false);
        var cr = closeGo.AddComponent<RectTransform>();
        cr.anchorMin = new Vector2(0.5f, 0f);
        cr.anchorMax = new Vector2(0.5f, 0f);
        cr.pivot = new Vector2(0.5f, 0f);
        cr.anchoredPosition = new Vector2(0f, 16f);
        cr.sizeDelta = new Vector2(180f, 40f);
        var cimg = closeGo.AddComponent<Image>();
        cimg.color = new Color(0.2f, 0.2f, 0.28f, 0.95f);
        var btnGo = closeGo.AddComponent<Button>();
        btnGo.targetGraphic = cimg;
        var ci = closeGo.GetComponent<Image>();
        btnGo.onClick.AddListener(Close);
        var label = new GameObject("Label");
        label.transform.SetParent(closeGo.transform, false);
        var lr = label.AddComponent<RectTransform>();
        lr.anchorMin = Vector2.zero;
        lr.anchorMax = Vector2.one;
        lr.offsetMin = Vector2.zero;
        lr.offsetMax = Vector2.zero;
        var ltmp = label.AddComponent<TextMeshProUGUI>();
        GameManager.Instance?.UIManager?.ApplyDefaultFont(ltmp);
        ltmp.text = Localization.T("Đóng");
        ltmp.fontSize = 18;
        ltmp.color = Color.white;
        ltmp.alignment = TextAlignmentOptions.Center;

        canvasGo.SetActive(false);
        PaletteCanvas = canvasGo.transform;
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
        PaletteCanvas.gameObject.SetActive(true);
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
                GameInput.SetCursorLocked(true);
        }
        PaletteCanvas.gameObject.SetActive(false);
    }

    private void Update()
    {
        if (!IsShown) return;
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            Close();
    }

    protected abstract void Refresh();
}
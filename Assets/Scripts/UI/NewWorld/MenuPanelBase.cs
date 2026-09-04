using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Base helper for Phase 8 modal menu panels (Task 8.2). Creates a full-screen dimmed overlay
/// with a centred panel, a title, body region and a close button. Subclasses populate the body
/// and control visibility. Uses the project's <see cref="ColorPalette.UIBackdrop"/> and a
/// default TMP font via <see cref="GameManager"/>.
/// </summary>
public abstract class MenuPanelBase : MonoBehaviour
{
    protected RectTransform PanelRect;
    protected RectTransform BodyRow;

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
        scaler.referenceResolution = new Vector2(1920f, 1080f);

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
        var w = Mathf.Min(Screen.width * 0.7f, 720f);
        var h = Mathf.Min(Screen.height * 0.7f, 560f);
        PanelRect.sizeDelta = new Vector2(w, h);
        var panelImg = panel.AddComponent<Image>();
        panelImg.color = ColorPalette.UIBackdrop;

        // Title.
        var titleGo = new GameObject("Title");
        titleGo.transform.SetParent(panel.transform, false);
        var tr = titleGo.AddComponent<RectTransform>();
        tr.anchorMin = new Vector2(0.5f, 1f);
        tr.anchorMax = new Vector2(0.5f, 1f);
        tr.pivot = new Vector2(0.5f, 1f);
        tr.anchoredPosition = new Vector2(0f, -16f);
        tr.sizeDelta = new Vector2(w - 24f, 30f);
        var tmp = titleGo.AddComponent<TextMeshProUGUI>();
        GameManager.Instance?.UIManager?.ApplyDefaultFont(tmp);
        tmp.text = title;
        tmp.fontSize = Mathf.Max(16f, Screen.height / 50f);
        tmp.color = Color.white;
        tmp.alignment = TextAlignmentOptions.Center;

        // Body row (subclasses add content here).
        var body = new GameObject("Body");
        body.transform.SetParent(panel.transform, false);
        BodyRow = body.AddComponent<RectTransform>();
        BodyRow.anchorMin = new Vector2(0.5f, 0.5f);
        BodyRow.anchorMax = new Vector2(0.5f, 0.5f);
        BodyRow.pivot = new Vector2(0.5f, 0.5f);
        BodyRow.anchoredPosition = Vector2.zero;
        BodyRow.sizeDelta = new Vector2(w - 40f, h - 90f);

        // Close button.
        var closeGo = new GameObject("Close");
        closeGo.transform.SetParent(panel.transform, false);
        var cr = closeGo.AddComponent<RectTransform>();
        cr.anchorMin = new Vector2(0.5f, 0f);
        cr.anchorMax = new Vector2(0.5f, 0f);
        cr.pivot = new Vector2(0.5f, 0f);
        cr.anchoredPosition = new Vector2(0f, 14f);
        cr.sizeDelta = new Vector2(140f, 32f);
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
        ltmp.fontSize = 14;
        ltmp.color = Color.white;
        ltmp.alignment = TextAlignmentOptions.Center;

        canvasGo.SetActive(false);
        PaletteCanvas = canvasGo.transform;
    }

    protected Transform PaletteCanvas;

    /// <summary>Show the panel overlay.</summary>
    public void Show()
    {
        if (PaletteCanvas != null)
        {
            PaletteCanvas.gameObject.SetActive(true);
            Refresh();
        }
    }

    /// <summary>Hide the panel overlay.</summary>
    public void Close()
    {
        if (PaletteCanvas != null)
            PaletteCanvas.gameObject.SetActive(false);
    }

    protected abstract void Refresh();
}
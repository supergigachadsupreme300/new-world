using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// Phase 3D: fast-travel panel. Lists every placed road sign; press a number
// (1-9) to teleport to that destination. Pauses the game while open.
public class FastTravelMenu : MonoBehaviour
{
    public static FastTravelMenu Instance { get; private set; }
    public bool IsOpen { get; private set; }

    private int _openFrame = -10;
    private bool _wasPausedBeforeOpen;
    private Canvas _canvas;
    private GameObject _panel;
    private TMP_Text _titleText;
    private TMP_Text _closeText;
    private GameObject _content;
    private readonly List<GameObject> _rows = new List<GameObject>();
    private List<FastTravelSign> _signs = new List<FastTravelSign>();

    public static FastTravelMenu Ensure()
    {
        if (Instance == null)
        {
            var go = new GameObject("FastTravelMenu");
            Instance = go.AddComponent<FastTravelMenu>();
        }
        return Instance;
    }

    private void Awake()
    {
        if (Instance == null)
            Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private void Update()
    {
        if (!IsOpen)
            return;

        var kb = UnityEngine.InputSystem.Keyboard.current;
        bool closePressed = (kb != null && kb.escapeKey.wasPressedThisFrame)
            || (((kb != null && kb.eKey.wasPressedThisFrame) || MobileInputController.Consume("interact"))
                && Time.frameCount > _openFrame);
        if (closePressed)
        {
            Close();
            return;
        }

        if (kb == null)
            return;
        for (int i = 0; i < _signs.Count && i < 9; i++)
        {
            if (kb[UnityEngine.InputSystem.Key.Digit1 + i].wasPressedThisFrame)
            {
                TravelTo(_signs[i]);
                return;
            }
        }
    }

    public void Open()
    {
        EnsurePanel();
        if (_panel == null)
            return;
        var found = Object.FindObjectsByType<FastTravelSign>(FindObjectsSortMode.None);
        _signs = new List<FastTravelSign>(found);
        _signs.Sort((a, b) => a.Index.CompareTo(b.Index));
        if (_signs.Count == 0)
        {
            GameManager.Instance?.UIManager?.ShowMessage(
                Localization.T("Chưa có biển báo nào trên bản đồ."), 2f);
            return;
        }
        _panel.SetActive(true);
        IsOpen = true;
        _openFrame = Time.frameCount;
        _wasPausedBeforeOpen = GameManager.Instance != null && GameManager.Instance.GamePaused;
        if (GameManager.Instance != null)
            GameManager.Instance.TogglePause(true);
        GameManager.Instance?.UIManager?.ShowPauseMenu(false);
        Refresh();
    }

    public void Close()
    {
        if (!IsOpen)
            return;
        IsOpen = false;
        if (_panel != null)
            _panel.SetActive(false);
        if (_wasPausedBeforeOpen)
            GameManager.Instance?.UIManager?.ShowPauseMenu(true);
        else
            GameManager.Instance?.TogglePause(false);
    }

    private void TravelTo(FastTravelSign sign)
    {
        var player = GameManager.Instance?.Player;
        if (player == null || sign == null)
            return;
        player.transform.position = sign.transform.position + Vector3.up * 1f;
        GameManager.Instance?.UIManager?.ShowMessage(
            Localization.F("Đã di chuyển đến {0}!", Localization.T(sign.Label)), 2f);
        Close();
    }

    private void Refresh()
    {
        ClearRows();
        bool mobile = GameInput.IsMobile;
        if (_titleText != null)
            _titleText.text = Localization.T("Di Chuyển Nhanh");
        if (_closeText != null)
            _closeText.text = mobile
                ? Localization.T("[Đóng] (Chạm)")
                : Localization.T("[Đóng] Ấn E");

        for (int i = 0; i < _signs.Count; i++)
        {
            int num = i + 1;
            CreateRow("TravelRow" + i,
                string.Format("{0}. {1}", num, Localization.T(_signs[i].Label)),
                new Color(0.8f, 0.9f, 0.85f),
                Localization.F("Đi {0}", num), () => TravelTo(_signs[i]));
        }

        var contentRt = _content != null ? _content.GetComponent<RectTransform>() : null;
        if (contentRt != null)
            contentRt.sizeDelta = new Vector2(0f,
                4f + _rows.Count * 40f + (_rows.Count > 0 ? (_rows.Count - 1) * 4f : 0f) + 4f);
    }

    private void CreateRow(string rowName, string label, Color color, string buttonText,
        UnityEngine.Events.UnityAction onClick)
    {
        var row = new GameObject(rowName);
        row.transform.SetParent(_content.transform, false);
        var rowRt = row.AddComponent<RectTransform>();
        rowRt.anchorMin = new Vector2(0f, 1f);
        rowRt.anchorMax = new Vector2(1f, 1f);
        rowRt.pivot = new Vector2(0.5f, 1f);
        rowRt.anchoredPosition = Vector2.zero;
        rowRt.sizeDelta = new Vector2(0f, 40f);
        var rowLE = row.AddComponent<LayoutElement>();
        rowLE.preferredHeight = 40f;
        rowLE.flexibleHeight = 0f;
        rowLE.flexibleWidth = 1f;

        var rowImg = row.AddComponent<Image>();
        rowImg.color = ColorPalette.UIBackdrop;

        var labelText = CountryLife.Helpers.UIHelper.MakeText(rowName + "_Txt", rowRt,
            Vector2.zero, label, 17, color, new Vector2(240f, 34f), true, true,
            TextAlignmentOptions.Left, false);
        labelText.rectTransform.anchorMin = new Vector2(0f, 0f);
        labelText.rectTransform.anchorMax = new Vector2(1f, 1f);
        labelText.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        labelText.rectTransform.anchoredPosition = new Vector2(-20f, 0f);
        labelText.rectTransform.sizeDelta = new Vector2(-120f, 0f);

        var goBtn = new GameObject(rowName + "_Btn");
        goBtn.transform.SetParent(rowRt, false);
        var btnRt = goBtn.AddComponent<RectTransform>();
        btnRt.anchorMin = new Vector2(1f, 0f);
        btnRt.anchorMax = new Vector2(1f, 1f);
        btnRt.pivot = new Vector2(1f, 0.5f);
        btnRt.anchoredPosition = new Vector2(-6f, 0f);
        btnRt.sizeDelta = new Vector2(60f, 24f);

        var btnImg = goBtn.AddComponent<Image>();
        btnImg.color = new Color(0.8f, 0.62f, 0.3f);
        var btn = goBtn.AddComponent<Button>();
        btn.targetGraphic = btnImg;
        btn.onClick.AddListener(onClick);

        CountryLife.Helpers.UIHelper.MakeText(rowName + "_BtnTxt", btnRt,
            Vector2.zero, buttonText, 14, Color.white, new Vector2(54f, 20f), true, true,
            TextAlignmentOptions.Center, false);

        _rows.Add(row);
    }

    private void ClearRows()
    {
        foreach (var row in _rows)
        {
            if (row != null)
                Destroy(row);
        }
        _rows.Clear();
    }

    private void EnsurePanel()
    {
        if (_panel != null)
            return;
        var hud = GameObject.Find("HUD_Canvas");
        _canvas = hud != null ? hud.GetComponent<Canvas>() : Object.FindAnyObjectByType<Canvas>();
        if (_canvas == null)
            return;

        float sw = Screen.width;
        float sh = Screen.height;

        _panel = new GameObject("FastTravelPanel");
        _panel.transform.SetParent(_canvas.transform, false);
        var rt = _panel.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = new Vector2(sw * 0.55f, sh * 0.6f);

        var img = _panel.AddComponent<Image>();
        img.color = ColorPalette.UIBackdrop;

        float panelW = rt.sizeDelta.x;
        float panelH = rt.sizeDelta.y;

        _titleText = MakeText("FastTravelTitle", rt, new Vector2(0f, panelH * 0.38f),
            "", 24, new Color(0.95f, 0.8f, 0.5f), new Vector2(panelW - 40f, 34f));

        float contentW = panelW - 40f;
        float viewportH = panelH * 0.55f;
        float scrollbarW = 12f;

        var viewportGo = new GameObject("FastTravelViewport");
        viewportGo.transform.SetParent(_panel.transform, false);
        var viewportRt = viewportGo.AddComponent<RectTransform>();
        viewportRt.anchorMin = new Vector2(0.5f, 0.5f);
        viewportRt.anchorMax = new Vector2(0.5f, 0.5f);
        viewportRt.pivot = new Vector2(0.5f, 0.5f);
        viewportRt.anchoredPosition = new Vector2(0f, -panelH * 0.05f);
        viewportRt.sizeDelta = new Vector2(contentW, viewportH);
        viewportGo.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0f);
        viewportGo.AddComponent<RectMask2D>();

        _content = new GameObject("FastTravelContent");
        _content.transform.SetParent(viewportGo.transform, false);
        var contentRt = _content.AddComponent<RectTransform>();
        contentRt.anchorMin = new Vector2(0f, 1f);
        contentRt.anchorMax = new Vector2(1f, 1f);
        contentRt.pivot = new Vector2(0.5f, 1f);
        contentRt.anchoredPosition = Vector2.zero;
        contentRt.sizeDelta = new Vector2(0f, 0f);
        var vlg = _content.AddComponent<VerticalLayoutGroup>();
        vlg.spacing = 4f;
        vlg.padding = new RectOffset(4, 4, 4, 4);
        vlg.childControlWidth = true;
        vlg.childControlHeight = false;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;

        var scrollbarGo = new GameObject("FastTravelScrollbar");
        scrollbarGo.transform.SetParent(_panel.transform, false);
        var scrollbarRt = scrollbarGo.AddComponent<RectTransform>();
        scrollbarRt.anchorMin = new Vector2(0.5f, 0.5f);
        scrollbarRt.anchorMax = new Vector2(0.5f, 0.5f);
        scrollbarRt.pivot = new Vector2(0.5f, 0.5f);
        scrollbarRt.anchoredPosition = new Vector2(contentW * 0.5f - scrollbarW * 0.5f - 2f, -panelH * 0.05f);
        scrollbarRt.sizeDelta = new Vector2(scrollbarW, viewportH);
        var scrollbarImg = scrollbarGo.AddComponent<Image>();
        scrollbarImg.color = new Color(0.15f, 0.15f, 0.15f, 0.6f);
        var scrollbar = scrollbarGo.AddComponent<Scrollbar>();

        var handleArea = new GameObject("SlidingArea");
        handleArea.transform.SetParent(scrollbarGo.transform, false);
        var haRt = handleArea.AddComponent<RectTransform>();
        haRt.anchorMin = Vector2.zero;
        haRt.anchorMax = Vector2.one;
        haRt.offsetMin = new Vector2(2f, 4f);
        haRt.offsetMax = new Vector2(-2f, -4f);
        var handle = new GameObject("Handle");
        handle.transform.SetParent(handleArea.transform, false);
        var hRt = handle.AddComponent<RectTransform>();
        hRt.anchorMin = Vector2.zero;
        hRt.anchorMax = new Vector2(1f, 0.3f);
        hRt.offsetMin = Vector2.zero;
        hRt.offsetMax = Vector2.zero;
        var hImg = handle.AddComponent<Image>();
        hImg.color = new Color(0.5f, 0.5f, 0.5f, 0.8f);
        scrollbar.handleRect = hRt;
        scrollbar.targetGraphic = hImg;
        scrollbar.direction = Scrollbar.Direction.BottomToTop;

        var scrollRect = viewportGo.AddComponent<ScrollRect>();
        scrollRect.viewport = viewportRt;
        scrollRect.content = contentRt;
        scrollRect.horizontal = false;
        scrollRect.vertical = true;
        scrollRect.verticalScrollbar = scrollbar;
        scrollRect.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHideAndExpandViewport;
        scrollRect.verticalScrollbarSpacing = 0f;
        scrollRect.movementType = ScrollRect.MovementType.Clamped;
        scrollRect.scrollSensitivity = 30f;

        _closeText = MakeText("FastTravelClose", rt, new Vector2(0f, -panelH * 0.4f),
            "", 16, new Color(0.9f, 0.9f, 0.9f), new Vector2(panelW - 40f, 26f));

        _panel.SetActive(false);
    }

    private TMP_Text MakeText(string name, RectTransform parent, Vector2 position, string text,
        int fontSize, Color color, Vector2 size)
        => CountryLife.Helpers.UIHelper.MakeText(name, parent, position, text, fontSize, color, size, true, true, TextAlignmentOptions.Left, false);
}
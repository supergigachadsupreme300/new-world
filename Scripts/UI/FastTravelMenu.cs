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

        float y = -8f;
        for (int i = 0; i < _signs.Count; i++)
        {
            int num = i + 1;
            CreateRow("TravelRow" + i,
                string.Format("{0}. {1}", num, Localization.T(_signs[i].Label)),
                new Color(0.8f, 0.9f, 0.85f), y,
                Localization.F("Đi {0}", num), () => TravelTo(_signs[i]));
            y -= 46f;
        }
    }

    private void CreateRow(string rowName, string label, Color color, float y, string buttonText,
        UnityEngine.Events.UnityAction onClick)
    {
        var row = new GameObject(rowName);
        row.transform.SetParent(_content.transform, false);
        var rowRt = row.AddComponent<RectTransform>();
        rowRt.anchorMin = new Vector2(0f, 1f);
        rowRt.anchorMax = new Vector2(1f, 1f);
        rowRt.pivot = new Vector2(0.5f, 1f);
        rowRt.anchoredPosition = new Vector2(0f, y);
        rowRt.sizeDelta = new Vector2(0f, 40f);

        var rowImg = row.AddComponent<Image>();
        rowImg.color = ColorPalette.UIBackdrop;

        var labelText = CountryLife.Helpers.UIHelper.MakeText(rowName + "_Txt", rowRt,
            new Vector2(-60f, 0f), label, 17, color, new Vector2(240f, 34f), true, true,
            TextAlignmentOptions.Left, false);
        labelText.rectTransform.anchorMin = new Vector2(0f, 0.5f);
        labelText.rectTransform.anchorMax = new Vector2(1f, 0.5f);
        labelText.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        labelText.rectTransform.anchoredPosition = new Vector2(-35f, 0f);

        var goBtn = new GameObject(rowName + "_Btn");
        goBtn.transform.SetParent(rowRt, false);
        var btnRt = goBtn.AddComponent<RectTransform>();
        btnRt.anchorMin = new Vector2(1f, 0f);
        btnRt.anchorMax = new Vector2(1f, 1f);
        btnRt.pivot = new Vector2(1f, 0.5f);
        btnRt.anchoredPosition = new Vector2(-8f, 0f);
        btnRt.sizeDelta = new Vector2(88f, 32f);

        var btnImg = goBtn.AddComponent<Image>();
        btnImg.color = new Color(0.8f, 0.62f, 0.3f);
        var btn = goBtn.AddComponent<Button>();
        btn.targetGraphic = btnImg;
        btn.onClick.AddListener(onClick);

        CountryLife.Helpers.UIHelper.MakeText(rowName + "_BtnTxt", btnRt,
            Vector2.zero, buttonText, 16, Color.white, new Vector2(80f, 28f), true, true,
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

        float sw = 1920f;
        float sh = 1080f;

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

        _content = new GameObject("FastTravelContent");
        _content.transform.SetParent(_panel.transform, false);
        var contentRt = _content.AddComponent<RectTransform>();
        contentRt.anchorMin = new Vector2(0.5f, 0.5f);
        contentRt.anchorMax = new Vector2(0.5f, 0.5f);
        contentRt.pivot = new Vector2(0.5f, 0.5f);
        contentRt.anchoredPosition = new Vector2(0f, -panelH * 0.05f);
        contentRt.sizeDelta = new Vector2(panelW - 40f, panelH * 0.55f);

        _closeText = MakeText("FastTravelClose", rt, new Vector2(0f, -panelH * 0.4f),
            "", 16, new Color(0.9f, 0.9f, 0.9f), new Vector2(panelW - 40f, 26f));

        _panel.SetActive(false);
    }

    private TMP_Text MakeText(string name, RectTransform parent, Vector2 position, string text,
        int fontSize, Color color, Vector2 size)
        => CountryLife.Helpers.UIHelper.MakeText(name, parent, position, text, fontSize, color, size, true, true, TextAlignmentOptions.Left, false);
}
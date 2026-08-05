using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using TMPro;

public class SleepManager : MonoBehaviour
{
    private bool _initialized;
    private bool _wasPausedBeforeOpen;
    private Canvas _canvas;
    private GameObject _panel;
    private Slider _slider;
    private TMP_Text _infoText;
    private TMP_Text _previewText;

    private const float StaminaPerHour = 150f;
    private const float HpPerHour = 12f;

    void Start()
    {
        if (!_initialized)
            Initialize();
    }

    void Update()
    {
        if (_panel != null && _panel.activeSelf && Keyboard.current != null)
        {
            if (Keyboard.current.escapeKey.wasPressedThisFrame || Keyboard.current.xKey.wasPressedThisFrame)
                Close();
        }
    }

    public void Initialize()
    {
        if (_initialized)
            return;
        _initialized = true;
        var hudGo = GameObject.Find("HUD_Canvas");
        _canvas = hudGo != null ? hudGo.GetComponent<Canvas>() : Object.FindAnyObjectByType<Canvas>();
        if (_canvas == null)
            return;

        float sw = Screen.width;
        float sh = Screen.height;
        float panelW = Mathf.Min(sw * 0.5f, 480f);
        float panelH = Mathf.Min(sh * 0.55f, 360f);
        float fontS = Mathf.Max(14f, sh / 40f);
        float btnH = sh * 0.055f;

        _panel = new GameObject("SleepPanel");
        _panel.transform.SetParent(_canvas.transform, false);
        var rect = _panel.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(panelW, panelH);
        var img = _panel.AddComponent<Image>();
        img.color = new Color(0.18f, 0.2f, 0.27f, 0.95f);
        img.raycastTarget = false;

        MakeText("SleepTitle", _panel.transform, Localization.T("Giấc Ngủ"),
            new Vector2(0f, panelH * 0.36f), new Vector2(panelW - 40f, fontS * 1.8f),
            (int)(fontS * 1.4f), TextAlignmentOptions.Center);

        _infoText = MakeText("SleepInfo", _panel.transform, "",
            new Vector2(0f, panelH * 0.2f), new Vector2(panelW - 40f, fontS * 1.4f),
            (int)fontS, TextAlignmentOptions.Center);

        _slider = MakeSlider("SleepSlider", _panel.transform,
            new Vector2(0f, panelH * 0.05f), new Vector2(panelW * 0.7f, 30f),
            1f, 12f, 8f);

        _previewText = MakeText("SleepPreview", _panel.transform, "",
            new Vector2(0f, -panelH * 0.12f), new Vector2(panelW - 40f, fontS * 1.4f),
            (int)fontS, TextAlignmentOptions.Center);

        MakeButton("SleepConfirm", _panel.transform, Localization.T("Ngủ"),
            new Vector2(-panelW * 0.14f, -panelH * 0.32f), new Vector2(panelW * 0.26f, btnH),
            (int)fontS, new Color(0.3f, 0.6f, 0.42f), Sleep);

        MakeButton("SleepCancel", _panel.transform, Localization.T("Hủy"),
            new Vector2(panelW * 0.14f, -panelH * 0.32f), new Vector2(panelW * 0.26f, btnH),
            (int)fontS, new Color(0.75f, 0.38f, 0.41f), Close);

        _slider.onValueChanged.AddListener(_ => UpdatePreview());
        UpdatePreview();

        _panel.SetActive(false);
    }

    public void Open()
    {
        if (!_initialized)
            Initialize();
        if (_panel == null)
            return;

        _wasPausedBeforeOpen = GameManager.Instance != null && GameManager.Instance.GamePaused;
        _panel.SetActive(true);

        if (GameManager.Instance != null)
            GameManager.Instance.TogglePause(true);
        if (GameManager.Instance?.UIManager != null)
            GameManager.Instance.UIManager.ShowPauseMenu(false);

        UpdatePreview();
    }

    public void Close()
    {
        if (_panel != null)
            _panel.SetActive(false);

        if (_wasPausedBeforeOpen)
        {
            if (GameManager.Instance?.UIManager != null)
                GameManager.Instance.UIManager.ShowPauseMenu(true);
            return;
        }

        if (GameManager.Instance != null)
            GameManager.Instance.TogglePause(false);
    }

    private void Sleep()
    {
        int hours = Mathf.RoundToInt(_slider.value);
        var player = GameManager.Instance?.Player;
        if (player != null)
        {
            player.Stamina = Mathf.Min(player.MaxStamina, player.Stamina + hours * StaminaPerHour);
            player.HP = Mathf.Min(player.MaxHP, player.HP + Mathf.RoundToInt(hours * HpPerHour));
            GameManager.Instance?.UIManager?.UpdatePlayerHud(player.HP, player.MaxHP, player.Stamina, player.MaxStamina, player.Money);
        }
        GameManager.Instance?.AdvanceTime(hours);
        GameManager.Instance?.UIManager?.ShowMessage(Localization.F("Bạn đã ngủ {0} tiếng.", hours), 2f);
        Close();
    }

    private void UpdatePreview()
    {
        int hours = Mathf.RoundToInt(_slider.value);
        if (_infoText != null)
            _infoText.text = Localization.F("Ngủ {0} tiếng", hours);

        var player = GameManager.Instance?.Player;
        if (_previewText == null || player == null)
            return;
        int stam = Mathf.RoundToInt(Mathf.Min(player.MaxStamina, player.Stamina + hours * StaminaPerHour) - player.Stamina);
        int hp = Mathf.Min(player.MaxHP, player.HP + Mathf.RoundToInt(hours * HpPerHour)) - player.HP;
        _previewText.text = Localization.F("Hồi phục: +{0} Stamina / +{1} HP", Mathf.Max(0, stam), Mathf.Max(0, hp));
    }

    private Slider MakeSlider(string name, Transform parent, Vector2 pos, Vector2 size, float min, float max, float value)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;

        var slider = go.AddComponent<Slider>();
        slider.minValue = min;
        slider.maxValue = max;
        slider.wholeNumbers = true;
        slider.value = value;

        var bgGo = new GameObject("Background");
        bgGo.transform.SetParent(go.transform, false);
        var bgRect = bgGo.AddComponent<RectTransform>();
        bgRect.anchorMin = new Vector2(0f, 0.25f);
        bgRect.anchorMax = new Vector2(1f, 0.75f);
        bgRect.sizeDelta = Vector2.zero;
        bgGo.AddComponent<Image>().color = new Color(0.1f, 0.1f, 0.1f, 0.9f);

        var fillAreaGo = new GameObject("FillArea");
        fillAreaGo.transform.SetParent(go.transform, false);
        var fillAreaRect = fillAreaGo.AddComponent<RectTransform>();
        fillAreaRect.anchorMin = new Vector2(0f, 0.25f);
        fillAreaRect.anchorMax = new Vector2(1f, 0.75f);
        fillAreaRect.sizeDelta = Vector2.zero;

        var fillGo = new GameObject("Fill");
        fillGo.transform.SetParent(fillAreaRect, false);
        var fillRect = fillGo.AddComponent<RectTransform>();
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        fillRect.sizeDelta = Vector2.zero;
        fillGo.AddComponent<Image>().color = new Color(0.3f, 0.7f, 0.4f, 0.9f);

        var handleAreaGo = new GameObject("HandleSlideArea");
        handleAreaGo.transform.SetParent(go.transform, false);
        var handleAreaRect = handleAreaGo.AddComponent<RectTransform>();
        handleAreaRect.anchorMin = Vector2.zero;
        handleAreaRect.anchorMax = Vector2.one;
        handleAreaRect.sizeDelta = Vector2.zero;

        var handleGo = new GameObject("Handle");
        handleGo.transform.SetParent(handleAreaRect, false);
        var handleRect = handleGo.AddComponent<RectTransform>();
        handleRect.sizeDelta = new Vector2(24f, 24f);
        var handleImg = handleGo.AddComponent<Image>();
        handleImg.color = Color.white;

        slider.fillRect = fillRect;
        slider.handleRect = handleRect;
        slider.targetGraphic = handleImg;
        return slider;
    }

    private TMP_Text MakeText(string name, Transform parent, string text, Vector2 pos, Vector2 size, int fontSize, TextAlignmentOptions align)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        var tmp = go.AddComponent<TextMeshProUGUI>();
        if (GameManager.Instance?.UIManager?.defaultTmpFont != null)
            tmp.font = GameManager.Instance.UIManager.defaultTmpFont;
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.color = Color.white;
        tmp.alignment = align;
        tmp.raycastTarget = false;
        return tmp;
    }

    private Button MakeButton(string name, Transform parent, string label, Vector2 pos, Vector2 size, int fontSize, Color color, UnityEngine.Events.UnityAction callback)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        var img = go.AddComponent<Image>();
        img.color = color;
        var btn = go.AddComponent<Button>();
        btn.targetGraphic = img;
        btn.onClick.AddListener(callback);

        var textGO = new GameObject("Text");
        textGO.transform.SetParent(go.transform, false);
        var tr = textGO.AddComponent<RectTransform>();
        tr.anchorMin = Vector2.zero;
        tr.anchorMax = Vector2.one;
        tr.offsetMin = Vector2.zero;
        tr.offsetMax = Vector2.zero;
        var tmp = textGO.AddComponent<TextMeshProUGUI>();
        if (GameManager.Instance?.UIManager?.defaultTmpFont != null)
            tmp.font = GameManager.Instance.UIManager.defaultTmpFont;
        tmp.text = label;
        tmp.fontSize = fontSize;
        tmp.color = Color.white;
        tmp.alignment = TextAlignmentOptions.Center;

        return btn;
    }
}

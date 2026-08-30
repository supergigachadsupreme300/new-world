using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// In-game panel opened by pressing E on the goblin: shows its stats and lets the
/// player issue a command (follow / stay / go home). Pauses the game while open.
/// </summary>
public class GoblinCommandMenu : MonoBehaviour
{
    public static GoblinCommandMenu Instance { get; private set; }
    public bool IsOpen { get; private set; }

    private GoblinPet _goblin;
    private int _openFrame = -10;
    private Canvas _canvas;
    private GameObject _panel;
    private TMP_Text _nameText;
    private TMP_Text _hpText;
    private TMP_Text _statusText;
    private TMP_Text _promptText;
    private GameObject _followRow;
    private GameObject _stayRow;
    private GameObject _homeRow;
    private GameObject _closeRow;
    private TMP_Text _followText;
    private TMP_Text _stayText;
    private TMP_Text _homeText;
    private TMP_Text _closeText;

    public static GoblinCommandMenu Ensure()
    {
        if (Instance == null)
        {
            var go = new GameObject("GoblinCommandMenu");
            Instance = go.AddComponent<GoblinCommandMenu>();
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
        if (kb != null && _goblin != null)
        {
            if (kb.digit1Key.wasPressedThisFrame)
                Apply(GoblinPet.CommandMode.Follow);
            if (kb.digit2Key.wasPressedThisFrame)
                Apply(GoblinPet.CommandMode.Stay);
            if (kb.digit3Key.wasPressedThisFrame)
                Apply(GoblinPet.CommandMode.GoHome);
        }

        bool closePressed = (kb != null && kb.escapeKey.wasPressedThisFrame)
            || (((kb != null && kb.eKey.wasPressedThisFrame) || MobileInputController.Consume("interact"))
                && Time.frameCount > _openFrame);
        if (closePressed)
            Close();
    }

    public void Open(GoblinPet goblin)
    {
        if (goblin == null)
            return;
        _goblin = goblin;
        EnsurePanel();
        if (_panel == null)
            return;
        _panel.SetActive(true);
        IsOpen = true;
        _openFrame = Time.frameCount;
        GameManager.Instance?.TogglePause(true);
        Refresh();
    }

    public void Close()
    {
        if (!IsOpen)
            return;
        IsOpen = false;
        if (_panel != null)
            _panel.SetActive(false);
        _goblin = null;
        GameManager.Instance?.TogglePause(false);
    }

    private void Apply(GoblinPet.CommandMode mode)
    {
        if (_goblin == null)
            return;
        _goblin.SetCommand(mode);
        Refresh();
        GameManager.Instance?.UIManager?.ShowMessage(Localization.T("Goblin đã nhận lệnh."), 1.5f);
    }

    private void Refresh()
    {
        if (_goblin == null)
            return;
        bool mobile = GameInput.IsMobile;
        if (_nameText != null)
            _nameText.text = Localization.T("Goblin");
        if (_hpText != null)
            _hpText.text = Localization.F("Máu: {0}/{1}", _goblin.Health, _goblin.MaxHealth);
        if (_statusText != null)
            _statusText.text = StatusText();
        if (_followText != null)
            _followText.text = mobile ? Localization.T("[Theo Dõi] (Chạm)") : Localization.T("[Theo Dõi] Ấn 1");
        if (_stayText != null)
            _stayText.text = mobile ? Localization.T("[Đứng Yên] (Chạm)") : Localization.T("[Đứng Yên] Ấn 2");
        if (_homeText != null)
            _homeText.text = mobile ? Localization.T("[Về Nhà] (Chạm)") : Localization.T("[Về Nhà] Ấn 3");
        if (_closeText != null)
            _closeText.text = mobile ? Localization.T("[Đóng] (Chạm)") : Localization.T("[Đóng] Ấn E");
        if (_promptText != null)
            _promptText.text = Localization.T("Ra lệnh cho goblin...");
    }

    private string StatusText()
    {
        if (_goblin.IsDead)
            return Localization.T("Đã chết (hồi sinh vào ban ngày)");
        if (_goblin.IsHiddenInHut)
            return Localization.T("Đang trốn trong chuồng");

        if (_goblin.IsHoldingSeed)
            return Localization.F("Đang gieo hạt: {0}", Localization.ItemName(_goblin.HeldSeedType));

        switch (_goblin.Command)
        {
            case GoblinPet.CommandMode.Stay:
                return Localization.T("Đứng yên tại chỗ");
            case GoblinPet.CommandMode.GoHome:
                return Localization.T("Đang về nhà nghỉ ngơi");
            default:
                return Localization.T("Đang theo dõi chủ nhân");
        }
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

        _panel = new GameObject("GoblinCommandPanel");
        _panel.transform.SetParent(_canvas.transform, false);
        var rt = _panel.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = new Vector2(sw * 0.5f, sh * 0.55f);

        var img = _panel.AddComponent<Image>();
        img.color = ColorPalette.UIBackdrop;

        float panelW = rt.sizeDelta.x;
        float panelH = rt.sizeDelta.y;

        _nameText = MakeText("GoblinCmdName", rt, new Vector2(0f, panelH * 0.38f),
            Localization.T("Goblin"), 26, new Color(0.7f, 0.95f, 0.45f), new Vector2(panelW - 40f, 34f));
        _hpText = MakeText("GoblinCmdHp", rt, new Vector2(0f, panelH * 0.24f),
            "", 20, Color.white, new Vector2(panelW - 40f, 30f));
        _statusText = MakeText("GoblinCmdStatus", rt, new Vector2(0f, panelH * 0.12f),
            "", 18, new Color(0.85f, 0.85f, 0.9f), new Vector2(panelW - 40f, 30f));
        _promptText = MakeText("GoblinCmdPrompt", rt, new Vector2(0f, -panelH * 0.04f),
            "", 15, new Color(0.7f, 0.7f, 0.7f), new Vector2(panelW - 40f, 26f));

        CreateRow("GoblinCmdFollowRow", "GoblinCmdFollowText", 4f,
            new Color(0.8f, 0.95f, 0.6f), out _followRow, out _followText,
            () => Apply(GoblinPet.CommandMode.Follow));
        CreateRow("GoblinCmdStayRow", "GoblinCmdStayText", -44f,
            new Color(0.95f, 0.85f, 0.45f), out _stayRow, out _stayText,
            () => Apply(GoblinPet.CommandMode.Stay));
        CreateRow("GoblinCmdHomeRow", "GoblinCmdHomeText", -94f,
            new Color(0.6f, 0.8f, 0.95f), out _homeRow, out _homeText,
            () => Apply(GoblinPet.CommandMode.GoHome));
        CreateRow("GoblinCmdCloseRow", "GoblinCmdCloseText", -150f,
            new Color(0.9f, 0.9f, 0.9f), out _closeRow, out _closeText, Close);

        _panel.SetActive(false);
    }

    private void CreateRow(string rowName, string textName, float yOffset, Color textColor,
        out GameObject row, out TMP_Text text, UnityEngine.Events.UnityAction onClick)
    {
        row = new GameObject(rowName);
        row.transform.SetParent(_panel.transform, false);
        var rowRt = row.AddComponent<RectTransform>();
        rowRt.anchorMin = new Vector2(1f, 1f);
        rowRt.anchorMax = new Vector2(1f, 1f);
        rowRt.pivot = new Vector2(1f, 1f);
        rowRt.anchoredPosition = new Vector2(-20f, yOffset);
        rowRt.sizeDelta = new Vector2(320f, 40f);

        var rowImg = row.AddComponent<Image>();
        rowImg.color = ColorPalette.UIBackdrop;
        rowImg.raycastTarget = true;

        var rowBtn = row.AddComponent<Button>();
        rowBtn.targetGraphic = rowImg;
        rowBtn.onClick.AddListener(onClick);

        text = MakeText(textName, rowRt, new Vector2(0f, 0f), "", 18, textColor, new Vector2(292f, 36f));
        text.alignment = TextAlignmentOptions.Left;

        row.SetActive(false);
    }

    private TMP_Text MakeText(string name, RectTransform parent, Vector2 position, string text,
        int fontSize, Color color, Vector2 size)
        => CountryLife.Helpers.UIHelper.MakeText(name, parent, position, text, fontSize, color, size, true, true, TextAlignmentOptions.Left, false);
}
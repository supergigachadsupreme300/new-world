using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Chest panel in front of the goblin hut: shows what the goblin stored and lets
/// the player take crops into their own inventory. Pauses the game while open.
/// </summary>
public class GoblinChestMenu : MonoBehaviour
{
    public static GoblinChestMenu Instance { get; private set; }
    public bool IsOpen { get; private set; }

    private GoblinPet _goblin;
    private int _openFrame = -10;
    private bool _wasPausedBeforeOpen;
    private Canvas _canvas;
    private GameObject _panel;
    private TMP_Text _titleText;
    private TMP_Text _statusText;
    private TMP_Text _closeText;
    private GameObject _content;
    private readonly List<GameObject> _rows = new List<GameObject>();

    public static GoblinChestMenu Ensure()
    {
        if (Instance == null)
        {
            var go = new GameObject("GoblinChestMenu");
            Instance = go.AddComponent<GoblinChestMenu>();
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
            Close();
    }

    public void Open()
    {
        EnsurePanel();
        if (_panel == null)
            return;
        _goblin = GoblinPet.Instance;
        if (_goblin == null)
        {
            GameManager.Instance?.UIManager?.ShowMessage(Localization.T("Goblin chưa ở đây."), 2f);
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
        _goblin = null;
        if (_wasPausedBeforeOpen)
            GameManager.Instance?.UIManager?.ShowPauseMenu(true);
        else
            GameManager.Instance?.TogglePause(false);
    }

    private void TakeOne(string cropType)
    {
        if (_goblin == null || string.IsNullOrEmpty(cropType))
            return;
        if (!ToolManager.Instance.CanHoldItem(cropType))
        {
            GameManager.Instance?.UIManager?.ShowMessage(Localization.T("Túi đồ đầy."), 1.5f);
            return;
        }
        if (_goblin.RemoveFromStorage(cropType, 1) && ToolManager.Instance.AddItem(cropType, 1))
            GameManager.Instance?.UIManager?.ShowMessage(
                Localization.F("Đã lấy {0} từ kho.", Localization.ItemName(cropType)), 1.5f);
        else
            _goblin.AddToStorage(cropType, 1);
        Refresh();
    }

    private void Refresh()
    {
        ClearRows();
        if (_goblin == null)
            return;

        bool mobile = GameInput.IsMobile;
        if (_titleText != null)
            _titleText.text = Localization.T("Kho goblin");
        if (_statusText != null)
            _statusText.text = StatusText();

        float y = -40f;
        foreach (var kv in _goblin.GetAllStored())
        {
            var type = kv.Key;
            int count = kv.Value;
            CreateRow(type, string.Format("{0} x{1}", Localization.ItemName(type), count),
                new Color(0.85f, 0.9f, 0.75f), y, Localization.T("Lấy"), () => TakeOne(type));
            y -= 46f;
        }

        if (_closeText != null)
            _closeText.text = mobile ? Localization.T("[Đóng] (Chạm)") : Localization.T("[Đóng] Ấn E");
    }

    private string StatusText()
    {
        if (_goblin.IsCarryingCrop)
            return Localization.F("Đang cầm: {0}", Localization.ItemName(_goblin.CarriedCrop));
        if (_goblin.IsHoldingSeed)
            return Localization.F("Đang giữ hạt: {0}", Localization.ItemName(_goblin.HeldSeedType));
        return Localization.T("Kho trống, goblin sẽ bỏ nông sản vào đây.");
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

        var takeGo = new GameObject(rowName + "_Btn");
        takeGo.transform.SetParent(rowRt, false);
        var takeRt = takeGo.AddComponent<RectTransform>();
        takeRt.anchorMin = new Vector2(1f, 0f);
        takeRt.anchorMax = new Vector2(1f, 1f);
        takeRt.pivot = new Vector2(1f, 0.5f);
        takeRt.anchoredPosition = new Vector2(-8f, 0f);
        takeRt.sizeDelta = new Vector2(88f, 32f);

        var takeImg = takeGo.AddComponent<Image>();
        takeImg.color = new Color(0.8f, 0.62f, 0.3f);
        var takeBtn = takeGo.AddComponent<Button>();
        takeBtn.targetGraphic = takeImg;
        takeBtn.onClick.AddListener(onClick);

        CountryLife.Helpers.UIHelper.MakeText(rowName + "_BtnTxt", takeRt,
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

        float sw = Screen.width;
        float sh = Screen.height;

        _panel = new GameObject("GoblinChestPanel");
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

        _titleText = MakeText("GoblinChestTitle", rt, new Vector2(0f, panelH * 0.4f),
            "", 24, new Color(0.95f, 0.8f, 0.5f), new Vector2(panelW - 40f, 34f));
        _statusText = MakeText("GoblinChestStatus", rt, new Vector2(0f, panelH * 0.3f),
            "", 17, new Color(0.85f, 0.85f, 0.9f), new Vector2(panelW - 40f, 30f));

        _content = new GameObject("GoblinChestContent");
        _content.transform.SetParent(_panel.transform, false);
        var contentRt = _content.AddComponent<RectTransform>();
        contentRt.anchorMin = new Vector2(0.5f, 0.5f);
        contentRt.anchorMax = new Vector2(0.5f, 0.5f);
        contentRt.pivot = new Vector2(0.5f, 0.5f);
        contentRt.anchoredPosition = new Vector2(0f, -panelH * 0.05f);
        contentRt.sizeDelta = new Vector2(panelW - 40f, panelH * 0.55f);

        _closeText = MakeText("GoblinChestClose", rt, new Vector2(0f, -panelH * 0.42f),
            "", 16, new Color(0.9f, 0.9f, 0.9f), new Vector2(panelW - 40f, 26f));

        _panel.SetActive(false);
    }

    private TMP_Text MakeText(string name, RectTransform parent, Vector2 position, string text,
        int fontSize, Color color, Vector2 size)
        => CountryLife.Helpers.UIHelper.MakeText(name, parent, position, text, fontSize, color, size, true, true, TextAlignmentOptions.Left, false);
}
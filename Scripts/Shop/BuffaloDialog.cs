using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using TMPro;

public class BuffaloDialog : MonoSingleton<BuffaloDialog>
{
private Canvas _canvas;
    private GameObject _panel;
    private TMP_Text _nameText;
    private TMP_Text _dialogText;
    private TMP_Text _promptText;
    private TMP_Text _openShopText;
    private GameObject _shopRow;
    private bool _dialogActive;
    private readonly Queue<string> _dialogQueue = new Queue<string>();

    private Transform _buffaloTransform;
    private Transform _playerTransform;
    private Quaternion _originalRotation = Quaternion.identity;

    public bool IsDialogActive => _dialogActive;

    void Start()
    {
        if (_canvas == null)
            Initialize();
    }

    void Update()
    {
        if (!_dialogActive || _shopRow == null || !_shopRow.activeSelf) return;
        if (Keyboard.current != null && Keyboard.current.tKey.wasPressedThisFrame)
            OpenShop();
    }
    public void Initialize()
    {
        if (_canvas != null)
            return;
        var hudGo = GameObject.Find("HUD_Canvas");
        _canvas = hudGo != null ? hudGo.GetComponent<Canvas>() : Object.FindAnyObjectByType<Canvas>();
        if (_canvas == null)
            return;
        CreatePanel();
    }
    public void Show()
    {
        if (_canvas == null)
            Initialize();
        if (_panel == null)
            return;

        _dialogActive = true;
        _panel.SetActive(true);

        if (_buffaloTransform == null)
        {
            var go = GameObject.Find("BuffaloEntity");
            if (go != null) _buffaloTransform = go.transform;
        }
        if (_playerTransform == null)
        {
            var player = GameObject.FindGameObjectWithTag("Player");
            if (player != null) _playerTransform = player.transform;
        }
        if (_buffaloTransform != null)
            _originalRotation = _buffaloTransform.rotation;
        FacePlayer();
        _nameText.text = Localization.T("Buffalo");
        if (_openShopText != null)
            _openShopText.text = GameInput.IsMobile ? Localization.T("[Mở Cửa Hàng] (Chạm)") : Localization.T("[Mở Cửa Hàng] Nhấn T");
        _dialogQueue.Clear();
        _dialogQueue.Enqueue("Tôi thích lúa mì!");
        Advance();
        if (_shopRow != null)
            _shopRow.SetActive(true);
    }
    public void Hide()
    {
        _dialogActive = false;
        if (_panel != null)
            _panel.SetActive(false);
        if (_buffaloTransform != null)
            _buffaloTransform.rotation = _originalRotation;
    }
    public void Advance()
    {
        if (_dialogQueue.Count == 0)
        {
            Hide();
            return;
        }
        _dialogText.text = Localization.T(_dialogQueue.Dequeue());
        _promptText.text = _dialogQueue.Count > 0
            ? (GameInput.IsMobile ? Localization.T("Chạm để tiếp tục") : Localization.T("Nhấn E để tiếp tục"))
            : (GameInput.IsMobile ? Localization.T("Chạm để đóng") : Localization.T("Nhấn E để đóng"));
    }
    private void OpenShop()
    {
        Hide();
        var shop = Object.FindAnyObjectByType<BuffaloShopManager>();
        if (shop == null)
        {
            var go = new GameObject("BuffaloShopManager");
            shop = go.AddComponent<BuffaloShopManager>();
            shop.Initialize();
        }
        shop.Open();
    }
    private void CreatePanel()
    {
        float sw = Screen.width;
        float sh = Screen.height;

        _panel = new GameObject("BuffaloDialogPanel");
        _panel.transform.SetParent(_canvas.transform, false);

        var rt = _panel.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0f);
        rt.anchorMax = new Vector2(0.5f, 0f);
        rt.pivot = new Vector2(0.5f, 0f);
        rt.anchoredPosition = new Vector2(0f, sh * 0.15f);
        rt.sizeDelta = new Vector2(sw * 0.55f, sh * 0.24f);

        var img = _panel.AddComponent<Image>();
        img.color = ColorPalette.UIBackdrop;

        var btn = _panel.AddComponent<Button>();
        btn.targetGraphic = img;
        btn.onClick.AddListener(Advance);

        float panelW = sw * 0.55f;
        float panelH = sh * 0.24f;

        _nameText = MakeText("BuffaloDialogName", rt, new Vector2(0f, panelH * 0.35f), "Buffalo", 24,
            new Color(0.9f, 0.8f, 0.5f), new Vector2(panelW - 40f, 34f));

        _dialogText = MakeText("BuffaloDialogText", rt, new Vector2(0f, -panelH * 0.05f), "", 20,
            Color.white, new Vector2(panelW - 40f, panelH * 0.55f));

        _promptText = MakeText("BuffaloDialogPrompt", rt, new Vector2(0f, -panelH * 0.38f), "", 16,
            new Color(0.7f, 0.7f, 0.7f), new Vector2(panelW - 40f, 25f));

        _shopRow = MakeShopRow(rt, "BuffaloShopRow", "BuffaloShopBtn", OpenShop);
        _shopRow.SetActive(false);

        _panel.SetActive(false);
    }
    private GameObject MakeShopRow(RectTransform parent, string rowName, string textName,
        UnityEngine.Events.UnityAction onClick)
    {
        var row = new GameObject(rowName);
        row.transform.SetParent(parent, false);
        var rowRt = row.AddComponent<RectTransform>();
        rowRt.anchorMin = new Vector2(0.5f, 0f);
        rowRt.anchorMax = new Vector2(0.5f, 0f);
        rowRt.pivot = new Vector2(0.5f, 1f);
        rowRt.anchoredPosition = new Vector2(0f, -10f);
        rowRt.sizeDelta = new Vector2(260f, 40f);

        var rowImg = row.AddComponent<Image>();
        rowImg.color = new Color(0.37f, 0.51f, 0.68f);
        rowImg.raycastTarget = true;

        var rowBtn = row.AddComponent<Button>();
        rowBtn.targetGraphic = rowImg;
        rowBtn.onClick.AddListener(onClick);

        _openShopText = MakeText(textName, rowRt, new Vector2(0f, 0f), Localization.T("[Mở Cửa Hàng]"), 18,
            Color.white, new Vector2(236f, 36f));

        return row;
    }
    private TMP_Text MakeText(string name, RectTransform parent, Vector2 position, string text,
        int fontSize, Color color, Vector2 size)
        => CountryLife.Helpers.UIHelper.MakeText(name, parent, position, text, fontSize, color, size, true, true, TextAlignmentOptions.Left, false);
    private void FacePlayer()
    {
        if (_buffaloTransform == null || _playerTransform == null)
            return;
        Vector3 to = _buffaloTransform.position - _playerTransform.position;
        to.y = 0f;
        if (to.sqrMagnitude > 0.001f)
            _buffaloTransform.rotation = Quaternion.LookRotation(to.normalized);
    }
}

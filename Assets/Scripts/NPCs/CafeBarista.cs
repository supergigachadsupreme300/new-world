using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class CafeBarista : MonoSingleton<CafeBarista>
{
private Transform _myTransform;
    private Transform _playerTransform;
    private Quaternion _originalRotation = Quaternion.identity;

    private Canvas _canvas;
    private GameObject _panel;
    private TMP_Text _nameText;
    private TMP_Text _dialogText;
    private TMP_Text _promptText;
    private bool _dialogActive;
    private readonly Queue<string> _dialogQueue = new Queue<string>();

    private bool _shopOpenedAfterDialog;

    public bool IsDialogActive => _dialogActive;

    void Start()
    {
        var playerGo = GameObject.Find("Player");
        if (playerGo != null) _playerTransform = playerGo.transform;

        if (_myTransform != null)
            _originalRotation = _myTransform.rotation;
    }
    public void Interact()
    {
        if (gameObject == null || !gameObject.activeInHierarchy)
            return;
        InitializeDialog();
        if (_panel == null)
            return;

        FacePlayer();
        _shopOpenedAfterDialog = false;
        _dialogActive = true;
        _panel.SetActive(true);
        _nameText.text = Localization.T("Nhân Viên Quán Cà Phê");
        _dialogQueue.Clear();
        _dialogQueue.Enqueue("Cà phê đen nóng hổi đây, ai cần tỉnh táo cày ruộng không?");
        _dialogQueue.Enqueue("Uống xong tỉnh cả người, nhưng mà say cà phê thì hồi sức hơi chậm đấy.");
        _dialogQueue.Enqueue("Cứ thử một ly xem sao!");
        Advance();
    }
    public void Advance()
    {
        if (_panel == null)
            return;
        if (_dialogQueue.Count == 0)
        {
            Hide();
            OpenShop();
            return;
        }
        _dialogText.text = Localization.T(_dialogQueue.Dequeue());
        _promptText.text = _dialogQueue.Count > 0
            ? (GameInput.IsMobile ? Localization.T("Chạm để tiếp tục") : Localization.T("Nhấn E để tiếp tục"))
            : (GameInput.IsMobile ? Localization.T("Chạm để xem thực đơn") : Localization.T("Nhấn E để xem thực đơn"));
    }
    private void OpenShop()
    {
        if (_shopOpenedAfterDialog)
            return;
        _shopOpenedAfterDialog = true;
        var shop = Object.FindAnyObjectByType<VendorShopManager>();
        if (shop == null)
        {
            var go = new GameObject("VendorShopManager");
            shop = go.AddComponent<VendorShopManager>();
            shop.Initialize();
        }
        shop.OpenCafe();
    }
    public void Hide()
    {
        _dialogActive = false;
        if (_panel != null)
            _panel.SetActive(false);
        if (_myTransform != null)
            _myTransform.rotation = _originalRotation;
    }
    private void FacePlayer()
    {
        if (_myTransform == null || _playerTransform == null)
            return;
        Vector3 to = _myTransform.position - _playerTransform.position;
        to.y = 0f;
        if (to.sqrMagnitude > 0.001f)
            _myTransform.rotation = Quaternion.LookRotation(to.normalized);
    }
    private void InitializeDialog()
    {
        if (_canvas != null)
            return;
        var hudGo = GameObject.Find("HUD_Canvas");
        _canvas = hudGo != null ? hudGo.GetComponent<Canvas>() : Object.FindAnyObjectByType<Canvas>();
        if (_canvas == null)
            return;
        CreatePanel();
    }
    private void CreatePanel()
    {
        float sw = Screen.width;
        float sh = Screen.height;

        _panel = new GameObject("CafeDialogPanel");
        _panel.transform.SetParent(_canvas.transform, false);

        var rt = _panel.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0f);
        rt.anchorMax = new Vector2(0.5f, 0f);
        rt.pivot = new Vector2(0.5f, 0f);
        rt.anchoredPosition = new Vector2(0f, sh * 0.15f);
        rt.sizeDelta = new Vector2(sw * 0.6f, sh * 0.2f);

        var img = _panel.AddComponent<Image>();
        img.color = ColorPalette.UIBackdrop;

        var btn = _panel.AddComponent<Button>();
        btn.targetGraphic = img;
        btn.onClick.AddListener(Advance);

        float panelW = sw * 0.6f;
        float panelH = sh * 0.2f;

        _nameText = MakeText("CafeDialogName", rt, new Vector2(0f, panelH * 0.36f),
            Localization.T("Nhân Viên Quán Cà Phê"), 24, new Color(1f, 0.75f, 0.4f),
            new Vector2(panelW - 40f, 34f));

        _dialogText = MakeText("CafeDialogText", rt, new Vector2(0f, -panelH * 0.02f),
            "", 20, Color.white, new Vector2(panelW - 40f, panelH * 0.55f));

        _promptText = MakeText("CafeDialogPrompt", rt, new Vector2(0f, -panelH * 0.36f),
            "", 16, new Color(0.7f, 0.7f, 0.7f), new Vector2(panelW - 40f, 25f));

        _panel.SetActive(false);
    }
    private TMP_Text MakeText(string name, RectTransform parent, Vector2 position, string text,
        int fontSize, Color color, Vector2 size)
        => CountryLife.Helpers.UIHelper.MakeText(name, parent, position, text, fontSize, color, size, true, true, TextAlignmentOptions.Left, false);
}

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ChefNPC : MonoBehaviour
{
    public static ChefNPC Instance { get; private set; }

    private Transform _myTransform;
    private Transform _playerTransform;
    private Transform _stirArm;

    private Canvas _canvas;
    private GameObject _panel;
    private TMP_Text _nameText;
    private TMP_Text _dialogText;
    private TMP_Text _promptText;
    private bool _dialogActive;
    private readonly Queue<string> _dialogQueue = new Queue<string>();

    private bool _shopOpenedAfterDialog;

    public bool IsDialogActive => _dialogActive;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        _myTransform = transform;
    }

    void Start()
    {
        var playerGo = GameObject.Find("Player");
        if (playerGo != null) _playerTransform = playerGo.transform;

        if (_myTransform != null)
        {
            var arm = _myTransform.Find("ArmR");
            if (arm != null)
                _stirArm = arm;
        }
    }

    void Update()
    {
        if (GameManager.Instance == null || GameManager.Instance.GamePaused)
            return;
        if (CutsceneManager.Instance != null && CutsceneManager.Instance.IsActive)
            return;
        if (_dialogActive)
            return;

        Stir();
    }

    private void Stir()
    {
        if (_stirArm == null)
            return;
        float angle = Mathf.Sin(Time.time * 2.4f) * 30f;
        _stirArm.localRotation = Quaternion.Euler(0f, angle, 0f);
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
        _nameText.text = Localization.T("Đầu Bếp");
        _dialogQueue.Clear();
        _dialogQueue.Enqueue("Món cơm nóng hổi đây, ai vào ăn nhanh nào!");
        _dialogQueue.Enqueue("Cơm Gà là đặc sản của quán ta đấy.");
        _dialogQueue.Enqueue("Ăn no xong là cày ruộng khỏe lại ngay!");
        _dialogQueue.Enqueue("Cứ xem thực đơn đi, chọn món nào cũng ngon.");
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
        shop.OpenRestaurant();
    }

    public void Hide()
    {
        _dialogActive = false;
        if (_panel != null)
            _panel.SetActive(false);
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

        _panel = new GameObject("ChefDialogPanel");
        _panel.transform.SetParent(_canvas.transform, false);

        var rt = _panel.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0f);
        rt.anchorMax = new Vector2(0.5f, 0f);
        rt.pivot = new Vector2(0.5f, 0f);
        rt.anchoredPosition = new Vector2(0f, sh * 0.15f);
        rt.sizeDelta = new Vector2(sw * 0.6f, sh * 0.2f);

        var img = _panel.AddComponent<Image>();
        img.color = new Color(0f, 0f, 0f, 0.8f);

        var btn = _panel.AddComponent<Button>();
        btn.targetGraphic = img;
        btn.onClick.AddListener(Advance);

        float panelW = sw * 0.6f;
        float panelH = sh * 0.2f;

        _nameText = MakeText("ChefDialogName", rt, new Vector2(0f, panelH * 0.36f),
            Localization.T("Đầu Bếp"), 24, new Color(1f, 0.75f, 0.4f),
            new Vector2(panelW - 40f, 34f));

        _dialogText = MakeText("ChefDialogText", rt, new Vector2(0f, -panelH * 0.02f),
            "", 20, Color.white, new Vector2(panelW - 40f, panelH * 0.55f));

        _promptText = MakeText("ChefDialogPrompt", rt, new Vector2(0f, -panelH * 0.36f),
            "", 16, new Color(0.7f, 0.7f, 0.7f), new Vector2(panelW - 40f, 25f));

        _panel.SetActive(false);
    }

    private TMP_Text MakeText(string name, RectTransform parent, Vector2 position, string text,
        int fontSize, Color color, Vector2 size)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);

        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = position;
        rt.sizeDelta = size;

        var tmp = go.AddComponent<TextMeshProUGUI>();
        if (GameManager.Instance?.UIManager?.defaultTmpFont != null)
            tmp.font = GameManager.Instance.UIManager.defaultTmpFont;
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.color = color;
        tmp.alignment = TextAlignmentOptions.Left;
        tmp.textWrappingMode = TextWrappingModes.Normal;
        tmp.overflowMode = TextOverflowModes.Ellipsis;
        tmp.raycastTarget = false;

        return tmp;
    }
}

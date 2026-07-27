using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
using TMPro;

public class UIManager : MonoBehaviour
{
    private Canvas _canvas;
    private Sprite _menuBgSprite;
    private Sprite _hudBgSprite;
    private GameObject _mainMenuPanel;
    private GameObject _pauseMenuPanel;
    private GameObject _recordPanel;
    private GameObject _questPanel;
    private GameObject _tutorialPanel;
    private int _tutorialSpreadIndex;
    private TMP_Text _tutorialLeftText;
    private TMP_Text _tutorialRightText;
    private Image _tutorialBookImage;
    private GameObject _tutorialLeftArrow;
    private GameObject _tutorialRightArrow;
    private Sprite _bookSprite;
    private Sprite _insidePageSprite;
    private Sprite _rightArrowSprite;
    private Sprite _leftArrowSprite;
    private Sprite _redXSprite;
    private readonly string[] _tutorialPages = {
        "Chào mừng đến với Country Life!\n\nSau khi tốt nghiệp ngành CNTT, thị trường việc làm đã quá khó khăn. Không có việc làm, bạn quay về nông thôn của ông nội đã khuất.\n\nTại đây, bạn phải xây dựng nông trại, bảo vệ làng, và tìm kiếm hạnh phúc cho mình. Biết đâu, cô gái hàng xóm sẽ là định mệnh của bạn...",
        "DI CHUYỂN\n\nWASD \u2014 Di chuyển\nSpace \u2014 Nhảy\nShift \u2014 Chạy nhanh\nChuột \u2014 Nhìn xung quanh",
        "HÀNH ĐỘNG\n\nChuột trái \u2014 Sử dụng công cụ\nE \u2014 Tương tác / Mở cửa\nQ \u2014 Bỏ vật phẩm",
        "XÂY DỰNG\n\nGiữ Búa + F \u2014 Mở menu xây dựng\nB / N \u2014 Đổi loại công trình\nChuột trái \u2014 Đặt công trình\nF \u2014 Hủy",
        "NÔNG NGHIỆP & CHIẾN ĐẤU\n\nCuốc \u2014 Làm đất để trồng cây\nLưỡi liềm \u2014 Thu hoạch\nRìu / Cuốc chim \u2014 Thu thập nguyên liệu\nKìm \u2014 Chiến đấu với kẻ thù",
        "MẸO\n\nThu hoạch lúa để kiếm tiền\nXây dựng tường và tháp canh để bảo vệ\nHoàn thành nhiệm vụ để nhận thưởng\nNgủ trên giường để lưu game"
    };
    private GameObject _endPanel;

    private TMP_Text _timeText;
    private TMP_Text _hpText;
    private TMP_Text _staminaText;
    private TMP_Text _moneyText;
    private TMP_Text _questText;
    private TMP_Text _questLinesText;
    private const int InventorySlotCount = 10;
    private readonly GameObject[] _inventorySlots = new GameObject[InventorySlotCount];
    private readonly TMP_Text[] _inventorySlotTexts = new TMP_Text[InventorySlotCount];
    private readonly Image[] _inventorySlotImages = new Image[InventorySlotCount];
    private RectTransform _statsBg;
    private TMP_Text _messageText;
    private TMP_Text _mobSpawnerText;
    private TMP_Text _crosshairText;
    private TMP_Text _infoText;

    public TMP_FontAsset defaultTmpFont;

    private int _lastScreenWidth;
    private int _lastScreenHeight;

    void Start()
    {
        _lastScreenWidth = Screen.width;
        _lastScreenHeight = Screen.height;
        InitializeUI();
    }

    void Update()
    {
        if (Screen.width != _lastScreenWidth || Screen.height != _lastScreenHeight)
        {
            _lastScreenWidth = Screen.width;
            _lastScreenHeight = Screen.height;
            ResizeInventory();
        }
    }

    private void ResizeInventory()
    {
        float sw = Screen.width;
        float slotW = sw * 0.06f;
        float slotH = Screen.height * 0.04f;
        float totalW = InventorySlotCount * slotW + (InventorySlotCount - 1) * 4f;
        float startX = -totalW * 0.5f;
        float y = Screen.height * 0.03f;

        for (int i = 0; i < InventorySlotCount; i++)
        {
            if (_inventorySlots[i] == null) continue;
            var rect = _inventorySlots[i].GetComponent<RectTransform>();
            if (rect != null)
                rect.anchoredPosition = new Vector2(startX + i * (slotW + 4f), y);
            if (_inventorySlotTexts[i] != null)
                _inventorySlotTexts[i].fontSize = Mathf.Clamp(sw / 120f, 8f, 16f);
        }
    }

    public void InitializeUI()
    {
        EnsureEventSystem();
        _canvas = Object.FindAnyObjectByType<Canvas>();
        if (_canvas == null || _canvas.gameObject.name != "HUD_Canvas")
            _canvas = CreateCanvas();

        if (defaultTmpFont == null)
            defaultTmpFont = Resources.Load<TMP_FontAsset>("VietPixel");

        // Calculate responsive sizes based on screen dimensions
        float screenHeight = Screen.height;
        float screenWidth = Screen.width;
        float hudWidthPercent = screenWidth * 0.3f; // 30% of screen width for HUD
        float fontSize = Mathf.Max(14f, screenHeight / 36f); // Font scales with height
        float largefontSize = fontSize * 1.4f;
        float padding = screenHeight * 0.02f; // 2% of height for padding
        float buttonHeight = screenHeight * 0.08f; // Buttons are 8% of height
        float lineHeight = screenHeight * 0.05f; // Line spacing
        float panelWidth = Mathf.Min(screenWidth * 0.4f, 560f);
        float panelHeight = Mathf.Min(screenHeight * 0.8f, 520f);
        // Stats background panel (behind Time, HP, Stamina, Money, Quest)
        _statsBg = CreateHudBackground("StatsBg",
            new Vector2(0f, 0f),
            new Vector2(400f, 250f),
            new Vector2(0f, 1f));

        _timeText = EnsureText(
            "TimeText",
            new Vector2(40f, -20f),
            "Ngày 1 - 08.00",
            20,
            null,
            TextAlignmentOptions.Left,
            true,
            new Vector2(420f, 30f),
            new Vector2(0f, 1f),
            new Vector2(0f, 1f),
            new Vector2(0f, 1f)
        );

        _hpText = EnsureText(
            "HPText",
            new Vector2(40f, -60f),
            "HP: 100/100",
            20,
            null,
            TextAlignmentOptions.Left,
            true,
            new Vector2(420f, 30f),
            new Vector2(0f, 1f),
            new Vector2(0f, 1f),
            new Vector2(0f, 1f)
        );

        _staminaText = EnsureText(
            "StaminaText",
            new Vector2(40f, -100f),
            "Sức Mạnh: 100/100",
            20,
            null,
            TextAlignmentOptions.Left,
            true,
            new Vector2(420f, 30f),
            new Vector2(0f, 1f),
            new Vector2(0f, 1f),
            new Vector2(0f, 1f)
        );

        _moneyText = EnsureText(
            "MoneyText",
            new Vector2(40f, -140f),
            "Tiền: 0",
            20,
            null,
            TextAlignmentOptions.Left,
            true,
            new Vector2(420f, 30f),
            new Vector2(0f, 1f),
            new Vector2(0f, 1f),
            new Vector2(0f, 1f)
        );

        _questText = EnsureText(
            "QuestText",
            new Vector2(40f, -180f),
            "Nhiệm Vụ: Sẵn sàng",
            18,
            null,
            TextAlignmentOptions.Left,
            true,
            new Vector2(420f, 40f),
            new Vector2(0f, 1f),
            new Vector2(0f, 1f),
            new Vector2(0f, 1f)
        );

        // Inventory: 10 individual slots at bottom center
        float slotW = screenWidth * 0.06f;
        float slotH = screenHeight * 0.04f;
        float totalW = InventorySlotCount * slotW + (InventorySlotCount - 1) * 4f;
        float startX = -totalW * 0.5f;

        for (int i = 0; i < InventorySlotCount; i++)
        {
            float x = startX + i * (slotW + 4f);
            string slotName = "InvSlot_" + i;

            var slotGo = new GameObject(slotName);
            slotGo.transform.SetParent(_canvas.transform, false);
            var slotRect = slotGo.AddComponent<RectTransform>();
            slotRect.anchorMin = new Vector2(0.5f, 0f);
            slotRect.anchorMax = new Vector2(0.5f, 0f);
            slotRect.pivot = new Vector2(0.5f, 0f);
            slotRect.anchoredPosition = new Vector2(x, padding * 3f);
            slotRect.sizeDelta = new Vector2(slotW, slotH);

            var slotImg = slotGo.AddComponent<Image>();
            if (_hudBgSprite == null)
            {
                var tex = Resources.Load<Texture2D>("menu");
                if (tex != null)
                    _hudBgSprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
            }
            if (_hudBgSprite != null)
            {
                slotImg.sprite = _hudBgSprite;
                slotImg.type = Image.Type.Simple;
                slotImg.preserveAspect = false;
                slotImg.color = new Color(0.3f, 0.3f, 0.35f, 0.85f);
            }
            else
            {
                slotImg.color = new Color(0.18f, 0.18f, 0.25f, 0.85f);
            }

            var textGo = new GameObject("Text");
            textGo.transform.SetParent(slotGo.transform, false);
            var text = textGo.AddComponent<TextMeshProUGUI>();
            if (defaultTmpFont != null)
                text.font = defaultTmpFont;
            text.text = $"{i + 1}:";
            text.fontSize = Mathf.Clamp(screenWidth / 120f, 8f, 16f);
            text.color = Color.white;
            text.alignment = TextAlignmentOptions.Center;

            var textRect = textGo.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            _inventorySlots[i] = slotGo;
            _inventorySlotTexts[i] = text;
            _inventorySlotImages[i] = slotImg;
        }

        // Message text: center of screen
        _messageText = EnsureText(
            "MessageText",
            new Vector2(0f, screenHeight * 0.15f),
            "",
            (int)largefontSize,
            null,
            TextAlignmentOptions.Center,
            true,
            new Vector2(screenWidth * 0.6f, lineHeight * 1.5f),
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f)
        );

        _mobSpawnerText = EnsureText(
            "MobSpawnerText",
            new Vector2(0f, -padding - buttonHeight),
            "",
            (int)fontSize,
            null,
            TextAlignmentOptions.Center,
            true,
            new Vector2(screenWidth * 0.3f, lineHeight),
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f)
        );

        var crosshairSize = (int)(fontSize * 2f);
        _crosshairText = EnsureText(
            "CrosshairText",
            Vector2.zero,
            "+",
            crosshairSize,
            null,
            TextAlignmentOptions.Center,
            false,
            new Vector2(lineHeight * 2f, lineHeight * 2f),
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f)
        );
        _crosshairText.color = Color.white;
        var crossMat = new Material(_crosshairText.fontSharedMaterial);
        crossMat.EnableKeyword("OUTLINE_ON");
        crossMat.SetFloat("_OutlineWidth", 0.3f);
        crossMat.SetColor("_OutlineColor", Color.black);
        _crosshairText.fontSharedMaterial = crossMat;
        _crosshairText.gameObject.SetActive(true);

        _infoText = EnsureText(
            "InfoText",
            new Vector2(0f, -(lineHeight * 1.5f)),
            "",
            (int)fontSize,
            null,
            TextAlignmentOptions.Center,
            true,
            new Vector2(screenWidth * 0.6f, lineHeight * 1.5f),
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f)
        );
        _infoText.gameObject.SetActive(false);

        // Panels - responsive sizes
        _pauseMenuPanel = CreateMenuPanel("PauseMenu", Vector2.zero, new Vector2(panelWidth, panelHeight));
        CreateButton("ContinueButton", _pauseMenuPanel.transform, "Tiếp Tục", new Vector2(0f, buttonHeight * 2.25f), () => GameManager.Instance?.TogglePause(false));
        CreateButton("SaveButton", _pauseMenuPanel.transform, "Lưu Game", new Vector2(0f, buttonHeight * 1.35f), () => SaveManager.Instance?.SaveGame());
        CreateButton("StatsButton", _pauseMenuPanel.transform, "Thống Kê", new Vector2(0f, buttonHeight * 0.45f), () => ShowRecordPanel(true));
        CreateButton("QuestsButton", _pauseMenuPanel.transform, "Nhiệm Vụ", new Vector2(0f, -buttonHeight * 0.45f), () => ShowQuestPanel(true));
        CreateButton("TutorialButton", _pauseMenuPanel.transform, "Hướng Dẫn", new Vector2(0f, -buttonHeight * 1.35f), () => ShowTutorial(true));
        CreateButton("ExitButton", _pauseMenuPanel.transform, "Thoát", new Vector2(0f, -buttonHeight * 2.25f), () => Application.Quit());
        _pauseMenuPanel.SetActive(false);

        _recordPanel = CreateMenuPanel("RecordPanel", Vector2.zero, new Vector2(panelWidth, panelHeight));
        EnsureText("RecordTitle", new Vector2(0f, panelHeight * 0.35f), "THỐNG KÊ", (int)largefontSize, _recordPanel.transform, TextAlignmentOptions.Center, true, new Vector2(panelWidth - padding * 4, lineHeight));
        EnsureText("RecordLines", new Vector2(0f, panelHeight * 0.1f), "Lúa đã thu hoạch: 0\nKẻ thù đã diệt: 0\nTiền đã kiếm: 0\nTiền bị cướp: 0", (int)fontSize, _recordPanel.transform, TextAlignmentOptions.Left, true, new Vector2(panelWidth - padding * 4, panelHeight * 0.4f));
        CreateButton("RecordBackButton", _recordPanel.transform, "Quay Lại", new Vector2(0f, -panelHeight * 0.35f), () => ShowRecordPanel(false));
        _recordPanel.SetActive(false);

        _questPanel = CreateMenuPanel("QuestPanel", Vector2.zero, new Vector2(panelWidth, panelHeight));
        EnsureText("QuestTitle", new Vector2(0f, panelHeight * 0.35f), "NHIỆM VỤ", (int)largefontSize, _questPanel.transform, TextAlignmentOptions.Center, true, new Vector2(panelWidth - padding * 4, lineHeight));
        _questLinesText = EnsureText("QuestLines", new Vector2(0f, panelHeight * 0.1f), "1. Thu hoạch lúa 0/100\n2. Diệt quái 0/30\n3. Kiếm tiền 0/100000", (int)fontSize, _questPanel.transform, TextAlignmentOptions.Left, true, new Vector2(panelWidth - padding * 4, panelHeight * 0.3f));
        CreateButton("QuestCloseButton", _questPanel.transform, "Đóng", new Vector2(0f, -panelHeight * 0.35f), () => ShowQuestPanel(false));
        _questPanel.SetActive(false);

        _tutorialPanel = new GameObject("TutorialPanel");
        _tutorialPanel.transform.SetParent(_canvas.transform, false);
        var tutRect = _tutorialPanel.AddComponent<RectTransform>();
        tutRect.anchorMin = Vector2.zero;
        tutRect.anchorMax = Vector2.one;
        tutRect.offsetMin = Vector2.zero;
        tutRect.offsetMax = Vector2.zero;
        var tutOverlay = _tutorialPanel.AddComponent<Image>();
        tutOverlay.color = new Color(0f, 0f, 0f, 0.6f);

        if (_bookSprite == null)
        {
            var tex = Resources.Load<Texture2D>("book");
            if (tex != null) _bookSprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
        }
        if (_insidePageSprite == null)
        {
            var tex = Resources.Load<Texture2D>("insidepage");
            if (tex != null) _insidePageSprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
        }
        if (_rightArrowSprite == null)
        {
            var tex = Resources.Load<Texture2D>("rightarrow");
            if (tex != null) _rightArrowSprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
        }
        if (_leftArrowSprite == null)
        {
            var tex = Resources.Load<Texture2D>("leftarrow");
            if (tex != null) _leftArrowSprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
        }
        if (_redXSprite == null)
        {
            var tex = Resources.Load<Texture2D>("redx");
            if (tex != null) _redXSprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
        }

        float bookH = Screen.height * 0.65f;

        var bookObj = new GameObject("TutorialBook");
        bookObj.transform.SetParent(_tutorialPanel.transform, false);
        var bookRt = bookObj.AddComponent<RectTransform>();
        bookRt.anchorMin = new Vector2(0.5f, 0.5f);
        bookRt.anchorMax = new Vector2(0.5f, 0.5f);
        bookRt.pivot = new Vector2(0.5f, 0.5f);
        bookRt.anchoredPosition = new Vector2(0f, bookH * 0.05f);
        bookRt.sizeDelta = new Vector2(bookH * 1.3f, bookH * 1.5f);
        _tutorialBookImage = bookObj.AddComponent<Image>();
        _tutorialBookImage.sprite = _bookSprite;
        _tutorialBookImage.type = Image.Type.Simple;
        _tutorialBookImage.preserveAspect = true;

        float redXSize = Screen.height * 0.05f;
        var redXObj = new GameObject("TutorialRedX");
        redXObj.transform.SetParent(bookObj.transform, false);
        var redXRt = redXObj.AddComponent<RectTransform>();
        redXRt.anchorMin = new Vector2(1f, 1f);
        redXRt.anchorMax = new Vector2(1f, 1f);
        redXRt.pivot = new Vector2(1f, 1f);
        redXRt.anchoredPosition = new Vector2(10f, 10f);
        redXRt.sizeDelta = new Vector2(redXSize, redXSize);
        var redXImg = redXObj.AddComponent<Image>();
        redXImg.sprite = _redXSprite;
        redXImg.type = Image.Type.Simple;
        redXImg.preserveAspect = true;
        var redXBtn = redXObj.AddComponent<Button>();
        redXBtn.targetGraphic = redXImg;
        redXBtn.onClick.AddListener(TutorialClose);

        float arrowH = Screen.height * 0.12f;
        float arrowY = -bookH * 0.25f;

        var rightArrowObj = new GameObject("TutorialRightArrow");
        rightArrowObj.transform.SetParent(_tutorialPanel.transform, false);
        var rightArrowRt = rightArrowObj.AddComponent<RectTransform>();
        rightArrowRt.anchorMin = new Vector2(0.5f, 0.5f);
        rightArrowRt.anchorMax = new Vector2(0.5f, 0.5f);
        rightArrowRt.pivot = new Vector2(0.5f, 0.5f);
        rightArrowRt.anchoredPosition = new Vector2(arrowH * 0.8f, arrowY);
        rightArrowRt.sizeDelta = new Vector2(arrowH, arrowH);
        var rightArrowImg = rightArrowObj.AddComponent<Image>();
        rightArrowImg.sprite = _rightArrowSprite;
        rightArrowImg.type = Image.Type.Simple;
        rightArrowImg.preserveAspect = true;
        var rightArrowBtn = rightArrowObj.AddComponent<Button>();
        rightArrowBtn.targetGraphic = rightArrowImg;
        rightArrowBtn.onClick.AddListener(TutorialNextPage);
        _tutorialRightArrow = rightArrowObj;

        var leftArrowObj = new GameObject("TutorialLeftArrow");
        leftArrowObj.transform.SetParent(_tutorialPanel.transform, false);
        var leftArrowRt = leftArrowObj.AddComponent<RectTransform>();
        leftArrowRt.anchorMin = new Vector2(0.5f, 0.5f);
        leftArrowRt.anchorMax = new Vector2(0.5f, 0.5f);
        leftArrowRt.pivot = new Vector2(0.5f, 0.5f);
        leftArrowRt.anchoredPosition = new Vector2(-arrowH * 0.8f, arrowY);
        leftArrowRt.sizeDelta = new Vector2(arrowH, arrowH);
        var leftArrowImg = leftArrowObj.AddComponent<Image>();
        leftArrowImg.sprite = _leftArrowSprite;
        leftArrowImg.type = Image.Type.Simple;
        leftArrowImg.preserveAspect = true;
        var leftArrowBtn = leftArrowObj.AddComponent<Button>();
        leftArrowBtn.targetGraphic = leftArrowImg;
        leftArrowBtn.onClick.AddListener(TutorialPrevPage);
        _tutorialLeftArrow = leftArrowObj;
        _tutorialLeftArrow.SetActive(false);

        float textW = bookH * 0.4f;
        float textH = bookH * 1.1f;
        float textY = bookH * 0.2f;
        float textSpacing = bookH * 0.27f;

        var leftTextObj = new GameObject("TutorialLeftText");
        leftTextObj.transform.SetParent(_tutorialPanel.transform, false);
        var leftTextRt = leftTextObj.AddComponent<RectTransform>();
        leftTextRt.anchorMin = new Vector2(0.5f, 0.5f);
        leftTextRt.anchorMax = new Vector2(0.5f, 0.5f);
        leftTextRt.pivot = new Vector2(0.5f, 0.5f);
        leftTextRt.anchoredPosition = new Vector2(-textSpacing * 0.8f, textY);
        leftTextRt.sizeDelta = new Vector2(textW, textH);
        _tutorialLeftText = leftTextObj.AddComponent<TextMeshProUGUI>();
        if (defaultTmpFont != null) _tutorialLeftText.font = defaultTmpFont;
        _tutorialLeftText.raycastTarget = false;
        _tutorialLeftText.fontSize = Mathf.Max(14, (int)(Screen.height * 0.018f));
        _tutorialLeftText.color = Color.black;
        _tutorialLeftText.alignment = TextAlignmentOptions.Center;
        _tutorialLeftText.text = "";
        leftTextObj.SetActive(false);

        var rightTextObj = new GameObject("TutorialRightText");
        rightTextObj.transform.SetParent(_tutorialPanel.transform, false);
        var rightTextRt = rightTextObj.AddComponent<RectTransform>();
        rightTextRt.anchorMin = new Vector2(0.5f, 0.5f);
        rightTextRt.anchorMax = new Vector2(0.5f, 0.5f);
        rightTextRt.pivot = new Vector2(0.5f, 0.5f);
        rightTextRt.anchoredPosition = new Vector2(textSpacing, textY);
        rightTextRt.sizeDelta = new Vector2(textW, textH);
        _tutorialRightText = rightTextObj.AddComponent<TextMeshProUGUI>();
        if (defaultTmpFont != null) _tutorialRightText.font = defaultTmpFont;
        _tutorialRightText.raycastTarget = false;
        _tutorialRightText.fontSize = Mathf.Max(14, (int)(Screen.height * 0.018f));
        _tutorialRightText.color = Color.black;
        _tutorialRightText.alignment = TextAlignmentOptions.Center;
        _tutorialRightText.text = "";
        rightTextObj.SetActive(false);

        _tutorialPanel.SetActive(false);

        _mainMenuPanel = CreateMenuPanel("MainMenuPanel", Vector2.zero, new Vector2(panelWidth, panelHeight));
        // Anchor menu to far left side, stretched vertically
        var menuRect = _mainMenuPanel.GetComponent<RectTransform>();
        if (menuRect != null)
        {
            menuRect.anchorMin = new Vector2(0f, 0f);
            menuRect.anchorMax = new Vector2(0f, 1f);
            menuRect.pivot = new Vector2(0f, 0.5f);
            menuRect.anchoredPosition = new Vector2(0f, 0f);
            menuRect.sizeDelta = new Vector2(panelWidth, 0f);
        }
        EnsureText("TitleText", new Vector2(0f, panelHeight * 0.3f), "XÂY DỰNG NÔNG TRẠI", (int)(largefontSize * 1.1f), _mainMenuPanel.transform, TextAlignmentOptions.Center, true, new Vector2(panelWidth - padding * 4, lineHeight * 1.5f));
        CreateButton("NewGameButton", _mainMenuPanel.transform, "Trò Mới", new Vector2(0f, buttonHeight * 1.2f), () => MainMenuController.Instance?.OnNewGameClicked());
        CreateButton("LoadGameButton", _mainMenuPanel.transform, "Tiếp Tục (Tải)", new Vector2(0f, buttonHeight * 0.4f), () => MainMenuController.Instance?.OnLoadGameClicked());
        CreateButton("WatchIntroButton", _mainMenuPanel.transform, "Xem Giới Thiệu", new Vector2(0f, -buttonHeight * 0.4f), () => MainMenuController.Instance?.OnWatchIntroClicked());
        CreateButton("SkipIntroButton", _mainMenuPanel.transform, "Bỏ Qua Giới Thiệu", new Vector2(0f, -buttonHeight * 1.2f), () => MainMenuController.Instance?.OnSkipIntroClicked());
        CreateButton("QuitButton", _mainMenuPanel.transform, "Thoát", new Vector2(0f, -buttonHeight * 2.0f), () => MainMenuController.Instance?.OnQuitClicked());
        _mainMenuPanel.SetActive(false);

        ShowAllGameUI(true);

        // Re-apply main menu visibility in case GameManager.Start() ran before slots were created
        if (GameManager.Instance != null && !GameManager.Instance.InGame)
            ShowMainMenuOnly(true);
    }

    private Canvas CreateCanvas()
    {
        var canvasObject = new GameObject("HUD_Canvas");
        canvasObject.layer = LayerMask.NameToLayer("UI");
        var canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 1000;
        canvasObject.AddComponent<CanvasScaler>();
        canvasObject.AddComponent<GraphicRaycaster>();
        canvasObject.SetActive(true);
        return canvas;
    }

    private void EnsureEventSystem()
    {
        var eventSystem = Object.FindAnyObjectByType<EventSystem>();
        if (eventSystem == null)
        {
            eventSystem = new GameObject("EventSystem").AddComponent<EventSystem>();
        }

        var standaloneModule = eventSystem.GetComponent<StandaloneInputModule>();
        if (standaloneModule != null)
        {
            DestroyImmediate(standaloneModule);
        }

        if (eventSystem.GetComponent<InputSystemUIInputModule>() == null)
        {
            eventSystem.gameObject.AddComponent<InputSystemUIInputModule>();
        }
    }

    private TMP_Text EnsureText(
        string name,
        Vector2 position,
        string text,
        int fontSize = 20,
        Transform parent = null,
        TextAlignmentOptions alignment = TextAlignmentOptions.Center,
        bool enableWrapping = true,
        Vector2? size = null,
        Vector2? anchorMin = null,
        Vector2? anchorMax = null,
        Vector2? pivot = null)
    {
        var existing = GameObject.Find(name);
        if (existing != null)
        {
            var existingText = existing.GetComponent<TMP_Text>();
            if (existingText != null)
                return existingText;
        }

        var go = new GameObject(name);
        go.transform.SetParent(parent != null ? parent : _canvas.transform, false);

        var rect = go.AddComponent<RectTransform>();

        if (anchorMin.HasValue && anchorMax.HasValue)
        {
            rect.anchorMin = anchorMin.Value;
            rect.anchorMax = anchorMax.Value;
        }
        else
        {
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
        }

        rect.pivot = pivot ?? new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size ?? new Vector2(540f, 60f);

        var textComponent = go.AddComponent<TextMeshProUGUI>();
        if (defaultTmpFont != null)
            textComponent.font = defaultTmpFont;
        textComponent.text = text;
        textComponent.fontSize = fontSize;
        textComponent.color = Color.white;
        textComponent.alignment = alignment;
        textComponent.textWrappingMode = enableWrapping ? TextWrappingModes.Normal : TextWrappingModes.NoWrap;
        textComponent.overflowMode = enableWrapping ? TextOverflowModes.Truncate : TextOverflowModes.Overflow;

        return textComponent;
    }

    private RectTransform CreateHudBackground(string name, Vector2 position, Vector2 size, Vector2 anchor)
    {
        var existing = GameObject.Find(name);
        if (existing != null)
        {
            var existingRect = existing.GetComponent<RectTransform>();
            if (existingRect != null) return existingRect;
        }

        var go = new GameObject(name);
        go.transform.SetParent(_canvas.transform, false);
        go.transform.SetAsFirstSibling();
        var rect = go.AddComponent<RectTransform>();
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = anchor;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        var img = go.AddComponent<Image>();
        if (_hudBgSprite == null)
        {
            var tex = Resources.Load<Texture2D>("menu");
            if (tex != null)
                _hudBgSprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
        }
        if (_hudBgSprite != null)
        {
            img.sprite = _hudBgSprite;
            img.type = Image.Type.Simple;
            img.preserveAspect = false;
            img.color = new Color(1f, 1f, 1f, 0.5f);
        }
        else
        {
            img.color = new Color(0.35f, 0.2f, 0.08f, 0.8f);
        }
        return rect;
    }

    private GameObject CreateMenuPanel(string name, Vector2 position, Vector2 size)
    {
        var panelObject = GameObject.Find(name);
        if (panelObject != null)
            return panelObject;

        panelObject = new GameObject(name);
        panelObject.transform.SetParent(_canvas.transform, false);

        var rect = panelObject.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);

        rect.anchoredPosition = position;
        rect.sizeDelta = size;

        var image = panelObject.AddComponent<Image>();
        if (_menuBgSprite == null)
        {
            var tex = Resources.Load<Texture2D>("menu");
            if (tex != null)
                _menuBgSprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
        }
        if (_menuBgSprite != null)
        {
            image.sprite = _menuBgSprite;
            image.type = Image.Type.Simple;
            image.preserveAspect = false;
            image.color = Color.white;
        }
        else
        {
            image.color = new Color(0.08f, 0.08f, 0.12f, 0.95f);
        }
        return panelObject;
    }

    private Button CreateButton(string name, Transform parent, string label, Vector2 position, UnityEngine.Events.UnityAction callback)
    {
        float screenHeight = Screen.height;
        float buttonWidth = Mathf.Max(160f, screenHeight * 0.25f);
        float buttonHeight = screenHeight * 0.05f;

        var buttonObject = GameObject.Find(name);
        if (buttonObject == null)
        {
            buttonObject = new GameObject(name);
            buttonObject.transform.SetParent(parent, false);

            var rect = buttonObject.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = new Vector2(buttonWidth, buttonHeight);

            var image = buttonObject.AddComponent<Image>();
            image.color = new Color(0.18f, 0.18f, 0.25f, 1f);

            var button = buttonObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(callback);

            var trigger = buttonObject.AddComponent<EventTrigger>();
            var enterEntry = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
            enterEntry.callback.AddListener(_ => buttonObject.transform.localScale = Vector3.one * 1.1f);
            trigger.triggers.Add(enterEntry);
            var exitEntry = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
            exitEntry.callback.AddListener(_ => buttonObject.transform.localScale = Vector3.one);
            trigger.triggers.Add(exitEntry);

            var textObject = new GameObject("Text");
            textObject.transform.SetParent(buttonObject.transform, false);
            var text = textObject.AddComponent<TextMeshProUGUI>();
            if (defaultTmpFont != null)
                text.font = defaultTmpFont;
            text.text = label;
            text.fontSize = Mathf.Max(12, (int)(screenHeight * 0.022f));
            text.color = Color.white;
            text.alignment = TextAlignmentOptions.Center;

            var textRect = textObject.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;
        }
        else
        {
            var button = buttonObject.GetComponent<Button>();
            if (button == null)
                button = buttonObject.AddComponent<Button>();
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(callback);
        }

        return buttonObject.GetComponent<Button>();
    }

    public void ShowAllGameUI(bool show)
    {
        _timeText?.gameObject.SetActive(show);
        _hpText?.gameObject.SetActive(show);
        _staminaText?.gameObject.SetActive(show);
        _moneyText?.gameObject.SetActive(show);
        _questText?.gameObject.SetActive(show);
        for (int i = 0; i < InventorySlotCount; i++)
            if (_inventorySlots[i] != null) _inventorySlots[i].SetActive(show);
        _messageText?.gameObject.SetActive(show);
        _mobSpawnerText?.gameObject.SetActive(show);
        _crosshairText?.gameObject.SetActive(show);
        _infoText?.gameObject.SetActive(show);
    }

    public void SetCrosshairVisible(bool visible)
    {
        if (_crosshairText != null)
            _crosshairText.gameObject.SetActive(visible);
    }

    public void SetInfoText(string text)
    {
        if (_infoText != null)
        {
            _infoText.text = text ?? "";
            _infoText.gameObject.SetActive(!string.IsNullOrEmpty(text));
        }
    }

    public void ShowMainMenu(bool show)
    {
        if (_mainMenuPanel != null)
            _mainMenuPanel.SetActive(show);
        if (show)
        {
            _pauseMenuPanel?.SetActive(false);
            _recordPanel?.SetActive(false);
            _questPanel?.SetActive(false);
            _tutorialPanel?.SetActive(false);
        }
        else
        {
            _statsBg?.gameObject.SetActive(true);
            for (int i = 0; i < InventorySlotCount; i++)
                if (_inventorySlots[i] != null) _inventorySlots[i].SetActive(true);
        }
    }

    public void ShowMainMenuOnly(bool show)
    {
        ShowMainMenu(show);
        if (show)
        {
            ShowAllGameUI(false);
            _statsBg?.gameObject.SetActive(false);
            for (int i = 0; i < InventorySlotCount; i++)
                if (_inventorySlots[i] != null) _inventorySlots[i].SetActive(false);
            if (_mainMenuPanel != null)
                _mainMenuPanel.SetActive(true);
        }
        else
        {
            _statsBg?.gameObject.SetActive(true);
            for (int i = 0; i < InventorySlotCount; i++)
                if (_inventorySlots[i] != null) _inventorySlots[i].SetActive(true);
        }
    }

    public void ShowPauseMenu(bool show)
    {
        if (_pauseMenuPanel != null)
            _pauseMenuPanel.SetActive(show);
    }

    public void ShowRecordPanel(bool show)
    {
        if (_recordPanel != null)
            _recordPanel.SetActive(show);
        if (show)
            _pauseMenuPanel?.SetActive(false);
        if (!show && GameManager.Instance != null && GameManager.Instance.GamePaused)
            ShowPauseMenu(true);
    }

    public void SetStatsBgVisible(bool visible)
    {
        if (_statsBg != null)
            _statsBg.gameObject.SetActive(visible);
    }

    public void ShowQuestPanel(bool show)
    {
        if (_questPanel != null)
            _questPanel.SetActive(show);
        if (show)
            _pauseMenuPanel?.SetActive(false);
        if (!show && GameManager.Instance != null && GameManager.Instance.GamePaused)
            ShowPauseMenu(true);
    }

    public void ShowTutorial(bool show)
    {
        if (_tutorialPanel != null)
            _tutorialPanel.SetActive(show);
        if (show)
        {
            _pauseMenuPanel?.SetActive(false);
            _tutorialSpreadIndex = 0;
            UpdateTutorialPage();
        }
        if (!show && GameManager.Instance != null && GameManager.Instance.GamePaused)
            ShowPauseMenu(true);
    }

    private void UpdateTutorialPage()
    {
        bool isCover = _tutorialSpreadIndex == 0;
        _tutorialBookImage.sprite = isCover ? _bookSprite : _insidePageSprite;
        _tutorialLeftArrow.SetActive(!isCover);
        _tutorialLeftText.gameObject.SetActive(!isCover);
        _tutorialRightText.gameObject.SetActive(!isCover);

        int totalSpreads = 1 + (_tutorialPages.Length + 1) / 2;
        _tutorialRightArrow.SetActive(_tutorialSpreadIndex < totalSpreads - 1);

        if (!isCover)
        {
            int leftIdx = (_tutorialSpreadIndex - 1) * 2;
            int rightIdx = leftIdx + 1;
            _tutorialLeftText.text = leftIdx < _tutorialPages.Length ? _tutorialPages[leftIdx] : "";
            _tutorialRightText.text = rightIdx < _tutorialPages.Length ? _tutorialPages[rightIdx] : "";
        }
    }

    private void TutorialNextPage()
    {
        int totalSpreads = 1 + (_tutorialPages.Length + 1) / 2;
        if (_tutorialSpreadIndex < totalSpreads - 1)
        {
            _tutorialSpreadIndex++;
            UpdateTutorialPage();
        }
    }

    private void TutorialPrevPage()
    {
        if (_tutorialSpreadIndex > 0)
        {
            _tutorialSpreadIndex--;
            UpdateTutorialPage();
        }
    }

    private void TutorialClose()
    {
        ShowTutorial(false);
    }

    public void ShowEndScreen(string title, string content)
    {
        if (_endPanel == null)
        {
            _endPanel = CreateMenuPanel("EndPanel", Vector2.zero, new Vector2(680f, 520f));
            EnsureText("EndTitle", new Vector2(0f, 170f), title, 32, _endPanel.transform, TextAlignmentOptions.Center, true, new Vector2(640f, 40f));
            EnsureText("EndContent", new Vector2(0f, 60f), content, 20, _endPanel.transform, TextAlignmentOptions.Center, true, new Vector2(640f, 120f));
            CreateButton("EndRestartButton", _endPanel.transform, "Chơi Lại", new Vector2(-110f, -180f), () => GameManager.Instance?.StartNewGame());
            CreateButton("EndQuitButton", _endPanel.transform, "Thoát", new Vector2(110f, -180f), () => Application.Quit());
        }
        var titleTf = _endPanel.transform.Find("EndTitle");
        if (titleTf != null) { var t = titleTf.GetComponent<TMP_Text>(); if (t != null) t.text = title; }
        var contentTf = _endPanel.transform.Find("EndContent");
        if (contentTf != null) { var t = contentTf.GetComponent<TMP_Text>(); if (t != null) t.text = content; }
        _endPanel.SetActive(true);
    }

    public void HideEndScreen()
    {
        if (_endPanel != null) _endPanel.SetActive(false);
    }

    public void UpdateTimeText(int day, float hour)
    {
        if (_timeText != null)
            _timeText.text = $"Ngày {day} - {hour:00.00}";
    }

    public void UpdatePlayerHud(int hp, int maxHp, float stamina, float maxStamina, long money)
    {
        if (_hpText != null)
            _hpText.text = $"HP: {hp}/{maxHp}";
        if (_staminaText != null)
            _staminaText.text = $"Sức Mạnh: {(int)stamina}/{(int)maxStamina}";
        if (_moneyText != null)
            _moneyText.text = $"Tiền: {money}";
    }

    public void UpdateInventoryText(ToolManager.InventorySlot[] slots, int selectedSlot)
    {
        for (int i = 0; i < InventorySlotCount; i++)
        {
            if (i >= slots.Length || _inventorySlotTexts[i] == null) continue;

            var item = slots[i];
            string label = item == null ? "" : (item.Count > 1 ? $"{item.Type}x{item.Count}" : item.Type);
            _inventorySlotTexts[i].text = $"{i + 1}: {label}";

            bool isSelected = (i == selectedSlot);
            if (_inventorySlotImages[i] != null)
                _inventorySlotImages[i].color = isSelected
                    ? new Color(0.35f, 0.55f, 0.75f, 0.95f)
                    : new Color(0.3f, 0.3f, 0.35f, 0.85f);
            if (_inventorySlotTexts[i] != null)
                _inventorySlotTexts[i].color = isSelected ? Color.yellow : Color.white;
        }
    }

    public void UpdateQuestHud(string text)
    {
        if (_questText != null)
            _questText.text = text;
    }

    public void UpdateQuestPanelText(string text)
    {
        if (_questLinesText != null)
            _questLinesText.text = text;
    }

    public void ShowMessage(string text, float duration)
    {
        if (_messageText == null)
            return;
        _messageText.text = text;
        StopAllCoroutines();
        StartCoroutine(HideMessageAfter(duration));
    }

    private IEnumerator HideMessageAfter(float delay)
    {
        yield return new WaitForSeconds(delay);
        if (_messageText != null)
            _messageText.text = string.Empty;
    }

}

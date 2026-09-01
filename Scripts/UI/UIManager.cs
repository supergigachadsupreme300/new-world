using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
using TMPro;

public enum GenderMenuMode { Intro, SkipIntro }

public partial class UIManager : MonoBehaviour
{
    private Canvas _canvas;
    private Sprite _menuBgSprite;
    private Sprite _hudBgSprite;
    private GameObject _mainMenuPanel;
    private GameObject _platformPanel;
    private Button _pcModeButton;
    private Button _mobileModeButton;
    private GameObject _pauseMenuPanel;
    private GameObject _settingsPanel;
    private TMP_Text _mouseSensText;
    private TMP_Text _touchSensText;
    private Button _invertYButton;
    private Button _languageButton;
    private Button _settingsPcModeButton;
    private Button _settingsMobileModeButton;
    private GameObject _recordPanel;
    private TMP_Text _recordLinesText;
    private GameObject _questPanel;
    private GameObject _saveSlotPanel;
    private TMP_Text _saveSlotTitleText;
    private Button[] _saveSlotButtons;
    private bool _saveSlotLoadMode;
    private GameObject _genderPanel;
    private GenderMenuMode _genderMenuMode;
    private GameObject _endingTreePanel;
    private TMP_Text _endingTreeTitleText;
    private Button _endingTreeSettingsButton;
    private Button _endingTreeExitButton;
    private GameObject _endingTreeContent;
    private RectTransform _endingTreeContentRect;
    private const float EndingTreeZoomMin = 0.5f;
    private const float EndingTreeZoomMax = 2.5f;
    private TMP_Text[] _endingRowTexts;
    private readonly List<EndingQuestUi> _endingQuestUis = new List<EndingQuestUi>();
    private GameObject _endingQuestTabPanel;
    private EndingQuestUi _endingQuestTabUi;
    private GameObject _endingDetailPanel;
    private int _endingDetailIndex = -1;

    private class EndingEntry
    {
        public readonly string TitleKey;
        public readonly string ContentKey;
        public readonly bool Unlocked;

        public EndingEntry(string titleKey, string contentKey, bool unlocked)
        {
            TitleKey = titleKey;
            ContentKey = contentKey;
            Unlocked = unlocked;
        }
    }

    private class EndingQuestDef
    {
        public readonly string QuestName;
        public readonly string Target;
        public readonly bool MustComplete;

        public EndingQuestDef(string questName, string target, bool mustComplete)
        {
            QuestName = questName;
            Target = target;
            MustComplete = mustComplete;
        }
    }

    private class EndingQuestUi
    {
        public EndingQuestDef Def;
        public readonly List<KeyValuePair<int, bool>> Targets = new List<KeyValuePair<int, bool>>();
        public TMP_Text Text;
        public Image Box;
    }

    private static readonly EndingQuestDef[][] EndingQuestDefs =
    {
        new[] { new EndingQuestDef("Trấn Áp Quỷ Vương", "boss_kill", true), new EndingQuestDef("Bí Mật Của Phú Ông", "mansion_secret", false) },
        new[] { new EndingQuestDef("Bí Mật Của Phú Ông", "mansion_secret", true), new EndingQuestDef("Trấn Áp Quỷ Vương", "boss_kill", false) },
        new[] { new EndingQuestDef("Bí Mật Của Phú Ông", "mansion_secret", true) },
        new[] { new EndingQuestDef("Bí Mật Của Phú Ông", "mansion_secret", true), new EndingQuestDef("Trấn Áp Quỷ Vương", "boss_kill", true), new EndingQuestDef("Xây Dựng Dinh Thự Cho Jessica", "mansion", true) },
        new EndingQuestDef[0],
        new[] { new EndingQuestDef("Trấn Áp Quỷ Vương", "boss_kill", false) },
        new EndingQuestDef[0],
        new[] { new EndingQuestDef("Xây Dựng Dinh Thự Cho Jessica", "mansion", true), new EndingQuestDef("Trấn Áp Quỷ Vương", "boss_kill", false), new EndingQuestDef("Bí Mật Của Phú Ông", "mansion_secret", false) }
    };

    private static readonly bool[] SpecialChoiceEndings = { false, false, true, false, false, true, false, false };
    private static readonly string[] EndingChoiceTexts = { null, null, "Nhận hối lộ của Phú Ông", null, null, "Chết khi giao chiến Quỷ Vương", null, null };

    private static readonly EndingEntry[] Endings =
    {
        new EndingEntry(
            "QUỶ VƯƠNG ĐÃ CHẾT NHƯNG CÁI ÁC CHƯA HẾT",
            "Quỷ Vương đã bị đánh bại, bóng tối bị đẩy lùi.\nNhưng khi cậu quay về làng...\nJessica đã bị một kẻ nghiện ngập do ma túy của Phú Ông hạ sát.\n\nKẻ gây án chỉ là bề nổi...\nCó thể đây là mưu đồ của lũ quỷ.\nCái ác chưa bị nhổ tận gốc.\nNgôi làng chưa thể yên bình.",
            true),
        new EndingEntry(
            "CÔNG LÝ ĐƯỢC THỰC THI NHƯNG HIỂM HỌA CHƯA QUA",
            "Cậu đã lật tẩy bộ mặt thật của Phú Ông.\nCảnh sát đã đến, và hắn bị bắt ngay trước dinh thự của chính mình.\n\nĐêm ấy, cậu và Jessica trở về nhà, ngủ say.\nGiữa đêm, cô chợt mở mắt...\nmột con quỷ đang nhìn cô chằm chằm.\n\nSáng hôm sau... Jessica đã biến mất.\nCảnh sát kéo đến điều tra căn nhà, nhưng không tìm được dấu vết nào.\n\nCậu chạy lên chùa tìm thầy. Thầy trầm ngâm:\n\"Jessica không bị người bắt... thứ bước vào đêm ấy là quỷ.\nHãy tìm cô ấy trước khi màn đêm buông xuống.\"\nHiểm họa thật sự vẫn chưa qua.",
            true),
        new EndingEntry(
            "KẾT THÚC ĐỒI BẠI",
            "Cậu đã im lặng. Và cậu đã được trả một cái giá rất hậu hĩnh.\n\nNhưng đêm xuống, những chiếc xe vẫn nối đuôi nhau đến dinh thự.\nJessica vẫn đang trong tầm ngắm của hắn...\n\nVà giờ, cậu là một phần của câu chuyện đó.",
            true),
        new EndingEntry(
            "KẾT THÚC HẠNH PHÚC",
            "Bạn và Jessica đã đi đến cuối con đường cùng nhau!",
            true),
        new EndingEntry(
            "KẾT THÚC NTR",
            "Bạn đã bỏ bê Jessica quá lâu.\nÔng chú giàu có đã lặng lẽ lấp đầy khoảng trống bạn để lại.\n\nKhi bạn quay lại... cô ấy đã không còn chờ đợi bạn nữa.\nBạn đã quá muộn.\n\nKhi bạn không quan tâm đến cô ấy,\nngười khác sẽ quan tâm thay bạn.",
            true),
        new EndingEntry(
            "RƠI VÀO BÓNG TỐI",
            "Quỷ Vương đã quật ngã con.\nBóng tối nuốt chửng ngôi làng.\n\nSố phận của con dừng lại tại đây...\nHãy quay về nơi lưu gần nhất và đối mặt với nó lần nữa.",
            true),
        new EndingEntry(
            "KẾT THÚC BUỒN",
            "Bạn đã đến quá muộn.\nTrong khi bạn đi tìm kiếm giàu sang,\nbạn đã quên đi điều thực sự quan trọng.\n\nCô ấy đợi...\ncho đến khi không thể đợi nữa.",
            true),
        new EndingEntry(
            "KẾT THÚC ĐỊNH MỆNH",
            "Bạn và Jessica đã xây xong dinh thự... nhưng không bao giờ diệt Quỷ Vương,\nkhông lật tẩy bí mật của Phú Ông.\n\nMột đêm, kẻ nghiện ngập do ma túy của Phú Ông đã đột nhập.\nCảnh sát tìm thấy hai thi thể trong chính ngôi nhà bạn xây nên.\nDấu vết: một vụ trộm... do nghiện ngập.\n\nVà lũ quỷ vẫn đứng im ở rìa màn đêm,\nkhông một ai nhìn thấy chúng.\n\nĐịnh mệnh của bạn đã kết thúc ngay trong nhà mình.",
            true)
    };
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
        "CHÀO MỪNG!\n\nChào mừng đến với Country Life!\n\nSau khi tốt nghiệp ngành CNTT, thị trường việc làm đã quá khó khăn. Không có việc làm, bạn quay về nông thôn của ông nội đã khuất.",
        "BẮT ĐẦU CUỘC SỐNG MỚI\n\nTại đây, bạn phải xây dựng nông trại, bảo vệ làng, và tìm kiếm hạnh phúc cho mình.\n\nBiết đâu, cô gái hàng xóm sẽ là định mệnh của bạn...",
        "DI CHUYỂN\n\nWASD \u2014 Di chuyển\nSpace \u2014 Nhảy\nShift \u2014 Chạy nhanh\nChuột \u2014 Nhìn xung quanh",
        "HÀNH ĐỘNG\n\nChuột trái \u2014 Sử dụng công cụ\nE \u2014 Tương tác / Mở cửa\nQ \u2014 Bỏ vật phẩm",
        "XÂY DỰNG\n\nGiữ Búa + F \u2014 Mở menu xây dựng\nB / N \u2014 Đổi loại công trình\nChuột trái \u2014 Đặt công trình\nF \u2014 Hủy",
        "NÔNG NGHIỆP\n\nCuốc \u2014 Làm đất để trồng cây\nLưỡi liềm \u2014 Thu hoạch\nRìu / Cuốc chim \u2014 Thu thập nguyên liệu",
        "KẺ THÙ\n\nBan đêm (18h \u2013 6h), lũ quỷ xuất hiện và tấn công bạn cùng các công trình.\nQuỷ thường: 50 máu, gây 10 sát thương.\nQuỷ khổng lồ: máu và sát thương cao hơn.",
        "TRỪ TÀ\n\nTràng hạt \u2014 Quả cầu thánh hạ gục một đòn.\nTrang bị Tràng Hạt rồi bấm chuột trái để thi triển.\n\nHãy đóng cửa khi trời tối để cản bước chúng!",
        "NGÔI CHÙA\n\nNgôi chùa 4 tầng mái cong nằm phía Đông làng, ngay cạnh nhà bà hàng xóm.\n\nĐây là công trình biểu tượng của làng \u2014 hãy đến chiêm bái và ngắm cảnh hoàng hôn từ nơi đây.",
        "CÂU CÁ\n\nTrò chơi nhỏ \u2014 Câu cá!\n\nTrang bị Cần Câu (quà của Jessica), đứng gần biển phía Tây, nhắm ra mặt nước và bấm chuột trái để thả lưỡi câu.\n\nChờ bóng cá bơi tới phao \u2014 khi phao rung, bấm chuột trái để bắt đầu kéo.",
        "CÂU CÁ (TIẾP)\n\nKéo vòng tròn giữa màn hình để di chuyển vạch trắng \u2014 giữ nó trong vùng xanh để lấp đầy thanh tiến độ.\n\nCá có thể quẫy trên bờ \u2014 dùng Gậy gõ cho xỉu rồi nhặt lên.\n\nCá bán được tiền: Chép 15, Hồi 25, Ngừ 40, Nóc 60.",
        "MẸO\n\nThu hoạch lúa để kiếm tiền\nXây dựng tường và tháp canh để bảo vệ\nHoàn thành nhiệm vụ để nhận thưởng\nNgủ trên giường để lưu game"
    };
    private GameObject _endPanel;
    private GameObject _bossEndPanel;
    private GameObject _bossBarRoot;
    private Image _bossBarFill;
    private TMP_Text _bossBarName;

    private GameObject _karmaBarRoot;
    private Image _karmaBarFill;
    private TMP_Text _karmaBarText;
    private int _lastKarma;
    private int _lastKarmaMax;
    private bool _karmaBarVisible;

    private GameObject _eventTestPanel;

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
    private bool _inventoryCreated;
    private bool _tutorialCreated;
    private RectTransform _statsBg;
    private float _statsScale = 1f;
    private TMP_Text _messageText;
    private Image _messageBg;
    private Canvas _messageCanvas;
    private Coroutine _typewriterCoroutine;
    private TMP_Text _mobSpawnerText;
    private TMP_Text _crosshairText;
    private TMP_Text _infoText;
    private TMP_Text _eKeyPromptText;
    private TMP_Text _lmbPromptText;

    public TMP_FontAsset defaultTmpFont;

    private int _lastScreenWidth;
    private int _lastScreenHeight;
    private bool _uiPipelineLogged;

    private int _lastHp = -1, _lastMaxHp = -1;
    private float _lastStamina = -1f, _lastMaxStamina = -1f;
    private long _lastMoney = -1;
    private int _lastTimeDay = -1;
    private float _lastTimeHour = -1f;

    void Start()
    {
        _lastScreenWidth = Screen.width;
        _lastScreenHeight = Screen.height;
        Localization.OnLanguageChanged += RefreshLocalizedText;
        InitializeUI();
        RefreshLocalizedText();
    }

    void OnDestroy()
    {
        Localization.OnLanguageChanged -= RefreshLocalizedText;
    }

    void Update()
    {
        if (Screen.width != _lastScreenWidth || Screen.height != _lastScreenHeight)
        {
            _lastScreenWidth = Screen.width;
            _lastScreenHeight = Screen.height;
            ResizeInventory();
        }

        // Defensive: while the main menu is showing, keep the cursor unlocked
        // (cancels any stray re-lock, e.g. PlayerController.Start() ordering).
        if (GameManager.Instance != null && !GameManager.Instance.InGame &&
            _mainMenuPanel != null && _mainMenuPanel.activeInHierarchy)
        {
            GameInput.SetCursorLocked(false);
        }
    }

    private void LogUiPipeline()
    {
        if (_uiPipelineLogged) return;
        _uiPipelineLogged = true;

        var es = EventSystem.current;
        InputSystemUIInputModule module = null;
        if (es != null)
            module = es.GetComponent<InputSystemUIInputModule>();
        var raycaster = _canvas != null ? _canvas.GetComponent<GraphicRaycaster>() : null;
        Debug.Log("[UIPipe] es=" + (es != null ? es.name : "NULL") +
            " esOn=" + (es != null && es.isActiveAndEnabled) +
            " module=" + (module != null ? module.GetType().Name : "NULL") +
            " modOn=" + (module != null && module.isActiveAndEnabled) +
            " point=" + (module != null && module.point != null) +
            " click=" + (module != null && module.leftClick != null) +
            " mouse=" + (Mouse.current != null) +
            " canvas=" + (_canvas != null ? _canvas.name : "NULL") +
            " raycaster=" + (raycaster != null) +
            " menuActive=" + (_mainMenuPanel != null && _mainMenuPanel.activeInHierarchy));
    }

    private void ResizeInventory()
    {
        float sw = Screen.width;
        float slotW = sw * 0.065f;
        float slotH = slotW;
        float totalW = InventorySlotCount * slotW + (InventorySlotCount - 1) * 4f;
        float startX = -totalW * 0.5f + sw * 0.02f;
        float y = Screen.height * 0.03f;

        for (int i = 0; i < InventorySlotCount; i++)
        {
            if (_inventorySlots[i] == null) continue;
            var rect = _inventorySlots[i].GetComponent<RectTransform>();
            if (rect != null)
            {
                rect.sizeDelta = new Vector2(slotW, slotH);
                rect.anchoredPosition = new Vector2(startX + i * (slotW + 4f), y);
            }
            if (_inventorySlotTexts[i] != null)
                _inventorySlotTexts[i].fontSize = Mathf.Clamp(sw / 120f, 8f, 16f);
        }
    }

    public void InitializeUI()
    {
        EnsureEventSystem();
        var existingHud = GameObject.Find("HUD_Canvas");
        if (existingHud != null)
            _canvas = existingHud.GetComponent<Canvas>();
        else
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
        float menuButtonWidth = Mathf.Max(160f, screenHeight * 0.25f);
        float panelWidth = Mathf.Min(screenWidth * 0.4f, 560f);
        float panelHeight = Mathf.Min(screenHeight * 0.8f, 520f);
        // Stats background panel (behind Time, HP, Stamina, Money, Quest)
        _statsScale = Mathf.Clamp(Screen.width / 1400f, 0.8f, 2f);
        float sW = 320f * _statsScale;
        float sH = 300f * _statsScale;
        _statsBg = CreateHudBackground("StatsBg",
            new Vector2(0f, 0f),
            new Vector2(sW, sH),
            new Vector2(0f, 1f));
        var statsImg = _statsBg.GetComponent<Image>();
        if (statsImg != null)
            statsImg.raycastTarget = true;
        if (_statsBg.GetComponent<Button>() == null)
        {
            var statsBtn = _statsBg.gameObject.AddComponent<Button>();
            statsBtn.targetGraphic = statsImg;
            statsBtn.onClick.AddListener(OpenSettingsFromStatsTab);
        }

        _timeText = EnsureText(
            "TimeText",
            new Vector2(40f * _statsScale, -20f * _statsScale),
            Localization.F("Ngày {0} - {1}", 1, "08.00"),
            Mathf.RoundToInt(20 * _statsScale),
            null,
            TextAlignmentOptions.Left,
            true,
            new Vector2(310f * _statsScale, 30f * _statsScale),
            new Vector2(0f, 1f),
            new Vector2(0f, 1f),
            new Vector2(0f, 1f)
        );

        _hpText = EnsureText(
            "HPText",
            new Vector2(40f * _statsScale, -60f * _statsScale),
            "HP: 100/100",
            Mathf.RoundToInt(20 * _statsScale),
            null,
            TextAlignmentOptions.Left,
            true,
            new Vector2(310f * _statsScale, 30f * _statsScale),
            new Vector2(0f, 1f),
            new Vector2(0f, 1f),
            new Vector2(0f, 1f)
        );

        _staminaText = EnsureText(
            "StaminaText",
            new Vector2(40f * _statsScale, -100f * _statsScale),
            Localization.F("Thể Lực: {0}/{1}", 100, 100),
            Mathf.RoundToInt(20 * _statsScale),
            null,
            TextAlignmentOptions.Left,
            true,
            new Vector2(310f * _statsScale, 30f * _statsScale),
            new Vector2(0f, 1f),
            new Vector2(0f, 1f),
            new Vector2(0f, 1f)
        );

        _moneyText = EnsureText(
            "MoneyText",
            new Vector2(40f * _statsScale, -140f * _statsScale),
            Localization.F("Tiền: {0}", 0),
            Mathf.RoundToInt(20 * _statsScale),
            null,
            TextAlignmentOptions.Left,
            true,
            new Vector2(310f * _statsScale, 30f * _statsScale),
            new Vector2(0f, 1f),
            new Vector2(0f, 1f),
            new Vector2(0f, 1f)
        );

        _questText = EnsureText(
            "QuestText",
            new Vector2(40f * _statsScale, -175f * _statsScale),
            Localization.T("Nhiệm Vụ: Sẵn sàng"),
            Mathf.RoundToInt(15 * _statsScale),
            null,
            TextAlignmentOptions.Left,
            true,
            new Vector2(300f * _statsScale, 190f * _statsScale),
            new Vector2(0f, 1f),
            new Vector2(0f, 1f),
            new Vector2(0f, 1f)
        );
        _questText.overflowMode = TextOverflowModes.Overflow;

        // Inventory: 10 individual slots at bottom center
        float slotW = screenWidth * 0.065f;
        float slotH = slotW;
        float totalW = InventorySlotCount * slotW + (InventorySlotCount - 1) * 4f;
        float startX = -totalW * 0.5f + screenWidth * 0.02f;

        if (!_inventoryCreated)
        {
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

            var slotButton = slotGo.AddComponent<Button>();
            slotButton.targetGraphic = slotImg;
            int slotIndex = i;
            slotButton.onClick.AddListener(() => ToolManager.Instance?.SelectSlot(slotIndex));

            var textGo = new GameObject("Text");
            textGo.transform.SetParent(slotGo.transform, false);
            var text = textGo.AddComponent<TextMeshProUGUI>();
            if (defaultTmpFont != null)
                text.font = defaultTmpFont;
            text.text = $"{i + 1}:";
            text.fontSize = Mathf.Clamp(screenWidth / 120f, 8f, 16f);
            text.color = Color.white;
            text.alignment = TextAlignmentOptions.Center;
            text.raycastTarget = false;

            var textRect = textGo.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            _inventorySlots[i] = slotGo;
            _inventorySlotTexts[i] = text;
            _inventorySlotImages[i] = slotImg;
        }
        _inventoryCreated = true;
        }

        // Message text: dedicated overlay canvas (isolated from HUD_Canvas rendering issues)
        CreateMessageCanvas(screenWidth, screenHeight, lineHeight, largefontSize);

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
        _crosshairText.fontMaterial = crossMat;
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

        _eKeyPromptText = EnsureText(
            "EKeyPrompt",
            new Vector2(0f, -(lineHeight * 3.5f)),
            "",
            (int)(fontSize * 0.9f),
            null,
            TextAlignmentOptions.Center,
            false,
            new Vector2(screenWidth * 0.4f, lineHeight * 1.5f),
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f)
        );
        _eKeyPromptText.color = new Color(1f, 1f, 0.8f);
        _eKeyPromptText.gameObject.SetActive(false);
        {
            var promptMat = new Material(_eKeyPromptText.fontSharedMaterial);
            promptMat.EnableKeyword("OUTLINE_ON");
            promptMat.SetFloat("_OutlineWidth", 0.25f);
            promptMat.SetColor("_OutlineColor", Color.black);
            _eKeyPromptText.fontSharedMaterial = promptMat;
        }

        _lmbPromptText = EnsureText(
            "LMBPrompt",
            new Vector2(0f, -(lineHeight * 5f)),
            "",
            (int)(fontSize * 0.8f),
            null,
            TextAlignmentOptions.Center,
            false,
            new Vector2(screenWidth * 0.4f, lineHeight * 1.5f),
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f)
        );
        _lmbPromptText.color = new Color(0.8f, 1f, 0.8f);
        _lmbPromptText.gameObject.SetActive(false);
        {
            var lmbMat = new Material(_lmbPromptText.fontSharedMaterial);
            lmbMat.EnableKeyword("OUTLINE_ON");
            lmbMat.SetFloat("_OutlineWidth", 0.25f);
            lmbMat.SetColor("_OutlineColor", Color.black);
            _lmbPromptText.fontMaterial = lmbMat;
        }

        // Panels - responsive sizes
        _pauseMenuPanel = CreateMenuPanel("PauseMenu", Vector2.zero, new Vector2(panelWidth, panelHeight));
        CreateButton("ContinueButton", _pauseMenuPanel.transform, Localization.T("Tiếp Tục"), new Vector2(0f, panelHeight * 0.40f), () => GameManager.Instance?.TogglePause(false));
        CreateButton("SaveButton", _pauseMenuPanel.transform, Localization.T("Lưu Game"), new Vector2(0f, panelHeight * 0.30f), () => ShowSaveSlotMenu(false));
        CreateButton("LoadButton", _pauseMenuPanel.transform, Localization.T("Tải Game"), new Vector2(0f, panelHeight * 0.20f), () => ShowSaveSlotMenu(true));
        CreateButton("StatsButton", _pauseMenuPanel.transform, Localization.T("Thống Kê"), new Vector2(0f, panelHeight * 0.10f), () => ShowRecordPanel(true));
        CreateButton("QuestsButton", _pauseMenuPanel.transform, Localization.T("Nhiệm Vụ"), new Vector2(0f, 0f), () => ShowQuestPanel(true));
        CreateButton("SettingsButton", _pauseMenuPanel.transform, Localization.T("Cài Đặt"), new Vector2(0f, -panelHeight * 0.10f), () => ShowSettingsPanel(true));
        CreateButton("TutorialButton", _pauseMenuPanel.transform, Localization.T("Hướng Dẫn"), new Vector2(0f, -panelHeight * 0.20f), () => ShowTutorial(true));
        CreateButton("ExitButton", _pauseMenuPanel.transform, Localization.T("Thoát"), new Vector2(0f, -panelHeight * 0.30f), () => GameManager.Instance?.ReturnToMainMenu());
        CreateButton("WatchDealButton", _pauseMenuPanel.transform, Localization.T("Xem Cảnh Giao Dịch (Test)"), new Vector2(0f, -panelHeight * 0.40f), () => RichManNPC.Instance?.ForceStartDealForWatch());
        _pauseMenuPanel.SetActive(false);

        CreateSaveSlotMenu(panelWidth, padding, largefontSize);

        _recordPanel = CreateMenuPanel("RecordPanel", Vector2.zero, new Vector2(panelWidth, panelHeight));
        EnsureText("RecordTitle", new Vector2(0f, panelHeight * 0.35f), Localization.T("THỐNG KÊ"), (int)largefontSize, _recordPanel.transform, TextAlignmentOptions.Center, true, new Vector2(panelWidth - padding * 4, lineHeight));
        EnsureText("RecordLines", new Vector2(0f, panelHeight * 0.1f), BuildRecordLines(0, 0, 0, 0), (int)fontSize, _recordPanel.transform, TextAlignmentOptions.Left, true, new Vector2(panelWidth - padding * 4, panelHeight * 0.4f));
        CreateButton("RecordBackButton", _recordPanel.transform, Localization.T("Quay Lại"), new Vector2(0f, -panelHeight * 0.35f), () => ShowRecordPanel(false));
        _recordPanel.SetActive(false);

        float settingsHeight = Mathf.Min(screenHeight * 0.85f, 620f);
        float settingsWidth = Mathf.Min(screenWidth * 0.5f, 680f);
        var settingsButtonSize = new Vector2(Mathf.Min(menuButtonWidth * 0.9f, settingsWidth * 0.34f), screenHeight * 0.04f);
        _settingsPanel = CreateMenuPanel("SettingsPanel", Vector2.zero, new Vector2(settingsWidth, settingsHeight));

        EnsureText("SettingsTitle", new Vector2(0f, settingsHeight * 0.34f), Localization.T("CÀI ĐẶT"), (int)largefontSize, _settingsPanel.transform, TextAlignmentOptions.Center, true, new Vector2(settingsWidth - padding * 4, lineHeight));

        float settingsContentW = settingsWidth - padding * 4;
        float settingsScrollbarW = 12f;

        var settingsViewport = new GameObject("SettingsViewport");
        settingsViewport.transform.SetParent(_settingsPanel.transform, false);
        var settingsViewportRect = settingsViewport.AddComponent<RectTransform>();
        settingsViewportRect.anchorMin = new Vector2(0.5f, 0.5f);
        settingsViewportRect.anchorMax = new Vector2(0.5f, 0.5f);
        settingsViewportRect.pivot = new Vector2(0.5f, 0.5f);
        settingsViewportRect.anchoredPosition = new Vector2(0f, -settingsHeight * 0.03f);
        settingsViewportRect.sizeDelta = new Vector2(settingsContentW, settingsHeight * 0.66f);
        settingsViewport.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0f);
        settingsViewport.AddComponent<RectMask2D>();

        var settingsContent = new GameObject("SettingsContent");
        settingsContent.transform.SetParent(settingsViewport.transform, false);
        var settingsContentRect = settingsContent.AddComponent<RectTransform>();
        settingsContentRect.anchorMin = new Vector2(0f, 1f);
        settingsContentRect.anchorMax = new Vector2(1f, 1f);
        settingsContentRect.pivot = new Vector2(0.5f, 1f);
        settingsContentRect.anchoredPosition = Vector2.zero;

        float settingsContentH = 8f
            + lineHeight + 8f + screenHeight * 0.04f + 8f
            + lineHeight + 8f + screenHeight * 0.04f + 8f
            + settingsButtonSize.y + 8f + settingsButtonSize.y + 8f
            + lineHeight + 8f + settingsButtonSize.y + 8f
            + settingsButtonSize.y + 8f;
        settingsContentRect.sizeDelta = new Vector2(0f, settingsContentH);
        var settingsLayout = settingsContent.AddComponent<VerticalLayoutGroup>();
        settingsLayout.spacing = 8f;
        settingsLayout.padding = new RectOffset(4, 4, 8, 8);
        settingsLayout.childControlWidth = true;
        settingsLayout.childControlHeight = false;
        settingsLayout.childForceExpandWidth = true;
        settingsLayout.childForceExpandHeight = false;
        var settingsCSF = settingsContent.AddComponent<ContentSizeFitter>();
        settingsCSF.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        settingsCSF.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

        var settingsScrollbarGo = new GameObject("SettingsScrollbar");
        settingsScrollbarGo.transform.SetParent(_settingsPanel.transform, false);
        var settingsScrollbarRt = settingsScrollbarGo.AddComponent<RectTransform>();
        settingsScrollbarRt.anchorMin = new Vector2(0.5f, 0.5f);
        settingsScrollbarRt.anchorMax = new Vector2(0.5f, 0.5f);
        settingsScrollbarRt.pivot = new Vector2(0.5f, 0.5f);
        settingsScrollbarRt.anchoredPosition = new Vector2(settingsContentW * 0.5f - settingsScrollbarW * 0.5f - 2f, -settingsHeight * 0.03f);
        settingsScrollbarRt.sizeDelta = new Vector2(settingsScrollbarW, settingsHeight * 0.66f);
        settingsScrollbarGo.AddComponent<Image>().color = new Color(0.15f, 0.15f, 0.15f, 0.6f);
        var settingsScrollbar = settingsScrollbarGo.AddComponent<Scrollbar>();

        var settingsHandleArea = new GameObject("SlidingArea");
        settingsHandleArea.transform.SetParent(settingsScrollbarGo.transform, false);
        var settingsHandleAreaRt = settingsHandleArea.AddComponent<RectTransform>();
        settingsHandleAreaRt.anchorMin = Vector2.zero;
        settingsHandleAreaRt.anchorMax = Vector2.one;
        settingsHandleAreaRt.offsetMin = new Vector2(2f, 4f);
        settingsHandleAreaRt.offsetMax = new Vector2(-2f, -4f);

        var settingsHandle = new GameObject("Handle");
        settingsHandle.transform.SetParent(settingsHandleArea.transform, false);
        var settingsHandleRt = settingsHandle.AddComponent<RectTransform>();
        settingsHandleRt.anchorMin = Vector2.zero;
        settingsHandleRt.anchorMax = new Vector2(1f, 0.3f);
        settingsHandleRt.offsetMin = Vector2.zero;
        settingsHandleRt.offsetMax = Vector2.zero;
        var settingsHandleImg = settingsHandle.AddComponent<Image>();
        settingsHandleImg.color = new Color(0.5f, 0.5f, 0.5f, 0.8f);
        settingsScrollbar.handleRect = settingsHandleRt;
        settingsScrollbar.targetGraphic = settingsHandleImg;
        settingsScrollbar.direction = Scrollbar.Direction.BottomToTop;

        var settingsScrollRect = settingsViewport.AddComponent<ScrollRect>();
        settingsScrollRect.viewport = settingsViewportRect;
        settingsScrollRect.content = settingsContentRect;
        settingsScrollRect.horizontal = false;
        settingsScrollRect.vertical = true;
        settingsScrollRect.verticalScrollbar = settingsScrollbar;
        settingsScrollRect.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHideAndExpandViewport;
        settingsScrollRect.verticalScrollbarSpacing = 0f;
        settingsScrollRect.movementType = ScrollRect.MovementType.Clamped;
        settingsScrollRect.scrollSensitivity = 30f;

        Transform settingsScrollContent = settingsContent.transform;

        var mouseSensCaption = EnsureText("MouseSensCaption", Vector2.zero, Localization.T("ĐỘ NHẠY CHUỘT"), (int)fontSize, settingsScrollContent, TextAlignmentOptions.Center, true, new Vector2(settingsWidth - padding * 4, lineHeight));
        var mouseSensCaptionLE = mouseSensCaption.gameObject.AddComponent<LayoutElement>();
        mouseSensCaptionLE.preferredHeight = lineHeight;
        var mouseSensRow = new GameObject("MouseSensRow");
        mouseSensRow.transform.SetParent(settingsScrollContent, false);
        var mouseSensRowRect = mouseSensRow.AddComponent<RectTransform>();
        mouseSensRowRect.sizeDelta = new Vector2(settingsWidth - padding * 4, screenHeight * 0.04f);
        var mouseSensRowLayout = mouseSensRow.AddComponent<HorizontalLayoutGroup>();
        mouseSensRowLayout.spacing = 10f;
        mouseSensRowLayout.childAlignment = TextAnchor.MiddleCenter;
        mouseSensRowLayout.childControlWidth = false;
        mouseSensRowLayout.childControlHeight = true;
        mouseSensRowLayout.childForceExpandWidth = false;
        mouseSensRowLayout.childForceExpandHeight = false;
        var mouseSensRowLE = mouseSensRow.AddComponent<LayoutElement>();
        mouseSensRowLE.preferredHeight = screenHeight * 0.04f;
        CreateButton("MouseSensMinus", mouseSensRow.transform, "<", Vector2.zero, () => { SettingsManager.SetMouseSensitivity(SettingsManager.MouseSensitivity - 0.25f); UpdateSettingsValues(); }, new Vector2(menuButtonWidth * 0.3f, screenHeight * 0.035f));
        _mouseSensText = EnsureText("MouseSensValue", Vector2.zero, SettingsManager.MouseSensitivity.ToString("0.00"), (int)fontSize, mouseSensRow.transform, TextAlignmentOptions.Center, true, new Vector2(menuButtonWidth * 0.4f, screenHeight * 0.035f));
        CreateButton("MouseSensPlus", mouseSensRow.transform, ">", Vector2.zero, () => { SettingsManager.SetMouseSensitivity(SettingsManager.MouseSensitivity + 0.25f); UpdateSettingsValues(); }, new Vector2(menuButtonWidth * 0.3f, screenHeight * 0.035f));

        var touchSensCaption = EnsureText("TouchSensCaption", Vector2.zero, Localization.T("ĐỘ NHẠY CẢM ỨNG"), (int)fontSize, settingsScrollContent, TextAlignmentOptions.Center, true, new Vector2(settingsWidth - padding * 4, lineHeight));
        var touchSensCaptionLE = touchSensCaption.gameObject.AddComponent<LayoutElement>();
        touchSensCaptionLE.preferredHeight = lineHeight;
        var touchSensRow = new GameObject("TouchSensRow");
        touchSensRow.transform.SetParent(settingsScrollContent, false);
        var touchSensRowRect = touchSensRow.AddComponent<RectTransform>();
        touchSensRowRect.sizeDelta = new Vector2(settingsWidth - padding * 4, screenHeight * 0.04f);
        var touchSensRowLayout = touchSensRow.AddComponent<HorizontalLayoutGroup>();
        touchSensRowLayout.spacing = 10f;
        touchSensRowLayout.childAlignment = TextAnchor.MiddleCenter;
        touchSensRowLayout.childControlWidth = false;
        touchSensRowLayout.childControlHeight = true;
        touchSensRowLayout.childForceExpandWidth = false;
        touchSensRowLayout.childForceExpandHeight = false;
        var touchSensRowLE = touchSensRow.AddComponent<LayoutElement>();
        touchSensRowLE.preferredHeight = screenHeight * 0.04f;
        CreateButton("TouchSensMinus", touchSensRow.transform, "<", Vector2.zero, () => { SettingsManager.SetTouchSensitivity(SettingsManager.TouchSensitivity - 0.03f); UpdateSettingsValues(); }, new Vector2(menuButtonWidth * 0.3f, screenHeight * 0.035f));
        _touchSensText = EnsureText("TouchSensValue", Vector2.zero, SettingsManager.TouchSensitivity.ToString("0.00"), (int)fontSize, touchSensRow.transform, TextAlignmentOptions.Center, true, new Vector2(menuButtonWidth * 0.4f, screenHeight * 0.035f));
        CreateButton("TouchSensPlus", touchSensRow.transform, ">", Vector2.zero, () => { SettingsManager.SetTouchSensitivity(SettingsManager.TouchSensitivity + 0.03f); UpdateSettingsValues(); }, new Vector2(menuButtonWidth * 0.3f, screenHeight * 0.035f));

        _invertYButton = CreateButton("InvertYButton", settingsScrollContent, "", Vector2.zero, () => { SettingsManager.SetInvertY(!SettingsManager.InvertY); UpdateSettingsValues(); }, settingsButtonSize);
        var invertYLE = _invertYButton.gameObject.AddComponent<LayoutElement>();
        invertYLE.preferredHeight = settingsButtonSize.y;

        _languageButton = CreateButton("LanguageButton", settingsScrollContent, "", Vector2.zero, () => { Localization.ToggleLanguage(); UpdateSettingsValues(); }, settingsButtonSize);
        var langLE = _languageButton.gameObject.AddComponent<LayoutElement>();
        langLE.preferredHeight = settingsButtonSize.y;

        var controlModeCaption = EnsureText("ControlModeCaption", Vector2.zero, Localization.T("CÁCH ĐIỀU KHIỂN"), (int)fontSize, settingsScrollContent, TextAlignmentOptions.Center, true, new Vector2(settingsWidth - padding * 4, lineHeight));
        var controlModeCaptionLE = controlModeCaption.gameObject.AddComponent<LayoutElement>();
        controlModeCaptionLE.preferredHeight = lineHeight;
        var controlModeRow = new GameObject("ControlModeRow");
        controlModeRow.transform.SetParent(settingsScrollContent, false);
        var controlModeRowRect = controlModeRow.AddComponent<RectTransform>();
        controlModeRowRect.sizeDelta = new Vector2(settingsWidth - padding * 4, settingsButtonSize.y);
        var controlModeRowLayout = controlModeRow.AddComponent<HorizontalLayoutGroup>();
        controlModeRowLayout.spacing = 12f;
        controlModeRowLayout.childAlignment = TextAnchor.MiddleCenter;
        controlModeRowLayout.childControlWidth = true;
        controlModeRowLayout.childControlHeight = true;
        controlModeRowLayout.childForceExpandWidth = true;
        controlModeRowLayout.childForceExpandHeight = false;
        var controlModeRowLE = controlModeRow.AddComponent<LayoutElement>();
        controlModeRowLE.preferredHeight = settingsButtonSize.y;
        _settingsPcModeButton = CreateButton("SettingsPCModeButton", controlModeRow.transform, Localization.T("PC / Bàn Phím"), Vector2.zero, () => SetControlMode(ControlMode.PC), settingsButtonSize);
        _settingsMobileModeButton = CreateButton("SettingsMobileModeButton", controlModeRow.transform, Localization.T("Điện Thoại / Cảm Ứng"), Vector2.zero, () => SetControlMode(ControlMode.Mobile), settingsButtonSize);
        var endingTabBtn = CreateButton("EndingTreeTabButton", settingsScrollContent, Localization.T("Cây Kết Thúc"), Vector2.zero, () => ShowEndingTreePanel(true), settingsButtonSize);
        var endingTabLE = endingTabBtn.gameObject.AddComponent<LayoutElement>();
        endingTabLE.preferredHeight = settingsButtonSize.y;

        CreateButton("SettingsCloseButton", _settingsPanel.transform, Localization.T("Đóng"), new Vector2(0f, -settingsHeight * 0.47f), () => ShowSettingsPanel(false), settingsButtonSize);

        UpdateSettingsValues();
        _settingsPanel.SetActive(false);

        _endingTreePanel = CreateFullScreenPanel("EndingTreePanel");
        _endingTreeContent = CreateEndingTreeContent(_endingTreePanel.transform);
        _endingTreeContentRect = _endingTreeContent.GetComponent<RectTransform>();
        _endingTreeTitleText = EnsureText("EndingTreeTitle", new Vector2(0f, screenHeight * 0.43f), Localization.T("CÂY KẾT THÚC"), (int)largefontSize, _endingTreePanel.transform, TextAlignmentOptions.Center, true, new Vector2(screenWidth * 0.7f, lineHeight));

        _endingTreeExitButton = CreateButton("EndingTreeExitButton", _endingTreePanel.transform, Localization.T("Đóng"), Vector2.zero, () => ShowEndingTreePanel(false), new Vector2(menuButtonWidth * 0.55f, buttonHeight));
        var exitRt = _endingTreeExitButton.GetComponent<RectTransform>();
        exitRt.anchorMin = new Vector2(1f, 1f);
        exitRt.anchorMax = new Vector2(1f, 1f);
        exitRt.pivot = new Vector2(1f, 1f);
        exitRt.anchoredPosition = new Vector2(-padding, -padding);

        _endingTreeSettingsButton = CreateButton("EndingTreeSettingsTabButton", _endingTreePanel.transform, Localization.T("Cài Đặt"), new Vector2(0f, -screenHeight * 0.44f), () => { _endingTreePanel?.SetActive(false); ShowSettingsPanel(true); });

        BuildEndingTreeLayout();
        _endingTreePanel.SetActive(false);

        _endingQuestTabPanel = CreateFullScreenPanel("EndingQuestTabPanel");
        var tabImg = _endingQuestTabPanel.GetComponent<Image>();
        if (tabImg != null)
            tabImg.color = new Color(0f, 0f, 0f, 0.88f);
        var tabBox = new GameObject("EndingQuestTabBox");
        tabBox.transform.SetParent(_endingQuestTabPanel.transform, false);
        var tabBoxRect = tabBox.AddComponent<RectTransform>();
        tabBoxRect.anchorMin = new Vector2(0.5f, 0.5f);
        tabBoxRect.anchorMax = new Vector2(0.5f, 0.5f);
        tabBoxRect.pivot = new Vector2(0.5f, 0.5f);
        tabBoxRect.anchoredPosition = Vector2.zero;
        tabBoxRect.sizeDelta = new Vector2(panelWidth, panelHeight);
        var tabBoxImg = tabBox.AddComponent<Image>();
        tabBoxImg.color = new Color(0.04f, 0.04f, 0.07f, 0.97f);
        EnsureText("EndingQuestTabTitle", new Vector2(0f, panelHeight * 0.34f), "", (int)(largefontSize * 1.05f), tabBox.transform, TextAlignmentOptions.Center, true, new Vector2(panelWidth - padding * 4, lineHeight * 1.5f));
        EnsureText("EndingQuestTabStory", new Vector2(0f, panelHeight * 0.14f), "", (int)fontSize, tabBox.transform, TextAlignmentOptions.Left, true, new Vector2(panelWidth - padding * 4, panelHeight * 0.22f));
        EnsureText("EndingQuestTabCond", new Vector2(0f, -panelHeight * 0.17f), "", (int)fontSize, tabBox.transform, TextAlignmentOptions.Left, true, new Vector2(panelWidth - padding * 4, panelHeight * 0.4f));
        CreateButton("EndingQuestTabCloseButton", tabBox.transform, Localization.T("Đóng"), new Vector2(0f, -panelHeight * 0.41f), () => ShowEndingQuestTab(false), new Vector2(menuButtonWidth * 0.55f, buttonHeight));
        _endingQuestTabPanel.SetActive(false);

        _endingDetailPanel = CreateFullScreenPanel("EndingDetailPanel");
        var detailImg = _endingDetailPanel.GetComponent<Image>();
        if (detailImg != null)
            detailImg.color = new Color(0f, 0f, 0f, 0.88f);
        var detailBox = new GameObject("EndingDetailBox");
        detailBox.transform.SetParent(_endingDetailPanel.transform, false);
        var detailBoxRect = detailBox.AddComponent<RectTransform>();
        detailBoxRect.anchorMin = new Vector2(0.5f, 0.5f);
        detailBoxRect.anchorMax = new Vector2(0.5f, 0.5f);
        detailBoxRect.pivot = new Vector2(0.5f, 0.5f);
        detailBoxRect.anchoredPosition = Vector2.zero;
        detailBoxRect.sizeDelta = new Vector2(panelWidth, panelHeight);
        var detailBoxImg = detailBox.AddComponent<Image>();
        detailBoxImg.color = new Color(0.04f, 0.04f, 0.07f, 0.97f);
        EnsureText("EndingDetailTitle", new Vector2(0f, panelHeight * 0.34f), "", (int)(largefontSize * 1.05f), detailBox.transform, TextAlignmentOptions.Center, true, new Vector2(panelWidth - padding * 4, lineHeight * 1.5f));
        EnsureText("EndingDetailStory", new Vector2(0f, panelHeight * 0.14f), "", (int)fontSize, detailBox.transform, TextAlignmentOptions.Left, true, new Vector2(panelWidth - padding * 4, panelHeight * 0.22f));
        EnsureText("EndingDetailCond", new Vector2(0f, -panelHeight * 0.17f), "", (int)fontSize, detailBox.transform, TextAlignmentOptions.Left, true, new Vector2(panelWidth - padding * 4, panelHeight * 0.4f));
        CreateButton("EndingDetailPlayButton", detailBox.transform, Localization.T("Phát Kết Thúc"), new Vector2(0f, -panelHeight * 0.38f), PlayEndingFromDetail, new Vector2(menuButtonWidth * 0.75f, buttonHeight * 1.1f));
        CreateButton("EndingDetailCloseButton", detailBox.transform, Localization.T("Đóng"), new Vector2(0f, -panelHeight * 0.44f), () => ShowEndingDetail(false), new Vector2(menuButtonWidth * 0.55f, buttonHeight));
        _endingDetailPanel.SetActive(false);

        _questPanel = CreateMenuPanel("QuestPanel", Vector2.zero, new Vector2(panelWidth, panelHeight));
        EnsureText("QuestTitle", new Vector2(0f, panelHeight * 0.35f), Localization.T("NHIỆM VỤ"), (int)largefontSize, _questPanel.transform, TextAlignmentOptions.Center, true, new Vector2(panelWidth - padding * 4, lineHeight));
        _questLinesText = EnsureText("QuestLines", new Vector2(0f, panelHeight * 0.1f), Localization.T("1. Thu hoạch lúa 0/100\n2. Diệt quái 0/30\n3. Kiếm tiền 0/100000"), (int)fontSize, _questPanel.transform, TextAlignmentOptions.Left, true, new Vector2(panelWidth - padding * 4, panelHeight * 0.6f));
        CreateButton("QuestCloseButton", _questPanel.transform, Localization.T("Đóng"), new Vector2(0f, -panelHeight * 0.35f), () => ShowQuestPanel(false));
        _questPanel.SetActive(false);

        if (!_tutorialCreated)
        {
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

        float redXSize = Screen.height * 0.1f;
        var redXObj = new GameObject("TutorialRedX");
        redXObj.transform.SetParent(bookObj.transform, false);
        var redXRt = redXObj.AddComponent<RectTransform>();
        redXRt.anchorMin = new Vector2(1f, 1f);
        redXRt.anchorMax = new Vector2(1f, 1f);
        redXRt.pivot = new Vector2(1f, 1f);
        redXRt.anchoredPosition = new Vector2(-10f, -30f);
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
        float textH = bookH * 1.35f;
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
        _tutorialLeftText.fontSize = Mathf.Max(14, (int)(Screen.height * 0.025f));
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
        _tutorialRightText.fontSize = Mathf.Max(14, (int)(Screen.height * 0.025f));
        _tutorialRightText.color = Color.black;
        _tutorialRightText.alignment = TextAlignmentOptions.Center;
        _tutorialRightText.text = "";
        rightTextObj.SetActive(false);

        _tutorialPanel.SetActive(false);
        _tutorialCreated = true;
        }

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
        EnsureText("TitleText", new Vector2(0f, panelHeight * 0.3f), Localization.T("XÂY DỰNG NÔNG TRẠI"), (int)(largefontSize * 1.1f), _mainMenuPanel.transform, TextAlignmentOptions.Center, true, new Vector2(panelWidth - padding * 4, lineHeight * 1.5f));
        float mainMenuPitch = panelHeight * 0.11f;
        float mainMenuStartY = panelHeight * 0.40f;
        var mainMenuButtonSize = new Vector2(Mathf.Min(menuButtonWidth * 0.8f, panelWidth * 0.6f), panelHeight * 0.08f);
        float frameContentHalfY = (menuRect.rect.height > 1f ? menuRect.rect.height : screenHeight) * 0.35f;
        float menuBtnHalfH = mainMenuButtonSize.y * 0.5f;
        float menuGap = padding * 0.5f;
        mainMenuStartY = Mathf.Min(mainMenuStartY, panelHeight * 0.30f - largefontSize * 1.1f * 0.5f - menuBtnHalfH - menuGap);
        mainMenuStartY = Mathf.Min(mainMenuStartY, frameContentHalfY - menuBtnHalfH - menuGap);
        mainMenuPitch = Mathf.Min(mainMenuPitch, (mainMenuStartY + frameContentHalfY - menuBtnHalfH - menuGap) / 5f);
        CreateButton("NewGameButton", _mainMenuPanel.transform, Localization.T("Trò Mới"), new Vector2(0f, mainMenuStartY), () => MainMenuController.Instance?.OnNewGameClicked(), mainMenuButtonSize);
        CreateButton("LoadGameButton", _mainMenuPanel.transform, Localization.T("Tiếp Tục (Tải)"), new Vector2(0f, mainMenuStartY - mainMenuPitch), () => ShowSaveSlotMenu(true), mainMenuButtonSize);
        CreateButton("QuitButton", _mainMenuPanel.transform, Localization.T("Thoát"), new Vector2(0f, mainMenuStartY - mainMenuPitch * 2f), () => MainMenuController.Instance?.OnQuitClicked(), mainMenuButtonSize);
        CreateButton("ControlsButton", _mainMenuPanel.transform, Localization.T("Cài Đặt"), new Vector2(0f, mainMenuStartY - mainMenuPitch * 3f), () => ShowSettingsPanel(true), mainMenuButtonSize);
        CreateButton("EndingTreeMenuButton", _mainMenuPanel.transform, Localization.T("Cây Kết Thúc"), new Vector2(0f, mainMenuStartY - mainMenuPitch * 4f), () => ShowEndingTreePanel(true), mainMenuButtonSize);
        _mainMenuPanel.SetActive(false);

        CreatePlatformPanel(panelWidth, panelHeight, padding, fontSize, largefontSize);

        CreateGenderSelectionPanel(panelWidth, panelHeight, fontSize, largefontSize);

        ShowAllGameUI(true);

        // Re-apply main menu visibility in case GameManager.Start() ran before slots were created
        if (GameManager.Instance != null && !GameManager.Instance.InGame)
            ShowMainMenuOnly(true);

        ResizeStatsBg();

        LogUiPipeline();
    }

    public void ApplyDefaultFont(TMP_Text target)
    {
        if (target == null) return;
        if (defaultTmpFont == null)
            defaultTmpFont = Resources.Load<TMP_FontAsset>("VietPixel");
        if (defaultTmpFont != null)
            target.font = defaultTmpFont;
    }

    private Canvas CreateCanvas()
    {
        var canvasObject = new GameObject("HUD_Canvas");
        canvasObject.layer = LayerMask.NameToLayer("UI");
        var canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 1000;
        var hudScaler = canvasObject.AddComponent<CanvasScaler>();
        hudScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        hudScaler.referenceResolution = new Vector2(1920, 1080);
        hudScaler.matchWidthOrHeight = 0.5f;
        canvasObject.AddComponent<GraphicRaycaster>();
        canvasObject.SetActive(true);
        return canvas;
    }

    private void CreateMessageCanvas(float screenWidth, float screenHeight, float lineHeight, float largefontSize)
    {
        if (_messageCanvas != null && _messageText != null) return;

        var canvasGO = new GameObject("MessageCanvas");
        canvasGO.layer = LayerMask.NameToLayer("UI");
        _messageCanvas = canvasGO.AddComponent<Canvas>();
        _messageCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _messageCanvas.sortingOrder = 1100;
        var scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        canvasGO.SetActive(true);

        var textGO = new GameObject("MessageText");
        textGO.transform.SetParent(canvasGO.transform, false);
        var rect = textGO.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 1f);
        rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = new Vector2(0f, -screenHeight * 0.08f);
        rect.sizeDelta = new Vector2(screenWidth * 0.7f, lineHeight * 3.5f);

        _messageText = textGO.AddComponent<TextMeshProUGUI>();
        if (defaultTmpFont != null)
            _messageText.font = defaultTmpFont;
        _messageText.text = "";
        _messageText.fontSize = (int)(largefontSize * 1.8f);
        _messageText.color = Color.white;
        _messageText.alignment = TextAlignmentOptions.Center;
        _messageText.textWrappingMode = TextWrappingModes.Normal;
        _messageText.overflowMode = TextOverflowModes.Overflow;
        _messageText.raycastTarget = false;
        _messageText.margin = new Vector4(20f, 10f, 20f, 10f);
        _messageText.gameObject.SetActive(false);

        var bgGo = new GameObject("MessageBg", typeof(RectTransform), typeof(Image));
        bgGo.transform.SetParent(canvasGO.transform, false);
        bgGo.transform.SetAsFirstSibling();
        var bgRect = bgGo.GetComponent<RectTransform>();
        bgRect.anchorMin = rect.anchorMin;
        bgRect.anchorMax = rect.anchorMax;
        bgRect.pivot = rect.pivot;
        bgRect.anchoredPosition = rect.anchoredPosition;
        bgRect.sizeDelta = new Vector2(rect.sizeDelta.x + 40f, rect.sizeDelta.y + 20f);
        _messageBg = bgGo.GetComponent<Image>();
        _messageBg.color = new Color(0f, 0f, 0f, 0.75f);
        _messageBg.raycastTarget = false;
        _messageBg.enabled = false;
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

        var uiModule = eventSystem.GetComponent<InputSystemUIInputModule>();
        if (uiModule == null)
        {
            uiModule = eventSystem.gameObject.AddComponent<InputSystemUIInputModule>();
        }

        if (uiModule != null && (uiModule.point == null || uiModule.leftClick == null))
        {
            uiModule.AssignDefaultActions();
        }

        if (eventSystem != null && !eventSystem.enabled)
            eventSystem.enabled = true;

        if (eventSystem != null && uiModule != null && !uiModule.enabled)
            uiModule.enabled = true;
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
        var targetParent = parent != null ? parent : (_canvas != null ? _canvas.transform : null);
        if (targetParent != null)
        {
            var t = targetParent.Find(name);
            if (t != null)
            {
                var tmp = t.GetComponent<TMP_Text>();
                if (tmp != null) return tmp;
            }
        }
        var existing = GameObject.Find(name);
        if (existing != null)
        {
            var existingText = existing.GetComponent<TMP_Text>();
            if (existingText != null)
                return existingText;
        }

        var go = new GameObject(name);
        go.transform.SetParent(targetParent ?? transform, false);

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
        textComponent.raycastTarget = false;

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
        img.raycastTarget = false;
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

    private GameObject CreateFullScreenPanel(string name)
    {
        var panelObject = GameObject.Find(name);
        if (panelObject != null)
            return panelObject;

        panelObject = new GameObject(name);
        panelObject.transform.SetParent(_canvas.transform, false);

        var rect = panelObject.AddComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        var image = panelObject.AddComponent<Image>();
        image.color = Color.black;
        return panelObject;
    }

    private GameObject CreateFullPanelChild(string name, Transform parent)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rect = go.AddComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        return go;
    }

    private Button CreateButton(string name, Transform parent, string label, Vector2 position, UnityEngine.Events.UnityAction callback, Vector2? size = null)
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
            rect.sizeDelta = size ?? new Vector2(buttonWidth, buttonHeight);

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

    private float lineHeight()
    {
        return Screen.height * 0.05f;
    }

    public void ShowRecordPanel(bool show)
    {
        if (_recordPanel != null)
            _recordPanel.SetActive(show);
        if (show)
        {
            _pauseMenuPanel?.SetActive(false);
            if (_recordLinesText == null)
            {
                var recordLinesGo = GameObject.Find("RecordLines");
                _recordLinesText = recordLinesGo != null ? recordLinesGo.GetComponent<TMP_Text>() : null;
            }
            if (_recordLinesText != null)
                _recordLinesText.text = BuildRecordLines(GameStats.WheatHarvested, GameStats.EnemiesDefeated, GameStats.MoneyEarned, GameStats.MoneyStolen);
        }
        if (!show && GameManager.Instance != null && GameManager.Instance.GamePaused)
            ShowPauseMenu(true);
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
    private void OpenSettingsFromStatsTab()
    {
        if (!GameInput.IsMobile) return;
        if (GameManager.Instance != null && GameManager.Instance.GamePaused) return;
        GameManager.Instance?.TogglePause(true);
        ShowSettingsPanel(true);
    }

    public void ShowSettingsPanel(bool show)
    {
        if (_settingsPanel != null)
            _settingsPanel.SetActive(show);
        if (show)
        {
            _pauseMenuPanel?.SetActive(false);
            _mainMenuPanel?.SetActive(false);
            UpdateSettingsValues();
        }
        else
        {
            if (GameManager.Instance != null && GameManager.Instance.GamePaused)
                ShowPauseMenu(true);
            else if (GameManager.Instance != null && !GameManager.Instance.InGame)
                ShowMainMenu(true);
        }
    }

    private void UpdateSettingsValues()
    {
        if (_mouseSensText != null)
            _mouseSensText.text = SettingsManager.MouseSensitivity.ToString("0.00");
        if (_touchSensText != null)
            _touchSensText.text = SettingsManager.TouchSensitivity.ToString("0.00");
        if (_invertYButton != null)
        {
            var label = _invertYButton.GetComponentInChildren<TMP_Text>();
            if (label != null)
                label.text = SettingsManager.InvertY ? Localization.T("Đảo Trục Dọc: BẬT") : Localization.T("Đảo Trục Dọc: TẮT");
        }
        if (_languageButton != null)
        {
            var label = _languageButton.GetComponentInChildren<TMP_Text>();
            if (label != null)
                label.text = Localization.T("Ngôn Ngữ") + ": " + (Localization.Current == Language.Vietnamese ? "Tiếng Việt" : "English");
        }
        SetModeButtonHighlight(_settingsPcModeButton, GameInput.Mode == ControlMode.PC);
        SetModeButtonHighlight(_settingsMobileModeButton, GameInput.Mode == ControlMode.Mobile);
    }

    public void ShowEndScreen(string title, string content)
    {
        if (_endPanel == null)
        {
            _endPanel = CreateMenuPanel("EndPanel", Vector2.zero, new Vector2(680f, 520f));
            EnsureText("EndTitle", new Vector2(0f, 170f), title, 32, _endPanel.transform, TextAlignmentOptions.Center, true, new Vector2(640f, 40f));
            EnsureText("EndContent", new Vector2(0f, 60f), content, 20, _endPanel.transform, TextAlignmentOptions.Center, true, new Vector2(640f, 120f));
            CreateButton("EndRestartButton", _endPanel.transform, Localization.T("Chơi Lại"), new Vector2(-110f, -180f), () => GameManager.Instance?.StartNewGame());
            CreateButton("EndQuitButton", _endPanel.transform, Localization.T("Thoát"), new Vector2(110f, -180f), () => Application.Quit());
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
        if (_bossEndPanel != null) _bossEndPanel.SetActive(false);
    }

    public void ShowBossEndScreen(string title, string content)
    {
        if (_bossEndPanel == null)
        {
            _bossEndPanel = CreateMenuPanel("BossEndPanel", Vector2.zero, new Vector2(680f, 520f));
            EnsureText("BossEndTitle", new Vector2(0f, 170f), title, 32, _bossEndPanel.transform, TextAlignmentOptions.Center, true, new Vector2(640f, 40f));
            EnsureText("BossEndContent", new Vector2(0f, 60f), content, 20, _bossEndPanel.transform, TextAlignmentOptions.Center, true, new Vector2(640f, 120f));
            CreateButton("BossEndLoadButton", _bossEndPanel.transform, Localization.T("Tải Save Gần Nhất"), new Vector2(-110f, -180f), () => GameManager.Instance?.ReloadFromBossDeath());
            CreateButton("BossEndQuitButton", _bossEndPanel.transform, Localization.T("Thoát"), new Vector2(110f, -180f), () => Application.Quit());
        }
        var titleTf = _bossEndPanel.transform.Find("BossEndTitle");
        if (titleTf != null) { var t = titleTf.GetComponent<TMP_Text>(); if (t != null) t.text = title; }
        var contentTf = _bossEndPanel.transform.Find("BossEndContent");
        if (contentTf != null) { var t = contentTf.GetComponent<TMP_Text>(); if (t != null) t.text = content; }
        HideBossBar();
        _bossEndPanel.SetActive(true);
    }

    private void RefreshLocalizedText()
    {
        SetText("RecordTitle", "THỐNG KÊ");
        SetText("SettingsTitle", "CÀI ĐẶT");
        SetText("MouseSensCaption", "ĐỘ NHẠY CHUỘT");
        SetText("TouchSensCaption", "ĐỘ NHẠY CẢM ỨNG");
        SetText("ControlModeCaption", "CÁCH ĐIỀU KHIỂN");
        SetText("PlatformTitle", "CÁCH ĐIỀU KHIỂN");
        SetText("PlatformHint", "Chọn thiết bị bạn sẽ chơi");
        SetText("QuestTitle", "NHIỆM VỤ");
        SetText("TitleText", "XÂY DỰNG NÔNG TRẠI");
        SetText("EndingTreeTitle", "CÂY KẾT THÚC");

        SetButtonText("ContinueButton", "Tiếp Tục");
        SetButtonText("SaveButton", "Lưu Game");
        SetButtonText("LoadButton", "Tải Game");
        SetButtonText("StatsButton", "Thống Kê");
        SetButtonText("QuestsButton", "Nhiệm Vụ");
        SetButtonText("SettingsButton", "Cài Đặt");
        SetButtonText("TutorialButton", "Hướng Dẫn");
        SetButtonText("ExitButton", "Thoát");
        SetButtonText("RecordBackButton", "Quay Lại");
        SetButtonText("SettingsCloseButton", "Đóng");
        SetButtonText("SettingsPCModeButton", "PC / Bàn Phím");
        SetButtonText("SettingsMobileModeButton", "Điện Thoại / Cảm Ứng");
        SetButtonText("QuestCloseButton", "Đóng");
        SetButtonText("EndingTreeTabButton", "Cây Kết Thúc");
        if (_endingTreeSettingsButton != null)
        {
            var settingsLabel = _endingTreeSettingsButton.GetComponentInChildren<TMP_Text>();
            if (settingsLabel != null)
                settingsLabel.text = Localization.T("Cài Đặt");
        }
        if (_endingTreePanel != null)
        {
            var closeTransform = _endingTreePanel.transform.Find("EndingTreeExitButton");
            if (closeTransform != null)
            {
                var closeLabel = closeTransform.GetComponentInChildren<TMP_Text>();
                if (closeLabel != null)
                    closeLabel.text = Localization.T("Đóng");
            }
        }
        if (_endingRowTexts != null)
        {
            for (int i = 0; i < Endings.Length && i < _endingRowTexts.Length; i++)
            {
                if (_endingRowTexts[i] != null)
                    _endingRowTexts[i].text = Localization.T(Endings[i].TitleKey);
            }
        }
        if (_endingTreeTitleText != null)
            _endingTreeTitleText.text = Localization.T("CÂY KẾT THÚC");
        RefreshEndingTree();
        SetButtonText("EndingQuestTabCloseButton", "Đóng");
        if (_endingQuestTabUi != null)
            RefreshEndingQuestTab(_endingQuestTabUi);
        SetButtonText("NewGameButton", "Trò Mới");
        SetButtonText("LoadGameButton", "Tiếp Tục (Tải)");
        SetButtonText("QuitButton", "Thoát");
        SetButtonText("ControlsButton", "Cài Đặt");
        SetButtonText("EndingTreeMenuButton", "Cây Kết Thúc");
        SetButtonText("PCModeButton", "PC / Bàn Phím");
        SetButtonText("MobileModeButton", "Điện Thoại / Cảm Ứng");
        SetButtonText("PlatformCloseButton", "Đóng");
        SetButtonText("EndRestartButton", "Chơi Lại");
        SetButtonText("EndQuitButton", "Thoát");
        SetButtonText("SaveSlotBackButton", "Quay Lại");
        if (_saveSlotTitleText != null)
            _saveSlotTitleText.text = Localization.T(_saveSlotLoadMode ? "Tải Game" : "Lưu Game");
        RefreshSaveSlots();
        if (_genderPanel != null)
        {
            SetText("GenderTitleText", "Chọn Giới Tính");
            SetText("GenderNoteText", "Chỉ là ngoại hình, không ảnh hưởng trò chơi.");
            SetButtonText("GenderMaleButton", "Nam");
            SetButtonText("GenderFemaleButton", "Nữ");
            SetButtonText("GenderBackButton", "Quay Lại");
        }

        UpdateSettingsValues();

        if (QuestManager.Instance != null)
            QuestManager.Instance.RefreshQuestUI();
        if (ToolManager.Instance != null)
            ToolManager.Instance.RefreshInventoryUI();

        MapBuilder.RefreshWorldSignTexts();
        if (WorldBuilder.Instance != null)
            WorldBuilder.Instance.RefreshBlueprintLabels();
    }

    private void SetText(string name, string vn)
    {
        var tf = GameObject.Find(name);
        if (tf == null) return;
        var t = tf.GetComponent<TMP_Text>();
        if (t != null) t.text = Localization.T(vn);
    }

    private void SetButtonText(string name, string vn)
    {
        var tf = GameObject.Find(name);
        if (tf == null) return;
        var t = tf.GetComponentInChildren<TMP_Text>();
        if (t != null) t.text = Localization.T(vn);
    }

    public Canvas GetCanvas()
    {
        return _canvas;
    }

    public TMP_Text GetEKeyPromptText() { return _eKeyPromptText; }
    public TMP_Text GetLmbPromptText() { return _lmbPromptText; }

    public void ShowMessage(string text, float duration)
    {
        if (_messageText == null || _messageCanvas == null)
        {
            Debug.LogWarning("[UI] ShowMessage: _messageText or _messageCanvas is null!");
            return;
        }
        if (!_messageCanvas.gameObject.activeSelf)
            _messageCanvas.gameObject.SetActive(true);
        _messageText.gameObject.SetActive(true);
        _messageText.enabled = true;
        if (defaultTmpFont != null && _messageText.font != defaultTmpFont)
            _messageText.font = defaultTmpFont;
        _messageText.color = Color.white;
        _messageText.alpha = 1f;
        _messageText.SetAllDirty();
        _messageText.ForceMeshUpdate();
        Canvas.ForceUpdateCanvases();
        if (_typewriterCoroutine != null)
            StopCoroutine(_typewriterCoroutine);
        _typewriterCoroutine = StartCoroutine(TypewriterMessage(text, duration));
    }

    private IEnumerator TypewriterMessage(string fullText, float duration)
    {
        _messageText.gameObject.SetActive(true);
        _messageText.enabled = true;
        _messageText.alpha = 1f;
        _messageText.color = Color.white;
        _messageText.transform.SetAsLastSibling();
        if (_messageBg != null)
        {
            _messageBg.enabled = true;
            _messageBg.transform.SetAsLastSibling();
        }
        _messageText.transform.SetAsLastSibling();
        _messageText.text = "";
        _messageText.ForceMeshUpdate();
        yield return null;
        _messageText.transform.SetAsLastSibling();

        var wait = new WaitForSeconds(0.02f);
        for (int i = 0; i <= fullText.Length; i++)
        {
            _messageText.text = fullText.Substring(0, i);
            _messageText.SetVerticesDirty();
            UpdateMessageBgSize();
            yield return wait;
        }
        _messageText.ForceMeshUpdate();

        yield return new WaitForSeconds(duration);

        _messageText.text = string.Empty;
        if (_messageBg != null) _messageBg.enabled = false;
        _typewriterCoroutine = null;
    }

    private void UpdateMessageBgSize()
    {
        if (_messageBg == null || _messageText == null) return;
        var textRect = _messageText.rectTransform;
        float w = textRect.rect.width + 40f;
        float h = textRect.rect.height + 20f;
        var bgRect = _messageBg.rectTransform;
        bgRect.anchorMin = textRect.anchorMin;
        bgRect.anchorMax = textRect.anchorMax;
        bgRect.pivot = textRect.pivot;
        bgRect.anchoredPosition = textRect.anchoredPosition;
        bgRect.sizeDelta = new Vector2(w, h);
    }

}

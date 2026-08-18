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

public class UIManager : MonoBehaviour
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
    private TMP_Text _mobSpawnerText;
    private TMP_Text _crosshairText;
    private TMP_Text _infoText;

    public TMP_FontAsset defaultTmpFont;

    private int _lastScreenWidth;
    private int _lastScreenHeight;
    private bool _uiPipelineLogged;

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
        float slotW = sw * 0.08f;
        float slotH = Screen.height * 0.06f;
        float totalW = InventorySlotCount * slotW + (InventorySlotCount - 1) * 4f;
        float startX = -totalW * 0.5f;
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
        float menuButtonWidth = Mathf.Max(160f, screenHeight * 0.25f);
        float panelWidth = Mathf.Min(screenWidth * 0.4f, 560f);
        float panelHeight = Mathf.Min(screenHeight * 0.8f, 520f);
        // Stats background panel (behind Time, HP, Stamina, Money, Quest)
        _statsScale = Mathf.Clamp(Screen.width / 1400f, 0.8f, 2f);
        float sW = 430f * _statsScale;
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
            new Vector2(420f * _statsScale, 30f * _statsScale),
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
            new Vector2(420f * _statsScale, 30f * _statsScale),
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
            new Vector2(420f * _statsScale, 30f * _statsScale),
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
            new Vector2(420f * _statsScale, 30f * _statsScale),
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
            new Vector2(410f * _statsScale, 190f * _statsScale),
            new Vector2(0f, 1f),
            new Vector2(0f, 1f),
            new Vector2(0f, 1f)
        );

        // Inventory: 10 individual slots at bottom center
        float slotW = screenWidth * 0.08f;
        float slotH = screenHeight * 0.06f;
        float totalW = InventorySlotCount * slotW + (InventorySlotCount - 1) * 4f;
        float startX = -totalW * 0.5f;

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
        CreateButton("ContinueButton", _pauseMenuPanel.transform, Localization.T("Tiếp Tục"), new Vector2(0f, panelHeight * 0.33f), () => GameManager.Instance?.TogglePause(false));
        CreateButton("SaveButton", _pauseMenuPanel.transform, Localization.T("Lưu Game"), new Vector2(0f, panelHeight * 0.22f), () => ShowSaveSlotMenu(false));
        CreateButton("LoadButton", _pauseMenuPanel.transform, Localization.T("Tải Game"), new Vector2(0f, panelHeight * 0.11f), () => ShowSaveSlotMenu(true));
        CreateButton("StatsButton", _pauseMenuPanel.transform, Localization.T("Thống Kê"), new Vector2(0f, 0f), () => ShowRecordPanel(true));
        CreateButton("QuestsButton", _pauseMenuPanel.transform, Localization.T("Nhiệm Vụ"), new Vector2(0f, -panelHeight * 0.11f), () => ShowQuestPanel(true));
        CreateButton("SettingsButton", _pauseMenuPanel.transform, Localization.T("Cài Đặt"), new Vector2(0f, -panelHeight * 0.22f), () => ShowSettingsPanel(true));
        CreateButton("TutorialButton", _pauseMenuPanel.transform, Localization.T("Hướng Dẫn"), new Vector2(0f, -panelHeight * 0.33f), () => ShowTutorial(true));
        CreateButton("ExitButton", _pauseMenuPanel.transform, Localization.T("Thoát"), new Vector2(0f, -panelHeight * 0.44f), () => Application.Quit());
        _pauseMenuPanel.SetActive(false);

        CreateSaveSlotMenu(panelWidth, padding, largefontSize);

        _recordPanel = CreateMenuPanel("RecordPanel", Vector2.zero, new Vector2(panelWidth, panelHeight));
        EnsureText("RecordTitle", new Vector2(0f, panelHeight * 0.35f), Localization.T("THỐNG KÊ"), (int)largefontSize, _recordPanel.transform, TextAlignmentOptions.Center, true, new Vector2(panelWidth - padding * 4, lineHeight));
        EnsureText("RecordLines", new Vector2(0f, panelHeight * 0.1f), BuildRecordLines(0, 0, 0, 0), (int)fontSize, _recordPanel.transform, TextAlignmentOptions.Left, true, new Vector2(panelWidth - padding * 4, panelHeight * 0.4f));
        CreateButton("RecordBackButton", _recordPanel.transform, Localization.T("Quay Lại"), new Vector2(0f, -panelHeight * 0.35f), () => ShowRecordPanel(false));
        _recordPanel.SetActive(false);

        float settingsHeight = Mathf.Min(screenHeight * 0.85f, 620f);
        float settingsWidth = Mathf.Min(screenWidth * 0.5f, 680f);
        float sy = settingsHeight * 0.78f;
        var settingsButtonSize = new Vector2(Mathf.Min(menuButtonWidth * 0.9f, settingsWidth * 0.34f), screenHeight * 0.033f);
        float settingsModeBtnX = settingsWidth * 0.35f - settingsButtonSize.x * 0.5f - 2f;
        _settingsPanel = CreateMenuPanel("SettingsPanel", Vector2.zero, new Vector2(settingsWidth, settingsHeight));
        EnsureText("SettingsTitle", new Vector2(0f, sy * 0.34f), Localization.T("CÀI ĐẶT"), (int)largefontSize, _settingsPanel.transform, TextAlignmentOptions.Center, true, new Vector2(settingsWidth - padding * 4, lineHeight));
        EnsureText("MouseSensCaption", new Vector2(0f, sy * 0.29f), Localization.T("ĐỘ NHẠY CHUỘT"), (int)fontSize, _settingsPanel.transform, TextAlignmentOptions.Center, true, new Vector2(settingsWidth - padding * 4, lineHeight));
        CreateButton("MouseSensMinus", _settingsPanel.transform, "<", new Vector2(-menuButtonWidth * 0.24f, sy * 0.215f), () => { SettingsManager.SetMouseSensitivity(SettingsManager.MouseSensitivity - 0.25f); UpdateSettingsValues(); }, new Vector2(menuButtonWidth * 0.3f, screenHeight * 0.04f));
        CreateButton("MouseSensPlus", _settingsPanel.transform, ">", new Vector2(menuButtonWidth * 0.24f, sy * 0.215f), () => { SettingsManager.SetMouseSensitivity(SettingsManager.MouseSensitivity + 0.25f); UpdateSettingsValues(); }, new Vector2(menuButtonWidth * 0.3f, screenHeight * 0.04f));
        _mouseSensText = EnsureText("MouseSensValue", new Vector2(0f, sy * 0.215f), SettingsManager.MouseSensitivity.ToString("0.00"), (int)fontSize, _settingsPanel.transform, TextAlignmentOptions.Center, true, new Vector2(menuButtonWidth * 0.4f, lineHeight * 1.5f));

        EnsureText("TouchSensCaption", new Vector2(0f, sy * 0.14f), Localization.T("ĐỘ NHẠY CẢM ỨNG"), (int)fontSize, _settingsPanel.transform, TextAlignmentOptions.Center, true, new Vector2(settingsWidth - padding * 4, lineHeight));
        CreateButton("TouchSensMinus", _settingsPanel.transform, "<", new Vector2(-menuButtonWidth * 0.24f, sy * 0.065f), () => { SettingsManager.SetTouchSensitivity(SettingsManager.TouchSensitivity - 0.03f); UpdateSettingsValues(); }, new Vector2(menuButtonWidth * 0.3f, screenHeight * 0.04f));
        CreateButton("TouchSensPlus", _settingsPanel.transform, ">", new Vector2(menuButtonWidth * 0.24f, sy * 0.065f), () => { SettingsManager.SetTouchSensitivity(SettingsManager.TouchSensitivity + 0.03f); UpdateSettingsValues(); }, new Vector2(menuButtonWidth * 0.3f, screenHeight * 0.04f));
        _touchSensText = EnsureText("TouchSensValue", new Vector2(0f, sy * 0.065f), SettingsManager.TouchSensitivity.ToString("0.00"), (int)fontSize, _settingsPanel.transform, TextAlignmentOptions.Center, true, new Vector2(menuButtonWidth * 0.4f, lineHeight * 1.5f));

        _invertYButton = CreateButton("InvertYButton", _settingsPanel.transform, "", new Vector2(0f, sy * -0.013f), () => { SettingsManager.SetInvertY(!SettingsManager.InvertY); UpdateSettingsValues(); }, settingsButtonSize);

        _languageButton = CreateButton("LanguageButton", _settingsPanel.transform, "", new Vector2(0f, sy * -0.09f), () => { Localization.ToggleLanguage(); UpdateSettingsValues(); }, settingsButtonSize);

        EnsureText("ControlModeCaption", new Vector2(0f, sy * -0.168f), Localization.T("CÁCH ĐIỀU KHIỂN"), (int)fontSize, _settingsPanel.transform, TextAlignmentOptions.Center, true, new Vector2(settingsWidth - padding * 4, lineHeight));
        _settingsPcModeButton = CreateButton("SettingsPCModeButton", _settingsPanel.transform, Localization.T("PC / Bàn Phím"), new Vector2(-settingsModeBtnX, sy * -0.245f), () => SetControlMode(ControlMode.PC), settingsButtonSize);
        _settingsMobileModeButton = CreateButton("SettingsMobileModeButton", _settingsPanel.transform, Localization.T("Điện Thoại / Cảm Ứng"), new Vector2(settingsModeBtnX, sy * -0.245f), () => SetControlMode(ControlMode.Mobile), settingsButtonSize);
        CreateButton("EndingTreeTabButton", _settingsPanel.transform, Localization.T("Cây Kết Thúc"), new Vector2(0f, sy * -0.322f), () => ShowEndingTreePanel(true), settingsButtonSize);
        CreateButton("SettingsCloseButton", _settingsPanel.transform, Localization.T("Đóng"), new Vector2(0f, sy * -0.40f), () => ShowSettingsPanel(false), settingsButtonSize);

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
        mainMenuPitch = Mathf.Min(mainMenuPitch, (mainMenuStartY + frameContentHalfY - menuBtnHalfH - menuGap) / 6f);
        CreateButton("NewGameButton", _mainMenuPanel.transform, Localization.T("Trò Mới"), new Vector2(0f, mainMenuStartY), () => MainMenuController.Instance?.OnNewGameClicked(), mainMenuButtonSize);
        CreateButton("LoadGameButton", _mainMenuPanel.transform, Localization.T("Tiếp Tục (Tải)"), new Vector2(0f, mainMenuStartY - mainMenuPitch), () => ShowSaveSlotMenu(true), mainMenuButtonSize);
        CreateButton("WatchIntroButton", _mainMenuPanel.transform, Localization.T("Xem Giới Thiệu"), new Vector2(0f, mainMenuStartY - mainMenuPitch * 2f), () => MainMenuController.Instance?.OnWatchIntroClicked(), mainMenuButtonSize);
        CreateButton("SkipIntroButton", _mainMenuPanel.transform, Localization.T("Bỏ Qua Giới Thiệu"), new Vector2(0f, mainMenuStartY - mainMenuPitch * 3f), () => MainMenuController.Instance?.OnSkipIntroClicked(), mainMenuButtonSize);
        CreateButton("QuitButton", _mainMenuPanel.transform, Localization.T("Thoát"), new Vector2(0f, mainMenuStartY - mainMenuPitch * 4f), () => MainMenuController.Instance?.OnQuitClicked(), mainMenuButtonSize);
        CreateButton("ControlsButton", _mainMenuPanel.transform, Localization.T("Cài Đặt"), new Vector2(0f, mainMenuStartY - mainMenuPitch * 5f), () => ShowSettingsPanel(true), mainMenuButtonSize);
        CreateButton("EndingTreeMenuButton", _mainMenuPanel.transform, Localization.T("Cây Kết Thúc"), new Vector2(0f, mainMenuStartY - mainMenuPitch * 6f), () => ShowEndingTreePanel(true), mainMenuButtonSize);
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

    private GameObject CreateEndingTreeContent(Transform parent)
    {
        var go = GameObject.Find("EndingTreeContent");
        if (go != null)
            return go;
        go = new GameObject("EndingTreeContent");
        go.transform.SetParent(parent, false);

        var rect = go.AddComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.pivot = new Vector2(0.5f, 0.5f);

        var img = go.AddComponent<Image>();
        img.color = new Color(0f, 0f, 0f, 0f);
        img.raycastTarget = true;

        var pan = go.AddComponent<EndingTreePanZoom>();
        pan.Init(rect);
        return go;
    }

    private class EndingTreePanZoom : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler, IScrollHandler
    {
        private RectTransform _rect;
        private Vector2 _dragStartPos;
        private Vector2 _startAnchorPos;
        private bool _dragging;

        public void Init(RectTransform rect)
        {
            _rect = rect;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            _dragging = true;
            _dragStartPos = eventData.position;
            _startAnchorPos = _rect != null ? _rect.anchoredPosition : Vector2.zero;
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!_dragging || _rect == null)
                return;
            _rect.anchoredPosition = _startAnchorPos + (eventData.position - _dragStartPos);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            _dragging = false;
        }

        public void OnScroll(PointerEventData eventData)
        {
            if (_rect == null)
                return;
            float scroll = eventData.scrollDelta.y;
            if (Mathf.Approximately(scroll, 0f))
                return;
            float factor = scroll > 0f ? 1.15f : 1f / 1.15f;
            float ns = Mathf.Clamp(_rect.localScale.x * factor, UIManager.EndingTreeZoomMin, UIManager.EndingTreeZoomMax);
            _rect.localScale = Vector3.one * ns;
        }
    }

    private GameObject CreateTreeLine(string name, Transform parent, Vector2 start, Vector2 end, float thickness, Color color)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);

        var rect = go.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = (start + end) * 0.5f;
        Vector2 delta = end - start;
        rect.sizeDelta = new Vector2(Mathf.Max(delta.magnitude, thickness), thickness);
        rect.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);

        var img = go.AddComponent<Image>();
        img.color = color;
        go.transform.SetAsFirstSibling();
        return go;
    }

    private void BuildEndingTreeLayout()
    {
        _endingRowTexts = new TMP_Text[Endings.Length];
        _endingQuestUis.Clear();

        float sw = Screen.width;
        float sh = Screen.height;
        float k = 1.3f;
        var lineColor = new Color(1f, 1f, 1f, 0.55f);
        var doneLine = new Color(0.35f, 0.9f, 0.45f, 0.8f);
        var silenceLine = new Color(0.95f, 0.4f, 0.4f, 0.8f);
        var treeParent = _endingTreeContent != null ? _endingTreeContent.transform : _endingTreePanel.transform;

        // Clear any tree nodes generated by a previous build (InitializeUI can run more than
        // once), so boxes/lines no longer created by the current quest config don't linger.
        for (int i = treeParent.childCount - 1; i >= 0; i--)
            Object.DestroyImmediate(treeParent.GetChild(i).gameObject);

        // Ending nodes: one horizontal row (ordered to keep parent/child clusters close).
        Vector2[] nodePos = new Vector2[Endings.Length];
        var nodeSize = new Vector2(Mathf.Min(sw * 0.098f, 192f) * k, Mathf.Max(44f, sh * 0.055f) * k);
        int[] rowOrder = { 3, 7, 0, 1, 5, 2, 4, 6 };
        float eGap = Mathf.Min(sw * 0.012f, 18f) * k;
        float totalRowW = nodeSize.x * rowOrder.Length + eGap * (rowOrder.Length - 1);
        for (int r = 0; r < rowOrder.Length; r++)
            nodePos[rowOrder[r]] = new Vector2(-totalRowW * 0.5f + r * (nodeSize.x + eGap) + nodeSize.x * 0.5f, 0f);

        for (int i = 0; i < Endings.Length; i++)
        {
            int index = i;
            var btn = CreateButton("EndingRow" + i, treeParent, Localization.T(Endings[i].TitleKey),
                nodePos[i], () => ShowEndingDetail(true, index), nodeSize);
            var btnText = btn.GetComponentInChildren<TMP_Text>();
            if (btnText != null)
                btnText.fontSize = Mathf.Max(11, (int)(sh * 0.013f * k));
            _endingRowTexts[i] = btnText;
        }

        // Parent/child ending branches (dim connectors under the row)
        for (int i = 0; i < Endings.Length; i++)
        {
            int parent = -1;
            if (i == 0 || i == 1 || i == 4) parent = 3;
            else if (i == 5) parent = 0;
            else if (i == 2) parent = 1;
            else if (i == 6) parent = 4;
            else if (i == 7) parent = 3;
            if (parent < 0) continue;
            Vector2 from = new Vector2(nodePos[parent].x, -nodeSize.y * 0.5f - 22f * k);
            Vector2 to = new Vector2(nodePos[i].x, -nodeSize.y * 0.5f - 22f * k);
            CreateTreeLine("EndingBranch_" + parent + "_" + i, treeParent, from, to, 2.5f, lineColor);
        }

        // Aggregate unique quests across endings (one box per quest)
        var questUis = new List<EndingQuestUi>();
        for (int i = 0; i < Endings.Length && i < EndingQuestDefs.Length; i++)
        {
            var quests = EndingQuestDefs[i];
            if (quests == null) continue;
            for (int j = 0; j < quests.Length; j++)
            {
                var def = quests[j];
                EndingQuestUi ui = null;
                for (int u = 0; u < questUis.Count; u++)
                {
                    if (questUis[u].Def.QuestName == def.QuestName)
                    {
                        ui = questUis[u];
                        break;
                    }
                }
                if (ui == null)
                {
                    ui = new EndingQuestUi { Def = def };
                    questUis.Add(ui);
                }
                if (!ui.Targets.Exists(t => t.Key == i))
                    ui.Targets.Add(new KeyValuePair<int, bool>(i, def.MustComplete));
            }
        }

        // Sort quest boxes: mansion (fewest targets) left, mansion_secret (most) right
        questUis.Sort((a, b) =>
        {
            int aCount = a.Targets.Count;
            int bCount = b.Targets.Count;
            if (aCount != bCount) return aCount.CompareTo(bCount);
            return string.Compare(a.Def.QuestName, b.Def.QuestName, System.StringComparison.Ordinal);
        });

        // Quest row at the top (one box per quest)
        var questSize = new Vector2(Mathf.Min(sw * 0.115f, 195f) * k, Mathf.Max(34f, sh * 0.05f) * k);
        float qGap = Mathf.Min(sw * 0.02f, 28f) * k;
        float questY = sh * 0.245f * k;
        int qc = questUis.Count;
        float totalW = questSize.x * qc + qGap * (qc - 1);
        bool[] markerAdded = new bool[Endings.Length];
        for (int q = 0; q < qc; q++)
        {
            var ui = questUis[q];
            float qx = -totalW * 0.5f + q * (questSize.x + qGap) + questSize.x * 0.5f;

            var boxImg = CreateTreeBox("EndingQuestRect_" + q, treeParent, new Vector2(qx, questY), questSize, new Color(0.2f, 0.2f, 0.28f, 1f));
            ui.Box = boxImg;
            var qBtn = boxImg.gameObject.GetComponent<Button>();
            if (qBtn == null)
                qBtn = boxImg.gameObject.AddComponent<Button>();
            qBtn.targetGraphic = boxImg;
            qBtn.onClick.RemoveAllListeners();
            qBtn.onClick.AddListener(() => ShowEndingQuestTab(true, ui));
            var qLabel = EnsureText("EndingQuestLabel_" + q, new Vector2(qx, questY), Localization.T(ui.Def.QuestName),
                Mathf.Max(10, (int)(sh * 0.011f * k)), treeParent, TextAlignmentOptions.Center, true, new Vector2(questSize.x * 0.95f, questSize.y * 0.8f));
            qLabel.color = Color.white;
            ui.Text = qLabel;

            Vector2 qBottom = new Vector2(qx, questY - questSize.y * 0.5f);
            for (int t = 0; t < ui.Targets.Count; t++)
            {
                int ending = ui.Targets[t].Key;
                bool mustComplete = ui.Targets[t].Value;
                Vector2 eTop = new Vector2(nodePos[ending].x, nodePos[ending].y + nodeSize.y * 0.5f);
                CreateTreeLine("EndingQuestLine_" + q + "_" + ending, treeParent, qBottom, eTop, 3f,
                    mustComplete ? doneLine : silenceLine);
                if (ending >= 0 && ending < SpecialChoiceEndings.Length && SpecialChoiceEndings[ending] && !markerAdded[ending])
                {
                    markerAdded[ending] = true;
                    var mSize = new Vector2(Mathf.Max(96f, nodeSize.x * 0.56f), Mathf.Max(20f, sh * 0.024f * k));
                    Vector2 mPos = new Vector2(eTop.x, eTop.y + mSize.y * 0.5f + 4f);
                    CreateTreeBox("SpecialChoiceBox_" + ending, treeParent, mPos, mSize, new Color(0.85f, 0.7f, 0.2f, 0.95f));
                    var mLabel = EnsureText("SpecialChoiceLabel_" + ending, mPos, Localization.T("Lựa chọn đặc biệt"),
                        Mathf.Max(9, (int)(sh * 0.010f * k)), treeParent, TextAlignmentOptions.Center, true, new Vector2(mSize.x * 0.95f, mSize.y * 0.9f));
                    mLabel.color = new Color(0.05f, 0.05f, 0.05f, 1f);
                }
            }
            _endingQuestUis.Add(ui);
        }
    }

    private Image CreateTreeBox(string name, Transform parent, Vector2 position, Vector2 size, Color color)
    {
        var go = GameObject.Find(name);
        if (go != null)
            return go.GetComponent<Image>();
        go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rect = go.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        var img = go.AddComponent<Image>();
        img.color = color;
        return img;
    }

    private void RefreshEndingTree()
    {
        if (_endingQuestUis == null)
            return;
        for (int i = 0; i < _endingQuestUis.Count; i++)
        {
            var ui = _endingQuestUis[i];
            if (ui == null || ui.Def == null || ui.Text == null)
                continue;
            bool done = false;
            if (QuestManager.Instance != null)
            {
                done = QuestManager.Instance.IsNamedQuestComplete(ui.Def.QuestName) || QuestManager.Instance.IsComplete(ui.Def.Target);
            }
            ui.Text.text = Localization.T(ui.Def.QuestName);
            if (ui.Box != null)
                ui.Box.color = done ? new Color(0.15f, 0.4f, 0.2f, 1f) : new Color(0.45f, 0.15f, 0.15f, 1f);
        }
    }

    private void ShowEndingQuestTab(bool show, EndingQuestUi ui = null)
    {
        if (_endingQuestTabPanel == null)
            return;
        if (show && ui != null)
        {
            _endingQuestTabUi = ui;
            RefreshEndingQuestTab(ui);
        }
        _endingQuestTabPanel.SetActive(show);
        if (show && _settingsPanel != null)
            _settingsPanel.SetActive(false);
    }

    private void RefreshEndingQuestTab(EndingQuestUi ui)
    {
        if (ui == null || ui.Def == null || _endingQuestTabPanel == null)
            return;

        var title = _endingQuestTabPanel.transform.Find("EndingQuestTabBox/EndingQuestTabTitle");
        if (title != null)
        {
            var t = title.GetComponent<TMP_Text>();
            if (t != null)
                t.text = Localization.T(ui.Def.QuestName);
        }

        string story = null;
        if (QuestManager.Instance != null)
            story = QuestManager.Instance.GetQuestDescription(ui.Def.QuestName);
        if (string.IsNullOrEmpty(story))
            story = Localization.T("Quỷ Vương đã thức tỉnh ở cuối con đường phía đông. Dùng Tràng Hạt tiêu diệt nó để bảo vệ làng!");

        var storyObj = _endingQuestTabPanel.transform.Find("EndingQuestTabBox/EndingQuestTabStory");
        if (storyObj != null)
        {
            var s = storyObj.GetComponent<TMP_Text>();
            if (s != null)
                s.text = story;
        }

        string target = null;
        int count = 0;
        int requiredDay = 0;
        if (QuestManager.Instance != null)
            QuestManager.Instance.TryGetQuestInfo(ui.Def.QuestName, out target, out count, out requiredDay);

        string unlockText;
        if (ui.Def.QuestName == "Trấn Áp Quỷ Vương")
            unlockText = Localization.T("Hoàn thành 'Trừ Tà Quanh Chùa' và nói chuyện với thầy ở chùa");
        else if (ui.Def.QuestName == "Xây Dựng Dinh Thự Cho Jessica")
            unlockText = Localization.T("Tỏ tình với Jessica và cô ấy đồng ý");
        else if (requiredDay > 0)
            unlockText = Localization.F("Mở khóa ngày {0}", requiredDay);
        else
            unlockText = Localization.T("Tự động mở khóa");

        string objective = GetQuestObjectiveText(target, count);
        string objectiveLine = "";
        if (!string.IsNullOrEmpty(objective))
        {
            int progress = QuestManager.Instance != null ? QuestManager.Instance.GetNamedQuestProgress(ui.Def.QuestName) : 0;
            bool money = target == "money_earned";
            string progressText = money ? progress.ToString("N0") : progress.ToString();
            string countText = money ? count.ToString("N0") : count.ToString();
            objectiveLine = "• " + objective + " (" + progressText + "/" + countText + ")";
        }

        var sb = new StringBuilder();
        sb.AppendLine(Localization.T("Điều kiện mở khóa:"));
        sb.AppendLine("• " + unlockText);
        sb.AppendLine();
        sb.AppendLine(Localization.T("Điều kiện hoàn thành:"));
        if (!string.IsNullOrEmpty(objectiveLine))
            sb.AppendLine(objectiveLine);
        sb.AppendLine();
        sb.AppendLine(Localization.T("Kết thúc liên quan:"));
        for (int t = 0; t < ui.Targets.Count; t++)
        {
            int ending = ui.Targets[t].Key;
            bool mustComplete = ui.Targets[t].Value;
            if (ending < 0 || ending >= Endings.Length)
                continue;
            string req = mustComplete
                ? "<color=#59E673>" + Localization.T("Cần hoàn thành") + "</color>"
                : "<color=#F26666>" + Localization.T("Cần im lặng") + "</color>";
            sb.AppendLine("• " + Localization.T(Endings[ending].TitleKey) + " — " + req);
        }

        var condObj = _endingQuestTabPanel.transform.Find("EndingQuestTabBox/EndingQuestTabCond");
        if (condObj != null)
        {
            var c = condObj.GetComponent<TMP_Text>();
            if (c != null)
                c.text = sb.ToString();
        }
    }

    private string GetQuestObjectiveText(string target, int count)
    {
        switch (target)
        {
            case "boss_kill": return Localization.F("Tiêu diệt Quỷ Vương {0} lần", count);
            case "mansion_secret": return Localization.F("Lấy bằng chứng bí mật của Phú Ông {0} lần", count);
            case "money_earned": return Localization.F("Kiếm {0:N0} vàng", count);
            case "mansion": return Localization.F("Xây dựng {0} phần dinh thự", count);
            default: return string.IsNullOrEmpty(target) ? "" : target + " x" + count;
        }
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

    private void CreateSaveSlotMenu(float panelWidth, float padding, float largefontSize)
    {
        float slotPanelHeight = Mathf.Min(Screen.height * 0.9f, 560f);
        _saveSlotPanel = CreateMenuPanel("SaveSlotPanel", Vector2.zero, new Vector2(panelWidth, slotPanelHeight));
        _saveSlotTitleText = EnsureText("SaveSlotTitle", new Vector2(0f, slotPanelHeight * 0.34f), Localization.T("Lưu Game"), (int)largefontSize, _saveSlotPanel.transform, TextAlignmentOptions.Center, true, new Vector2(panelWidth - padding * 4, lineHeight()));
        _saveSlotButtons = new Button[10];

        var viewportObject = new GameObject("SaveSlotViewport");
        viewportObject.transform.SetParent(_saveSlotPanel.transform, false);
        var viewportRect = viewportObject.AddComponent<RectTransform>();
        viewportRect.anchorMin = new Vector2(0.5f, 0.5f);
        viewportRect.anchorMax = new Vector2(0.5f, 0.5f);
        viewportRect.pivot = new Vector2(0.5f, 0.5f);
        viewportRect.anchoredPosition = new Vector2(0f, -slotPanelHeight * 0.02f);
        viewportRect.sizeDelta = new Vector2(panelWidth - padding * 4, slotPanelHeight * 0.56f);
        viewportObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0f);
        viewportObject.AddComponent<RectMask2D>();

        var contentObject = new GameObject("SaveSlotContent");
        contentObject.transform.SetParent(viewportObject.transform, false);
        var contentRect = contentObject.AddComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0.5f, 1f);
        contentRect.anchorMax = new Vector2(0.5f, 1f);
        contentRect.pivot = new Vector2(0.5f, 1f);
        contentRect.anchoredPosition = Vector2.zero;
        contentRect.sizeDelta = new Vector2(panelWidth - padding * 4, 0f);
        var layout = contentObject.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 6f;
        layout.padding = new RectOffset(4, 4, 4, 4);
        layout.childControlWidth = true;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        contentObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        var scrollRect = viewportObject.AddComponent<ScrollRect>();
        scrollRect.viewport = viewportRect;
        scrollRect.content = contentRect;
        scrollRect.horizontal = false;
        scrollRect.vertical = true;
        scrollRect.movementType = ScrollRect.MovementType.Clamped;
        scrollRect.scrollSensitivity = 30f;

        float slotWidth = panelWidth - padding * 4 - 8f;
        float slotHeight = Mathf.Max(36f, Screen.height * 0.045f);
        for (int i = 0; i < _saveSlotButtons.Length; i++)
        {
            int index = i;
            _saveSlotButtons[i] = CreateButton("SaveSlotButton" + i.ToString(), contentObject.transform, "", new Vector2(0f, 0f), () => OnSaveSlotClicked(index), new Vector2(slotWidth, slotHeight));
        }

        CreateButton("SaveSlotBackButton", _saveSlotPanel.transform, Localization.T("Quay Lại"), new Vector2(0f, -slotPanelHeight * 0.38f), () => CloseSaveSlotMenu());
        _saveSlotPanel.SetActive(false);
    }

    private float lineHeight()
    {
        return Screen.height * 0.05f;
    }

    private void CreateGenderSelectionPanel(float panelWidth, float panelHeight, float fontSize, float largefontSize)
    {
        float gpW = 520f;
        float gpH = 360f;
        _genderPanel = CreateMenuPanel("GenderSelectionPanel", Vector2.zero, new Vector2(gpW, gpH));
        if (_genderPanel == null)
            return;

        EnsureText("GenderTitleText", new Vector2(0f, gpH * 0.3f), Localization.T("Chọn Giới Tính"), (int)(largefontSize * 0.95f), _genderPanel.transform, TextAlignmentOptions.Center, true, new Vector2(gpW - 60f, lineHeight() * 1.3f));
        EnsureText("GenderNoteText", new Vector2(0f, gpH * 0.16f), Localization.T("Chỉ là ngoại hình, không ảnh hưởng trò chơi."), Mathf.Max(14, (int)(fontSize * 0.7f)), _genderPanel.transform, TextAlignmentOptions.Center, true, new Vector2(gpW - 80f, lineHeight() * 1.1f));

        float pitch = 70f;
        CreateButton("GenderMaleButton", _genderPanel.transform, Localization.T("Nam"), new Vector2(-gpW * 0.16f, -40f), () => SelectGender(PlayerGender.Male), new Vector2(gpW * 0.28f, 56f));
        CreateButton("GenderFemaleButton", _genderPanel.transform, Localization.T("Nữ"), new Vector2(gpW * 0.16f, -40f), () => SelectGender(PlayerGender.Female), new Vector2(gpW * 0.28f, 56f));
        CreateButton("GenderBackButton", _genderPanel.transform, Localization.T("Quay Lại"), new Vector2(0f, -40f - pitch), () => CloseGenderSelectionMenu(), new Vector2(gpW * 0.4f, 50f));

        _genderPanel.SetActive(false);
    }

    public void ShowGenderSelectionMenu(GenderMenuMode mode)
    {
        if (_genderPanel == null)
            return;
        _genderMenuMode = mode;
        SetText("GenderTitleText", "Chọn Giới Tính");
        SetText("GenderNoteText", "Chỉ là ngoại hình, không ảnh hưởng trò chơi.");
        SetButtonText("GenderMaleButton", "Nam");
        SetButtonText("GenderFemaleButton", "Nữ");
        SetButtonText("GenderBackButton", "Quay Lại");
        _genderPanel.SetActive(true);
        _pauseMenuPanel?.SetActive(false);
        _mainMenuPanel?.SetActive(false);
        if (_settingsPanel != null)
            _settingsPanel.SetActive(false);
        if (_recordPanel != null)
            _recordPanel.SetActive(false);
        if (_questPanel != null)
            _questPanel.SetActive(false);
        if (_saveSlotPanel != null)
            _saveSlotPanel.SetActive(false);
    }

    private void CloseGenderSelectionMenu()
    {
        if (_genderPanel != null)
            _genderPanel.SetActive(false);
        if (GameManager.Instance == null)
            return;
        if (GameManager.Instance.InGame)
        {
            if (GameManager.Instance.GamePaused)
                ShowPauseMenu(true);
        }
        else
        {
            ShowMainMenu(true);
        }
    }

    private void SelectGender(PlayerGender gender)
    {
        MapBuilder.ActiveGender = gender;
        GameManager.Instance?.Player?.ApplyGender();
        if (_genderPanel != null)
            _genderPanel.SetActive(false);
        if (GameManager.Instance == null)
            return;
        if (_genderMenuMode == GenderMenuMode.SkipIntro)
            GameManager.Instance.StartNewGameSkipIntro();
        else
            GameManager.Instance.StartNewGame();
    }

    public void ShowSaveSlotMenu(bool loadMode)
    {
        if (_saveSlotPanel == null)
            return;
        _saveSlotLoadMode = loadMode;
        if (_saveSlotTitleText != null)
            _saveSlotTitleText.text = Localization.T(loadMode ? "Tải Game" : "Lưu Game");
        RefreshSaveSlots();
        _saveSlotPanel.SetActive(true);
        _pauseMenuPanel?.SetActive(false);
        _mainMenuPanel?.SetActive(false);
        if (_settingsPanel != null)
            _settingsPanel.SetActive(false);
        if (_recordPanel != null)
            _recordPanel.SetActive(false);
        if (_questPanel != null)
            _questPanel.SetActive(false);
    }

    private void CloseSaveSlotMenu()
    {
        if (_saveSlotPanel != null)
            _saveSlotPanel.SetActive(false);
        if (GameManager.Instance == null)
            return;
        if (GameManager.Instance.InGame)
        {
            if (GameManager.Instance.GamePaused)
                ShowPauseMenu(true);
        }
        else
        {
            ShowMainMenu(true);
        }
    }

    private void OnSaveSlotClicked(int slot)
    {
        if (_saveSlotLoadMode)
        {
            if (SaveManager.Instance != null && SaveManager.Instance.LoadGame(slot))
                CloseSaveSlotMenu();
        }
        else
        {
            SaveManager.Instance?.SaveGame(slot);
            CloseSaveSlotMenu();
        }
    }

    private void RefreshSaveSlots()
    {
        if (_saveSlotButtons == null)
            return;
        for (int i = 0; i < _saveSlotButtons.Length; i++)
        {
            int day = 0;
            float timeOfDay = 0f;
            float playedSeconds = 0f;
            bool hasSave = SaveManager.Instance != null && SaveManager.Instance.GetSlotInfo(i, out day, out timeOfDay, out playedSeconds);
            string label;
            if (hasSave)
            {
                int hour = Mathf.FloorToInt(timeOfDay);
                int minute = Mathf.FloorToInt((timeOfDay - hour) * 60f);
                string timeStr = hour.ToString("00") + "." + minute.ToString("00");
                label = (i + 1).ToString() + ". " + Localization.F("Ngày {0} - {1}", day, timeStr) + "\n" + Localization.F("Chơi: {0}", FormatPlayTime(playedSeconds));
            }
            else
            {
                label = (i + 1).ToString() + ". " + Localization.T("Trống");
            }

            var button = _saveSlotButtons[i];
            if (button == null)
                continue;
            var text = button.GetComponentInChildren<TMP_Text>();
            if (text != null)
                text.text = label;
            bool interactable = !_saveSlotLoadMode || hasSave;
            button.interactable = interactable;
            var image = button.GetComponent<Image>();
            if (image != null)
                image.color = interactable ? new Color(0.18f, 0.18f, 0.25f, 1f) : new Color(0.1f, 0.1f, 0.14f, 0.6f);
        }
    }

    private string FormatPlayTime(float seconds)
    {
        int total = Mathf.Max(0, Mathf.RoundToInt(seconds));
        int h = total / 3600;
        int m = (total % 3600) / 60;
        int s = total % 60;
        return h.ToString("00") + ":" + m.ToString("00") + ":" + s.ToString("00");
    }

    private void CreatePlatformPanel(float panelWidth, float panelHeight, float padding, float fontSize, float largefontSize)
    {
        float hintH = Screen.height * 0.05f;
        _platformPanel = CreateMenuPanel("PlatformPanel", Vector2.zero, new Vector2(panelWidth * 0.7f, panelHeight * 0.5f));
        EnsureText("PlatformTitle", new Vector2(0f, panelHeight * 0.16f), Localization.T("CÁCH ĐIỀU KHIỂN"), (int)largefontSize, _platformPanel.transform, TextAlignmentOptions.Center, true, new Vector2(panelWidth - padding * 6, hintH));
        EnsureText("PlatformHint", new Vector2(0f, panelHeight * 0.1f), Localization.T("Chọn thiết bị bạn sẽ chơi"), (int)fontSize, _platformPanel.transform, TextAlignmentOptions.Center, true, new Vector2(panelWidth - padding * 6, hintH));

        _pcModeButton = CreateButton("PCModeButton", _platformPanel.transform, Localization.T("PC / Bàn Phím"), new Vector2(0f, -panelHeight * 0.02f), () => SetControlMode(ControlMode.PC));
        _mobileModeButton = CreateButton("MobileModeButton", _platformPanel.transform, Localization.T("Điện Thoại / Cảm Ứng"), new Vector2(0f, -panelHeight * 0.1f), () => SetControlMode(ControlMode.Mobile));
        CreateButton("PlatformCloseButton", _platformPanel.transform, Localization.T("Đóng"), new Vector2(0f, -panelHeight * 0.18f), () => ShowPlatformPanel(false));

        _platformPanel.SetActive(false);
    }

    public void ShowPlatformPanel(bool show)
    {
        if (_platformPanel != null)
        {
            _platformPanel.SetActive(show);
            UpdatePlatformPanelHighlight();
        }
    }

    private void UpdatePlatformPanelHighlight()
    {
        SetModeButtonHighlight(_pcModeButton, GameInput.Mode == ControlMode.PC);
        SetModeButtonHighlight(_mobileModeButton, GameInput.Mode == ControlMode.Mobile);
    }

    private void SetModeButtonHighlight(Button button, bool selected)
    {
        if (button == null) return;
        var img = button.GetComponent<Image>();
        if (img != null)
            img.color = selected ? new Color(0.35f, 0.55f, 0.75f, 1f) : new Color(0.18f, 0.18f, 0.25f, 1f);
    }

    private void SetControlMode(ControlMode mode)
    {
        GameInput.Mode = mode;
        PlayerPrefs.SetInt("ControlMode", (int)mode);
        PlayerPrefs.Save();
        UpdatePlatformPanelHighlight();
        UpdateSettingsValues();
    }

    public void ShowAllGameUI(bool show)
    {
        if (show)
            _platformPanel?.SetActive(false);
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
            _settingsPanel?.SetActive(false);
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
        {
            _pauseMenuPanel?.SetActive(false);
            var recordLinesGo = GameObject.Find("RecordLines");
            if (recordLinesGo != null)
            {
                var t = recordLinesGo.GetComponent<TMP_Text>();
                if (t != null)
                    t.text = BuildRecordLines(GameStats.WheatHarvested, GameStats.EnemiesDefeated, GameStats.MoneyEarned, GameStats.MoneyStolen);
            }
        }
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

    public void ShowEndingTreePanel(bool show)
    {
        if (_endingTreePanel != null)
            _endingTreePanel.SetActive(show);
        if (!show)
        {
            if (_endingQuestTabPanel != null)
                _endingQuestTabPanel.SetActive(false);
            if (_endingDetailPanel != null)
                _endingDetailPanel.SetActive(false);
        }
        if (_settingsPanel != null && show)
            _settingsPanel.SetActive(false);
        if (show)
        {
            _pauseMenuPanel?.SetActive(false);
            _mainMenuPanel?.SetActive(false);
            ShowEndingList();
            RefreshEndingTree();
            GameInput.SetCursorLocked(false);
        }
        else
        {
            if (GameManager.Instance != null && GameManager.Instance.GamePaused)
                ShowPauseMenu(true);
            else if (GameManager.Instance != null && !GameManager.Instance.InGame)
                ShowMainMenu(true);
        }
    }

    public void ShowEndingList()
    {
        if (_endingTreeTitleText != null)
            _endingTreeTitleText.text = Localization.T("CÂY KẾT THÚC");
        if (_endingTreeSettingsButton != null)
            _endingTreeSettingsButton.gameObject.SetActive(true);
    }

    public void PlayEndingScene(int index)
    {
        if (index < 0 || index >= Endings.Length)
            return;
        if (!Endings[index].Unlocked)
            return;
        var cm = GameManager.Instance != null ? GameManager.Instance.CutsceneManager : null;
        if (cm == null)
            return;
        if (_endingTreePanel != null)
            _endingTreePanel.SetActive(false);
        switch (index)
        {
            case 0: cm.PlayDemonEnding(OnEndingSceneDone); break;
            case 1: cm.PlayJusticeEnding(OnEndingSceneDone); break;
            case 2: cm.PlayBlackmailEnding(OnEndingSceneDone); break;
            case 3: cm.PlayHappyEnding(OnEndingSceneDone); break;
            case 4: cm.PlayNtrEnding(OnEndingSceneDone); break;
            case 5: cm.PlayBossBadEnding(OnEndingSceneDone); break;
            case 6: cm.PlaySadEnding(OnEndingSceneDone); break;
            case 7: cm.PlayFatedEnding(OnEndingSceneDone); break;
        }
    }

    private void OnEndingSceneDone()
    {
        ShowEndingTreePanel(true);
    }

    public void ShowEndingDetail(bool show, int index = -1)
    {
        if (_endingDetailPanel == null)
            return;
        if (show && index >= 0 && index < Endings.Length)
        {
            _endingDetailIndex = index;
            RefreshEndingDetail(index);
        }
        _endingDetailPanel.SetActive(show);
        if (show && _settingsPanel != null)
            _settingsPanel.SetActive(false);
    }

    private void RefreshEndingDetail(int index)
    {
        if (index < 0 || index >= Endings.Length || _endingDetailPanel == null)
            return;

        var title = _endingDetailPanel.transform.Find("EndingDetailBox/EndingDetailTitle");
        if (title != null)
        {
            var t = title.GetComponent<TMP_Text>();
            if (t != null)
                t.text = Localization.T(Endings[index].TitleKey);
        }

        var story = _endingDetailPanel.transform.Find("EndingDetailBox/EndingDetailStory");
        if (story != null)
        {
            var s = story.GetComponent<TMP_Text>();
            if (s != null)
                s.text = Localization.T(Endings[index].ContentKey);
        }

        var sb = new StringBuilder();
        sb.AppendLine(Localization.T("Điều kiện hoàn thành:"));
        var quests = (index < EndingQuestDefs.Length) ? EndingQuestDefs[index] : null;
        if (quests != null && quests.Length > 0)
        {
            for (int j = 0; j < quests.Length; j++)
            {
                var def = quests[j];
                bool done = false;
                if (QuestManager.Instance != null)
                    done = QuestManager.Instance.IsNamedQuestComplete(def.QuestName) || QuestManager.Instance.IsComplete(def.Target);
                string status = done
                    ? "<color=#59E673>" + Localization.T("Đã hoàn thành") + "</color>"
                    : "<color=#F26666>" + Localization.T("Chưa hoàn thành") + "</color>";
                string req = def.MustComplete
                    ? Localization.T("Cần hoàn thành")
                    : Localization.T("Cần im lặng");
                sb.AppendLine("• " + Localization.T(def.QuestName) + " — " + status + " (" + req + ")");
            }
        }
        else
        {
            sb.AppendLine(Localization.T("Không có điều kiện"));
        }
        if (index >= 0 && index < SpecialChoiceEndings.Length && SpecialChoiceEndings[index] &&
            index < EndingChoiceTexts.Length && !string.IsNullOrEmpty(EndingChoiceTexts[index]))
        {
            sb.AppendLine();
            sb.AppendLine(Localization.T("Lựa chọn đặc biệt:"));
            sb.AppendLine("• <color=#E6C760>" + Localization.T(EndingChoiceTexts[index]) + "</color>");
        }
        if (!Endings[index].Unlocked)
        {
            sb.AppendLine();
            sb.AppendLine("<color=#F26666>" + Localization.T("Chưa mở khóa") + "</color>");
        }

        var condObj = _endingDetailPanel.transform.Find("EndingDetailBox/EndingDetailCond");
        if (condObj != null)
        {
            var c = condObj.GetComponent<TMP_Text>();
            if (c != null)
                c.text = sb.ToString();
        }
    }

    private void PlayEndingFromDetail()
    {
        if (_endingDetailIndex < 0)
            return;
        if (_endingDetailPanel != null)
            _endingDetailPanel.SetActive(false);
        PlayEndingScene(_endingDetailIndex);
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

    public void ShowTutorial(bool show)
    {
        if (_tutorialPanel != null)
        {
            _tutorialPanel.SetActive(show);
            if (show)
                _tutorialPanel.transform.SetAsLastSibling();
        }
        if (show)
        {
            _pauseMenuPanel?.SetActive(false);
            _tutorialSpreadIndex = 0;
            UpdateTutorialPage();
            GameInput.SetCursorLocked(false);
        }
        else
        {
            if (GameManager.Instance != null && GameManager.Instance.GamePaused)
                ShowPauseMenu(true);
            else
            {
                GameInput.SetCursorLocked(true);
            }
        }
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
            _tutorialLeftText.text = leftIdx < _tutorialPages.Length ? Localization.T(_tutorialPages[leftIdx]) : "";
            _tutorialRightText.text = rightIdx < _tutorialPages.Length ? Localization.T(_tutorialPages[rightIdx]) : "";
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

    public void ShowBossBar(string bossName, int currentHp, int maxHp)
    {
        if (_bossBarRoot == null)
            CreateBossBar();
        if (_bossBarRoot == null)
            return;

        _bossBarRoot.SetActive(true);
        if (_bossBarName != null)
            _bossBarName.text = bossName;
        SetBossBar(currentHp, maxHp);
    }

    public void SetBossBar(int currentHp, int maxHp)
    {
        if (_bossBarFill == null)
            return;
        float ratio = maxHp > 0 ? Mathf.Clamp01(currentHp / (float)maxHp) : 0f;
        _bossBarFill.fillAmount = ratio;
        var color = ratio > 0.4f ? new Color(0.9f, 0.25f, 0.2f) : new Color(1f, 0.55f, 0.1f);
        _bossBarFill.color = color;
    }

    public void HideBossBar()
    {
        if (_bossBarRoot != null)
            _bossBarRoot.SetActive(false);
    }

    private void CreateBossBar()
    {
        if (_canvas == null)
            return;
        float sw = Screen.width;
        float sh = Screen.height;

        _bossBarRoot = new GameObject("BossBarRoot");
        _bossBarRoot.transform.SetParent(_canvas.transform, false);

        var rootRect = _bossBarRoot.AddComponent<RectTransform>();
        rootRect.anchorMin = new Vector2(0.5f, 1f);
        rootRect.anchorMax = new Vector2(0.5f, 1f);
        rootRect.pivot = new Vector2(0.5f, 1f);
        rootRect.anchoredPosition = new Vector2(0f, -sh * 0.08f);
        rootRect.sizeDelta = new Vector2(sw * 0.5f, 34f);

        var bg = new GameObject("BossBarBg");
        bg.transform.SetParent(_bossBarRoot.transform, false);
        var bgRect = bg.AddComponent<RectTransform>();
        bgRect.anchorMin = new Vector2(0.5f, 0.5f);
        bgRect.anchorMax = new Vector2(0.5f, 0.5f);
        bgRect.sizeDelta = new Vector2(sw * 0.5f, 20f);
        var bgImg = bg.AddComponent<Image>();
        bgImg.color = new Color(0f, 0f, 0f, 0.75f);
        bgImg.raycastTarget = false;

        var fill = new GameObject("BossBarFill");
        fill.transform.SetParent(_bossBarRoot.transform, false);
        var fillRect = fill.AddComponent<RectTransform>();
        fillRect.anchorMin = new Vector2(0.5f, 0.5f);
        fillRect.anchorMax = new Vector2(0.5f, 0.5f);
        fillRect.sizeDelta = new Vector2(sw * 0.5f - 6f, 14f);
        var fillImg = fill.AddComponent<Image>();
        fillImg.type = Image.Type.Filled;
        fillImg.fillMethod = Image.FillMethod.Horizontal;
        fillImg.fillAmount = 1f;
        fillImg.color = new Color(0.9f, 0.25f, 0.2f);
        fillImg.raycastTarget = false;
        _bossBarFill = fillImg;

        _bossBarName = EnsureText("BossBarName", new Vector2(0f, 20f), "", 16, _bossBarRoot.transform,
            TextAlignmentOptions.Center, false, new Vector2(sw * 0.5f, 20f));

        _bossBarRoot.SetActive(false);
    }

    public void UpdateTimeText(int day, float hour)
    {
        if (_timeText != null)
            _timeText.text = Localization.F("Ngày {0} - {1}", day, hour.ToString("00.00"));
    }

    public void UpdatePlayerHud(int hp, int maxHp, float stamina, float maxStamina, long money)
    {
        if (_hpText != null)
            _hpText.text = $"HP: {hp}/{maxHp}";
        if (_staminaText != null)
            _staminaText.text = Localization.F("Thể Lực: {0}/{1}", (int)stamina, (int)maxStamina);
        if (_moneyText != null)
            _moneyText.text = Localization.F("Tiền: {0}", money);
    }

    public void UpdateInventoryText(ToolManager.InventorySlot[] slots, int selectedSlot)
    {
        for (int i = 0; i < InventorySlotCount; i++)
        {
            if (i >= slots.Length || _inventorySlotTexts[i] == null) continue;

            var item = slots[i];
            string label = item == null ? "" : (item.Count > 1 ? $"{Localization.ItemName(item.Type)}x{item.Count}" : Localization.ItemName(item.Type));
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
        {
            _questText.text = text;
            ResizeStatsBg();
        }
    }

    private void ResizeStatsBg()
    {
        if (_statsBg == null || _questText == null)
            return;
        var prevOverflow = _questText.overflowMode;
        _questText.overflowMode = TextOverflowModes.Overflow;
        _questText.ForceMeshUpdate();
        float needed = Mathf.Max(_questText.preferredHeight, 24f);
        _questText.overflowMode = prevOverflow;
        _questText.rectTransform.sizeDelta = new Vector2(410f * _statsScale, needed);
        _statsBg.sizeDelta = new Vector2(430f * _statsScale, (175f + needed + 30f) * _statsScale);
    }

    public void UpdateQuestPanelText(string text)
    {
        if (_questLinesText != null)
            _questLinesText.text = text;
    }

    private string BuildRecordLines(long wheat, long enemies, long earned, long stolen)
    {
        return Localization.F("Lúa đã thu hoạch: {0}", wheat) + "\n"
             + Localization.F("Kẻ thù đã diệt: {0}", enemies) + "\n"
             + Localization.F("Tiền đã kiếm: {0}", earned) + "\n"
             + Localization.F("Tiền bị cướp: {0}", stolen);
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
        SetButtonText("WatchIntroButton", "Xem Giới Thiệu");
        SetButtonText("SkipIntroButton", "Bỏ Qua Giới Thiệu");
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

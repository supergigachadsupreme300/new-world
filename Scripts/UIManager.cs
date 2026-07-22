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
    private GameObject _mainMenuPanel;
    private GameObject _pauseMenuPanel;
    private GameObject _statsPanel;
    private GameObject _questPanel;
    private GameObject _instructionsPanel;
    private GameObject _endPanel;

    private TMP_Text _timeText;
    private TMP_Text _ammoText;
    private TMP_Text _hpText;
    private TMP_Text _staminaText;
    private TMP_Text _moneyText;
    private TMP_Text _questText;
    private TMP_Text _questLinesText;
    private TMP_Text _inventoryText;
    private RectTransform _inventoryBg;
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
        if (_inventoryText == null) return;
        var rect = _inventoryText.GetComponent<RectTransform>();
        if (rect != null)
            rect.sizeDelta = new Vector2(Screen.width * 0.65f, Screen.height * 0.05f);
        _inventoryText.fontSize = Mathf.Clamp(Screen.width / 100f, 12f, 30f);
        if (_inventoryBg != null)
            _inventoryBg.sizeDelta = new Vector2(Screen.width * 0.65f, Screen.height * 0.05f + 12f);
    }

    public void InitializeUI()
    {
        EnsureEventSystem();
        _canvas = Object.FindAnyObjectByType<Canvas>();
        if (_canvas == null || _canvas.gameObject.name != "HUD_Canvas")
            _canvas = CreateCanvas();

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
            new Vector2(15f, -10f),
            new Vector2(250f, 225f),
            new Vector2(0f, 1f));

        _timeText = EnsureText(
            "TimeText",
            new Vector2(20f, -20f),
            "Day 1 - 08.00",
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
            new Vector2(20f, -60f),
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
            new Vector2(20f, -100f),
            "Stamina: 100/100",
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
            new Vector2(20f, -140f),
            "Money: 0",
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
            new Vector2(20f, -180f),
            "Quest: Ready",
            18,
            null,
            TextAlignmentOptions.Left,
            true,
            new Vector2(420f, 40f),
            new Vector2(0f, 1f),
            new Vector2(0f, 1f),
            new Vector2(0f, 1f)
        );

        // Ammo (giữa dưới)
        _ammoText = EnsureText(
            "AmmoText",
            new Vector2(0f, -140f),
            "Ammo: 0/0",
            20,
            null,
            TextAlignmentOptions.Center,
            true,
            new Vector2(300f, 30f),
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f)
        );
        _ammoText.gameObject.SetActive(false);

        // Inventory background panel (behind text)
        _inventoryBg = CreateHudBackground("InventoryBg",
            new Vector2(0f, padding * 3f),
            new Vector2(screenWidth * 0.65f, lineHeight + 12f),
            new Vector2(0.5f, 0f));

        // Inventory: center-bottom, raised up for visibility
        _inventoryText = EnsureText(
            "InventoryText",
            new Vector2(0f, padding * 3f),
            "Inventory",
            (int)Mathf.Clamp(screenWidth / 100f, 12f, 30f),
            null,
            TextAlignmentOptions.Center,
            false,
            new Vector2(screenWidth * 0.65f, lineHeight),
            new Vector2(0.5f, 0f),
            new Vector2(0.5f, 0f),
            new Vector2(0.5f, 0f)
        );

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
        CreateButton("ContinueButton", _pauseMenuPanel.transform, "Continue", new Vector2(0f, buttonHeight * 1.8f), () => GameManager.Instance?.TogglePause(false));
        CreateButton("SaveButton", _pauseMenuPanel.transform, "Save Game", new Vector2(0f, buttonHeight * 0.9f), () => SaveManager.Instance?.SaveGame());
        CreateButton("StatsButton", _pauseMenuPanel.transform, "Stats", new Vector2(0f, 0f), () => ShowStatsPanel(true));
        CreateButton("QuestsButton", _pauseMenuPanel.transform, "Quests", new Vector2(0f, -buttonHeight * 1.8f), () => ShowQuestPanel(true));
        CreateButton("InstructionsButton", _pauseMenuPanel.transform, "Instructions", new Vector2(0f, -buttonHeight * 2.7f), () => ShowInstructions(true));
        CreateButton("ExitButton", _pauseMenuPanel.transform, "Exit", new Vector2(0f, -buttonHeight * 3.6f), () => Application.Quit());
        _pauseMenuPanel.SetActive(false);

        _statsPanel = CreateMenuPanel("StatsPanel", Vector2.zero, new Vector2(panelWidth, panelHeight));
        EnsureText("StatsTitle", new Vector2(0f, panelHeight * 0.35f), "PLAYER STATS", (int)largefontSize, _statsPanel.transform, TextAlignmentOptions.Center, true, new Vector2(panelWidth - padding * 4, lineHeight));
        EnsureText("StatsLines", new Vector2(0f, panelHeight * 0.1f), "Harvested wheat: 0\nEnemies killed: 0\nMoney earned: 0\nMoney stolen: 0", (int)fontSize, _statsPanel.transform, TextAlignmentOptions.Left, true, new Vector2(panelWidth - padding * 4, panelHeight * 0.4f));
        CreateButton("StatsBackButton", _statsPanel.transform, "Back", new Vector2(0f, -panelHeight * 0.35f), () => ShowStatsPanel(false));
        _statsPanel.SetActive(false);

        _questPanel = CreateMenuPanel("QuestPanel", Vector2.zero, new Vector2(panelWidth, panelHeight));
        EnsureText("QuestTitle", new Vector2(0f, panelHeight * 0.35f), "QUESTS", (int)largefontSize, _questPanel.transform, TextAlignmentOptions.Center, true, new Vector2(panelWidth - padding * 4, lineHeight));
        _questLinesText = EnsureText("QuestLines", new Vector2(0f, panelHeight * 0.1f), "1. Harvest wheat 0/100\n2. Slay monsters 0/30\n3. Earn coins 0/100000", (int)fontSize, _questPanel.transform, TextAlignmentOptions.Left, true, new Vector2(panelWidth - padding * 4, panelHeight * 0.3f));
        CreateButton("QuestCloseButton", _questPanel.transform, "Close", new Vector2(0f, -panelHeight * 0.35f), () => ShowQuestPanel(false));
        _questPanel.SetActive(false);

        _instructionsPanel = CreateMenuPanel("InstructionsPanel", Vector2.zero, new Vector2(panelWidth, panelHeight));
        EnsureText("InstructionsTitle", new Vector2(0f, panelHeight * 0.35f), "HƯƠNG DẪN", (int)largefontSize, _instructionsPanel.transform, TextAlignmentOptions.Center, true, new Vector2(panelWidth - padding * 4, lineHeight));
        EnsureText("InstructionsContent", new Vector2(0f, panelHeight * 0.05f), "WASD: Move\nSpace: Jump\nE: Interact\nQ: Drop item\nR: Reload\nLeft click: Use tool\nB/N: Change building type\nF5: Intro cutscene\nF6: Happy ending\nF7: Sad ending", (int)fontSize, _instructionsPanel.transform, TextAlignmentOptions.Left, true, new Vector2(panelWidth - padding * 4, panelHeight * 0.4f));
        CreateButton("InstructionsBackButton", _instructionsPanel.transform, "Back", new Vector2(0f, -panelHeight * 0.35f), () => ShowInstructions(false));
        _instructionsPanel.SetActive(false);

        _mainMenuPanel = CreateMenuPanel("MainMenuPanel", Vector2.zero, new Vector2(panelWidth, panelHeight));
        // Anchor menu to far left side, stretched vertically
        var menuRect = _mainMenuPanel.GetComponent<RectTransform>();
        if (menuRect != null)
        {
            menuRect.anchorMin = new Vector2(0f, 0f);
            menuRect.anchorMax = new Vector2(0f, 1f);
            menuRect.pivot = new Vector2(0f, 0.5f);
            menuRect.anchoredPosition = new Vector2(5f, 0f);
            menuRect.sizeDelta = new Vector2(panelWidth, 0f);
        }
        EnsureText("TitleText", new Vector2(0f, panelHeight * 0.3f), "BUILD YOUR FARM", (int)(largefontSize * 1.1f), _mainMenuPanel.transform, TextAlignmentOptions.Center, true, new Vector2(panelWidth - padding * 4, lineHeight * 1.5f));
        CreateButton("NewGameButton", _mainMenuPanel.transform, "Game Mới", new Vector2(0f, buttonHeight * 1.2f), () => MainMenuController.Instance?.OnNewGameClicked());
        CreateButton("LoadGameButton", _mainMenuPanel.transform, "Tiếp tục (Load)", new Vector2(0f, buttonHeight * 0.4f), () => MainMenuController.Instance?.OnLoadGameClicked());
        CreateButton("WatchIntroButton", _mainMenuPanel.transform, "Xem mở đầu", new Vector2(0f, -buttonHeight * 0.4f), () => MainMenuController.Instance?.OnWatchIntroClicked());
        CreateButton("SkipIntroButton", _mainMenuPanel.transform, "Bỏ qua (vào game)", new Vector2(0f, -buttonHeight * 1.2f), () => MainMenuController.Instance?.OnSkipIntroClicked());
        CreateButton("QuitButton", _mainMenuPanel.transform, "Thoát", new Vector2(0f, -buttonHeight * 2.0f), () => MainMenuController.Instance?.OnQuitClicked());
        _mainMenuPanel.SetActive(false);

        ShowAllGameUI(true);
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
        img.color = new Color(0.35f, 0.2f, 0.08f, 0.8f);
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
        image.color = new Color(0.08f, 0.08f, 0.12f, 0.95f);
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
        _ammoText?.gameObject.SetActive(show);
        _inventoryText?.gameObject.SetActive(show);
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
            _statsPanel?.SetActive(false);
            _questPanel?.SetActive(false);
            _instructionsPanel?.SetActive(false);
        }
        else
        {
            _statsBg?.gameObject.SetActive(true);
            _inventoryBg?.gameObject.SetActive(true);
        }
    }

    public void ShowMainMenuOnly(bool show)
    {
        ShowMainMenu(show);
        if (show)
        {
            ShowAllGameUI(false);
            _statsBg?.gameObject.SetActive(false);
            _inventoryBg?.gameObject.SetActive(false);
            if (_mainMenuPanel != null)
                _mainMenuPanel.SetActive(true);
        }
        else
        {
            _statsBg?.gameObject.SetActive(true);
            _inventoryBg?.gameObject.SetActive(true);
        }
    }

    public void ShowPauseMenu(bool show)
    {
        if (_pauseMenuPanel != null)
            _pauseMenuPanel.SetActive(show);
    }

    public void ShowStatsPanel(bool show)
    {
        if (_statsPanel != null)
            _statsPanel.SetActive(show);
        if (show)
            _pauseMenuPanel?.SetActive(false);
    }

    public void ShowQuestPanel(bool show)
    {
        if (_questPanel != null)
            _questPanel.SetActive(show);
        if (show)
            _pauseMenuPanel?.SetActive(false);
    }

    public void ShowInstructions(bool show)
    {
        if (_instructionsPanel != null)
            _instructionsPanel.SetActive(show);
        if (show)
            _pauseMenuPanel?.SetActive(false);
        if (!show && GameManager.Instance != null && GameManager.Instance.GamePaused)
            ShowPauseMenu(true);
    }

    public void ShowEndScreen(string title, string content)
    {
        if (_endPanel == null)
        {
            _endPanel = CreateMenuPanel("EndPanel", Vector2.zero, new Vector2(680f, 520f));
            EnsureText("EndTitle", new Vector2(0f, 170f), title, 32, _endPanel.transform, TextAlignmentOptions.Center, true, new Vector2(640f, 40f));
            EnsureText("EndContent", new Vector2(0f, 60f), content, 20, _endPanel.transform, TextAlignmentOptions.Center, true, new Vector2(640f, 120f));
            CreateButton("EndRestartButton", _endPanel.transform, "Chơi lại", new Vector2(-110f, -180f), () => GameManager.Instance?.StartNewGame());
            CreateButton("EndQuitButton", _endPanel.transform, "Thoát", new Vector2(110f, -180f), () => Application.Quit());
        }
        _endPanel.SetActive(true);
    }

    public void UpdateTimeText(int day, float hour)
    {
        if (_timeText != null)
            _timeText.text = $"Day {day} - {hour:00.00}";
    }

    public void UpdatePlayerHud(int hp, int maxHp, float stamina, float maxStamina, long money)
    {
        if (_hpText != null)
            _hpText.text = $"HP: {hp}/{maxHp}";
        if (_staminaText != null)
            _staminaText.text = $"Stamina: {(int)stamina}/{(int)maxStamina}";
        if (_moneyText != null)
            _moneyText.text = $"Money: {money}";
    }

    public void UpdateInventoryText(ToolManager.InventorySlot[] slots, int selectedSlot)
    {
        if (_inventoryText == null)
            return;

        var lines = new List<string>();
        for (int i = 0; i < slots.Length; i++)
        {
            var item = slots[i];
            string label = item == null ? "empty" : (item.Count > 1 ? $"{item.Type} x{item.Count}" : item.Type);
            if (i == selectedSlot)
                lines.Add($"[{i + 1}: {label}]");
            else
                lines.Add($"{i + 1}: {label}");
        }

        _inventoryText.text = string.Join("   ", lines);
    }

    public void UpdateAmmoText(int current, int max)
    {
        if (_ammoText == null)
            return;
        _ammoText.text = $"Ammo: {current}/{max}";
        _ammoText.gameObject.SetActive(true);
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

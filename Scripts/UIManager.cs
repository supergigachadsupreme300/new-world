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
    private TMP_Text _inventoryText;
    private TMP_Text _messageText;
    private TMP_Text _mobSpawnerText;
    private TMP_Text _crosshairText;

    public TMP_FontAsset defaultTmpFont;

    void Start()
    {
        InitializeUI();
    }

    public void InitializeUI()
    {
        EnsureEventSystem();
        _canvas = Object.FindAnyObjectByType<Canvas>();
        if (_canvas == null || _canvas.gameObject.name != "HUD_Canvas")
            _canvas = CreateCanvas();

        // Top-left HUD items (tránh căn giữa để không bị "bay" ra ngoài)
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

        // Inventory: giữ ở đáy (center-bottom) như ban đầu, nhưng không wrap và overflow để nằm trên 1 hàng
        _inventoryText = EnsureText(
            "InventoryText",
            new Vector2(0f, 50f),
            "Inventory",
            15,
            null,
            TextAlignmentOptions.Center,
            false,
            new Vector2(800f, 30f),
            new Vector2(0.5f, 0f),
            new Vector2(0.5f, 0f),
            new Vector2(0.5f, 0f)
        );

        _messageText = EnsureText(
            "MessageText",
            new Vector2(0f, 180f),
            "",
            20,
            null,
            TextAlignmentOptions.Center,
            true,
            new Vector2(600f, 40f),
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f)
        );

        _mobSpawnerText = EnsureText(
            "MobSpawnerText",
            new Vector2(0f, -140f),
            "",
            18,
            null,
            TextAlignmentOptions.Center,
            true,
            new Vector2(400f, 30f),
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f)
        );
        _mobSpawnerText.gameObject.SetActive(false);

        _crosshairText = EnsureText(
            "CrosshairText",
            Vector2.zero,
            "+",
            32,
            null,
            TextAlignmentOptions.Center,
            true,
            new Vector2(60f, 60f),
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f)
        );
        _crosshairText.gameObject.SetActive(false);

        // Panels
        _pauseMenuPanel = CreateMenuPanel("PauseMenu", Vector2.zero, new Vector2(420f, 520f));
        CreateButton("ContinueButton", _pauseMenuPanel.transform, "Continue", new Vector2(0f, 140f), () => GameManager.Instance?.TogglePause(false));
        CreateButton("SaveButton", _pauseMenuPanel.transform, "Save Game", new Vector2(0f, 70f), () => SaveManager.Instance?.SaveGame());
        CreateButton("StatsButton", _pauseMenuPanel.transform, "Stats", new Vector2(0f, 0f), () => ShowStatsPanel(true));
        CreateButton("QuestsButton", _pauseMenuPanel.transform, "Quests", new Vector2(0f, -70f), () => ShowQuestPanel(true));
        CreateButton("InstructionsButton", _pauseMenuPanel.transform, "Instructions", new Vector2(0f, -140f), () => ShowInstructions(true));
        CreateButton("ExitButton", _pauseMenuPanel.transform, "Exit", new Vector2(0f, -210f), () => Application.Quit());
        _pauseMenuPanel.SetActive(false);

        _statsPanel = CreateMenuPanel("StatsPanel", Vector2.zero, new Vector2(560f, 520f));
        EnsureText("StatsTitle", new Vector2(0f, 190f), "PLAYER STATS", 28, _statsPanel.transform, TextAlignmentOptions.Center, true, new Vector2(520f, 40f));
        EnsureText("StatsLines", new Vector2(0f, 120f), "Harvested wheat: 0\nEnemies killed: 0\nMoney earned: 0\nMoney stolen: 0", 18, _statsPanel.transform, TextAlignmentOptions.Left, true, new Vector2(520f, 200f));
        CreateButton("StatsBackButton", _statsPanel.transform, "Back", new Vector2(0f, -200f), () => ShowStatsPanel(false));
        _statsPanel.SetActive(false);

        _questPanel = CreateMenuPanel("QuestPanel", Vector2.zero, new Vector2(520f, 520f));
        EnsureText("QuestTitle", new Vector2(0f, 190f), "QUESTS", 28, _questPanel.transform, TextAlignmentOptions.Center, true, new Vector2(480f, 40f));
        EnsureText("QuestLines", new Vector2(0f, 60f), "1. Harvest wheat\n2. Earn coins\n3. Slay monsters", 16, _questPanel.transform, TextAlignmentOptions.Left, true, new Vector2(480f, 200f));
        CreateButton("QuestCloseButton", _questPanel.transform, "Close", new Vector2(0f, -220f), () => ShowQuestPanel(false));
        _questPanel.SetActive(false);

        _instructionsPanel = CreateMenuPanel("InstructionsPanel", Vector2.zero, new Vector2(620f, 520f));
        EnsureText("InstructionsTitle", new Vector2(0f, 190f), "HƯỚNG DẪN", 28, _instructionsPanel.transform, TextAlignmentOptions.Center, true, new Vector2(580f, 40f));
        EnsureText("InstructionsContent", new Vector2(0f, 20f), "WASD: Move\nSpace: Jump\nE: Interact\nQ: Drop item\nR: Reload\nLeft click: Use tool\nB/N: Change building type", 18, _instructionsPanel.transform, TextAlignmentOptions.Left, true, new Vector2(580f, 300f));
        CreateButton("InstructionsBackButton", _instructionsPanel.transform, "Back", new Vector2(0f, -220f), () => ShowInstructions(false));
        _instructionsPanel.SetActive(false);

        _mainMenuPanel = CreateMenuPanel("MainMenuPanel", Vector2.zero, new Vector2(520f, 520f));
        EnsureText("TitleText", new Vector2(0f, 160f), "NÔNG TRẠI SINH TỒN", 30, _mainMenuPanel.transform, TextAlignmentOptions.Center, true, new Vector2(480f, 50f));
        CreateButton("NewGameButton", _mainMenuPanel.transform, "Game Mới", new Vector2(0f, 60f), () => MainMenuController.Instance?.OnNewGameClicked());
        CreateButton("LoadGameButton", _mainMenuPanel.transform, "Tiếp tục (Load)", new Vector2(0f, 0f), () => MainMenuController.Instance?.OnLoadGameClicked());
        CreateButton("QuitButton", _mainMenuPanel.transform, "Thoát", new Vector2(0f, -60f), () => MainMenuController.Instance?.OnQuitClicked());
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
            rect.sizeDelta = new Vector2(280f, 56f);

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
            text.fontSize = 22;
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
    }

    public void SetCrosshairVisible(bool visible)
    {
        if (_crosshairText != null)
            _crosshairText.gameObject.SetActive(visible);
    }

    public void ShowMainMenu(bool show)
    {
        if (_mainMenuPanel != null)
            _mainMenuPanel.SetActive(show);
        if (show)
            _pauseMenuPanel?.SetActive(false);
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

    public void UpdateQuestText(string name, int progress, int goal)
    {
        if (_questText != null)
        {
            var status = progress >= goal ? "Completed" : $"{progress}/{goal}";
            _questText.text = $"Quest: {name} {status}";
        }
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

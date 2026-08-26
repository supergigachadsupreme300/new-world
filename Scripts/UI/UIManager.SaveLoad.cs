using UnityEngine;
using UnityEngine.UI;
using TMPro;

public partial class UIManager
{
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
}

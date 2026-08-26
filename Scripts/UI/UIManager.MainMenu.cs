using UnityEngine;
using UnityEngine.UI;
using TMPro;

public partial class UIManager
{
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
        _genderPanel.transform.SetAsLastSibling();
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
}

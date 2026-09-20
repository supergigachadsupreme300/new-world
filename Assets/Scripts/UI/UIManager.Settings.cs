/// <summary>
/// Settings partial of UIManager: the in-game settings panel show/hide and value refresh.
/// </summary>
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
using TMPro;

public partial class UIManager
{
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
}
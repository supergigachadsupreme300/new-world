/// <summary>
/// Menus partial of UIManager: record/quest panels and the end-game (end/boss-end) screens.
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
}
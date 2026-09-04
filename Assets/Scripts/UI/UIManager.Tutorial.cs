using UnityEngine;
using UnityEngine.UI;
using TMPro;

public partial class UIManager
{
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
}

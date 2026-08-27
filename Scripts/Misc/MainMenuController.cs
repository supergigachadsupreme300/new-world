using UnityEngine;

public class MainMenuController : MonoSingleton<MainMenuController>
{
private GameManager _gameManager;

    public void InitializeMenu(GameManager gameManager)
    {
        _gameManager = gameManager;
    }
    public void OnNewGameClicked()
    {
        if (GameManager.Instance?.UIManager != null)
            GameManager.Instance.UIManager.ShowGenderSelectionMenu(GenderMenuMode.Intro);
        else
            _gameManager?.StartNewGame();
    }
    public void OnLoadGameClicked()
    {
        if (GameManager.Instance?.UIManager != null)
            GameManager.Instance.UIManager.ShowSaveSlotMenu(true);
    }
    public void OnQuitClicked()
    {
        Application.Quit();
    }
    public void OnWatchIntroClicked()
    {
        if (_gameManager == null) return;
        _gameManager.ShowMainMenu(false);

        if (CutsceneManager.Instance != null)
            CutsceneManager.Instance.PlayIntroCutscene(() => _gameManager.ShowMainMenu(true));
        else
            _gameManager.ShowMainMenu(true);
    }
    public void OnSkipIntroClicked()
    {
        if (GameManager.Instance?.UIManager != null)
            GameManager.Instance.UIManager.ShowGenderSelectionMenu(GenderMenuMode.SkipIntro);
        else
            _gameManager?.StartNewGameSkipIntro();
    }
}

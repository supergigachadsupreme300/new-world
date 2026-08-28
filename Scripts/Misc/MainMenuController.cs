using UnityEngine;

public class MainMenuController : MonoSingleton<MainMenuController>
{
private GameManager _gameManager;
    private UIManager _uiManager;

    public void InitializeMenu(GameManager gameManager)
    {
        _gameManager = gameManager;
        _uiManager = GameManager.Instance?.UIManager;
    }
    public void OnNewGameClicked()
    {
        if (_uiManager != null)
            _uiManager.ShowGenderSelectionMenu(GenderMenuMode.Intro);
        else
            _gameManager?.StartNewGame();
    }
    public void OnLoadGameClicked()
    {
        if (_uiManager != null)
            _uiManager.ShowSaveSlotMenu(true);
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
        if (_uiManager != null)
            _uiManager.ShowGenderSelectionMenu(GenderMenuMode.SkipIntro);
        else
            _gameManager?.StartNewGameSkipIntro();
    }
}

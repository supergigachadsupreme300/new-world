using UnityEngine;

public class MainMenuController : MonoBehaviour
{
    public static MainMenuController Instance { get; private set; }

    private GameManager _gameManager;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    public void InitializeMenu(GameManager gameManager)
    {
        _gameManager = gameManager;
    }

    public void OnNewGameClicked()
    {
        _gameManager?.StartNewGame();
    }

    public void OnLoadGameClicked()
    {
        SaveManager.Instance?.LoadGame();
        _gameManager?.ShowMainMenu(false);
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
        _gameManager?.StartNewGameSkipIntro();
    }
}

using UnityEngine;
using UnityEngine.EventSystems;

public class GameBootstrap : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void InitializeGameRoot()
    {
        if (GameManager.Instance != null)
            return;

        if (GameObject.Find("GameRoot") != null)
            return;

        var root = new GameObject("GameRoot");
        Object.DontDestroyOnLoad(root);

        var gameManager = root.AddComponent<GameManager>();
        var uiManager = root.AddComponent<UIManager>();
        var worldBuilder = root.AddComponent<WorldBuilder>();
        var toolManager = root.AddComponent<ToolManager>();
        var existingPlayer = Object.FindAnyObjectByType<PlayerController>();
        PlayerController playerController;
        if (existingPlayer != null)
        {
            playerController = existingPlayer;
            Object.DontDestroyOnLoad(playerController.gameObject);
        }
        else
        {
            playerController = root.AddComponent<PlayerController>();
        }
        var mainMenuController = root.AddComponent<MainMenuController>();
        var saveManager = root.AddComponent<SaveManager>();
        var soundManager = root.AddComponent<SoundManager>();
        var questManager = root.AddComponent<QuestManager>();
        var cutsceneManager = root.AddComponent<CutsceneManager>();

        gameManager.UIManager = uiManager;
        gameManager.WorldBuilder = worldBuilder;
        gameManager.ToolManager = toolManager;
        gameManager.Player = playerController;
        gameManager.CutsceneManager = cutsceneManager;

        uiManager.InitializeUI();
        toolManager.Initialize(uiManager, worldBuilder);
        mainMenuController.InitializeMenu(gameManager);
        soundManager.LoadSoundClips();
        saveManager.Initialize(gameManager, toolManager, worldBuilder, uiManager, questManager);
        questManager.InitializeQuests();
        cutsceneManager.Initialize(uiManager);
    }
}

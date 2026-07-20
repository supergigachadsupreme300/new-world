using UnityEngine;
using UnityEngine.EventSystems;

public class GameBootstrap : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void InitializeGameRoot()
    {
        if (GameObject.Find("GameRoot") != null)
            return;

        var root = new GameObject("GameRoot");
        Object.DontDestroyOnLoad(root);

        var gameManager = Object.FindAnyObjectByType<GameManager>() ?? root.AddComponent<GameManager>();
        var uiManager = Object.FindAnyObjectByType<UIManager>() ?? root.AddComponent<UIManager>();
        var worldBuilder = Object.FindAnyObjectByType<WorldBuilder>() ?? root.AddComponent<WorldBuilder>();
        var toolManager = Object.FindAnyObjectByType<ToolManager>() ?? root.AddComponent<ToolManager>();
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
        var mainMenuController = Object.FindAnyObjectByType<MainMenuController>() ?? root.AddComponent<MainMenuController>();
        var saveManager = Object.FindAnyObjectByType<SaveManager>() ?? root.AddComponent<SaveManager>();
        var soundManager = Object.FindAnyObjectByType<SoundManager>() ?? root.AddComponent<SoundManager>();
        var questManager = Object.FindAnyObjectByType<QuestManager>() ?? root.AddComponent<QuestManager>();
        var cutsceneManager = Object.FindAnyObjectByType<CutsceneManager>() ?? root.AddComponent<CutsceneManager>();

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

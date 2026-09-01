using UnityEngine;
using UnityEngine.EventSystems;

public class GameBootstrap : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void InitializeGameRoot()
    {
        if (GameObject.Find("GameRoot") != null)
            return;

        GameInput.Mode = (ControlMode)PlayerPrefs.GetInt("ControlMode", 0);

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
        var randomEventManager = Object.FindAnyObjectByType<RandomEventManager>() ?? root.AddComponent<RandomEventManager>();
        var wifeNPC = Object.FindAnyObjectByType<WifeNPC>() ?? root.AddComponent<WifeNPC>();
        var mobileInput = Object.FindAnyObjectByType<MobileInputController>() ?? root.AddComponent<MobileInputController>();
        var sleepManager = Object.FindAnyObjectByType<SleepManager>() ?? root.AddComponent<SleepManager>();
        var karmaManager = Object.FindAnyObjectByType<KarmaManager>() ?? root.AddComponent<KarmaManager>();
        var skillManager = Object.FindAnyObjectByType<SkillManager>() ?? root.AddComponent<SkillManager>();
        var friendshipManager = Object.FindAnyObjectByType<FriendshipManager>() ?? root.AddComponent<FriendshipManager>();
        var typingMinigame = Object.FindAnyObjectByType<TypingMinigame>() ?? root.AddComponent<TypingMinigame>();

        gameManager.UIManager = uiManager;
        gameManager.WorldBuilder = worldBuilder;
        gameManager.ToolManager = toolManager;
        gameManager.Player = playerController;
        gameManager.CutsceneManager = cutsceneManager;
        gameManager.RandomEventManager = randomEventManager;
        gameManager.KarmaManager = karmaManager;

        uiManager.InitializeUI();
        toolManager.Initialize(uiManager, worldBuilder);
        mainMenuController.InitializeMenu(gameManager);
        soundManager.LoadSoundClips();
        saveManager.Initialize(gameManager, toolManager, worldBuilder, uiManager, questManager);
        questManager.InitializeQuests();
        cutsceneManager.Initialize(uiManager);
        randomEventManager.Initialize(uiManager);
        wifeNPC.Initialize(uiManager.GetCanvas());
        wifeNPC.LoadState();
        karmaManager.Initialize();
        skillManager.Initialize();
        friendshipManager.Initialize();
    }
}

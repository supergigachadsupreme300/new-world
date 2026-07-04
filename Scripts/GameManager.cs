using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    public bool InGame { get; private set; }
    public bool GamePaused { get; private set; }
    public int CurrentDay = 1;
    public float TimeOfDay = 8f;
    public float TimeSpeed = 1f;

    public PlayerController Player;
    public WorldBuilder WorldBuilder;
    public UIManager UIManager;
    public ToolManager ToolManager;
    public CutsceneManager CutsceneManager;
    public List<PetController> Pets = new List<PetController>();
    public List<EnemyController> Enemies = new List<EnemyController>();
    public bool AutoStartGame = true;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void Start()
    {
        AutoResolveReferences();

        if (WorldBuilder != null)
            WorldBuilder.GenerateWorld();

        // Ensure UI is visible after initialization (fix cases where UI stays hidden)
        if (UIManager != null)
            UIManager.ShowAllGameUI(true);

        if (ToolManager != null)
            ToolManager.ResetSelection();

        SpawnDefaultPets();

        if (AutoStartGame)
        {
            StartNewGame();
        }
        else
        {
            ShowMainMenu(true);
        }
    }

    private void Update()
    {
        if (!InGame || GamePaused)
            return;

        TimeOfDay += TimeSpeed * Time.deltaTime;
        if (TimeOfDay >= 24f)
        {
            TimeOfDay -= 24f;
            CurrentDay++;
        }

        UpdateTimeUI();

        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            TogglePause(true);
        }
    }

    public void AutoResolveReferences()
    {
        Player = Object.FindAnyObjectByType<PlayerController>();
        WorldBuilder = Object.FindAnyObjectByType<WorldBuilder>();
        UIManager = Object.FindAnyObjectByType<UIManager>();
        ToolManager = Object.FindAnyObjectByType<ToolManager>();
        CutsceneManager = Object.FindAnyObjectByType<CutsceneManager>();
        Pets = new List<PetController>(Object.FindObjectsByType<PetController>(FindObjectsSortMode.None));
        Enemies = new List<EnemyController>(Object.FindObjectsByType<EnemyController>(FindObjectsSortMode.None));

        if (UIManager == null)
            UIManager = gameObject.AddComponent<UIManager>();
        if (ToolManager == null)
            ToolManager = gameObject.AddComponent<ToolManager>();
        if (Object.FindAnyObjectByType<MainMenuController>() == null)
            gameObject.AddComponent<MainMenuController>();

        if (MainMenuController.Instance != null)
            MainMenuController.Instance.InitializeMenu(this);

        UIManager.InitializeUI();
        ToolManager.Initialize(UIManager, WorldBuilder);
        if (CutsceneManager != null)
            CutsceneManager.Initialize(UIManager);
    }

    public void SpawnDefaultPets()
    {
        if (Pets.Count > 0)
            return;

        var petGO = new GameObject("Pet_01");
        petGO.transform.position = new Vector3(2f, 0.5f, 2f);
        var pet = petGO.AddComponent<PetController>();
        Pets.Add(pet);
        Debug.Log("[GameManager] Spawned default pet");
    }

    public void ShowMainMenu(bool show)
    {
        if (UIManager != null)
        {
            UIManager.ShowMainMenu(show);
            if (show)
            {
                if (Player != null)
                    Player.EnableInput(false);
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
        }
    }

    public void StartNewGame()
    {
        InGame = true;
        GamePaused = false;
        CurrentDay = 1;
        TimeOfDay = 8f;

        if (WorldBuilder != null)
        {
            WorldBuilder.ResetWorld();
            WorldBuilder.GenerateWorld();
            WorldBuilder.SetDayNight(TimeOfDay);
        }

        if (Player != null)
        {
            Player.EnableInput(true);
            Player.ResetPlayer();
        }

        if (UIManager != null)
        {
            UIManager.ShowAllGameUI(true);
            UIManager.ShowPauseMenu(false);
            UIManager.ShowMainMenu(false);
        }

        if (ToolManager != null)
            ToolManager.ResetSelection();

        if (CutsceneManager != null)
            CutsceneManager.CancelCutscene();

        UpdateTimeUI();
    }

    public void LoadGame()
    {
        if (UIManager != null)
        {
            UIManager.ShowAllGameUI(true);
            UIManager.ShowMainMenu(false);
        }

        InGame = true;
        GamePaused = false;

        if (Player != null)
            Player.EnableInput(true);

        UpdateTimeUI();
    }

    public void TogglePause(bool paused)
    {
        GamePaused = paused;
        if (UIManager != null)
            UIManager.ShowPauseMenu(paused);

        if (Player != null)
            Player.EnableInput(!paused);

        Cursor.lockState = paused ? CursorLockMode.None : CursorLockMode.Locked;
        Cursor.visible = paused;
    }

    public void SetTimeOfDay(float hour)
    {
        TimeOfDay = Mathf.Repeat(hour, 24f);
        UpdateTimeUI();
        if (WorldBuilder != null)
            WorldBuilder.SetDayNight(TimeOfDay);
    }

    public void UpdateTimeUI()
    {
        if (UIManager != null)
            UIManager.UpdateTimeText(CurrentDay, TimeOfDay);
    }

    public void RequestHappyEnding()
    {
        if (CutsceneManager != null)
            CutsceneManager.PlaySadEnding();
        else if (UIManager != null)
            UIManager.ShowEndScreen("KẾT THÚC ĐAU BUỒN", "\"Skibidi.\ndop dop.\"");
    }
}

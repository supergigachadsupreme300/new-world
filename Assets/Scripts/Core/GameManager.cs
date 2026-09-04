using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class GameManager : MonoSingleton<GameManager>
{
    public bool InGame { get; private set; }
    public bool GamePaused { get; private set; }
    public bool IsPlayerDead { get; private set; }
    public int CurrentDay = 1;
    public float TimeOfDay = 8f;
    public float TimeSpeed = 1f;
    public float PlayTime;

    public PlayerController Player;
    public WorldBuilder WorldBuilder;
    public UIManager UIManager;
    public ToolManager ToolManager;
    public CutsceneManager CutsceneManager;
    public RandomEventManager RandomEventManager;
    public KarmaManager KarmaManager;
    public List<PetController> Pets = new List<PetController>();
    public bool AutoStartGame = false;

    protected override void Awake()
    {
        base.Awake();
        TimeSpeed = 0.01f;
        AutoStartGame = false;
    }

    private void Start()
    {
        AutoResolveReferences();

        // Legacy finite map is gated by WorldBuilder.EnableLegacyGeneration (default false).
        if (WorldBuilder != null)
            WorldBuilder.GenerateWorld();

        // Ensure UI is visible after initialization (fix cases where UI stays hidden)
        if (UIManager != null)
            UIManager.ShowMainMenuOnly(true);

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
            UIManager?.ShowPlatformPanel(true);
        }
    }

    private void Update()
    {
        // Handle Escape for Buffalo Shop even when paused
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            if (CutsceneManager != null && CutsceneManager.JustCancelledCutscene)
                return;

            if (TypingMinigame.Instance != null && TypingMinigame.Instance.IsOpen)
            {
                TypingMinigame.Instance.Close();
                return;
            }

            var shop = Object.FindAnyObjectByType<BuffaloShopManager>();
            if (shop != null && shop.IsOpen())
            {
                shop.Close();
                return;
            }

            if (TryCloseActiveDialog())
                return;
        }

#if UNITY_EDITOR
        // Cutscene test shortcuts (work even when paused)
        if (CutsceneManager != null && Keyboard.current != null)
        {
            if (Keyboard.current.f5Key.wasPressedThisFrame)
                CutsceneManager.PlayIntroCutscene(null);
            else if (Keyboard.current.f6Key.wasPressedThisFrame)
                CutsceneManager.RequestHappyEnding();
            else if (Keyboard.current.f7Key.wasPressedThisFrame)
            {
                if (CutsceneManager.IsActive)
                    CutsceneManager.CancelCutscene();
                CutsceneManager.PlaySadEnding();
            }
            else if (Keyboard.current.f8Key.wasPressedThisFrame)
            {
                if (CutsceneManager.IsActive)
                    CutsceneManager.CancelCutscene();
                RequestJusticeEnding();
            }
            else if (Keyboard.current.f9Key.wasPressedThisFrame)
            {
                if (CutsceneManager.IsActive)
                    CutsceneManager.CancelCutscene();
                RequestBlackmailEnding();
            }
            else if (Keyboard.current.f10Key.wasPressedThisFrame)
            {
                if (CutsceneManager.IsActive)
                    CutsceneManager.CancelCutscene();
                CutsceneManager.PlayDemonEnding();
            }
            else if (Keyboard.current.f11Key.wasPressedThisFrame)
            {
                if (CutsceneManager.IsActive)
                    CutsceneManager.CancelCutscene();
                CutsceneManager.PlayHappyEnding();
            }
        }
#endif

        if (!InGame || GamePaused)
            return;

        PlayTime += Time.deltaTime;

        TimeOfDay += TimeSpeed * Time.deltaTime;
        if (TimeOfDay >= 24f)
        {
            TimeOfDay -= 24f;
            CurrentDay++;
            if (WifeNPC.Instance != null)
                WifeNPC.Instance.OnDayChanged();
            if (ImmigrantNpc.Instance != null)
                ImmigrantNpc.Instance.OnDayChanged();
        }

        UpdateTimeUI();

        if (KarmaManager != null)
            KarmaManager.RegenKarma(Time.deltaTime);

        if (WorldBuilder != null && WorldBuilder.EnableLegacyGeneration)
        {
            WorldBuilder.SetDayNight(TimeOfDay);
            WorldBuilder.UpdateWorld(Time.deltaTime);
        }

        if ((Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) || MobileInputController.Consume("pause"))
        {
            if (CutsceneManager != null && CutsceneManager.JustCancelledCutscene)
                return;
            if (RichManNPC.Instance != null && RichManNPC.Instance.IsDealCameraActive)
                return;
            if (Player != null && Player.IsSitting)
                return;
            if (ToolManager != null && ToolManager.EscapeHandledThisFrame)
                return;
            if (TypingMinigame.Instance != null && TypingMinigame.Instance.IsOpen)
                return;
            if (TryCloseActiveDialog())
                return;
            TogglePause(true);
        }
    }

    private bool TryCloseActiveDialog()
    {
        var wife = WifeNPC.Instance;
        if (wife != null && wife.IsDialogActive)
        {
            wife.HideDialog(true);
            return true;
        }

        var buffalo = BuffaloDialog.Instance;
        if (buffalo != null && buffalo.IsDialogActive)
        {
            buffalo.Hide();
            return true;
        }

        var richMan = RichManNPC.Instance;
        if (richMan != null && richMan.IsDialogActive)
        {
            richMan.Hide();
            return true;
        }

        var chef = ChefNPC.Instance;
        if (chef != null && chef.IsDialogActive)
        {
            chef.Hide();
            return true;
        }

        var monk = PagodaMonkNPC.Instance;
        if (monk != null && monk.IsDialogActive)
        {
            monk.Hide();
            return true;
        }

        var police = PoliceOfficerNPC.Instance;
        if (police != null && police.IsDialogActive)
        {
            police.Hide();
            return true;
        }

        var librarian = LibrarianNPC.Instance;
        if (librarian != null && librarian.IsDialogActive)
        {
            librarian.Hide();
            return true;
        }

        var goblinMenu = GoblinCommandMenu.Instance;
        if (goblinMenu != null && goblinMenu.IsOpen)
        {
            goblinMenu.Close();
            return true;
        }

        return false;
    }

    public void AutoResolveReferences()
    {
        Player = Object.FindAnyObjectByType<PlayerController>();
        WorldBuilder = Object.FindAnyObjectByType<WorldBuilder>();
        UIManager = Object.FindAnyObjectByType<UIManager>();
        ToolManager = Object.FindAnyObjectByType<ToolManager>();
        CutsceneManager = Object.FindAnyObjectByType<CutsceneManager>();
        RandomEventManager = Object.FindAnyObjectByType<RandomEventManager>();
        Pets = new List<PetController>(Object.FindObjectsByType<PetController>(FindObjectsSortMode.None));

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
        if (RandomEventManager != null)
            RandomEventManager.Initialize(UIManager);
    }

    public void SpawnDefaultPets()
    {
        EnsureGoblin();

        if (Pets.Count > 0)
            return;

        var petGO = new GameObject("Pet_01");
        petGO.transform.position = new Vector3(6f, 0.5f, 2f);
        var pet = petGO.AddComponent<PetController>();
        Pets.Add(pet);
        Debug.Log("[GameManager] Spawned default pet");
    }

    public void EnsureGoblin()
    {
        if (GoblinPet.Instance != null) return;

        var wb = WorldBuilder.Instance;
        if (wb == null || !wb.HasGoblinHut())
            return;

        var goblinGO = new GameObject("GoblinPet_01");
        Vector3 goblinSpawn = new Vector3(0f, 0.5f, 13f);
        foreach (var b in wb.GetAllBuildings())
        {
            if (b == null || b.Type != "goblin_hut") continue;
            goblinSpawn = b.Position + new Vector3(0f, 0.5f, -3f);
            break;
        }
        goblinGO.transform.position = goblinSpawn;
        goblinGO.AddComponent<GoblinPet>();
        Debug.Log("[GameManager] Spawned goblin pet");
    }

    public void DespawnGoblin()
    {
        if (GoblinPet.Instance == null) return;
        var goblinGO = GoblinPet.Instance.gameObject;
        GoblinPet.Instance = null;
        Destroy(goblinGO);
        Debug.Log("[GameManager] Despawned goblin pet");
    }

    public void ShowMainMenu(bool show)
    {
        if (UIManager != null)
        {
            if (show)
                UIManager.ShowMainMenuOnly(true);
            else
                UIManager.ShowMainMenu(false);

            if (show)
            {
                if (Player != null)
                    Player.EnableInput(false);
                GameInput.SetCursorLocked(false);

                if (CutsceneManager != null)
                    CutsceneManager.PlayMainMenuVisual();
            }
            else
            {
                if (CutsceneManager != null)
                    CutsceneManager.StopMainMenuVisual();
            }
        }
    }

    public void ReturnToMainMenu()
    {
        InGame = false;
        GamePaused = false;
        IsPlayerDead = false;
        if (CutsceneManager != null)
            CutsceneManager.StopIntroIfActive();
        if (ToolManager != null)
            ToolManager.ClearInventory();
        ShowMainMenu(true);
    }

    public void StartNewGame()
    {
        InGame = true;
        GamePaused = false;
        IsPlayerDead = false;
        CurrentDay = 1;
        TimeOfDay = 8f;
        PlayTime = 0f;

        if (Player != null)
        {
            Player.EnableInput(true);
            Player.ResetPlayer();
        }

        if (UIManager != null)
        {
            UIManager.HideEndScreen();
            UIManager.ShowAllGameUI(true);
            UIManager.ShowPauseMenu(false);
            UIManager.ShowMainMenu(false);
        }

        if (QuestManager.Instance != null)
        {
            QuestManager.Instance.ResetQuests();
            QuestManager.Instance.InitializeQuests();
            QuestManager.Instance.RefreshQuestUI();
        }

        if (ToolManager != null)
            ToolManager.ClearInventory();

        if (WifeNPC.Instance != null)
            WifeNPC.Instance.ResetForNewGame();

        if (KarmaManager != null)
            KarmaManager.Initialize();

        if (CutsceneManager != null)
        {
            CutsceneManager.StopMainMenuVisual(true);
            CutsceneManager.PlayIntroCutscene(() => UIManager?.ShowTutorial(true));
        }

        var spawner = Object.FindAnyObjectByType<LivestockSpawner>();
        if (spawner != null) spawner.Restart();

        UpdateTimeUI();
    }

    public void StartNewGameSkipIntro()
    {
        InGame = true;
        GamePaused = false;
        IsPlayerDead = false;
        CurrentDay = 1;
        TimeOfDay = 8f;
        PlayTime = 0f;

        if (Player != null)
        {
            Player.EnableInput(true);
            Player.ResetPlayer();
        }

        if (UIManager != null)
        {
            UIManager.HideEndScreen();
            UIManager.ShowAllGameUI(true);
            UIManager.ShowPauseMenu(false);
            UIManager.ShowMainMenu(false);
        }

        if (QuestManager.Instance != null)
        {
            QuestManager.Instance.ResetQuests();
            QuestManager.Instance.InitializeQuests();
            QuestManager.Instance.RefreshQuestUI();
        }

        if (ToolManager != null)
            ToolManager.ClearInventory();

        if (WifeNPC.Instance != null)
            WifeNPC.Instance.ResetForNewGame();

        if (CutsceneManager != null)
        {
            CutsceneManager.StopMainMenuVisual();
            CutsceneManager.AttachCamera();
        }

        var spawner2 = Object.FindAnyObjectByType<LivestockSpawner>();
        if (spawner2 != null) spawner2.Restart();

        UIManager?.ShowTutorial(true);

        UpdateTimeUI();
    }

    public void LoadGame()
    {
        IsPlayerDead = false;
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

        bool fishing = FishingController.IsFishingActive;
        if (Player != null && !fishing)
            Player.EnableInput(!paused);

        GameInput.SetCursorLocked(!paused && !fishing);
    }

    public void SetTimeOfDay(float hour)
    {
        TimeOfDay = Mathf.Repeat(hour, 24f);
        UpdateTimeUI();
        if (WorldBuilder != null)
            WorldBuilder.SetDayNight(TimeOfDay);
    }

    public void AdvanceTime(float hours)
    {
        TimeOfDay += hours;
        bool dayRolled = false;
        while (TimeOfDay >= 24f)
        {
            TimeOfDay -= 24f;
            CurrentDay++;
            dayRolled = true;
        }
        SetTimeOfDay(TimeOfDay);
        UpdateTimeUI();
        if (dayRolled && WifeNPC.Instance != null)
            WifeNPC.Instance.OnDayChanged();
        if (dayRolled && ImmigrantNpc.Instance != null)
            ImmigrantNpc.Instance.OnDayChanged();
    }

    public void UpdateTimeUI()
    {
        if (UIManager != null)
            UIManager.UpdateTimeText(CurrentDay, TimeOfDay);
    }

    public void RequestHappyEnding()
    {
        if (CutsceneManager != null)
            CutsceneManager.RequestHappyEnding();
    }

    public void TriggerPlayerDeath()
    {
        if (IsPlayerDead) return;
        IsPlayerDead = true;
        CutsceneManager?.PlaySadEnding();
    }

    public void ReloadFromBossDeath()
    {
        IsPlayerDead = false;
        if (UIManager != null)
        {
            UIManager.HideBossBar();
            UIManager.HideEndScreen();
        }
        bool loaded = SaveManager.Instance != null && SaveManager.Instance.LoadGame();
        if (!loaded && UIManager != null)
            StartNewGame();
    }

    public void RequestNtrEnding()
    {
        if (CutsceneManager != null)
            CutsceneManager.RequestNtrEnding();
    }

    public void RequestJusticeEnding()
    {
        if (CutsceneManager == null)
            return;
        bool demonSlain = QuestManager.Instance != null && QuestManager.Instance.IsComplete("boss_kill");
        if (demonSlain)
            CutsceneManager.PlayHappyEnding();
        else
            CutsceneManager.PlayJusticeEnding();
    }

    public void RequestBlackmailEnding()
    {
        if (CutsceneManager != null)
            CutsceneManager.PlayBlackmailEnding();
    }

    public void RequestDemonEnding()
    {
        if (CutsceneManager != null)
            CutsceneManager.PlayDemonEnding();
    }
}

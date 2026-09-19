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
        var worldStreamer = Object.FindAnyObjectByType<WorldStreamer>() ?? root.AddComponent<WorldStreamer>();
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
            // Keep the synthesized player on its own object: root hosts the shared singletons
            // (incl. NewWorldSystems/AudioManager) and must never be the player, otherwise
            // those managers writing to their own transform would teleport the player.
            var playerObject = new GameObject("Player");
            playerController = playerObject.AddComponent<PlayerController>();
            Object.DontDestroyOnLoad(playerObject);
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
        var religionManager = Object.FindAnyObjectByType<ReligionManager>() ?? root.AddComponent<ReligionManager>();
        var skillManager = Object.FindAnyObjectByType<SkillManager>() ?? root.AddComponent<SkillManager>();
        var friendshipManager = Object.FindAnyObjectByType<FriendshipManager>() ?? root.AddComponent<FriendshipManager>();
        var fishingProgression = Object.FindAnyObjectByType<FishingProgression>() ?? root.AddComponent<FishingProgression>();
        var chestStorageManager = Object.FindAnyObjectByType<ChestStorageManager>() ?? root.AddComponent<ChestStorageManager>();
        var typingMinigame = Object.FindAnyObjectByType<TypingMinigame>() ?? root.AddComponent<TypingMinigame>();
        var farmingManager = Object.FindAnyObjectByType<FarmingManager>() ?? root.AddComponent<FarmingManager>();

        gameManager.UIManager = uiManager;
        gameManager.WorldBuilder = worldBuilder;
        gameManager.ToolManager = toolManager;
        gameManager.Player = playerController;
        gameManager.CutsceneManager = cutsceneManager;
        gameManager.RandomEventManager = randomEventManager;
        gameManager.KarmaManager = karmaManager;
        gameManager.ReligionManager = religionManager;

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
        religionManager.Initialize();
        skillManager.Initialize();
        friendshipManager.Initialize();
        fishingProgression.Initialize();
        chestStorageManager.Initialize();

        // --- Open-world chunk streaming -------------------------------------------------
        // The new seed/coordinate open world. If a default ground material is not
        // configured, create a simple lit one so generated chunks render.
        if (worldStreamer.RenderDistance == null)
        {
            var rd = ScriptableObject.CreateInstance<RenderDistanceController>();
            rd.Radius = 20;
            rd.MaxRadius = 160;
            worldStreamer.RenderDistance = rd;
        }
        if (worldStreamer.GroundMaterial == null)
        {
            // Layered terrain material: a URP-lit vertex-color shader whose per-vertex colors
            // paint the grass/dirt/stone strata bands (ChunkMeshGenerator.TerrainBandColor). The
            // vertex colors carry the full look, so the base color is white — grass-green base
            // would tint the dirt/stone deep enough to wash out. Falls back to the plain URP Lit
            // grass material if the layered shader isn't present (e.g. it was stripped).
            Shader shader = Shader.Find("NewWorld/TerrainLayered");
            var mat = new Material(shader != null ? shader : Shader.Find("Universal Render Pipeline/Lit"));
            mat.color = Color.white;
            worldStreamer.GroundMaterial = mat;
        }

        // Generate ONLY the spawn chunk synchronously so the player has ground to land on before
        // the first frame; the surrounding chunks build in the background from frame 1 (the chunk
        // pipeline + ChunksPerFrame budget fills the render radius over ~1.5s).
        //
        // Boot order is "ground first, then player": the player is placed on the pre-generated
        // spawn chunk at (0, ~y+2, -10) — never an unloaded void. NewWorldTestGround builds its
        // independent floating platform in Awake but does NOT auto-teleport the player onto it
        // (that's opt-in, AutoTeleportPlayerOnStart) — the pad is left for the player to walk to.
        TerrainChunkCoord spawnChunk = TerrainChunkCoord.FromTile(new ChunkCoord(0, -10));
        worldStreamer.GenerateChunkSync(spawnChunk);

        if (playerController != null)
        {
            float spawnY = TerrainNoiseGenerator.GetHeight(worldStreamer.Seed, 0.5f, -9.5f);
            playerController.TeleportTo(new Vector3(0f, spawnY + 2f, -10f));
        }

        worldStreamer.SetFocus(playerController != null ? playerController.transform : null);

        // --- Phase 8/9 UI, LOD, culling, pooling ---------------------------------------
        var newWorldSystems = Object.FindAnyObjectByType<NewWorldSystems>()
            ?? root.AddComponent<NewWorldSystems>();

        // --- Testing ground (weapons, enemies, skills, NPCs) ----------------------------
        var testGround = Object.FindAnyObjectByType<NewWorldTestGround>()
            ?? root.AddComponent<NewWorldTestGround>();
    }
}

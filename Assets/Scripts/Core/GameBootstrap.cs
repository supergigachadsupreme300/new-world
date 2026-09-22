using System;
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
        UnityEngine.Object.DontDestroyOnLoad(root);

        // --- Managers (1e5) --------------------------------------------------------------
        // Lookups go through ComponentRegistry: the first find per type casts from ONE shared
        // scene sweep instead of a per-type FindAnyObjectByType scan (~24 sweeps at boot before
        // this change). Missing singletons are created on the GameRoot exactly as before, and the
        // fresh component is cached so later resolve passes stay sweepless.
        var gameManager = Ensure(root, ComponentRegistry.Find<GameManager>());
        var uiManager = Ensure(root, ComponentRegistry.Find<UIManager>());
        var worldBuilder = Ensure(root, ComponentRegistry.Find<WorldBuilder>());
        var worldStreamer = Ensure(root, ComponentRegistry.Find<WorldStreamer>());
        var toolManager = Ensure(root, ComponentRegistry.Find<ToolManager>());
        var existingPlayer = ComponentRegistry.Find<PlayerController>();
        PlayerController playerController;
        if (existingPlayer != null)
        {
            playerController = existingPlayer;
            UnityEngine.Object.DontDestroyOnLoad(playerController.gameObject);
        }
        else
        {
            // Keep the synthesized player on its own object: root hosts the shared singletons
            // (incl. NewWorldSystems/AudioManager) and must never be the player, otherwise
            // those managers writing to their own transform would teleport the player.
            var playerObject = new GameObject("Player");
            playerController = playerObject.AddComponent<PlayerController>();
            UnityEngine.Object.DontDestroyOnLoad(playerObject);
        }
        var mainMenuController = Ensure(root, ComponentRegistry.Find<MainMenuController>());
        var saveManager = Ensure(root, ComponentRegistry.Find<SaveManager>());
        var soundManager = Ensure(root, ComponentRegistry.Find<SoundManager>());
        var questManager = Ensure(root, ComponentRegistry.Find<QuestManager>());
        var cutsceneManager = Ensure(root, ComponentRegistry.Find<CutsceneManager>());
        var randomEventManager = Ensure(root, ComponentRegistry.Find<RandomEventManager>());
        var wifeNPC = Ensure(root, ComponentRegistry.Find<WifeNPC>());
        var mobileInput = Ensure(root, ComponentRegistry.Find<MobileInputController>());
        var sleepManager = Ensure(root, ComponentRegistry.Find<SleepManager>());
        var karmaManager = Ensure(root, ComponentRegistry.Find<KarmaManager>());
        var religionManager = Ensure(root, ComponentRegistry.Find<ReligionManager>());
        var skillManager = Ensure(root, ComponentRegistry.Find<SkillManager>());
        var friendshipManager = Ensure(root, ComponentRegistry.Find<FriendshipManager>());
        var fishingProgression = Ensure(root, ComponentRegistry.Find<FishingProgression>());
        var chestStorageManager = Ensure(root, ComponentRegistry.Find<ChestStorageManager>());
        var typingMinigame = Ensure(root, ComponentRegistry.Find<TypingMinigame>());
        var farmingManager = Ensure(root, ComponentRegistry.Find<FarmingManager>());

        gameManager.UIManager = uiManager;
        gameManager.WorldBuilder = worldBuilder;
        gameManager.ToolManager = toolManager;
        gameManager.Player = playerController;
        gameManager.CutsceneManager = cutsceneManager;
        gameManager.RandomEventManager = randomEventManager;
        gameManager.KarmaManager = karmaManager;
        gameManager.ReligionManager = religionManager;

        // Boot-critical (frame 0): the HUD panels the player reads immediately and the tool
        // catalog. The remaining manager setups are queued to BootInitDeferrer and trickle in
        // over the next frames instead of stalling the first rendered frame (1e5). Note the
        // idempotency guards (1e5) mean UIManager.InitializeUI / ToolManager.Initialize /
        // SoundManager.LoadSoundClips each actually run once even though UIManager.Start and
        // GameManager.AutoResolveReferences call them again.
        uiManager.InitializeUI();
        toolManager.Initialize(uiManager, worldBuilder);

        // --- Open-world chunk streaming -------------------------------------------------
        // The new seed/coordinate open world. If a default ground material is not
        // configured, create a simple lit one so generated chunks render.
        if (worldStreamer.RenderDistance == null)
        {
            var rd = ScriptableObject.CreateInstance<RenderDistanceController>();
            // 1ef: ~2,000 m horizon (67 chunks). Real ChunkObjects only fill NearRingRadius (9);
            // the far shell (WorldStreamer.FarShell.cs) covers the rest as coarse sectors.
            rd.Radius = 67;
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

        // --- Testing ground (weapons, enemies, skills, NPCs) ----------------------------
        // Resolved BEFORE the spawn-chunk decision (1e5). The floating platform is the DEFAULT
        // spawn, so building a ground chunk synchronously under it spent 5-20ms of frame-0 budget
        // on ground never seen. The synchronous boot chunk is kept as the landing ground for the
        // non-platform fallback path only; platform spawns stream their chunks normally from frame 1.
        var testGround = Ensure(root, ComponentRegistry.Find<NewWorldTestGround>());
        var spawnOnPlatform = testGround != null && testGround.CreatePlatform && testGround.IsArenaReady;

        if (!spawnOnPlatform)
        {
            TerrainChunkCoord spawnChunk = TerrainChunkCoord.FromTile(new ChunkCoord(0, -10));
            worldStreamer.GenerateChunkSync(spawnChunk);
        }

        if (playerController != null)
        {
            Vector3 spawn = spawnOnPlatform
                ? testGround.GetSpawnPoint()
                : new Vector3(0f, TerrainNoiseGenerator.GetHeight(worldStreamer.Seed, 0.5f, -9.5f) + 2f, -10f);
            playerController.TeleportTo(spawn);
        }

        worldStreamer.SetFocus(playerController != null ? playerController.transform : null);

        // --- Phase 8/9 UI, LOD, culling, pooling ---------------------------------------
        Ensure(root, ComponentRegistry.Find<NewWorldSystems>());
        // 1e6: generic transient pool (impact VFX, excavation debris). Created on the GameRoot so
        // SpawnTransient call sites never have to worry about a missing singleton.
        Ensure(root, ComponentRegistry.Find<ObjectPooler>());

        // --- Deferred initializers (1e5) -------------------------------------------------
        // Non-critical manager setup that used to run synchronously in this method. Runs after
        // the first rendered frame; original dependency order preserved (bag/tool -> save; HUD
        // canvas -> menu/cutscene/wife). quest/karma/religion are deliberately NOT queued here —
        // GameManager.Start -> StartNewGame already re-initializes them on frame 1.
        var deferrer = root.AddComponent<BootInitDeferrer>();
        deferrer.Queue(new[]
        {
            // Frame 1: menus, save system, quest state, cutscene/random-event wiring.
            new Action(() =>
            {
                mainMenuController.InitializeMenu(gameManager);
                saveManager.Initialize(gameManager, toolManager, worldBuilder, uiManager, questManager);
                questManager.InitializeQuests();
                cutsceneManager.Initialize(uiManager);
                randomEventManager.Initialize(uiManager);
            }),
            // Frame 2: wife NPC (needs the HUD canvas) + remaining lightweight manager setup.
            new Action(() =>
            {
                wifeNPC.Initialize(uiManager.GetCanvas());
                wifeNPC.LoadState();
                skillManager.Initialize();
                friendshipManager.Initialize();
                fishingProgression.Initialize();
                chestStorageManager.Initialize();
            }),
        });

        // Kept locals alive for the deferred closures/singleton guarantees (suppress unused).
        _ = mobileInput;
        _ = sleepManager;
        _ = soundManager;
        _ = typingMinigame;
        _ = farmingManager;
    }

    /// <summary>Reuse an existing singleton or create it on the boot root and cache it (1e5).</summary>
    private static T Ensure<T>(GameObject root, T existing) where T : Component
    {
        if (existing != null)
            return existing;
        var created = root.AddComponent<T>();
        ComponentRegistry.Cache(created);
        return created;
    }
}
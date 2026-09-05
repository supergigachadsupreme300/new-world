using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Phase 8/9 bootstrap: single entry-point MonoBehaviour. Drop this ONE component on a GameObject
/// in the Hierarchy and it auto-builds the UI/UX overlays (Phase 8) plus the optimization managers
/// (Phase 9) on the same object — no per-component scene wiring required. Every subsystem is opted
/// in/out with a serialized toggle; the running registration of chunk LOD/culling candidates is
/// optimized to a time-throttled delta scan (low cadence, O(changed), no per-frame sweeps).
/// </summary>
public sealed class NewWorldSystems : MonoBehaviour
{
    [Header("Phase 8 - UI/UX")]
    public bool EnableHUD = true;
    public bool EnableMenus = true;
    public bool EnableInteraction = true;
    [Tooltip("Hide the legacy UIManager stat-text + inventory-slot HUD, which the new HUD (bars, skill bar, menus) replaces. Message/crosshair/quest panels are kept.")]
    public bool UseNewHud = true;
    [Tooltip("Multiplayer overlay is a specialized HUD indicator; disable to avoid an idle poller by default.")]
    public bool EnableMultiplayerIndicator = false;

    [Header("Phase 9 - Optimization")]
    public bool EnableObjectPooler = true;
    public bool EnableAudio = true;
    public bool EnableLod = true;
    public bool EnableCulling = true;
    [Tooltip("Also register POI/enemy roots as culling candidates. More thorough but costs a FindObjectsOfType sweep.")]
    public bool IncludePoisAsCullCandidates = false;

    [Header("Registration sync")]
    [Tooltip("Seconds between chunk LOD/culling registration re-syncs (delta-diff, not a full sweep).")]
    public float RegSyncInterval = 0.5f;

    private WorldStreamer _streamer;
    private ChunkLodManager _lod;
    private CullManager _cull;
    private readonly Dictionary<ChunkCoord, ChunkObject> _registered = new Dictionary<ChunkCoord, ChunkObject>();
    private float _syncTimer;

    /// <summary>The lazily-created shared ObjectPooler instance.</summary>
    public ObjectPooler Pool => ObjectPooler.Instance;

    private void Awake()
    {
        if (EnableObjectPooler) Ensure<ObjectPooler>();
        if (EnableAudio) Ensure<AudioManager>();

        if (EnableHUD)
        {
            Ensure<PlayerBarsHUD>();
            Ensure<CompassMinimapHUD>();
            Ensure<SkillBarHUD>();
            Ensure<EnemyHealthBarHUD>();
            if (EnableMultiplayerIndicator) Ensure<MultiplayerIndicatorHUD>();
        }

        if (EnableMenus)
        {
            Ensure<EquipmentSystem>();
            Ensure<CharacterCreationUI>();
            Ensure<RaceStatSheetUI>();
            Ensure<InventoryEquipmentUI>();
            Ensure<WorldMapUI>();
            Ensure<MultiplayerBrowserUI>();
            Ensure<CharacterInfoUI>();
        }

        if (EnableInteraction)
        {
            Ensure<ContextPromptUI>();
            Ensure<NpcDialogueUI>();
        }

        if (EnableLod) _lod = Ensure<ChunkLodManager>();
        if (EnableCulling) _cull = Ensure<CullManager>();

        // Focus the streaming system on the player if the world bootstrap did not already.
        _streamer = Object.FindAnyObjectByType<WorldStreamer>();
        if (_streamer != null && GameManager.Instance?.Player != null)
            _streamer.SetFocus(GameManager.Instance.Player.transform);

        // New-HUD mode: hide the legacy stat-text + inventory-slot overlay that the new HUD
        // duplicates. GameManager.StartNewGame re-shows legacy elements, which the UIManager now
        // defers while this flag is on, so a one-time call here is sufficient.
        if (UseNewHud && (EnableHUD || EnableMenus))
            GameManager.Instance?.UIManager?.SetNewWorldHudMode(true);
    }

    private void Update()
    {
        if (!EnableLod && !EnableCulling)
            return;

        _syncTimer += Time.deltaTime;
        if (RegSyncInterval > 0f && _syncTimer < RegSyncInterval)
            return;
        _syncTimer = 0f;

        SyncChunkRegistration();
    }

    private void SyncChunkRegistration()
    {
        if (_streamer == null)
            _streamer = Object.FindAnyObjectByType<WorldStreamer>();
        if (_streamer == null)
            return;

        var loaded = _streamer.Loaded;
        if (loaded == null)
            return;

        // Delta-diff on the loaded chunk set; runs in O(changed), not a full sweep.
        // Added chunks: register their root into LOD + culling.
        foreach (var pair in loaded)
        {
            ChunkCoord coord = pair.Key;
            ChunkObject obj = pair.Value;
            if (obj == null) continue;
            if (!_registered.ContainsKey(coord))
            {
                _lod?.RegisterChunk(obj.gameObject);
                // Terrain chunks must NOT be registered with CullManager —
                // occlusion raycasts incorrectly hide distant-but-visible terrain.
                // Only discrete objects (NPCs, buildings) are culled.
                _registered[coord] = obj;
            }
        }

        // Removed chunks: unregister the previously-tracked object.
        if (_registered.Count != loaded.Count)
        {
            List<ChunkCoord> removed = null;
            foreach (var kv in _registered)
            {
                if (!loaded.ContainsKey(kv.Key))
                {
                    if (removed == null) removed = new List<ChunkCoord>();
                    removed.Add(kv.Key);
                }
            }
            if (removed != null)
            {
                foreach (var coord in removed)
                {
                    ChunkObject obj = _registered[coord];
                    _registered.Remove(coord);
                    if (obj != null)
                    {
                        _lod?.UnregisterChunk(obj.gameObject);
                    }
                }
            }
        }

        if (IncludePoisAsCullCandidates && _cull != null)
        {
            foreach (var town in Object.FindObjectsByType<Town>(FindObjectsSortMode.None))
                if (town != null) _cull.AddCandidate(town.gameObject);
            foreach (var node in Object.FindObjectsByType<FastTravelNode>(FindObjectsSortMode.None))
                if (node != null) _cull.AddCandidate(node.gameObject);
            foreach (var dungeon in Object.FindObjectsByType<DungeonSystem>(FindObjectsSortMode.None))
                if (dungeon != null) _cull.AddCandidate(dungeon.gameObject);
            foreach (var enemy in Object.FindObjectsByType<EnemyController>(FindObjectsSortMode.None))
                if (enemy != null) _cull.AddCandidate(enemy.gameObject);
        }
    }

    /// <summary>
    /// Reuse-or-create a component on this object (idempotent; mirrors the GameBootstrap pattern).
    /// </summary>
    private T Ensure<T>() where T : Component
    {
        return Object.FindAnyObjectByType<T>() ?? gameObject.AddComponent<T>();
    }

    // ---- Menu accessors (no force-opening at startup) ----

    public void ShowCharacterCreation() => Menu<CharacterCreationUI>()?.Show();
    public void ShowRaceStatSheet() => Menu<RaceStatSheetUI>()?.Show();
    public void ShowInventory() => Menu<InventoryEquipmentUI>()?.Show();
    public void ShowWorldMap() => Menu<WorldMapUI>()?.Show();
    public void ShowMultiplayerBrowser() => Menu<MultiplayerBrowserUI>()?.Show();
    public void ShowCharacterInfo() => Menu<CharacterInfoUI>()?.Show();

    private T Menu<T>() where T : MenuPanelBase
    {
        return Object.FindAnyObjectByType<T>();
    }
}
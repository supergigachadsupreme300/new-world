using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public partial class PlayerController : MonoBehaviour, IHealable
{
    public float MoveSpeed = 5f;
    public float SprintMultiplier = 2f;
    public float Gravity = -9.81f;
    public float JumpHeight = 1.5f;
    public int HP = 100;

    public float Stamina = 1000f;

    public float StaminaRegenRate = 4f;
    public float StaminaRegenMultiplier = 1f;
    public float StaminaRegenModifier = 1f;
    public float SprintCost = 35f;
    public long Money = 1000;
public bool IgnoreInput { get; private set; }

    private int _cachedFightSlot = -1;

    private CharacterController _controller;
    private Vector3 _velocity;

    // Per-frame component caches (1dr): the player root ran GetComponent/graph scans ~14x/frame
    // across Movement/Combat/Stamina/HUD reads. All of these components are permanent on the player
    // root (stamina rig + PlayerStats from build, CombatController/class passives from WeaponRigBuilder/
    // race systems, the camera made in Awake), so each lazy look-up caches forever after first use.
    private PlayerStats _statsCached;
    private CombatController _combatCached;
    private ClassPassiveManager _classPassivesCached;
    private SpellCaster _casterCached;
    private Camera _mainCamCached;

    private PlayerStats StatsCached => _statsCached != null ? _statsCached : _statsCached = GetComponent<PlayerStats>();
    private CombatController CombatCached => _combatCached != null ? _combatCached : _combatCached = GetComponent<CombatController>();
    private ClassPassiveManager ClassPassivesCached => _classPassivesCached != null ? _classPassivesCached : _classPassivesCached = GetComponent<ClassPassiveManager>();
    private SpellCaster SpellCasterRef => _casterCached != null ? _casterCached : _casterCached = GetComponent<SpellCaster>();
    private Camera MainCam => _mainCamCached != null ? _mainCamCached : _mainCamCached = Camera.main;
    // Character Info tab sheet (1ee): cached like the rest so Tab open/close never runs a per-press
    // scene scan. A destroyed object nulls out and re-finds itself on the next Tab.
    private CharacterInfoUI _charInfoCached;
    private CharacterInfoUI CharacterInfoRef => _charInfoCached != null ? _charInfoCached : _charInfoCached = Object.FindAnyObjectByType<CharacterInfoUI>();
    // Physics-integrity fail-net (1ca): a corrupted collider (NaN/garbage mesh height) can
    // depenetrate the CharacterController thousands of metres in one step. _lastSafePosition
    // holds the last sane position; EnforcePhysicsSanity reverts any such launch.
    private Vector3 _lastSafePosition;
    private bool _hadSafePosition;

    /// <summary>The effective movement speed (m/s) resolved last frame by HandleMovement — lets the
    /// fail-net (1cc) size its one-frame tolerance to fast-but-legit movement instead of assuming a
    /// ~14 m/s dodge.</summary>
    private float _lastEffectiveSpeed;
    private const float MaxSanityStepMeters = 150f;
    private Transform _cameraPivot;
    private float _yaw;
    private float _pitch;
    private PlayerSitController _sitController;
    private GameObject _playerModelInstance;
    private bool _raceSubscribed;
    private readonly List<(string id, bool isLeft)> _pendingAutoRig = new List<(string id, bool isLeft)>();
    private float _waterSpeedMul = 1f;
    private bool _waterAllowJump = true;
    private float _staminaRegenModifierUntil = 0f;
    private int _jumpFrame = -100;

    private bool _dodging;
    private float _dodgeTimer;
    private float _invulnerableUntil;
    private const float DodgeDuration = 0.25f;
    private const float DodgeSpeed = 14f;
    private const float DodgeIFrameDuration = 0.3f;
    public float DodgeCost = 20f;

    // Class skills (§3.2.1): stealth + aura buff state.
    private float _stealthUntil;
    private Renderer[] _stealthRenderers;
    private float _classBuffUntil;
    private float _classBuffDamageReduction;
    private float _classBuffHpRegenPerSecond;

    // Flight (Wind Walk spell, §3.8): timed free vertical movement gated in HandleMovement.
    private float _flightUntil;

    public float FlightSpeed = 12f;
    public float FlightVerticalSpeed = 6f;

    // Aim/charge (armed magic via the Alt wheel, or any ranged weapon): hold LMB to aim only,
    // hold RMB to charge/draw (releasing RMB freezes the built level, re-holding resumes), and
    // release LMB to fire at the current level. Mobile taps still cast instantly.
    private const float MagicChargeTapThreshold = 0.15f;
    private const float MagicChargeMaxTime = 2f;
    private const float MagicChargeFullTime = 1.2f;
    private bool _aiming;
    private bool _chargeRmbHeld;
    private float _chargeAccum;
    private float _chargeDrained;
    private AoeAimPreview _aoePreview;
    private CastingCircle _castingCircle;
    private ProjectilePathPreview _pathPreview;
    private CameraModeSwitch _cameraMode;

    // Per-hand dual-wield charge accumulators (ranged in one of the two hands keeps its draw).
    private float _dualChargeL;
    private bool _dualChargeActiveL;
    private float _dualChargeR;
    private bool _dualChargeActiveR;

    private void Awake()
    {
        EnsurePlayerPhysics();

        // Ensure the player camera exists and will follow this player.
        if (Camera.main == null)
        {
            CreateCamera();
        }
        SetupPlayerCamera();

        // Ensure the camera has exactly one audio listener
        var cameraObj = Camera.main?.gameObject;
        if (cameraObj != null && cameraObj.GetComponent<AudioListener>() == null)
            cameraObj.AddComponent<AudioListener>();

        LoadPlayerModel();

        // The Alt magic wheel is a persistent HUD singleton: instantiate it now so its
        // Update poll runs on every frame (nothing else has an interaction site to lazy-create it).
        MagicWheelUI.Ensure();
    }

    private void EnsurePlayerPhysics()
    {
        _controller = GetComponent<CharacterController>();
        if (_controller == null)
            _controller = gameObject.AddComponent<CharacterController>();

        if (_controller != null)
        {
            // sensible defaults so the player collides with geometry and can move
            _controller.skinWidth = 0.08f;
            _controller.stepOffset = 0.5f;
            _controller.minMoveDistance = 0.001f;
            _controller.radius = Mathf.Max(0.3f, _controller.radius);
            _controller.height = _controller.height < 1.2f ? 1.8f : _controller.height;
            _controller.center = new Vector3(0f, _controller.height * 0.5f, 0f);
        }

        if (GetComponent<Rigidbody>() != null)
        {
            Debug.LogWarning("[PlayerController] Rigidbody detected on player. CharacterController movement is used instead. Remove Rigidbody to avoid physics conflicts.");
        }
    }

    public bool AutoEnableInput = true;

    private void Start()
    {
        StaminaRegenRate = 4f;
        StaminaRegenMultiplier = 1f;
        StaminaRegenModifier = 1f;
        _staminaRegenModifierUntil = 0f;
        ResetPlayer();
        // Allow developer to enable input automatically for quick testing.
        EnableInput(AutoEnableInput);
        if (GameManager.Instance != null)
            GameManager.Instance.Player = this;

        var promptGO = new GameObject("InteractionPrompt");
        var prompt = promptGO.AddComponent<InteractionPrompt>();
        var uiMgr = GameManager.Instance?.UIManager;
        prompt.Initialize(
            uiMgr?.GetEKeyPromptText(),
            uiMgr?.GetLmbPromptText()
        );

        if (GetComponent<PlayerSitController>() == null)
            _sitController = gameObject.AddComponent<PlayerSitController>();
    }

    private void Update()
    {
        if (GameManager.Instance != null && GameManager.Instance.GamePaused)
            return;
        if (SleepManager.IsSleeping)
            return;

        // 1cd: the percent-perk fix lowers the maxima, so a stored/legacy HP or Stamina value can
        // exceed them. Snap down so the HUD never shows over-max (heal/regen clamp on their own).
        if (HP > MaxHP) HP = MaxHP;
        if (Stamina > MaxStamina) Stamina = MaxStamina;

        // Fail-net (1ca): a corrupted collider can depenetrate the CharacterController thousands
        // of metres in one step. Revert to the last sane position before any further input runs.
        EnforcePhysicsSanity();

        // A new-world modal menu is open: only process menu-management keys (Tab closes
        // Character Info; Escape closes the topmost panel via MenuPanelBase.Update).
        if (MenuPanelBase.AnyShown)
        {
            if (Keyboard.current != null && Keyboard.current.tabKey.wasPressedThisFrame)
            {
                var info = CharacterInfoRef;
                if (info != null && info.IsShown)
                    info.Close();
            }
            return;
        }

        if (IsSitting)
        {
            HandleMouseLook();
            _sitController.UpdateSitting();
            UpdateHud();
            return;
        }

        if (IgnoreInput)
            return;

        HandleMouseLook();
        HandleMovement();
        HandleStamina();
        UpdateClassState();
        HandleInteractionKeys();
        UpdateHud();
    }

    public void ResetPlayer()
    {
        HP = MaxHP;
        Stamina = MaxStamina;
        Money = 1000;
        var testGround = Object.FindAnyObjectByType<NewWorldTestGround>();
        if (testGround != null && testGround.IsArenaReady)
        {
            // Default spawn is the test ground since 1dn (new game / death reset): the platform is
            // built synchronously in Awake, so this is never a void. The boot chunk is the fallback
            // when the test platform isn't built.
            TeleportTo(testGround.GetSpawnPoint());
        }
        else
        {
            // Fallback spawn: the world's boot chunk. GameBootstrap generates the tile under
            // (0, -10) synchronously, so the player never falls into the void.
            TeleportTo(BootSpawnPosition());
        }
        transform.rotation = Quaternion.identity;
        _velocity = Vector3.zero;
        ClearSpawnOverlap();
    }

    /// <summary>The world's boot-chunk spawn point: above the terrain under (0, -10).</summary>
    private Vector3 BootSpawnPosition()
    {
        var streamer = Object.FindAnyObjectByType<WorldStreamer>();
        long spawnSeed = streamer != null ? streamer.Seed : 1337;
        float terrainY = TerrainNoiseGenerator.GetHeight(spawnSeed, 0f, -10f);
        return new Vector3(0f, terrainY + 3f, -10f);
    }

    /// <summary>Teleports the player and records the destination as the fail-net's last safe
    /// position, so intentional teleports (spawn, fast travel, sleep, load) never trip it.</summary>
    public void TeleportTo(Vector3 destination)
    {
        transform.position = destination;
        _lastSafePosition = destination;
        _hadSafePosition = true;
        _velocity = Vector3.zero;
        if (_controller != null)
            Physics.SyncTransforms();
    }

    /// Below the world floor (1gg): the 5-octave noise band is ~±82.6 m and deformation clamps inside
    /// the ±200 m sanity band, so any real surface lives above -300 — crossing it means a
    /// missing-collider void fall, and the player is reverted (see <see cref="EnforcePhysicsSanity"/>).
    private const float VoidFallFloor = -300f;

    /// <summary>
    /// Physics-integrity fail-net (1ca/1cc). Reverts the player to the last sane position when a
    /// single frame moved them farther than the tolerable step: at least 150 m, scaled up by the
    /// last frame's effective speed (max(speed×1.5, 150)) so fast-but-legit movement during frame
    /// hitches never trips it, while every corrupted-collider depenetration launch (thousands of
    /// metres) still does. Non-finite coordinates always revert. Falling below the world floor
    /// (missing-collider void, 1gg) also reverts.
    /// </summary>
    private void EnforcePhysicsSanity()
    {
        Vector3 p = transform.position;
        if (!float.IsFinite(p.x) || !float.IsFinite(p.y) || !float.IsFinite(p.z))
        {
            Vector3 safe = _hadSafePosition ? _lastSafePosition : BootSpawnPosition();
            Debug.LogWarning($"[PlayerController] Non-finite position {p} — restored to {safe}.");
            TeleportTo(safe);
            return;
        }

        // (1gg) Void-fall rescue: a chunk whose MeshCollider is missing (deferred-collider bug under
        // a heavy sprint) lets the controller sink straight through the visible ground and never
        // stop — the motion is gradual, so the blast tolerance above never trips. Real surfaces never
        // live below here: the 5-octave noise band is ~±82.6 m and deformation is clamped inside the
        // ±200 m sanity band, so crossing -300 means the player is in the void. Revert to the last
        // safe position before the drop instead of falling forever.
        if (p.y < VoidFallFloor)
        {
            Vector3 safe = _hadSafePosition ? _lastSafePosition : BootSpawnPosition();
            Debug.LogWarning($"[PlayerController] Fell below the world floor ({VoidFallFloor:0}) at {p} — restored to {safe}.");
            TeleportTo(safe);
            return;
        }

        if (_hadSafePosition)
        {
            // Tolerance is speed-aware (1cc): fast-but-legit movement (e.g. a buffed sprint during a
            // frame hitch of ~0.5-1 s) must not be mistaken for a corrupted-collider blast. The 150 m
            // floor still catches every real depenetration launch (those are thousands of metres).
            float threshold = Mathf.Max(MaxSanityStepMeters, _lastEffectiveSpeed * 1.5f);
            float ds = (_lastSafePosition - p).sqrMagnitude;
            float maxSqr = threshold * threshold;
            if (ds > maxSqr)
            {
                LogSanityBlast(p, threshold);
                TeleportTo(_lastSafePosition);
                return;
            }
        }

        _lastSafePosition = p;
        _hadSafePosition = true;
    }

    /// <summary>Logs the blast and scans nearby colliders for corrupted (non-finite / oversized)
    /// bounds, so the culprit chunk can be identified and fixed in one targeted follow-up.</summary>
    private void LogSanityBlast(Vector3 blastPos, float threshold)
    {
        var streamer = Object.FindAnyObjectByType<WorldStreamer>();
        float terrain = streamer != null
            ? TerrainNoiseGenerator.GetHeight(streamer.Seed, blastPos.x, blastPos.z)
            : 0f;
        string msg = $"[PlayerController] Physics blast restored to last safe position. " +
            $"Blast pos {blastPos} (local terrain height there ~{terrain:F2}). " +
            $"Last safe {_lastSafePosition}. Suspicious colliders:";
        bool any = false;

        void Probe(Vector3 origin, float radius, float maxExtent)
        {
            Collider[] hits = Physics.OverlapSphere(origin, radius, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            if (hits == null)
                return;
            foreach (Collider c in hits)
            {
                if (c == null || c.transform == null)
                    continue;
                Vector3 max = c.bounds.max;
                Vector3 min = c.bounds.min;
                if (!float.IsFinite(max.x) || !float.IsFinite(max.y) || !float.IsFinite(max.z) ||
                    !float.IsFinite(min.x) || !float.IsFinite(min.y) || !float.IsFinite(min.z) ||
                    Mathf.Abs(max.y) > maxExtent)
                {
                    msg += $"\n  {c.name} bounds {min} .. {max}";
                    any = true;
                }
            }
        }

        Probe(_lastSafePosition, threshold, 400f);
        Probe(blastPos, 50f, 400f);
        if (!any)
            msg += " none found in a radius sweep.";
        Debug.LogWarning(msg);
    }

    /// <summary>
    /// Guarantees the spawn volume is empty so the CharacterController is never
    /// depenetration-pushed upward at start. If anything overlaps the capsule after
    /// teleporting, lift the player just above the highest offending collider.
    /// </summary>
    private void ClearSpawnOverlap()
    {
        if (_controller == null)
            return;

        Physics.SyncTransforms();

        Vector3 center = transform.position + _controller.center;
        Vector3 half = Vector3.up * (_controller.height * 0.5f);
        float radius = Mathf.Max(0.1f, _controller.radius);

        Collider[] overlaps = Physics.OverlapCapsule(
            center - half, center + half, radius,
            Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        if (overlaps == null || overlaps.Length == 0)
            return;

        float highest = float.MinValue;
        Collider worst = null;
        foreach (Collider col in overlaps)
        {
            if (col == null || col.transform == null || col.transform.IsChildOf(transform))
                continue;
            if (col.bounds.max.y > highest)
            {
                highest = col.bounds.max.y;
                worst = col;
            }
        }

        if (worst == null)
            return;

        float shieldY = highest + 1.2f;
        transform.position = new Vector3(transform.position.x, shieldY, transform.position.z);
        Debug.LogWarning($"[PlayerController] Spawn overlapped collider '{worst.name}' (top {highest:F2}); lifted player to {shieldY:F2} to avoid depenetration push.");
        Physics.SyncTransforms();
    }

    public void EnableInput(bool enabled)
    {
        IgnoreInput = !enabled;
        GameInput.SetCursorLocked(enabled);
        if (GameManager.Instance != null)
            GameManager.Instance.UIManager?.SetCrosshairVisible(enabled);
    }

    private void UpdateHud()
    {
        if (GameManager.Instance != null && GameManager.Instance.UIManager != null)
            GameManager.Instance.UIManager.UpdatePlayerHud(HP, MaxHP, Stamina, MaxStamina, Money);
    }

}

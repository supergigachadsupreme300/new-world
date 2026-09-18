using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour, IHealable
{
    public float MoveSpeed = 5f;
    public float SprintMultiplier = 2f;
    public float RideSpeed = 13f;
    public float Gravity = -9.81f;
    public float JumpHeight = 1.5f;
    public int HP = 100;

    /// <summary>Max health (HP cap). Reads the current <see cref="PlayerStats"/> maximum
    /// (Health-scaled + tree perks §3.3) when available; falls back to 100 before stats are rigged.</summary>
    public int MaxHP
    {
        get
        {
            var stats = GetComponent<PlayerStats>();
            return stats != null ? Mathf.Max(1, Mathf.RoundToInt(stats.MaxHP)) : 100;
        }
    }

    public float Stamina = 1000f;

    /// <summary>Max stamina (stamina cap). Reads the current <see cref="PlayerStats"/> maximum
    /// (Endurance-scaled + tree perks §3.3) when available; falls back to 1000 before stats are rigged.</summary>
    public float MaxStamina
    {
        get
        {
            var stats = GetComponent<PlayerStats>();
            return stats != null ? stats.MaxStamina : 1000f;
        }
    }
    public float StaminaRegenRate = 4f;
    public float StaminaRegenMultiplier = 1f;
    public float StaminaRegenModifier = 1f;
    public float SprintCost = 35f;
    public long Money = 1000;
    public bool IgnoreInput { get; private set; }

    public bool FightingMode { get; private set; }
    private int _cachedFightSlot = -1;

    public bool InWater { get; private set; }

    public bool IsRiding => HorseMount.Instance != null && HorseMount.Instance.IsMounted;

    public bool IsMoving
    {
        get
        {
            if (_controller == null)
                return false;
            var v = _controller.velocity;
            return new Vector2(v.x, v.z).magnitude > 0.5f;
        }
    }

    private CharacterController _controller;
    private Vector3 _velocity;
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
    public bool IsFlying => Time.time < _flightUntil;
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

    public void SetInWater(bool inWater, float speedMul, bool allowJump)
    {
        InWater = inWater;
        _waterSpeedMul = inWater ? speedMul : 1f;
        _waterAllowJump = inWater ? allowJump : true;
    }

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

    public bool IsSitting => _sitController != null && _sitController.IsSitting;

    public void SnapLookYaw(float yaw)
    {
        _yaw = yaw;
        transform.rotation = Quaternion.Euler(0f, _yaw, 0f);
    }

    private bool TrySitNearby()
    {
        if (_sitController == null)
            return false;
        var seat = SittableSeat.FindNearest(transform.position, 2.6f);
        if (seat == null)
            return false;
        _sitController.BeginSit(seat);
        return true;
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
                var info = Object.FindAnyObjectByType<CharacterInfoUI>();
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
        if (testGround != null && testGround.IsArenaReady && IsOnOrNearArena(testGround))
        {
            // The player has already walked to the test platform — re-home them onto its top
            // instead of yanking them elsewhere. Boot never auto-teleports here (the pad is
            // opt-in via NewWorldTestGround.AutoTeleportPlayerOnStart).
            TeleportTo(testGround.GetSpawnPoint());
        }
        else
        {
            // Default spawn: the world's boot chunk. GameBootstrap generates the tile under
            // (0, -10) synchronously, so the player never falls into the void; the platform
            // (NewWorldTestGround) stays where it is for the player to walk to.
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

    /// <summary>
    /// Physics-integrity fail-net (1ca/1cc). Reverts the player to the last sane position when a
    /// single frame moved them farther than the tolerable step: at least 150 m, scaled up by the
    /// last frame's effective speed (max(speed×1.5, 150)) so fast-but-legit movement during frame
    /// hitches never trips it, while every corrupted-collider depenetration launch (thousands of
    /// metres) still does. Non-finite coordinates always revert.
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

    /// <summary>True when the player has actually reached the test platform (stands on or within a
    /// few metres of its top surface). Keeps a respawn on the bench only for players already there,
    /// never yanks the player onto the pad from the open world.</summary>
    private bool IsOnOrNearArena(NewWorldTestGround testGround)
    {
        if (NewWorldTestGround.PlatformTopY == float.MinValue)
            return false;
        Vector3 p = transform.position;
        Vector3 c = testGround.PlatformCenter;
        float margin = testGround.PlatformSize * 0.6f;
        bool nearXZ = Mathf.Abs(p.x - c.x) <= margin && Mathf.Abs(p.z - c.z) <= margin;
        bool onLevel = Mathf.Abs(p.y - NewWorldTestGround.PlatformTopY) <= 6f;
        return nearXZ && onLevel;
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

    public void SetLookRotation(float yaw, float pitch)
    {
        _yaw = yaw;
        _pitch = pitch;
        transform.rotation = Quaternion.Euler(0f, _yaw, 0f);
        if (_cameraPivot != null)
            _cameraPivot.localRotation = Quaternion.Euler(_pitch, 0f, 0f);
    }

    /// <summary>Current vertical look pitch (°; positive = looking down, negative = up).
    /// Used by the procedural torso/upper-body animation to bend with the camera.</summary>
    public float LookPitch => _pitch;

    public void TakeDamage(int amount)
    {
        if (HP <= 0) return;
        if (Time.time < _invulnerableUntil) return;
        // Aura buff: flat damage reduction from class aura skills (clamped to sane bounds).
        if (_classBuffDamageReduction > 0f && Time.time < _classBuffUntil)
            amount = Mathf.RoundToInt(amount * (1f - Mathf.Min(_classBuffDamageReduction, 0.5f)));
        // Skill-tree perks: flat damage reduction stacks on top (§3.3).
        var pStats = GetComponent<PlayerStats>();
        if (pStats != null)
            amount -= Mathf.RoundToInt(amount * Mathf.Min(pStats.DamageReductionPerkFlat, 0.45f));
        // Melee guard: blocking absorbs 80% of the hit while stamina holds; if stamina runs out
        // the guard breaks and the full hit lands.
        var combat = GetComponent<CombatController>();
        if (combat != null && combat.IsBlocking)
        {
            if (combat.OnBlockedHit(amount))
            {
                amount = Mathf.RoundToInt(amount * combat.BlockTakenMultiplier);
                if (amount <= 0) return;
            }
        }
        HP -= amount;
        if (HP <= 0)
        {
            HP = 0;
            Debug.Log("Player died");
            GameManager.Instance?.TriggerPlayerDeath();
        }
        GameManager.Instance?.UIManager?.UpdatePlayerHud(HP, MaxHP, Stamina, MaxStamina, Money);
    }

    /// <summary>Restore HP up to max (class heal skills §3.2.1, consumables, miracles).</summary>
    public void Heal(int amount)
    {
        if (HP <= 0 || amount <= 0) return;
        HP = Mathf.Min(MaxHP, HP + amount);
        GameManager.Instance?.UIManager?.UpdatePlayerHud(HP, MaxHP, Stamina, MaxStamina, Money);
    }

    /// <summary>
    /// Stealth state — enemies ignore the player while active (Rogue "Vanish").
    /// </summary>
    public bool IsInvisible => Time.time < _stealthUntil;

    /// <summary>Enter stealth for the given duration (hides renderers; enemies skip targeting).</summary>
    public void ActivateStealth(float seconds)
    {
        if (seconds <= 0f) return;
        if (_stealthRenderers == null || _stealthRenderers.Length == 0)
            _stealthRenderers = GetComponentsInChildren<Renderer>(true);
        if (Time.time < _stealthUntil) return; // already hidden
        _stealthUntil = Time.time + seconds;
        foreach (var r in _stealthRenderers)
            if (r != null) r.enabled = false;
    }

    /// <summary>Grant the class aura buff (damage reduction + passive HP regen) for the duration.</summary>
    public void ApplyClassBuff(float seconds, float damageReduction, float hpRegenPerSecond)
    {
        _classBuffUntil = Mathf.Max(_classBuffUntil, Time.time + Mathf.Max(seconds, 0f));
        _classBuffDamageReduction = Mathf.Max(_classBuffDamageReduction, damageReduction);
        _classBuffHpRegenPerSecond = Mathf.Max(_classBuffHpRegenPerSecond, hpRegenPerSecond);
    }

    /// <summary>Start flying for the given duration (free vertical movement, no gravity).</summary>
    public void BeginFlight(float seconds)
    {
        if (seconds <= 0f) return;
        _flightUntil = Mathf.Max(_flightUntil, Time.time + seconds);
    }

    /// <summary>End flight immediately; gravity resumes and the player falls/lands normally.</summary>
    public void EndFlight()
    {
        _flightUntil = 0f;
    }

    /// <summary>Tick stealth expiry + aura buff lifetime once per frame.</summary>
    private void UpdateClassState()
    {
        // Stealth expiry un-hides renderers.
        if (_stealthUntil > 0f && Time.time >= _stealthUntil)
        {
            _stealthUntil = 0f;
            if (_stealthRenderers != null)
                foreach (var r in _stealthRenderers)
                    if (r != null) r.enabled = true;
        }
        // Aura buff expiry resets both components together.
        if (_classBuffUntil > 0f && Time.time >= _classBuffUntil)
        {
            _classBuffUntil = 0f;
            _classBuffDamageReduction = 0f;
            _classBuffHpRegenPerSecond = 0f;
        }
    }

    public bool SpendStamina(float amount)
    {
        if (Stamina < amount)
            return false;
        Stamina -= amount;
        return true;
    }

    private void HandleMouseLook()
    {
        if (MagicWheelUI.IsOpen)
            return;
        Vector2 delta = Vector2.zero;
        if (!GameInput.IsMobile && Mouse.current != null)
            delta = Mouse.current.delta.ReadValue();
        if (GameInput.IsMobile)
            delta += MobileInputController.TakeLookDelta();

        if (delta == Vector2.zero)
            return;

        float sens = SettingsManager.MouseSensitivity;
        _yaw += delta.x * sens * 0.02f;
        _pitch -= delta.y * sens * 0.02f * (SettingsManager.InvertY ? -1f : 1f);
        _pitch = Mathf.Clamp(_pitch, -60f, 60f);

        transform.rotation = Quaternion.Euler(0f, _yaw, 0f);
        if (_cameraPivot != null)
            _cameraPivot.localRotation = Quaternion.Euler(_pitch, 0f, 0f);
    }

    private void HandleMovement()
    {
        bool dialogBlocked = (WifeNPC.Instance != null && WifeNPC.Instance.IsDialogActive) ||
                             (BuffaloDialog.Instance != null && BuffaloDialog.Instance.IsDialogActive) ||
                             (RichManNPC.Instance != null && RichManNPC.Instance.IsDialogActive) ||
                             (PoliceOfficerNPC.Instance != null && PoliceOfficerNPC.Instance.IsDialogActive) ||
                             (PagodaMonkNPC.Instance != null && PagodaMonkNPC.Instance.IsDialogActive) ||
                             (ChefNPC.Instance != null && ChefNPC.Instance.IsDialogActive) ||
                             (LibrarianNPC.Instance != null && LibrarianNPC.Instance.IsDialogActive) ||
                             (CraftingManager.Instance != null && CraftingManager.Instance.IsOpen);
        Vector2 input = dialogBlocked ? Vector2.zero : ReadMoveInput();
        Vector3 direction = new Vector3(input.x, 0f, input.y);
        float mag = direction.magnitude;
        if (mag > 1f)
        {
            direction /= mag;
            mag = 1f;
        }

        bool canSprint = !InWater && !IsRiding;
        bool flying = IsFlying;
        bool sprint = canSprint && !flying &&
            ((Keyboard.current != null && Keyboard.current.leftShiftKey.isPressed) ||
             (GameInput.IsMobile && MobileInputController.IsHeld("sprint"))) &&
            Stamina > 0f && mag > 0f;
        var playerStats = GetComponent<PlayerStats>();
        float moveSpeedPerkMult = playerStats != null && playerStats.BaseMoveSpeed > 0f
            ? playerStats.MaxMoveSpeed / playerStats.BaseMoveSpeed : 1f;
        float speed = IsRiding
            ? RideSpeed * _waterSpeedMul
            : (flying ? FlightSpeed : MoveSpeed * _waterSpeedMul * (sprint ? SprintMultiplier : 1f) * moveSpeedPerkMult);
        _lastEffectiveSpeed = speed;

        bool dodgePressed = !dialogBlocked && !IsRiding && !flying && _controller != null && _controller.isGrounded &&
            ((Keyboard.current != null && Keyboard.current.cKey.wasPressedThisFrame) ||
             (GameInput.IsMobile && MobileInputController.Consume("dodge")));
        if (dodgePressed && !_dodging && Stamina >= DodgeCost)
        {
            _dodging = true;
            _dodgeTimer = DodgeDuration;
            _invulnerableUntil = Time.time + DodgeIFrameDuration;
            SpendStamina(DodgeCost);
        }

        if (_controller != null)
        {
            Vector3 move = transform.TransformDirection(direction) * speed;

            if (_dodging)
            {
                _dodgeTimer -= Time.deltaTime;
                if (_dodgeTimer <= 0f)
                    _dodging = false;
            }

            if (flying)
            {
                // Free vertical movement: hold Space to ascend, LeftCtrl to descend.
                float vertical = 0f;
                if (Keyboard.current != null)
                {
                    if (Keyboard.current.spaceKey.isPressed) vertical += 1f;
                    if (Keyboard.current.leftCtrlKey.isPressed) vertical -= 1f;
                }
                _velocity.y = vertical * FlightVerticalSpeed;
            }
            else if (_controller.isGrounded)
            {
                if (_velocity.y < 0f)
                    _velocity.y = -1f;

                bool jumpPressed =
                    _waterAllowJump && !dialogBlocked && !IsRiding && !_dodging &&
                    ((Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame) ||
                     MobileInputController.Consume("jump"));
                if (jumpPressed)
                {
                    _velocity.y = Mathf.Sqrt(JumpHeight * -2f * Gravity);
                    _jumpFrame = Time.frameCount;
                }
                else if (_velocity.y > 0f && Time.frameCount > _jumpFrame + 2)
                {
                    // No jump in progress: any surviving upward velocity is a stray
                    // injection/residual, so kill it or it would lift the player forever.
                    _velocity.y = 0f;
                }
            }
            else
            {
                _velocity.y += Gravity * Time.deltaTime;
            }

            Vector3 finalMove = move + Vector3.up * _velocity.y;
            if (_dodging)
            {
                Vector3 dash = transform.forward * DodgeSpeed;
                dash.y = Mathf.Max(dash.y, _velocity.y);
                finalMove = dash + Vector3.up * _velocity.y;
            }
            _controller.Move(finalMove * Time.deltaTime);
        }

        if (sprint)
            Stamina = Mathf.Max(0f, Stamina - SprintCost * Time.deltaTime);
    }

    private void HandleStamina()
    {
        bool sprinting = (Keyboard.current != null && Keyboard.current.leftShiftKey.isPressed) ||
                         (GameInput.IsMobile && MobileInputController.IsHeld("sprint"));
        bool grounded = _controller != null && _controller.isGrounded;
        if (!sprinting || !grounded || Stamina <= 0f)
        {
            float regenMul = StaminaRegenMultiplier;
            if (Time.time < _staminaRegenModifierUntil)
                regenMul *= StaminaRegenModifier;
            else if (StaminaRegenModifier != 1f)
                StaminaRegenModifier = 1f;

            // Class passive: Taoist/Monk stamina-regen modifiers compound multiplicatively (§3.2.1).
            var passives = GetComponent<ClassPassiveManager>();
            if (passives != null)
                regenMul *= passives.StaminaRegenMul;

            // Skill-tree perk: stamina-regen % (§3.3).
            var pStats = GetComponent<PlayerStats>();
            if (pStats != null)
                regenMul *= pStats.StaminaRegenMul;

            Stamina = Mathf.Min(MaxStamina, Stamina + StaminaRegenRate * regenMul * Time.deltaTime);

            // Base HP regen + class passive HP regen (Monk "Meditation", Taoist "Yi Symbol", aura buffs).
            float hpRegenFraction = 2f * (Stamina / MaxStamina) * Time.deltaTime;
            if (passives != null)
                hpRegenFraction += passives.HpRegenPerSecond * MaxHP * Time.deltaTime;
            if (pStats != null)
                hpRegenFraction += pStats.HealthRegenPerSecondFlat * MaxHP * Time.deltaTime;
            if (Time.time < _classBuffUntil && _classBuffHpRegenPerSecond > 0f)
                hpRegenFraction += _classBuffHpRegenPerSecond * MaxHP * Time.deltaTime;
            if (hpRegenFraction > 0f && HP < MaxHP)
            {
                HP = Mathf.Min(MaxHP, HP + Mathf.RoundToInt(hpRegenFraction));
                GameManager.Instance?.UIManager?.UpdatePlayerHud(HP, MaxHP, Stamina, MaxStamina, Money);
            }
        }
    }

    public void ApplyStaminaRegenModifier(float modifier, float duration)
    {
        StaminaRegenModifier = modifier;
        _staminaRegenModifierUntil = Time.time + duration;
    }

    private void HandleInteractionKeys()
    {
        bool wifeDialog = WifeNPC.Instance != null && WifeNPC.Instance.IsDialogActive;
        bool buffaloDialog = BuffaloDialog.Instance != null && BuffaloDialog.Instance.IsDialogActive;
        bool richManDialog = RichManNPC.Instance != null && RichManNPC.Instance.IsDialogActive;
        bool policeDialog = PoliceOfficerNPC.Instance != null && PoliceOfficerNPC.Instance.IsDialogActive;
        bool monkDialog = PagodaMonkNPC.Instance != null && PagodaMonkNPC.Instance.IsDialogActive;
        bool chefDialog = ChefNPC.Instance != null && ChefNPC.Instance.IsDialogActive;
        bool cafeBaristaDialog = CafeBarista.Instance != null && CafeBarista.Instance.IsDialogActive;
        bool librarianDialog = LibrarianNPC.Instance != null && LibrarianNPC.Instance.IsDialogActive;
        bool fishingShopDialog = FishingShopNPC.Instance != null && FishingShopNPC.Instance.IsDialogActive;
        bool goblinMenuOpen = GoblinCommandMenu.Instance != null && GoblinCommandMenu.Instance.IsOpen;
        bool craftingOpen = CraftingManager.Instance != null && CraftingManager.Instance.IsOpen;
        bool dialogBlocked = wifeDialog || buffaloDialog || richManDialog || policeDialog || monkDialog || chefDialog || cafeBaristaDialog || librarianDialog || fishingShopDialog || goblinMenuOpen || craftingOpen;

        bool ePressed = (Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame) ||
                        (!wifeDialog && MobileInputController.Consume("interact"));
        if (ePressed && buffaloDialog)
            BuffaloDialog.Instance.Advance();
        if (ePressed && richManDialog)
            RichManNPC.Instance.Advance();
        if (ePressed && policeDialog)
            PoliceOfficerNPC.Instance.Advance();
        if (ePressed && monkDialog)
            PagodaMonkNPC.Instance.Advance();
        if (ePressed && chefDialog)
            ChefNPC.Instance.Advance();
        if (ePressed && librarianDialog)
            LibrarianNPC.Instance.Advance();
        if (ePressed && fishingShopDialog)
            FishingShopNPC.Instance.Advance();
        if (ePressed && cafeBaristaDialog)
            CafeBarista.Instance.Advance();
        if (richManDialog && RichManNPC.Instance != null && RichManNPC.Instance.IsEndingChoiceShown)
        {
            if (Keyboard.current != null && Keyboard.current.digit1Key.wasPressedThisFrame)
                RichManNPC.Instance.ChooseLeave();
            else if (Keyboard.current != null && Keyboard.current.digit2Key.wasPressedThisFrame)
                RichManNPC.Instance.ChooseBribe();
        }

        if (!dialogBlocked)
        {
            if (ePressed)
            {
                if (IsRiding)
                {
                    HorseMount.Instance?.Dismount();
                    return;
                }
                var wb = WorldBuilder.Instance;
                if (RichManNPC.Instance != null && RichManNPC.Instance.TryEavesdropDeal(transform.position))
                    return;
                var cam = Camera.main;
                if (cam != null && wb != null)
                {
                    var ray = new Ray(cam.transform.position, cam.transform.forward);
                    if (Physics.Raycast(ray, out var hit, 4f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide))
                    {
                        var stand = hit.collider.GetComponentInParent<WeaponRackStand>();
                        if (stand != null)
                        {
                            PickupWeaponStand(stand);
                            return;
                        }
                        if (hit.collider.transform.name == "WifeNpc")
                        {
                            if (WifeNPC.Instance != null && !WifeNPC.Instance.IsDialogActive)
                                WifeNPC.Instance.Interact();
                            QuestManager.Instance?.AddProgress("greet", 1);
                            return;
                        }
                        if (hit.collider.transform.name == "Bed")
                        {
                            if (SleepManager.Instance != null)
                                SleepManager.Instance.Open();
                            return;
                        }
                        if (hit.collider.transform.name == "BuffaloEntity")
                        {
                            var dlg = Object.FindAnyObjectByType<BuffaloDialog>();
                            if (dlg == null)
                            {
                                var go = new GameObject("BuffaloDialog");
                                dlg = go.AddComponent<BuffaloDialog>();
                                dlg.Initialize();
                            }
                            dlg.Show();
                            return;
                        }
                        if (hit.collider.transform.name == "VendorNPC")
                        {
                            var shop = Object.FindAnyObjectByType<VendorShopManager>();
                            if (shop == null)
                            {
                                var go = new GameObject("VendorShopManager");
                                shop = go.AddComponent<VendorShopManager>();
                                shop.Initialize();
                            }
                            shop.Open();
                            return;
                        }
                        if (hit.collider.transform.name == "RichManNpc")
                        {
                            if (RichManNPC.Instance != null && !RichManNPC.Instance.IsDialogActive)
                                RichManNPC.Instance.Interact();
                            return;
                        }
                        if (hit.collider.transform.name == "PoliceOfficer")
                        {
                            if (PoliceOfficerNPC.Instance != null && !PoliceOfficerNPC.Instance.IsDialogActive)
                                PoliceOfficerNPC.Instance.Interact();
                            return;
                        }
                        if (hit.collider.transform.name == "RestaurantNPC")
                        {
                            if (ChefNPC.Instance != null && !ChefNPC.Instance.IsDialogActive)
                                ChefNPC.Instance.Interact();
                            return;
                        }
                        if (hit.collider.transform.name == "PagodaMonkNpc")
                        {
                            if (PagodaMonkNPC.Instance != null && !PagodaMonkNPC.Instance.IsDialogActive)
                                PagodaMonkNPC.Instance.Interact();
                            return;
                        }
                        if (hit.collider.transform.name == "PriestNpc")
                        {
                            if (PriestNPC.Instance != null && !PriestNPC.Instance.IsDialogActive)
                                PriestNPC.Instance.Interact();
                            return;
                        }
                        if (hit.collider.transform.name == "TaoistPriestNpc")
                        {
                            if (TaoistPriestNPC.Instance != null && !TaoistPriestNPC.Instance.IsDialogActive)
                                TaoistPriestNPC.Instance.Interact();
                            return;
                        }
                        if (hit.collider.transform.name == "ToolShopNPC")
                        {
                            OpenVendorShop("tools");
                            return;
                        }
                        if (hit.collider.transform.name == "ConvenienceNPC")
                        {
                            OpenVendorShop("convenience");
                            return;
                        }
                        if (hit.collider.transform.name == "GroceryNPC")
                        {
                            OpenVendorShop("grocery");
                            return;
                        }
                        if (hit.collider.transform.name == "CafeNPC")
                        {
                            if (CafeBarista.Instance != null && !CafeBarista.Instance.IsDialogActive)
                                CafeBarista.Instance.Interact();
                            return;
                        }
                        if (hit.collider.transform.name == "FishingShopNPC")
                        {
                            if (FishingShopNPC.Instance != null && !FishingShopNPC.Instance.IsDialogActive)
                                FishingShopNPC.Instance.Interact();
                            return;
                        }
                        if (hit.collider.transform.name == "LibrarianNPC")
                        {
                            if (LibrarianNPC.Instance != null && !LibrarianNPC.Instance.IsDialogActive)
                                LibrarianNPC.Instance.Interact();
                            return;
                        }
                        if (hit.collider.transform.name.StartsWith("GoblinPet"))
                        {
                            var goblin = hit.collider.GetComponentInParent<GoblinPet>();
                            if (goblin != null)
                                GoblinCommandMenu.Ensure().Open(goblin);
                            return;
                        }
                        if (hit.collider.transform.name.StartsWith("GoblinChest"))
                        {
                            GoblinChestMenu.Ensure().Open();
                            return;
                        }
                        var chestHit = hit.collider.transform;
                        while (chestHit != null && chestHit.name != "chest")
                            chestHit = chestHit.parent;
                        if (chestHit != null)
                        {
                            PlayerChestMenu.Ensure().OpenAt(chestHit.position);
                            return;
                        }
                        var rideHorse = hit.collider.GetComponentInParent<HorseMount>();
                        if (rideHorse != null)
                        {
                            rideHorse.ToggleMount();
                            return;
                        }
                        var roadSign = hit.collider.GetComponentInParent<FastTravelSign>();
                        if (roadSign != null)
                        {
                            FastTravelMenu.Ensure().Open();
                            return;
                        }
                        if (CraftingManager.ResolveStationCategory(hit.collider) != null)
                        {
                            CraftingManager.Ensure().InteractStation(hit.collider);
                            return;
                        }
                        if (wb.TryToggleDoor(hit)) return;
                    }
                }
                if (!(ToolManager.Instance?.TryPickupNearby() ?? false))
                    TrySitNearby();
            }
        }

        bool gPressed = (Keyboard.current != null && Keyboard.current.gKey.wasPressedThisFrame) ||
                        MobileInputController.Consume("invite");
        if (!dialogBlocked && gPressed)
        {
            var npcGO = GameObject.Find("WifeNpc");
            if (npcGO != null && Vector3.Distance(transform.position, npcGO.transform.position) < 6f)
            {
                WifeNPC.Instance?.InviteToHouse();
            }
        }

        bool leftClick = !FishingController.IsFishingActive &&
                         ((!GameInput.IsMobile && Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame) ||
                          MobileInputController.Consume("use"));
        if (FightingMode)
        {
            // Re-rig ONLY after a model reload (gender/race change) destroyed the hand rigs, and
            // only for the weapons that were equipped before the rebuild. An intentional drag-out /
            // unequip never gets resurrected.
            if (_pendingAutoRig.Count > 0)
            {
                var combat = GetComponent<CombatController>();
                if (combat != null && combat.RightHand == null && combat.LeftHand == null)
                {
                    foreach (var pending in _pendingAutoRig)
                    {
                        var weapon = WeaponCatalog.Find(pending.id);
                        if (weapon != null)
                            WeaponRigBuilder.EquipInto(gameObject, weapon, pending.isLeft);
                    }
                    ReApplyWeaponPose(instant: true);
                }
                _pendingAutoRig.Clear();
            }
            // Nothing equipped (fought barehanded before the reload) — put the fists back on.
            var combatNow = GetComponent<CombatController>();
            if (combatNow != null && combatNow.RightHand == null && combatNow.LeftHand == null)
                WeaponRigBuilder.EnsureFists(gameObject);
        }
        // Per-hand dual-wield (both hands hold real weapons, NOT both magic): LMB and RMB drive each
        // hand directly instead of the magic aim / RMB-block scheme — so dual loadouts never enter
        // the single-weapon aim flow below. Runs regardless of dialog so a held ranged release fires.
        var dualCombat = GetComponent<CombatController>();
        bool dualMode = FightingMode && dualCombat != null && !GameInput.IsMobile &&
                        dualCombat.HasLoadedDual && !dualCombat.BothHandsMagic;
        if (dualMode)
            HandleDualModeCombat(dualCombat, !dialogBlocked && !MagicWheelUI.IsOpen && !MagicTestMatrix.IsOpen);
        // Aim/charge: while _aiming (armed magic or ranged), RMB builds the charge level and RMB
        // release freezes it; releasing LMB fires at the current level. Runs even while dialog-ish
        // UI is up so the release isn't mired.
        if (_aiming && !dualMode)
        {
            var combat = GetComponent<CombatController>();
            if (ShouldCancelCharge())
            {
                _aiming = false;
                _chargeRmbHeld = false;
                _chargeAccum = 0f;
                _chargeDrained = 0f;
                HideAoePreview();
                HideCastingCircle();
                HidePathPreview();
                combat?.EndCharge(false);
            }
            else
            {
                bool rmbDown = !GameInput.IsMobile && Mouse.current != null && Mouse.current.rightButton.isPressed;
                _chargeRmbHeld = rmbDown;
                var armedSpell = ArmedSpell();
                bool armedMagic = armedSpell != null;
                bool chargeGrow = false;
                if (rmbDown)
                {
                    if (armedSpell != null)
                    {
                        // Magic overcharges past level 1 until the pool runs dry; each aim frame
                        // drains focus in real time (gated by remaining FP) so holding is a gamble.
                        var caster = GetComponent<SpellCaster>();
                        if (caster != null)
                        {
                            float level = SpellChargeLevel(_chargeAccum);
                            if (caster.CurrentFp > 0f)
                            {
                                float drain = armedSpell.FpCost * caster.ChargeFpCostBonus *
                                              level * caster.FpChargeDrainRate * Time.deltaTime;
                                drain = Mathf.Min(drain, caster.CurrentFp);
                                if (caster.TrySpendFocus(drain))
                                    _chargeDrained += drain;
                            }
                            chargeGrow = caster.CurrentFp > 0f;
                        }
                    }
                    else
                    {
                        // Ranged keeps its capped draw (no focus involved).
                        _chargeAccum = Mathf.Min(_chargeAccum + Time.deltaTime, MagicChargeMaxTime);
                    }
                }
                if (chargeGrow)
                    _chargeAccum += Time.deltaTime;
                float previewLevel = armedMagic ? SpellChargeLevel(_chargeAccum) : MagicChargeLevel(_chargeAccum);
                float hudLevel = MagicChargeLevel(_chargeAccum);
                combat?.SetChargeLevel(Mathf.Clamp01(hudLevel));

                UpdateAoePreview(previewLevel);
                UpdateCastingCircle(previewLevel);
                UpdatePathPreview(previewLevel, armedSpell);

                bool lmbUp = !GameInput.IsMobile && Mouse.current != null &&
                             (Mouse.current.leftButton.wasReleasedThisFrame || !Mouse.current.leftButton.isPressed);
                if (lmbUp)
                {
                    float charge = armedMagic ? SpellChargeLevel(_chargeAccum) : MagicChargeLevel(_chargeAccum);
                    float prepaid = _chargeDrained;
                    _aiming = false;
                    _chargeRmbHeld = false;
                    _chargeAccum = 0f;
                    _chargeDrained = 0f;
                    combat?.EndCharge(true);
                    HidePathPreview();
                    if (MagicWheelUI.HasArmedMagic())
                    {
                        bool previewShown = _aoePreview != null && _aoePreview.IsActive;
                        if (MagicWheelUI.ReleaseArmedCast(charge, prepaid))
                        {
                            BurstCastingCircle(charge);
                            HideCastingCircle();
                            // Keep the marker up until the spell actually lands, then it hides itself.
                            if (previewShown)
                            {
                                var caster = GetComponent<SpellCaster>();
                                if (caster != null) _aoePreview.Lock(caster);
                                else HideAoePreview();
                            }
                        }
                        else
                        {
                            // Rejected (e.g. tap with an empty pool): tear the charge down silently —
                            // no ring-without-bolt phantom.
                            HideCastingCircle();
                            if (previewShown) HideAoePreview();
                        }
                    }
                    else if (IsRangedEquipped(combat))
                    {
                        HideAoePreview();
                        combat.FireRanged(charge);
                    }
                    else
                        combat?.EndCharge(false);
                }
            }
        }
        if (!dialogBlocked && leftClick && !MagicWheelUI.IsOpen && !MagicTestMatrix.IsOpen && !dualMode && !BeamChanneling())
        {
            if (FightingMode)
            {
                var combatPress = GetComponent<CombatController>();
                // Auto-arm a spell on demand so magic aim/charge/fire works without the Alt wheel first.
                bool aimable = !GameInput.IsMobile && !WeaponTransitionBusy() &&
                    (MagicWheelUI.EnsureArmedMagic() || IsRangedEquipped(combatPress));
                if (aimable)
                {
                    // Hold LMB to aim (no charge yet); the release fires, driven above.
                    _aiming = true;
                    _chargeDrained = 0f;
                    combatPress?.PlayCharge();
                }
                else if (!WeaponTransitionBusy() && !MagicWheelUI.ConsumeArmedCast())
                {
                    combatPress?.LightAttack();
                }
            }
            else
                ToolManager.Instance?.UseSelectedItem();
        }
        if (!dialogBlocked && !GameInput.IsMobile && !MagicWheelUI.IsOpen && !MagicTestMatrix.IsOpen && Mouse.current != null && !dualMode)
        {
            if (FightingMode)
            {
                var combat = GetComponent<CombatController>();
                if (combat != null)
                {
                    if (IsMeleeEquipped(combat) || IsShieldEquipped(combat))
                    {
                        // RMB hold = block for melee weapons and shields (incl. fists, incl. a
                        // shield held in the off-hand). Melee no longer has a heavy attack — the
                        // finisher swing is dropped for melee, and shields never charge.
                        if (!WeaponTransitionBusy())
                            combat.SetBlocking(Mouse.current.rightButton.isPressed);
                    }
                    // Ranged/magic: RMB is the charge/draw — driven by the aim session above.
                }
                if (Mouse.current.rightButton.wasPressedThisFrame)
                    return;
            }
            else if (Mouse.current.rightButton.wasPressedThisFrame)
            {
                var cam = Camera.main;
            if (cam != null)
            {
                var ray = new Ray(cam.transform.position, cam.transform.forward);
                if (Physics.Raycast(ray, out var hit, 5f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide))
                {
                    string hitName = hit.collider.transform.name;

                    if (hitName == "BuffaloEntity")
                    {
                        var dlg = Object.FindAnyObjectByType<BuffaloDialog>();
                        if (dlg == null)
                        {
                            var go = new GameObject("BuffaloDialog");
                            dlg = go.AddComponent<BuffaloDialog>();
                            dlg.Initialize();
                        }
                        dlg.Show();
                        return;
                    }

                    if (hitName == "VendorNPC")
                    {
                        var shop = Object.FindAnyObjectByType<VendorShopManager>();
                        if (shop == null)
                        {
                            var go = new GameObject("VendorShopManager");
                            shop = go.AddComponent<VendorShopManager>();
                            shop.Initialize();
                        }
                        shop.Open();
                        return;
                    }

                    if (hitName == "ToolShopNPC")
                    {
                        OpenVendorShop("tools");
                        return;
                    }

                    if (hitName == "ConvenienceNPC")
                    {
                        OpenVendorShop("convenience");
                        return;
                    }

                    if (hitName == "GroceryNPC")
                    {
                        OpenVendorShop("grocery");
                        return;
                    }
                }
            }
        }
        }
        if (!dialogBlocked && ((Keyboard.current != null && Keyboard.current.qKey.wasPressedThisFrame) ||
            MobileInputController.Consume("drop")))
            ToolManager.Instance?.DropSelectedItem();
        if (!dialogBlocked && Keyboard.current != null && Keyboard.current.tKey.wasPressedThisFrame)
            WorldBuilder.Instance?.RotateBuildingPreview(90);
        if (!dialogBlocked && Keyboard.current != null && Keyboard.current.kKey.wasPressedThisFrame)
            GameManager.Instance?.UIManager?.ToggleSkillPanel();
        if (!dialogBlocked && Keyboard.current != null && Keyboard.current.lKey.wasPressedThisFrame)
            GameManager.Instance?.UIManager?.ToggleFriendPanel();
        if (!dialogBlocked && Keyboard.current != null && Keyboard.current.oKey.wasPressedThisFrame)
            ToolManager.Instance?.SortInventory();
        if (!dialogBlocked && Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame)
            HorseMount.Instance?.Dismount();
        if (!dialogBlocked && Keyboard.current != null && Keyboard.current.tabKey.wasPressedThisFrame)
        {
            var info = Object.FindAnyObjectByType<CharacterInfoUI>();
            if (info != null)
            {
                if (info.IsShown)
                    info.Close();
                else
                    info.Show();
            }
        }
        if (!dialogBlocked && GameManager.Instance?.UIManager != null)
            GameManager.Instance.UIManager.HandleFriendPanelKeys();
        bool friendOpen = GameManager.Instance?.UIManager != null && GameManager.Instance.UIManager.FriendPanelVisible;
        if (!dialogBlocked && !friendOpen && !FightingMode && Keyboard.current != null && Keyboard.current.digit1Key.wasPressedThisFrame)
            ToolManager.Instance?.SelectSlot(0);
        if (!dialogBlocked && !friendOpen && !FightingMode && Keyboard.current != null && Keyboard.current.digit2Key.wasPressedThisFrame)
            ToolManager.Instance?.SelectSlot(1);
        if (!dialogBlocked && !friendOpen && !FightingMode && Keyboard.current != null && Keyboard.current.digit3Key.wasPressedThisFrame)
            ToolManager.Instance?.SelectSlot(2);
        if (!dialogBlocked && !friendOpen && !FightingMode && Keyboard.current != null && Keyboard.current.digit4Key.wasPressedThisFrame)
            ToolManager.Instance?.SelectSlot(3);
        if (!dialogBlocked && !friendOpen && !FightingMode && Keyboard.current != null && Keyboard.current.digit5Key.wasPressedThisFrame)
            ToolManager.Instance?.SelectSlot(4);
        if (!dialogBlocked && !friendOpen && !FightingMode && Keyboard.current != null && Keyboard.current.digit6Key.wasPressedThisFrame)
            ToolManager.Instance?.SelectSlot(5);
        if (!dialogBlocked && !friendOpen && !FightingMode && Keyboard.current != null && Keyboard.current.digit7Key.wasPressedThisFrame)
            ToolManager.Instance?.SelectSlot(6);
        if (!dialogBlocked && !friendOpen && !FightingMode && Keyboard.current != null && Keyboard.current.digit8Key.wasPressedThisFrame)
            ToolManager.Instance?.SelectSlot(7);
        if (!dialogBlocked && !friendOpen && !FightingMode && Keyboard.current != null && Keyboard.current.digit9Key.wasPressedThisFrame)
            ToolManager.Instance?.SelectSlot(8);
        if (!dialogBlocked && !friendOpen && !FightingMode && Keyboard.current != null && Keyboard.current.digit0Key.wasPressedThisFrame)
            ToolManager.Instance?.SelectSlot(9);
        if (!dialogBlocked && Keyboard.current != null && Keyboard.current.f1Key.wasPressedThisFrame)
            ToggleCombatMode();
        if (!dialogBlocked && Keyboard.current != null && Keyboard.current.xKey.wasPressedThisFrame)
        {
            // Toggle two-hand grip on a single held weapon (no-op while dual-wielding).
            var combat = GetComponent<CombatController>();
            if (combat != null)
                combat.SetTwoHand(!combat.TwoHandIntent);
        }
    }

    private void PickupWeaponStand(WeaponRackStand stand)
    {
        if (stand == null || stand.Collected) return;
        var player = GameManager.Instance?.Player;
        if (player == null) return;

        var inv = player.GetComponent<WeaponInventory>();
        if (inv == null)
            inv = player.gameObject.AddComponent<WeaponInventory>();

        var weapon = WeaponCatalog.Find(stand.WeaponId);
        string name = weapon != null && !string.IsNullOrEmpty(weapon.displayName) ? weapon.displayName : stand.WeaponId;

        if (inv.Own(stand.WeaponId))
            ShowPrompt(Localization.F("Picked up {0}.", name));
        else
            ShowPrompt(Localization.F("{0} is already in your inventory.", name));

        // Take it as a normal item: hotbar (0-9) first, then the backpack storage grid.
        var tm = ToolManager.Instance;
        if (tm == null)
        {
            var go = new GameObject("ToolManager");
            tm = go.AddComponent<ToolManager>();
        }
        if (!tm.AddItem(stand.WeaponId, 1))
            ShowPrompt(Localization.T("Túi đồ đầy."));

        stand.Collect();
    }

    private void ToggleCombatMode()
    {
        if (FightingMode)
        {
            FightingMode = false;
            GameManager.Instance?.UIManager?.SetHotbarVisible(true);
            var skillBar = Object.FindAnyObjectByType<SkillBarHUD>();
            if (skillBar != null) skillBar.SetVisible(false);
            if (_cachedFightSlot >= 0)
                ToolManager.Instance?.SelectSlot(_cachedFightSlot);
            _cachedFightSlot = -1;
            ShowPrompt(Localization.T("Casual mode."));
            // Sheathe the equipped weapon onto the body (waist/back) in third person; in first
            // person WeaponsDrawn keeps it in the hand so the player always sees what they hold.
            ReApplyWeaponPose(instant: false);
        }
        else
        {
            FightingMode = true;
            var tm = ToolManager.Instance;
            _cachedFightSlot = tm != null ? tm.SelectedSlotIndex : -1;
            ToolManager.Instance?.ResetSelection();
            GameManager.Instance?.UIManager?.SetHotbarVisible(false);
            var skillBar = Object.FindAnyObjectByType<SkillBarHUD>();
            if (skillBar != null) skillBar.SetVisible(true);
            TryAutoRigWeapon();
            // Draw the weapon from its stow point into the hand.
            ReApplyWeaponPose(instant: false);
        }
    }

    /// <summary>True while the equipped weapon is mid draw/stow transition (attacks gated).</summary>
    private bool WeaponTransitionBusy()
    {
        var animator = GetComponent<WeaponStowAnimator>();
        return animator != null && animator.IsBusy;
    }

    /// <summary>True when the equipped hand(s) hold a melee weapon (incl. bare fists).</summary>
    private bool IsMeleeEquipped(CombatController combat)
    {
        var hand = combat.RightHand ?? combat.LeftHand;
        var host = hand != null ? hand.GetComponent<WeaponRigHost>() : null;
        return host != null && host.Data != null && host.Data.Category == WeaponCategory.Melee;
    }

    /// <summary>True when a shield weapon is held in either hand (enables RMB block + strict guard).</summary>
    private bool IsShieldEquipped(CombatController combat)
    {
        return combat != null && combat.HasShield;
    }

    /// <summary>
    /// Per-hand dual-wield input (§5.16). Both hands carry real weapons (not both magic — that keeps
    /// the normal magic flow). LMB drives one hand and RMB the other: same-side when no ranged weapon
    /// is present, CROSSED (LMB→right hand, RMB→left hand) whenever a bow/throwing hammer is among the
    /// two so the ranged hand keeps its hold-to-charge/release-to-fire draw. Melee swings on press,
    /// shields guard while held, magic fires the armed spell uncharged on press (loses its charge),
    /// ranged charges on hold and fires on release. Only a shield hand can raise a guard (the dual
    /// weapon trade-off).
    /// </summary>
    private void HandleDualModeCombat(CombatController combat, bool inputAvailable)
    {
        if (Mouse.current == null) return;

        bool crossed = combat.HasRangedDual;
        GameObject lmbHand = crossed ? combat.RightHand : combat.LeftHand;
        GameObject rmbHand = crossed ? combat.LeftHand : combat.RightHand;

        var lmb = Mouse.current.leftButton;
        var rmb = Mouse.current.rightButton;

        bool blockRequested = false;
        HandleDualHand(combat, lmbHand, lmb.isPressed, lmb.wasPressedThisFrame,
            lmb.wasReleasedThisFrame || !lmb.isPressed, inputAvailable,
            ref _dualChargeL, ref _dualChargeActiveL, ref blockRequested);
        HandleDualHand(combat, rmbHand, rmb.isPressed, rmb.wasPressedThisFrame,
            rmb.wasReleasedThisFrame || !rmb.isPressed, inputAvailable,
            ref _dualChargeR, ref _dualChargeActiveR, ref blockRequested);

        combat.SetBlocking(blockRequested);
    }

    /// <summary>Drive a single hand's weapon from its mapped mouse button in dual mode.</summary>
    private void HandleDualHand(CombatController combat, GameObject hand, bool held, bool pressed,
        bool released, bool inputAvailable, ref float charge, ref bool charging, ref bool blockRequested)
    {
        if (hand == null) return;

        switch (combat.CategoryOfHand(hand))
        {
            case WeaponCategory.Melee:
                // Swing on press. A raised guard (other hand's shield) drops first so the swing lands.
                if (pressed && inputAvailable && !WeaponTransitionBusy())
                {
                    if (combat.IsBlocking) combat.SetBlocking(false);
                    combat.LightAttackWith(hand);
                }
                break;

            case WeaponCategory.Shield:
                // Hold to guard, release to unguard. The caller re-applies SetBlocking after both
                // hands so a same-frame attack click takes the guard down again.
                if (held && inputAvailable) blockRequested = true;
                break;

            case WeaponCategory.Magic:
                // Magic loses its charge in a mixed dual: a tap fires the armed spell uncharged.
                if (pressed && inputAvailable && !WeaponTransitionBusy())
                {
                    if (combat.IsBlocking) combat.SetBlocking(false);
                    MagicWheelUI.EnsureArmedMagic();
                    MagicWheelUI.ReleaseArmedCast(0f);
                }
                break;

            case WeaponCategory.Ranged:
                // Ranged keeps its charge in dual: hold to draw, release to fire at that level.
                if (pressed && inputAvailable)
                {
                    charging = true;
                    charge = 0f;
                }
                if (!charging) break;
                if (held)
                {
                    charge = Mathf.Min(charge + Time.deltaTime, MagicChargeMaxTime);
                    ShowRangedPathPreview(hand.GetComponent<RangedWeaponBehavior>(), hand, charge);
                }
                combat.SetChargeLevel(MagicChargeLevel(charge));
                if (released)
                {
                    float level = MagicChargeLevel(charge);
                    charging = false;
                    charge = 0f;
                    HidePathPreview();
                    if (inputAvailable && !WeaponTransitionBusy())
                        combat.FireRangedWith(hand, level);
                    combat.EndCharge(level > 0f);
                }
                break;
        }
    }

    /// <summary>True when the equipped hand(s) hold a ranged weapon (bow / throwing hammer).</summary>
    private bool IsRangedEquipped(CombatController combat)
    {
        var hand = combat != null ? (combat.RightHand ?? combat.LeftHand) : null;
        var host = hand != null ? hand.GetComponent<WeaponRigHost>() : null;
        return host != null && host.Data != null && host.Data.Category == WeaponCategory.Ranged;
    }

    /// <summary>True while an aim/charge is in progress (drives the HUD charge bar visibility).</summary>
    public bool IsCharging => _aiming && (_chargeRmbHeld || _chargeAccum > 0f);

    /// <summary>Charge progress for the HUD. Magic overcharges past 100% (level 1+ keeps climbing);
    /// ranged picks only the capped draw level.</summary>
    public float MagicChargeProgress => IsCharging
        ? (ArmedSpell() != null ? SpellChargeLevel(_chargeAccum) : MagicChargeLevel(_chargeAccum))
        : 0f;

    /// <summary>Charge level (0..1) for a held cast; taps under the threshold cast uncharged.</summary>
    private static float MagicChargeLevel(float holdTime)
    {
        float t = (holdTime - MagicChargeTapThreshold) / (MagicChargeMaxTime - MagicChargeTapThreshold);
        return Mathf.Clamp01(t);
    }

    /// <summary>
    /// Uncapped charge level for held magic casts: same ramp as <see cref="MagicChargeLevel"/> but
    /// with no upper limit, so overcharging past full grows power/size/cost and real-time FP drain
    /// becomes the only ceiling. Taps under the threshold sit at 0 (cast uncharged).
    /// </summary>
    private static float SpellChargeLevel(float holdTime)
    {
        float t = (holdTime - MagicChargeTapThreshold) / (MagicChargeFullTime - MagicChargeTapThreshold);
        return Mathf.Max(t, 0f);
    }

    /// <summary>
    /// Show/refresh the AoE landing preview each aim frame — but only for armed zone/vortex
    /// magic, so projectile/instant spells and ranged weapons get no marker.
    /// </summary>
    private void UpdateAoePreview(float charge)
    {
        var spell = ArmedSpell();
        if (spell != null && (spell.Delivery == SpellDelivery.Zone || spell.Delivery == SpellDelivery.Vortex
            || spell.Delivery == SpellDelivery.Summon || spell.Delivery == SpellDelivery.Storm))
        {
            if (TryAoeTarget(spell, charge, out var center, out var radius, out var color))
                AoePreview().Show(center, radius, color);
        }
        else
        {
            HideAoePreview();
        }
    }

    private void HideAoePreview()
    {
        if (_aoePreview != null)
            _aoePreview.Hide();
    }

    private AoeAimPreview AoePreview()
    {
        if (_aoePreview == null)
            _aoePreview = AoeAimPreview.Instance;
        return _aoePreview;
    }

    /// <summary>
    /// Show/refresh the halo casting circle around the held magic weapon each aim frame. Only
    /// armed magic gets the halo — ranged draws show their own weapon accent instead.
    /// </summary>
    private void UpdateCastingCircle(float charge)
    {
        if (!MagicWheelUI.HasArmedMagic())
        {
            HideCastingCircle();
            return;
        }
        var combat = GetComponent<CombatController>();
        var hand = MagicHand(combat);
        if (hand == null)
        {
            HideCastingCircle();
            return;
        }
        var spell = ArmedSpell();
        Color color;
        if (spell != null) color = DamageNumber.ColorFor(spell.Type);
        else
        {
            var skill = SkillCatalog.Find(MagicWheelUI.ArmedSkillId);
            color = skill != null ? DamageNumber.ColorFor(skill.DamageKind) : Color.white;
        }
        Casting().Show(hand.transform, charge, color);
    }

    /// <summary>One-shot expansion ring at the magic weapon on cast release.</summary>
    private void BurstCastingCircle(float charge)
    {
        var combat = GetComponent<CombatController>();
        var hand = MagicHand(combat);
        if (hand == null) return;
        var spell = ArmedSpell();
        Color color = spell != null ? DamageNumber.ColorFor(spell.Type) : Color.white;
        float radius = spell != null ? spell.Radius : 1.5f;
        Casting().Burst(radius * (0.6f + charge * 0.5f), color, hand.transform.up);
        HideCastingCircle();
    }

    private void HideCastingCircle()
    {
        if (_castingCircle != null)
            _castingCircle.Hide();
    }

    private CastingCircle Casting()
    {
        if (_castingCircle == null)
            _castingCircle = CastingCircle.Instance;
        return _castingCircle;
    }

    /// <summary>
    /// Show/refresh the projectile flight-path preview each aim frame — a wide cone over the
    /// possible spread that narrows into a precision ray as the draw/charge builds. Covers the
    /// bow &amp; throwing hammer (true Dexterity spread) and projectile magic (straight laser,
    /// shrinking cone is focus feedback).
    /// </summary>
    private void UpdatePathPreview(float charge, SpellData armedSpell)
    {
        if (armedSpell != null && armedSpell.Delivery == SpellDelivery.Projectile)
        {
            var combat = GetComponent<CombatController>();
            var hand = MagicHand(combat);
            var cam = Camera.main;
            if (hand == null || cam == null) { HidePathPreview(); return; }

            // Mirror caster aim (SpellCaster.Execute): from the hand toward the camera line.
            Vector3 pos = hand.transform.position;
            Vector3 fwd = cam.transform.position + cam.transform.forward * Mathf.Max(armedSpell.Range, 5f) - pos;
            if (fwd.sqrMagnitude < 0.0001f) fwd = hand.transform.forward; else fwd = fwd.normalized;

            float c = Mathf.Clamp01(charge);
            float reach = Mathf.Max(armedSpell.ProjectileSpeed, 1f) * 4f; // SpellEffect flight envelope
            PathPreview().Show(pos + fwd * 0.5f, fwd, reach,
                8f * (1f - c), DamageNumber.ColorFor(armedSpell.Type), transform);
            return;
        }

        var rangedCombat = GetComponent<CombatController>();
        var hand2 = rangedCombat != null ? (rangedCombat.RightHand ?? rangedCombat.LeftHand) : null;
        var ranged = hand2 != null ? hand2.GetComponent<RangedWeaponBehavior>() : null;
        if (ranged == null) { HidePathPreview(); return; }
        ShowRangedPathPreview(ranged, hand2, charge);
    }

    /// <summary>Bounded ranged-weapon preview (regular aim or dual per-hand draw).</summary>
    private void ShowRangedPathPreview(RangedWeaponBehavior ranged, GameObject hand, float charge)
    {
        if (ranged == null) { HidePathPreview(); return; }

        float accuracy = 1f;
        if (ranged.Stats != null && ranged.Data != null)
            accuracy = 1f + ranged.Stats.GetStat(WeaponScalingStat.Dexterity) * ranged.Data.AccuracyFromDex;
        float spread = Mathf.Atan2(0.15f / Mathf.Max(accuracy, 0.01f), 1f) * Mathf.Rad2Deg;

        float c = Mathf.Clamp01(charge);
        float speed = ranged.ProjectileSpeed * Mathf.Lerp(1f, 1.5f, c);
        float lifetime = ranged.BaseLifetime * Mathf.Lerp(1f, 2f, c);
        Vector3 origin = ranged.Muzzle != null ? ranged.Muzzle.position
            : hand != null ? hand.transform.position : transform.position;
        PathPreview().Show(origin, transform.forward, speed * lifetime,
            spread * (1f - c), DamageNumber.ColorFor(ranged.ShotType), transform);
    }

    private void HidePathPreview()
    {
        if (_pathPreview != null)
            _pathPreview.Hide();
    }

    private ProjectilePathPreview PathPreview()
    {
        if (_pathPreview == null)
            _pathPreview = ProjectilePathPreview.Instance;
        return _pathPreview;
    }

    /// <summary>Returns the equipped hand holding a magic weapon, or null.</summary>
    private GameObject MagicHand(CombatController combat)
    {
        if (combat == null) return null;
        if (combat.RightHand != null && HandIsMagic(combat.RightHand)) return combat.RightHand;
        if (combat.LeftHand != null && HandIsMagic(combat.LeftHand)) return combat.LeftHand;
        return null;
    }

    private static bool HandIsMagic(GameObject hand)
    {
        if (hand == null) return false;
        var host = hand.GetComponent<WeaponRigHost>();
        return host != null && host.Data != null && host.Data.Category == WeaponCategory.Magic;
    }

    /// <summary>True while a beam channel is active (LMB is sustaining the beam, so it must not re-aim/attack).</summary>
    private bool BeamChanneling()
    {
        var caster = GetComponent<SpellCaster>();
        return caster != null && caster.IsChanneling;
    }

    /// <summary>SpellData of the armed magic skill (SpellCastEffect), or null when none is previewable.</summary>
    private SpellData ArmedSpell()
    {
        string id = MagicWheelUI.ArmedSkillId;
        if (string.IsNullOrEmpty(id)) return null;
        var skill = SkillCatalog.Find(id);
        if (skill == null || skill.Effect is not SpellCastEffect cast || cast.Spell == null)
            return null;
        return cast.Spell;
    }

    /// <summary>
    /// Project the ground target for an AoE spell — mirrors <see cref="SpellCaster"/> zone/vortex
    /// placement (ray along the camera forward to the spell range, then dropped to the ground).
    /// The radius grows with the charge level using the caster's charge-size bonus.
    /// </summary>
    private bool TryAoeTarget(SpellData spell, float charge, out Vector3 center, out float radius, out Color color)
    {
        center = transform.position;
        radius = 1f;
        color = Color.white;
        if (spell == null) return false;

        var cam = Camera.main;
        if (cam == null) return false;

        Vector3 pos = cam.transform.position;
        Vector3 fwd = cam.transform.forward;
        Vector3 at = pos + fwd * Mathf.Max(spell.Range, 5f);
        if (Physics.Raycast(pos, fwd, out RaycastHit aimHit, Mathf.Max(spell.Range, 0.1f)))
            at = aimHit.point;
        center = at;
        if (Physics.Raycast(at + Vector3.up * 0.1f, Vector3.down, out RaycastHit groundHit, 30f))
            center = groundHit.point;

        var caster = GetComponent<SpellCaster>();
        float sizeBonus = caster != null ? caster.ChargeSizeBonus : 0.8f;
        radius = spell.Radius * (1f + charge * sizeBonus);
        color = DamageNumber.ColorFor(spell.Type);
        return true;
    }

    /// <summary>True when an in-progress aim/charge should be dropped without firing.</summary>
    private bool ShouldCancelCharge()
    {
        if (GameInput.IsMobile || Mouse.current == null) return true;
        if (!FightingMode || WeaponTransitionBusy()) return true;
        if (MagicWheelUI.IsOpen) return true;
        if (MagicTestMatrix.IsOpen) return true;
        var combat = GetComponent<CombatController>();
        return !MagicWheelUI.HasArmedMagic() && !IsRangedEquipped(combat);
    }

    private void TryAutoRigWeapon()
    {
        WeaponCatalog.EnsureBuilt();
        // If the weapon is out of combat (stowed on the body), leave it there — the draw
        // transition animates it into the hand afterward. Only re-seat onto the hands when the
        // weapon is already drawn (or has no animator yet, e.g. first equip / parked rig).
        var animator = GetComponent<WeaponStowAnimator>();
        bool stowed = animator != null && !animator.IsDrawn;
        if (!stowed)
            WeaponRigBuilder.ReparentToHands(gameObject);
        var combat = GetComponent<CombatController>();
        if (combat != null && (combat.RightHand != null || combat.LeftHand != null)) return;
        // No weapon equipped — fight with the innate bare fists instead of auto-equipping an
        // owned or starter weapon. The player chooses real weapons via the gear sheet.
        WeaponRigBuilder.EnsureFists(gameObject);
    }

    private static void ShowPrompt(string message)
    {
        var prompt = Object.FindAnyObjectByType<ContextPromptUI>();
        if (prompt != null)
            prompt.ShowPrompt(message, 2.5f);
    }

    private void OpenVendorShop(string mode)
    {
        var shop = Object.FindAnyObjectByType<VendorShopManager>();
        if (shop == null)
        {
            var go = new GameObject("VendorShopManager");
            shop = go.AddComponent<VendorShopManager>();
            shop.Initialize();
        }
        switch (mode)
        {
            case "tools": shop.OpenTools(); break;
            case "convenience": shop.OpenConvenience(); break;
            case "grocery": shop.OpenGrocery(); break;
            default: shop.Open(); break;
        }
    }

    private void UpdateHud()
    {
        if (GameManager.Instance != null && GameManager.Instance.UIManager != null)
            GameManager.Instance.UIManager.UpdatePlayerHud(HP, MaxHP, Stamina, MaxStamina, Money);
    }

    private Vector2 ReadMoveInput()
    {
        if (GameInput.IsMobile)
        {
            var joy = MobileInputController.MoveAxis;
            if (joy != Vector2.zero)
                return joy;
        }

        if (Keyboard.current == null)
            return Vector2.zero;

        float x = 0f;
        if (Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed)
            x += 1f;
        if (Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed)
            x -= 1f;

        float y = 0f;
        if (Keyboard.current.wKey.isPressed || Keyboard.current.upArrowKey.isPressed)
            y += 1f;
        if (Keyboard.current.sKey.isPressed || Keyboard.current.downArrowKey.isPressed)
            y -= 1f;

        return new Vector2(x, y);
    }

    private void CreateCamera()
    {
        var cameraObject = new GameObject("Main Camera");
        cameraObject.tag = "MainCamera";
        var cameraComponent = cameraObject.AddComponent<Camera>();
        cameraComponent.fieldOfView = 60f;
        cameraComponent.clearFlags = CameraClearFlags.Skybox;
        cameraObject.transform.position = transform.position + new Vector3(0f, 1.5f, -4f);
        cameraObject.transform.rotation = Quaternion.LookRotation(transform.position + Vector3.up * 1.5f - cameraObject.transform.position);
    }

    private void SetupPlayerCamera()
    {
        if (_cameraPivot == null)
        {
            _cameraPivot = new GameObject("CameraPivot").transform;
            _cameraPivot.SetParent(transform);
            _cameraPivot.localPosition = new Vector3(0f, 1.5f, 0f);
            _cameraPivot.localRotation = Quaternion.identity;
        }

        var cam = Camera.main;
        if (cam == null)
            return;

        cam.tag = "MainCamera";
        if (cam.transform.parent != null)
            cam.transform.SetParent(null);

        var follow = cam.GetComponent<CameraFollow>();
        if (follow == null)
            follow = cam.gameObject.AddComponent<CameraFollow>();

        cam.transform.position = _cameraPivot.position;
        cam.transform.rotation = _cameraPivot.rotation;

        follow.Target = _cameraPivot;
        follow.Offset = Vector3.zero;
        follow.SmoothSpeed = 20f;

        // First / third-person camera switch.
        var switcher = GetComponent<CameraModeSwitch>();
        if (switcher == null)
            switcher = gameObject.AddComponent<CameraModeSwitch>();
        switcher.Setup(this, cam, _cameraPivot);
        _cameraMode = switcher;
    }

    /// <summary>Public accessor for the camera pivot (used by <see cref="CameraModeSwitch"/>).</summary>
    public Transform PlayerCameraPivot => _cameraPivot;

    /// <summary>
    /// Whether the equipped weapons should be visually drawn in the hands (vs. stowed on the body).
    /// Weapons are always drawn while fighting, and stay drawn in first person so the player always
    /// sees what they hold; only third-person casual mode sheathes them onto the back/waist.
    /// </summary>
    public bool WeaponsDrawn => FightingMode || (_cameraMode != null && _cameraMode.IsFirstPerson);

    /// <summary>
    /// Re-apply the current draw/stow pose for all equipped weapons based on
    /// <see cref="WeaponsDrawn"/> (combat mode or camera mode changed). No-op until the combat
    /// stack/hands exist so a camera toggle during Awake is safe.
    /// </summary>
    public void ReApplyWeaponPose(bool instant = true)
    {
        var combat = GetComponent<CombatController>();
        if (combat == null) return;
        WeaponRigBuilder.ApplyPose(gameObject, draw: WeaponsDrawn, instant);
    }

    public void ApplyGender()
    {
        LoadPlayerModel();
    }

    private void LoadPlayerModel()
    {
        if (_playerModelInstance != null)
            Destroy(_playerModelInstance);

        var existing = transform.Find("PlayerModel");
        if (existing != null)
            Destroy(existing.gameObject);

        // Remember what was equipped so a model reload (gender/race change) can re-rig the same
        // weapons once the fresh hands exist — but never auto-equips weapons the player unequipped.
        _pendingAutoRig.Clear();
        var combat = GetComponent<CombatController>();
        if (combat != null)
        {
            var rh = combat.RightHand != null ? combat.RightHand.GetComponent<WeaponRigHost>() : null;
            if (rh != null && rh.Data != null && rh.Data.id != WeaponCatalog.FistWeaponId)
                _pendingAutoRig.Add((rh.Data.id, false));
            var lh = combat.LeftHand != null ? combat.LeftHand.GetComponent<WeaponRigHost>() : null;
            if (lh != null && lh.Data != null && lh.Data.id != WeaponCatalog.FistWeaponId)
                _pendingAutoRig.Add((lh.Data.id, true));
        }

        _playerModelInstance = MapBuilder.BuildPlayerModel(transform);

        if (_playerModelInstance != null)
        {
            // Body renderers → layer 6 (culled in first person). Arm/hand renderers (and any
            // weapon rig parented to a hand, which hangs under the Shoulder pivots) → layer 7,
            // which CameraModeSwitch keeps visible so the player sees their own arms in 1st person.
            foreach (var r in _playerModelInstance.GetComponentsInChildren<Renderer>())
                r.gameObject.layer = IsArmUnderShoulder(r.transform) ? 7 : 6;
            _playerModelInstance.AddComponent<PlayerAnimator>();
        }

        // The rebuilt model may have appeared after an early equip parked the weapon rig on the
        // player root (hidden inside the torso); re-seat it onto the fresh hand bones.
        WeaponRigBuilder.ReparentToHands(gameObject);
        // Re-apply the current weapon pose (drawn in combat or first person, stowed otherwise) now
        // that the model's hand + body anchors exist again. Snap immediately — a fresh model has no
        // in-flight draw/stow transition to continue.
        ReApplyWeaponPose(instant: true);

        // Subscribe once: a race change rebuilds the model with the new palette/body ratios
        // (§3.5 Race Visuals). Idempotent — LoadPlayerModel runs on Awake, gender, and respawn.
        if (!_raceSubscribed)
        {
            var rcm = GetComponent<RaceChangeManager>();
            if (rcm != null)
            {
                rcm.OnActiveRaceChanged += _ => LoadPlayerModel();
                _raceSubscribed = true;
            }
        }
    }

    /// <summary>True when the renderer sits on the arm chain (Shoulder → Elbow → Hand) or a held
    /// weapon rig parented to it. Declared inline so no top-level helper is added to the class.</summary>
    private static bool IsArmUnderShoulder(Transform t)
    {
        while (t != null)
        {
            if (t.name.StartsWith("Shoulder")) return true;
            t = t.parent;
        }
        return false;
    }
}

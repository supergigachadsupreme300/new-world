using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    public float MoveSpeed = 5f;
    public float SprintMultiplier = 2f;
    public float RideSpeed = 13f;
    public float Gravity = -9.81f;
    public float JumpHeight = 1.5f;
    public int HP = 100;
    public int MaxHP = 100;
    public float Stamina = 1000f;
    public float MaxStamina = 1000f;
    public float StaminaRegenRate = 4f;
    public float StaminaRegenMultiplier = 1f;
    public float StaminaRegenModifier = 1f;
    public float SprintCost = 35f;
    public long Money = 1000;
    public bool IgnoreInput { get; private set; }

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
    private Transform _cameraPivot;
    private float _yaw;
    private float _pitch;
    private PlayerSitController _sitController;
    private GameObject _playerModelInstance;
    private float _waterSpeedMul = 1f;
    private bool _waterAllowJump = true;
    private float _staminaRegenModifierUntil = 0f;

    private bool _dodging;
    private float _dodgeTimer;
    private float _invulnerableUntil;
    private const float DodgeDuration = 0.25f;
    private const float DodgeSpeed = 14f;
    private const float DodgeIFrameDuration = 0.3f;
    public float DodgeCost = 20f;

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
        HandleInteractionKeys();
        UpdateHud();
    }

    public void ResetPlayer()
    {
        HP = MaxHP;
        Stamina = MaxStamina;
        Money = 1000;
        var testGround = Object.FindAnyObjectByType<NewWorldTestGround>();
        if (testGround != null)
        {
            transform.position = testGround.GetSpawnPoint();
        }
        else
        {
            float spawnX = 0f;
            float spawnZ = -10f;
            float terrainY = TerrainNoiseGenerator.GetHeight(1337, spawnX, spawnZ);
            transform.position = new Vector3(spawnX, terrainY + 3f, spawnZ);
        }
        transform.rotation = Quaternion.identity;
        _velocity = Vector3.zero;
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

    public void TakeDamage(int amount)
    {
        if (HP <= 0) return;
        if (Time.time < _invulnerableUntil) return;
        HP -= amount;
        if (HP <= 0)
        {
            HP = 0;
            Debug.Log("Player died");
            GameManager.Instance?.TriggerPlayerDeath();
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
                             (ImmigrantNpc.Instance != null && ImmigrantNpc.Instance.IsDialogActive) ||
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
        bool sprint = canSprint &&
            ((Keyboard.current != null && Keyboard.current.leftShiftKey.isPressed) ||
             (GameInput.IsMobile && MobileInputController.IsHeld("sprint"))) &&
            Stamina > 0f && mag > 0f;
        float speed = IsRiding
            ? RideSpeed * _waterSpeedMul
            : MoveSpeed * _waterSpeedMul * (sprint ? SprintMultiplier : 1f);

        bool dodgePressed = !dialogBlocked && !IsRiding && _controller != null && _controller.isGrounded &&
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

            if (_controller.isGrounded)
            {
                if (_velocity.y < 0f)
                    _velocity.y = -1f;

                if (_waterAllowJump && !dialogBlocked && !IsRiding && !_dodging &&
                    ((Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame) ||
                     MobileInputController.Consume("jump")))
                {
                    _velocity.y = Mathf.Sqrt(JumpHeight * -2f * Gravity);
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
            Stamina = Mathf.Min(MaxStamina, Stamina + StaminaRegenRate * regenMul * Time.deltaTime);
            HP = Mathf.Min(MaxHP, HP + Mathf.RoundToInt(2f * (Stamina / MaxStamina) * Time.deltaTime));
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
        bool immigrantDialog = ImmigrantNpc.Instance != null && ImmigrantNpc.Instance.IsDialogActive;
        bool fishingShopDialog = FishingShopNPC.Instance != null && FishingShopNPC.Instance.IsDialogActive;
        bool goblinMenuOpen = GoblinCommandMenu.Instance != null && GoblinCommandMenu.Instance.IsOpen;
        bool craftingOpen = CraftingManager.Instance != null && CraftingManager.Instance.IsOpen;
        bool dialogBlocked = wifeDialog || buffaloDialog || richManDialog || policeDialog || monkDialog || chefDialog || cafeBaristaDialog || librarianDialog || immigrantDialog || fishingShopDialog || goblinMenuOpen || craftingOpen;

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
        if (ePressed && immigrantDialog)
            ImmigrantNpc.Instance.Advance();
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
                        if (hit.collider.transform.name == "ImmigrantNpc")
                        {
                            if (ImmigrantNpc.Instance != null && !ImmigrantNpc.Instance.IsDialogActive)
                                ImmigrantNpc.Instance.Interact();
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
        if (!dialogBlocked && leftClick)
            ToolManager.Instance?.UseSelectedItem();
        if (!dialogBlocked && !GameInput.IsMobile && Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame)
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
        if (!dialogBlocked && !friendOpen && Keyboard.current != null && Keyboard.current.digit1Key.wasPressedThisFrame)
            ToolManager.Instance?.SelectSlot(0);
        if (!dialogBlocked && !friendOpen && Keyboard.current != null && Keyboard.current.digit2Key.wasPressedThisFrame)
            ToolManager.Instance?.SelectSlot(1);
        if (!dialogBlocked && !friendOpen && Keyboard.current != null && Keyboard.current.digit3Key.wasPressedThisFrame)
            ToolManager.Instance?.SelectSlot(2);
        if (!dialogBlocked && !friendOpen && Keyboard.current != null && Keyboard.current.digit4Key.wasPressedThisFrame)
            ToolManager.Instance?.SelectSlot(3);
        if (!dialogBlocked && !friendOpen && Keyboard.current != null && Keyboard.current.digit5Key.wasPressedThisFrame)
            ToolManager.Instance?.SelectSlot(4);
        if (!dialogBlocked && !friendOpen && Keyboard.current != null && Keyboard.current.digit6Key.wasPressedThisFrame)
            ToolManager.Instance?.SelectSlot(5);
        if (!dialogBlocked && !friendOpen && Keyboard.current != null && Keyboard.current.digit7Key.wasPressedThisFrame)
            ToolManager.Instance?.SelectSlot(6);
        if (!dialogBlocked && !friendOpen && Keyboard.current != null && Keyboard.current.digit8Key.wasPressedThisFrame)
            ToolManager.Instance?.SelectSlot(7);
        if (!dialogBlocked && !friendOpen && Keyboard.current != null && Keyboard.current.digit9Key.wasPressedThisFrame)
            ToolManager.Instance?.SelectSlot(8);
        if (!dialogBlocked && !friendOpen && Keyboard.current != null && Keyboard.current.digit0Key.wasPressedThisFrame)
            ToolManager.Instance?.SelectSlot(9);
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

        stand.Collect();
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
    }

    /// <summary>Public accessor for the camera pivot (used by <see cref="CameraModeSwitch"/>).</summary>
    public Transform PlayerCameraPivot => _cameraPivot;

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

        _playerModelInstance = MapBuilder.BuildPlayerModel(transform);

        if (_playerModelInstance != null)
        {
            foreach (var r in _playerModelInstance.GetComponentsInChildren<Renderer>())
                r.gameObject.layer = 6;
        }
    }
}

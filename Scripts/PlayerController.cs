using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    public float MoveSpeed = 5f;
    public float SprintMultiplier = 2f;
    public float Gravity = -9.81f;
    public float JumpHeight = 1.5f;
    public int HP = 100;
    public int MaxHP = 100;
    public float Stamina = 1000f;
    public float MaxStamina = 1000f;
    public float StaminaRegenRate = 25f;
    public float SprintCost = 35f;
    public long Money = 10000000000;
    public bool IgnoreInput { get; private set; }

    private CharacterController _controller;
    private Vector3 _velocity;
    private Transform _cameraPivot;
    private float _yaw;
    private float _pitch;
    private const float MouseSensitivity = 2.5f;
    private const string PlayerModelResourcePath = "Models/Player/PlayerModel";
    private GameObject _playerModelInstance;

    private void Awake()
    {
        EnsurePlayerPhysics();

        // Remove any existing audio listeners to prevent duplicates
        var existingListeners = Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None);
        foreach (var listener in existingListeners)
        {
            if (listener.gameObject != gameObject)
                Destroy(listener);
        }

        // Ensure the player camera exists and will follow this player.
        if (Camera.main == null)
        {
            CreateCamera();
        }
        SetupPlayerCamera();

        // Ensure exactly one audio listener on the camera
        var cameraObj = Camera.main.gameObject;
        var audioListener = cameraObj.GetComponent<AudioListener>();
        if (audioListener == null)
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
            _controller.stepOffset = 0.3f;
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
        ResetPlayer();
        // Allow developer to enable input automatically for quick testing.
        EnableInput(AutoEnableInput);
        if (GameManager.Instance != null)
            GameManager.Instance.Player = this;
    }

    private void Update()
    {
        if (IgnoreInput || (GameManager.Instance != null && GameManager.Instance.GamePaused))
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
        transform.position = new Vector3(0f, 2f, -10f);
        transform.rotation = Quaternion.identity;
        _velocity = Vector3.zero;
    }

    public void EnableInput(bool enabled)
    {
        IgnoreInput = !enabled;
        Cursor.lockState = enabled ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !enabled;
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
        HP -= amount;
        if (HP <= 0)
        {
            HP = 0;
            Debug.Log("Player died");
        }
    }

    private void HandleMouseLook()
    {
        if (Mouse.current == null)
            return;

        var delta = Mouse.current.delta.ReadValue();
        _yaw += delta.x * MouseSensitivity * 0.02f;
        _pitch -= delta.y * MouseSensitivity * 0.02f;
        _pitch = Mathf.Clamp(_pitch, -60f, 60f);

        transform.rotation = Quaternion.Euler(0f, _yaw, 0f);
        if (_cameraPivot != null)
            _cameraPivot.localRotation = Quaternion.Euler(_pitch, 0f, 0f);
    }

    private void HandleMovement()
    {
        Vector2 input = ReadMoveInput();
        Vector3 direction = new Vector3(input.x, 0f, input.y);
        if (direction.magnitude > 1f)
            direction.Normalize();

        bool sprint = Keyboard.current != null && Keyboard.current.leftShiftKey.isPressed && Stamina > 0f && direction.magnitude > 0f;
        float speed = MoveSpeed * (sprint ? SprintMultiplier : 1f);

        if (_controller != null)
        {
            Vector3 move = transform.TransformDirection(direction) * speed;

            if (_controller.isGrounded)
            {
                if (_velocity.y < 0f)
                    _velocity.y = -1f;

                if (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame)
                {
                    _velocity.y = Mathf.Sqrt(JumpHeight * -2f * Gravity);
                }
            }
            else
            {
                _velocity.y += Gravity * Time.deltaTime;
            }

            Vector3 finalMove = move + Vector3.up * _velocity.y;
            _controller.Move(finalMove * Time.deltaTime);
        }

        if (sprint)
            Stamina = Mathf.Max(0f, Stamina - SprintCost * Time.deltaTime);
    }

    private void HandleStamina()
    {
        if (Keyboard.current == null || !Keyboard.current.leftShiftKey.isPressed || _controller == null || !_controller.isGrounded)
        {
            Stamina = Mathf.Min(MaxStamina, Stamina + StaminaRegenRate * Time.deltaTime);
        }
    }

    private void HandleInteractionKeys()
    {
        if (Keyboard.current == null)
            return;

        if (Keyboard.current.eKey.wasPressedThisFrame)
        {
            var wb = WorldBuilder.Instance;
            if (wb != null && wb.IsNearVendorSpawnButton(transform.position))
            {
                wb.SpawnVendorCart();
                return;
            }
            ToolManager.Instance?.TryPickupNearby();
        }
        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
            ToolManager.Instance?.UseSelectedItem();
        if (Keyboard.current.qKey.wasPressedThisFrame)
            ToolManager.Instance?.DropSelectedItem();
        if (Keyboard.current.rKey.wasPressedThisFrame)
            ToolManager.Instance?.ReloadGun();
        if (Keyboard.current.bKey.wasPressedThisFrame)
            WorldBuilder.Instance?.CycleBuildingType(1);
        if (Keyboard.current.nKey.wasPressedThisFrame)
            WorldBuilder.Instance?.CycleBuildingType(-1);
        if (Keyboard.current.tKey.wasPressedThisFrame)
            WorldBuilder.Instance?.RotateBuildingPreview(90);
        if (Keyboard.current.digit1Key.wasPressedThisFrame)
            ToolManager.Instance?.SelectSlot(0);
        if (Keyboard.current.digit2Key.wasPressedThisFrame)
            ToolManager.Instance?.SelectSlot(1);
        if (Keyboard.current.digit3Key.wasPressedThisFrame)
            ToolManager.Instance?.SelectSlot(2);
        if (Keyboard.current.digit4Key.wasPressedThisFrame)
            ToolManager.Instance?.SelectSlot(3);
        if (Keyboard.current.digit5Key.wasPressedThisFrame)
            ToolManager.Instance?.SelectSlot(4);
        if (Keyboard.current.digit6Key.wasPressedThisFrame)
            ToolManager.Instance?.SelectSlot(5);
        if (Keyboard.current.digit7Key.wasPressedThisFrame)
            ToolManager.Instance?.SelectSlot(6);
        if (Keyboard.current.digit8Key.wasPressedThisFrame)
            ToolManager.Instance?.SelectSlot(7);
        if (Keyboard.current.digit9Key.wasPressedThisFrame)
            ToolManager.Instance?.SelectSlot(8);
        if (Keyboard.current.digit0Key.wasPressedThisFrame)
            ToolManager.Instance?.SelectSlot(9);
    }

    private void UpdateHud()
    {
        if (GameManager.Instance != null && GameManager.Instance.UIManager != null)
            GameManager.Instance.UIManager.UpdatePlayerHud(HP, MaxHP, Stamina, MaxStamina, Money);
    }

    private Vector2 ReadMoveInput()
    {
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
    }

    private void LoadPlayerModel()
    {
        if (_playerModelInstance != null)
            Destroy(_playerModelInstance);

        var existing = transform.Find("PlayerModel");
        if (existing != null)
            Destroy(existing.gameObject);

        var capsule = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        capsule.name = "PlayerModel";
        capsule.transform.SetParent(transform);
        capsule.transform.localPosition = Vector3.zero;
        capsule.transform.localRotation = Quaternion.identity;
        capsule.transform.localScale = new Vector3(1f, 1.8f, 1f);
        var renderer = capsule.GetComponent<Renderer>();
        if (renderer != null)
            renderer.material.color = new Color(0.2f, 0.6f, 0.9f);

        Destroy(capsule.GetComponent<Collider>());
        _playerModelInstance = capsule;
    }

    private void CreateBlockyPlayerModel()
    {
        // This method is no longer used. Player model is now a capsule.
    }

    private void CreatePart(Transform parent, string name, Vector3 scale, Vector3 localPosition, Color color)
    {
        var part = GameObject.CreatePrimitive(PrimitiveType.Cube);
        part.name = name;
        part.transform.SetParent(parent);
        part.transform.localScale = scale;
        part.transform.localPosition = localPosition;
        var renderer = part.GetComponent<Renderer>();
        if (renderer != null)
            renderer.material.color = color;
        Destroy(part.GetComponent<Collider>());
    }
}

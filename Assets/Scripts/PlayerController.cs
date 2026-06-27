using System.Collections;
using UnityEngine;

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

    private void Awake()
    {
        _controller = GetComponent<CharacterController>();
        if (_controller == null)
            _controller = gameObject.AddComponent<CharacterController>();

        if (Camera.main == null)
            CreateCamera();

        CreateBlockyPlayerModel();
    }

    private void Start()
    {
        ResetPlayer();
        EnableInput(false);
        if (GameManager.Instance != null)
            GameManager.Instance.Player = this;
    }

    private void Update()
    {
        if (GameManager.Instance == null || !GameManager.Instance.InGame || GameManager.Instance.GamePaused || IgnoreInput)
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
        _yaw += Input.GetAxis("Mouse X") * MouseSensitivity;
        _pitch -= Input.GetAxis("Mouse Y") * MouseSensitivity;
        _pitch = Mathf.Clamp(_pitch, -60f, 60f);

        transform.rotation = Quaternion.Euler(0f, _yaw, 0f);
        if (_cameraPivot != null)
            _cameraPivot.localRotation = Quaternion.Euler(_pitch, 0f, 0f);
    }

    private void HandleMovement()
    {
        float horizontal = Input.GetAxis("Horizontal");
        float vertical = Input.GetAxis("Vertical");
        Vector3 direction = new Vector3(horizontal, 0f, vertical);
        if (direction.magnitude > 1f)
            direction.Normalize();

        bool sprint = Input.GetKey(KeyCode.LeftShift) && Stamina > 0f && direction.magnitude > 0f;
        float speed = MoveSpeed * (sprint ? SprintMultiplier : 1f);

        if (_controller != null)
        {
            Vector3 move = transform.TransformDirection(direction) * speed;
            _controller.Move(move * Time.deltaTime);

            if (_controller.isGrounded && Input.GetButtonDown("Jump"))
            {
                _velocity.y = Mathf.Sqrt(JumpHeight * -2f * Gravity);
            }

            _velocity.y += Gravity * Time.deltaTime;
            _controller.Move(_velocity * Time.deltaTime);
        }

        if (sprint)
            Stamina = Mathf.Max(0f, Stamina - SprintCost * Time.deltaTime);
    }

    private void HandleStamina()
    {
        if (!Input.GetKey(KeyCode.LeftShift) || _controller == null || !_controller.isGrounded)
        {
            Stamina = Mathf.Min(MaxStamina, Stamina + StaminaRegenRate * Time.deltaTime);
        }
    }

    private void HandleInteractionKeys()
    {
        if (Input.GetKeyDown(KeyCode.E))
            ToolManager.Instance?.TryPickupNearby();
        if (Input.GetKeyDown(KeyCode.Q))
            ToolManager.Instance?.DropSelectedItem();
        if (Input.GetKeyDown(KeyCode.R))
            ToolManager.Instance?.ReloadGun();
        if (Input.GetMouseButtonDown(0))
            ToolManager.Instance?.UseSelectedItem();
        if (Input.GetKeyDown(KeyCode.B))
            WorldBuilder.Instance?.CycleBuildingType(1);
        if (Input.GetKeyDown(KeyCode.N))
            WorldBuilder.Instance?.CycleBuildingType(-1);
        if (Input.GetKeyDown(KeyCode.T))
            WorldBuilder.Instance?.RotateBuildingPreview(90);
        if (Input.GetKeyDown(KeyCode.Alpha1))
            ToolManager.Instance?.SelectSlot(0);
        if (Input.GetKeyDown(KeyCode.Alpha2))
            ToolManager.Instance?.SelectSlot(1);
        if (Input.GetKeyDown(KeyCode.Alpha3))
            ToolManager.Instance?.SelectSlot(2);
        if (Input.GetKeyDown(KeyCode.Alpha4))
            ToolManager.Instance?.SelectSlot(3);
        if (Input.GetKeyDown(KeyCode.Alpha5))
            ToolManager.Instance?.SelectSlot(4);
        if (Input.GetKeyDown(KeyCode.Alpha6))
            ToolManager.Instance?.SelectSlot(5);
        if (Input.GetKeyDown(KeyCode.Alpha7))
            ToolManager.Instance?.SelectSlot(6);
        if (Input.GetKeyDown(KeyCode.Alpha8))
            ToolManager.Instance?.SelectSlot(7);
        if (Input.GetKeyDown(KeyCode.Alpha9))
            ToolManager.Instance?.SelectSlot(8);
        if (Input.GetKeyDown(KeyCode.Alpha0))
            ToolManager.Instance?.SelectSlot(9);
    }

    private void UpdateHud()
    {
        if (GameManager.Instance != null && GameManager.Instance.UIManager != null)
            GameManager.Instance.UIManager.UpdatePlayerHud(HP, MaxHP, Stamina, MaxStamina, Money);
    }

    private void CreateCamera()
    {
        var cameraObject = new GameObject("Main Camera");
        var cameraComponent = cameraObject.AddComponent<Camera>();
        cameraComponent.fieldOfView = 60f;
        cameraComponent.clearFlags = CameraClearFlags.Skybox;
        cameraObject.AddComponent<AudioListener>();

        _cameraPivot = new GameObject("CameraPivot").transform;
        _cameraPivot.SetParent(transform);
        _cameraPivot.localPosition = new Vector3(0f, 1.5f, 0f);
        cameraObject.transform.SetParent(_cameraPivot);
        cameraObject.transform.localPosition = new Vector3(0f, 0f, 0f);
        cameraObject.transform.localRotation = Quaternion.identity;
    }

    private void CreateBlockyPlayerModel()
    {
        var root = new GameObject("PlayerModel");
        root.transform.SetParent(transform);
        root.transform.localPosition = Vector3.zero;

        CreatePart(root.transform, "Torso", new Vector3(0.88f, 0.45f, 0.46f), new Vector3(0f, 1.25f, 0f), new Color(0.05f, 0.41f, 0.69f));
        CreatePart(root.transform, "Head", new Vector3(0.5f, 0.5f, 0.5f), new Vector3(0f, 1.8f, 0f), new Color(1f, 0.8f, 0.58f));
        CreatePart(root.transform, "LeftLeg", new Vector3(0.4f, 0.55f, 0.42f), new Vector3(-0.25f, 0.65f, 0f), new Color(0.16f, 0.5f, 0.28f));
        CreatePart(root.transform, "RightLeg", new Vector3(0.4f, 0.55f, 0.42f), new Vector3(0.25f, 0.65f, 0f), new Color(0.16f, 0.5f, 0.28f));
        CreatePart(root.transform, "LeftArm", new Vector3(0.36f, 0.45f, 0.38f), new Vector3(-0.62f, 1.25f, 0f), new Color(0.05f, 0.41f, 0.69f));
        CreatePart(root.transform, "RightArm", new Vector3(0.36f, 0.45f, 0.38f), new Vector3(0.62f, 1.25f, 0f), new Color(0.05f, 0.41f, 0.69f));
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

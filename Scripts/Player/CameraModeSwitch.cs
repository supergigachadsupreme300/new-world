using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// First / third-person camera mode switch for the open world player. Reuses the player's existing
/// <see cref="PlayerController"/> yaw/pitch/<c>CameraPivot</c> orientation and only offsets the
/// camera each frame, so there is no duplicate look handling.
///
/// Modes:
///   First  - camera sits at the head pivot (player model layer is culled).
///   Third  - camera is pulled back behind the character's facing and looks at the pivot, with
///            terrain collision so it does not clip into the ground.
///
/// Toggles with F5 (PC new Input System). Attach the component to the player object and call
/// <see cref="Setup"/> from <see cref="PlayerController.SetupPlayerCamera"/>.
/// </summary>
public sealed class CameraModeSwitch : MonoBehaviour
{
    public enum Mode
    {
        First,
        Third
    }

    [Header("Startup")]
    [Tooltip("Start in first person on spawn.")]
    public bool StartInFirstPerson = true;

    [Header("Third-person")]
    [Min(0.5f)] public float ThirdPersonDistance = 3.5f;
    [Tooltip("Vertical offset of the third-person camera above the pivot.")]
    public float ThirdPersonY = 1.8f;
    [Tooltip("Position smoothing seconds for the third-person camera.")]
    public float SmoothTime = 0.15f;

    [Header("Collision")]
    [Tooltip("Layers the third-person camera should be pushed out of (terrain/walls).")]
    public LayerMask CollisionMask = ~0;
    [Min(0.01f)] public float CollisionRadius = 0.2f;

    [Header("Player model")]
    [Tooltip("Layer the player model renderers live on (culled in first person, shown in third).")]
    public int PlayerModelLayer = 6;

    private PlayerController _player;
    private Camera _camera;
    private Transform _pivot;
    private CameraFollow _follow;
    private Vector3 _velocity;

    public Mode CurrentMode { get; private set; }

    private void OnEnable()
    {
        if (_player == null)
            _player = GetComponent<PlayerController>();
        if (_pivot == null && _player != null)
            _pivot = _player.PlayerCameraPivot;
        CurrentMode = StartInFirstPerson ? Mode.First : Mode.Third;
        ApplyPlayerModelVisibility();
    }

    /// <summary>Configure the switch to drive a specific camera around the player.</summary>
    public void Setup(PlayerController player, Camera cam, Transform pivot)
    {
        _player = player;
        _camera = cam;
        _pivot = pivot;
        if (cam != null)
            _follow = cam.GetComponent<CameraFollow>();
        ApplyCameraFollow();
        ApplyPlayerModelVisibility();
    }

    /// <summary>Quick accessor for systems that need to know the current view.</summary>
    public bool IsFirstPerson => CurrentMode == Mode.First;

    /// <summary>Toggle between first and third person.</summary>
    public void Toggle()
    {
        SetMode(CurrentMode == Mode.First ? Mode.Third : Mode.First);
    }

    public void SetFirst() => SetMode(Mode.First);
    public void SetThird() => SetMode(Mode.Third);

    private void SetMode(Mode mode)
    {
        if (CurrentMode == mode) return;
        CurrentMode = mode;
        ApplyPlayerModelVisibility();
        ApplyCameraFollow();
    }

    /// <summary>
    /// Disable the legacy rigid follow while third-person is active so it does not yank the
    /// camera back to the head; re-enable it in first person / when unconfigured placeholders.
    /// </summary>
    private void ApplyCameraFollow()
    {
        if (_follow == null) return;
        _follow.enabled = CurrentMode == Mode.First;
    }

    private void Update()
    {
        if (GameInput.IsMobile) return;
        if (Keyboard.current != null && Keyboard.current[Key.F5].wasPressedThisFrame)
            Toggle();
    }

    private void LateUpdate()
    {
        if (_player == null)
            _player = GetComponent<PlayerController>();
        if (_camera == null)
            _camera = Camera.main;
        if (_camera == null || _pivot == null)
            return;

        if (CurrentMode == Mode.First)
        {
            _camera.transform.position = _pivot.position;
            _camera.transform.rotation = _pivot.rotation;
        }
        else
        {
            UpdateThirdPerson();
        }
    }

    private void UpdateThirdPerson()
    {
        Vector3 pivotPos = _pivot.position;
        // Place the camera behind the character's facing so we see the back, not the front.
        Vector3 desired = pivotPos
            + Vector3.up * (ThirdPersonY - _pivot.localPosition.y)
            - _pivot.forward * ThirdPersonDistance;

        // Terrain / wall collision: pull the camera forward if it would be inside geometry.
        Vector3 toCam = (desired - pivotPos).normalized;
        float targetDist = Vector3.Distance(pivotPos, desired);
        float finalDist = targetDist;
        if (Physics.SphereCast(pivotPos, CollisionRadius, toCam,
                out RaycastHit hit, targetDist, CollisionMask, QueryTriggerInteraction.Ignore))
        {
            finalDist = Mathf.Max(hit.distance - CollisionRadius, 0.1f);
            desired = pivotPos + toCam * finalDist;
        }

        _camera.transform.position = Vector3.SmoothDamp(
            _camera.transform.position, desired, ref _velocity, SmoothTime);
        _camera.transform.rotation = Quaternion.LookRotation(pivotPos - _camera.transform.position);
    }

    /// <summary>
    /// Show the player model only in third person. In first person the model layer is excluded
    /// from the camera culling mask (mirrors the existing head-camera setup).
    /// </summary>
    private void ApplyPlayerModelVisibility()
    {
        if (_camera == null) return;
        int layerBit = 1 << PlayerModelLayer;
        if (CurrentMode == Mode.First)
            _camera.cullingMask &= ~layerBit;
        else
            _camera.cullingMask |= layerBit;
    }
}
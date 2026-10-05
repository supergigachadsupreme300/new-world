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
    [Tooltip("Start in first person on spawn. 1jf: false - the game opens in third person. F5 still toggles both ways, so this only chooses the spawn view; nothing else in the codebase reads it, so flipping it needs no other change (grep: this is its only reader, its own OnEnable).")]
    public bool StartInFirstPerson = false;

    [Header("Third-person")]
    [Min(0.5f)] public float ThirdPersonDistance = 6.5f;
    [Tooltip("Height of the third-person camera above the player's FEET, not above the pivot: UpdateThirdPerson adds up * (ThirdPersonY - pivot.localPosition.y) to the pivot's world position, so the two terms cancel and the camera lands at feet + this. With the pivot at 1.5 m, 2.6 reads as '2.6 m up the player's body', which is why raising this does NOT raise the look-at point - the camera still looks at the pivot.")]
    public float ThirdPersonY = 2.6f;
    [Tooltip("Position smoothing seconds for the third-person camera.")]
    public float SmoothTime = 0.15f;

    [Header("Collision")]
    [Tooltip("Layers the third-person camera should be pushed out of (terrain/walls).")]
    public LayerMask CollisionMask = ~0;
    [Min(0.01f)] public float CollisionRadius = 0.2f;

    [Header("Player model")]
    [Tooltip("Layer the player model renderers live on (culled in first person, shown in third).")]
    public int PlayerModelLayer = 6;
    [Tooltip("Layer the arm/hand (+ held weapon) renderers live on. Always visible, so the player see their own arms holding the equipped weapon even in first person.")]
    public int ArmsLayer = 7;

    private PlayerController _player;
    private Camera _camera;
    private Transform _pivot;
    private CameraFollow _follow;
    private Vector3 _velocity;

    // Perf (§OPT): terrain-collision SphereCast every frame in third person; re-run at ~10 Hz
    // and reuse the cached clamp distance between casts.
    private const float CollisionCheckInterval = 0.1f;
    private float _collisionTimer;
    private float _cachedFinalDist = -1f;

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
        // First person keeps equipped weapons drawn (even out of combat); third person sheathes
        // them when casual. Re-pose so an F5 switch mirrors the new view.
        if (_player != null)
            _player.ReApplyWeaponPose();
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
        // The SphereCast runs at ~10 Hz; the cached clamp distance is reused between casts so
        // direction changes (facing) stay smooth without re-querying physics each frame.
        Vector3 toCam = (desired - pivotPos).normalized;
        float targetDist = Vector3.Distance(pivotPos, desired);
        float finalDist = targetDist;
        _collisionTimer -= Time.deltaTime;
        if (_collisionTimer <= 0f)
        {
            _collisionTimer = CollisionCheckInterval;
            if (Physics.SphereCast(pivotPos, CollisionRadius, toCam,
                    out RaycastHit hit, targetDist, CollisionMask, QueryTriggerInteraction.Ignore))
                _cachedFinalDist = Mathf.Max(hit.distance - CollisionRadius, 0.1f);
            else
                _cachedFinalDist = -1f;
        }
        if (_cachedFinalDist >= 0f)
            finalDist = Mathf.Min(finalDist, _cachedFinalDist);
        desired = pivotPos + toCam * finalDist;

        _camera.transform.position = Vector3.SmoothDamp(
            _camera.transform.position, desired, ref _velocity, SmoothTime);
        _camera.transform.rotation = Quaternion.LookRotation(pivotPos - _camera.transform.position);
    }

    /// <summary>
    /// Show the player model only in third person. In first person the body layer is excluded
    /// from the camera culling mask (mirrors the existing head-camera setup), but the arms/hands
    /// layer stays visible so the player always sees their own arms holding the equipped weapon.
    /// </summary>
    private void ApplyPlayerModelVisibility()
    {
        if (_camera == null) return;
        int bodyBit = 1 << PlayerModelLayer;
        int armsBit = (ArmsLayer >= 0 && ArmsLayer < 32) ? 1 << ArmsLayer : 0;
        if (CurrentMode == Mode.First)
            _camera.cullingMask &= ~bodyBit;
        else
            _camera.cullingMask |= bodyBit;
        if (armsBit != 0)
            _camera.cullingMask |= armsBit;
    }
}
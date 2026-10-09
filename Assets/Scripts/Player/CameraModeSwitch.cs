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
    [Tooltip("Height of the third-person camera above the player's FEET, not above the pivot: UpdateThirdPerson adds up * (ThirdPersonY - pivot.localPosition.y) to the pivot's world position, so the two terms cancel and the camera lands at feet + this. With the pivot at 1.5 m, 2.6 reads as '2.6 m up the player's body', which is why raising this does NOT raise the look-at point - the camera still looks at the pivot (plus ThirdPersonSideOffset's lateral term, which is horizontal and so cannot change the height).")]
    public float ThirdPersonY = 2.6f;
    [Tooltip("Lateral offset of the third-person camera in metres. Positive = to the player's RIGHT, negative = to the LEFT, 0 = the pre-1jl centred look. Added as pivot.right * this to BOTH the camera position and the look-at point, so the view direction is unchanged and the character sits off-centre (over the shoulder). Offsetting only the position would not move the character on screen: the camera would just rotate to keep re-centring it. First person is unaffected - it orbits the pivot with no offset. This moves FRAMING only: nothing that flies is derived from the camera's position (the projectile aim is a direction off the look pivot, see SpellCaster.StraightFlightDirection), so raising it cannot bend a shot.")]
    public float ThirdPersonSideOffset = 0.9f;
    [Tooltip("Position smoothing seconds for the third-person camera. 1ju: this now affects POSITION only - the view direction is derived from the unlagged boom position, so smoothing can no longer steer the camera's aim. Raising it makes the follow heavier without turning the view.")]
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

    /// <summary>1jv (QA, read-only): the boom's length BEFORE the collision clamp, in metres.
    /// Published by UpdateThirdPerson from its own `targetDist` local rather than recomputed by the
    /// audit lane, so the lane cannot hold a second spelling of the boom vector that rots when this
    /// file's geometry changes (rule 7's "never re-derive a private formula"). 0 until the first
    /// UpdateThirdPerson runs, which is why the lane gates on the mode check first.</summary>
    public float BoomRestLength { get; private set; }

    /// <summary>1jv (QA, read-only): the boom length actually APPLIED, after the collision clamp.
    /// Equal to <see cref="BoomRestLength"/> whenever nothing is obstructing the boom. Comparing
    /// this against the measured camera-to-pivot distance separates "the boom got shorter" (this
    /// moved) from "the camera is trailing past the boom's end" (this held).</summary>
    public float BoomAppliedLength { get; private set; }

    // Perf (§OPT): terrain-collision SphereCast every frame in third person; re-run at ~10 Hz
    // and reuse the cached clamp distance between casts.
    private const float CollisionCheckInterval = 0.1f;

    // How far the boom is allowed to turn before the cached clamp distance stops being trustworthy.
    // Small enough that a real obstruction is caught early, large enough that ordinary aiming jitter
    // does not spend a physics query every frame.
    private const float RecastOnTurnDegrees = 8f;

    private float _collisionTimer;
    private float _cachedFinalDist = -1f;

    // The direction _cachedFinalDist was measured ALONG - a bare distance carries no record of which
    // ray produced it. Seeded to zero deliberately: the angle against it is ~90 degrees, which forces a
    // real cast on the first frame rather than letting an unseeded value authorise a reuse
    // (rule 8: a tracker with no seed has a fictional first sample).
    private Vector3 _cachedDir = Vector3.zero;

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
        // 1jl: shoulder offset, a LATERAL translation of the camera position AND the look-at point
        // by the same amount, so the view direction stays exactly the pre-1jl one (fwd * distance
        // - up * height) and the character simply sits off-centre instead of being re-centred.
        // The collision cast still starts at pivotPos, so the lateral term is inside the direction
        // it sweeps and a wall beside the player now pulls the camera in - a play-test item.
        Vector3 lookTarget = pivotPos + _pivot.right * ThirdPersonSideOffset;
        // Place the camera behind the character's facing so we see the back, not the front.
        // 1ko: the boom's GEOMETRY is yaw-only now. It used `_pivot.forward`, so the PIVOT's pitch
        // tipped the -forward * Distance term down: `up * Rise` (Rise = ThirdPersonY -
        // pivot.localPosition.y = 1.1 m) cancelled against Distance * sin(pitch) and the rest length
        // shrank by up to a metre the further the player looked down. Past ~9.7 deg of look-down the
        // camera - and its collision sweep - dropped BELOW the pivot's height, which re-admitted flat
        // open ground into the SphereCast (1jv's falsification assumed a level view), so the boom
        // clamped and released on terrain exactly while walking forward and looking down: the reported
        // momentary zoom. Stripping the vertical off the forward makes the sweep rise Rise over
        // Distance from the pivot at EVERY pitch, so 1jv's flat-ground falsification holds for all
        // pitches, and the rest length is the constant sqrt(Distance^2 + offset^2 + Rise^2) ~ 6.65 m.
        // Pitch is unrecoverable here only if |forward.y| == 1, which the +-60 deg look clamp and the
        // single SetLookRotation(0, 0) cutscene call both rule out. The pitch that was removed is
        // re-injected into the VIEW below, never into the distance.
        Vector3 flatForward = _pivot.forward;
        flatForward.y = 0f;
        flatForward.Normalize();
        Vector3 desired = lookTarget
            + Vector3.up * (ThirdPersonY - _pivot.localPosition.y)
            - flatForward * ThirdPersonDistance;

        // Terrain / wall collision: pull the camera forward if it would be inside geometry.
        // The SphereCast runs at ~10 Hz; the cached clamp distance is reused between casts so
        // direction changes (facing) stay smooth without re-querying physics each frame.
        Vector3 toCam = (desired - pivotPos).normalized;
        float targetDist = Vector3.Distance(pivotPos, desired);
        float finalDist = targetDist;
        // 1jv: publish the unclamped length from the local that produced it (see BoomRestLength).
        BoomRestLength = targetDist;
        _collisionTimer -= Time.deltaTime;

        // A cached DISTANCE is only meaningful along the direction it was measured on, and `toCam`
        // is that direction. 1ju: `toCam` changes when the player TURNS only - it is built from
        // `desired - pivotPos`, the pivotPos term cancels, and after 1ko the boom is yaw-only, so
        // it depends on the pivot's YAW and the three constants (never its pitch, never its
        // translation). Strafing is pure translation and leaves it exactly invariant (a comment
        // from before 1ju claimed otherwise, and was wrong).
        // Re-measure whenever the boom has swung more
        // than RecastOnTurnDegrees since the last cast: otherwise a clamp taken "straight back" gets
        // applied to "back and to the left", which reads as the camera randomly zooming in precisely
        // while the player is turning. Only worth doing while a clamp is ACTIVE - with no obstruction
        // cached there is no clamp to misapply, and the 10 Hz timer still catches new ones.
        bool turnedSinceCast =
            _cachedFinalDist >= 0f && Vector3.Angle(toCam, _cachedDir) > RecastOnTurnDegrees;
        if (_collisionTimer <= 0f || turnedSinceCast)
        {
            _collisionTimer = CollisionCheckInterval;
            _cachedDir = toCam;
            if (Physics.SphereCast(pivotPos, CollisionRadius, toCam,
                    out RaycastHit hit, targetDist, CollisionMask, QueryTriggerInteraction.Ignore))
                _cachedFinalDist = Mathf.Max(hit.distance - CollisionRadius, 0.1f);
            else
                _cachedFinalDist = -1f;
        }
        if (_cachedFinalDist >= 0f)
            finalDist = Mathf.Min(finalDist, _cachedFinalDist);
        // 1jv: publish the clamped length alongside it, so the QA lane can tell a shorter boom
        // apart from a camera trailing past a full-length one.
        BoomAppliedLength = finalDist;
        desired = pivotPos + toCam * finalDist;

        _camera.transform.position = Vector3.SmoothDamp(
            _camera.transform.position, desired, ref _velocity, SmoothTime);
        // 1ju: aim from `desired`, NOT from the smoothed position. SmoothDamp leaves the camera
        // trailing the player, and a strafe is the one ordinary motion that puts a LATERAL
        // component in that trail - running forward trails along the view axis (pitch only),
        // but strafing trails sideways, which steers the look-at and yaws the whole view.
        // The lag is v * SmoothTime, so at 5 m/s walk it is 0.75 m over a 6.5 m boom = 6.6 deg
        // of yaw, 13 deg sprinting at 10 m/s, flipping sign with the strafe direction.
        // `desired` is already the unlagged boom position (pivotPos + toCam * finalDist), and
        // `lookTarget - desired` has no pivotPos term and no smoothed position in it - so this is
        // byte-identical to the old expression whenever the camera has caught up, and also
        // identical under a collision pull-in, and carries no lag at all when it has not.
        // 1kq: the VIEW is re-derived from the PITCHED boom, exactly as pre-1ko, while the POSITION
        // stays on the yaw-only boom (1ko). At full boom this is bit-for-bit the pre-1ko view
        // -up * Rise + pivot.forward * Distance at EVERY pitch. 1ko shipped the view as
        // LookRotation(lookTarget - desired) * _pivot.localRotation and claimed the pitch "commutes"
        // with the -Rise/Distance bias; that claim is false (THINKING 1kq): the composition rotates
        // the WHOLE aim vector - including the -up * Rise term - about the horizontal right, so at
        // full boom it reads lower than pre-1ko by ~0.7 deg at 30 deg pitch and ~4.4 deg at the 60
        // deg clamp (identical only at level). Building the view point from `pitchedBoomEnd` - and
        // only shortening it to the clamp when a clamp is ACTIVE - lets the -up * Rise and right * off
        // terms drop out of `lookTarget - viewPoint` untouched, exactly as the pre-1ko base was formed.
        // Under a pull-in the shortened pitched direction still swings the base toward lookTarget
        // (the documented clamp yaw), and nothing here reads the SMOOTHED camera position, so 1ju's
        // aim-from-the-unlagged-boom invariant survives.
        Vector3 pitchedBoomEnd = lookTarget
            + Vector3.up * (ThirdPersonY - _pivot.localPosition.y)
            - _pivot.forward * ThirdPersonDistance;
        Vector3 viewBoom = finalDist < targetDist
            ? (pitchedBoomEnd - pivotPos).normalized * finalDist
            : pitchedBoomEnd - pivotPos;
        _camera.transform.rotation =
            Quaternion.LookRotation(lookTarget - (pivotPos + viewBoom));
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
using UnityEngine;

/// <summary>
/// Third-person orbit camera (Elden Ring style) for the open world.
///
/// Orbits a target around a configurable pivot with mouse/joystick look,
/// zoom (scroll), terrain collision (keeps the view out of the ground), and
/// optional lock-on to an enemy/object target.
///
/// This is a distinct component from the legacy "CameraFollow" so it can be
/// attached to any camera without conflicts. Attach it to your main camera and
/// assign Target + Pivot.
/// </summary>
public class ThirdPersonCamera : MonoBehaviour
{
    [Header("Targets")]
    public Transform Target;           // the character/root to follow
    public Transform Pivot;            // the point the camera orbits (e.g. character head)
    public Transform LockOnTarget;     // optional target to face when locked on

    [Header("Distances")]
    [Min(0.1f)] public float Distance = 4f;
    [Min(0.1f)] public float MinDistance = 1.5f;
    public float MaxDistance = 10f;
    public float ZoomSpeed = 5f;

    [Header("Look")]
    public float Sensitivity = 3f;
    [Range(-1f, 1f)] public float MinPitch = -0.5f;   // look down
    [Range(-1f, 1f)] public float MaxPitch = 0.8f;    // look up
    public bool InvertY = false;

    [Header("Smoothing")]
    public float PositionSmoothTime = 0.08f;
    public float RotationSmoothTime = 0.1f;

    [Header("Collision")]
    [Tooltip("Layers the camera should collide with (terrain, walls).")]
    public LayerMask CollisionMask = ~0;
    public float CollisionRadius = 0.2f;

    private float _yaw;
    private float _pitch;
    private Vector3 _currentVelocity;
    private bool _lockOnActive;

    private void Start()
    {
        if (Pivot == null && Target != null)
            Pivot = Target;

        // Initial orientation from the target's current yaw so the camera is not
        // snapped arbitrarily on start.
        if (Target != null)
            _yaw = Target.eulerAngles.y;
    }

    /// <summary>Feed raw look input (mouse delta or right stick).</summary>
    public void FeedLook(Vector2 look)
    {
        float invert = InvertY ? -1f : 1f;
        _yaw += look.x * Sensitivity;
        _pitch -= look.y * Sensitivity * invert;
        _pitch = Mathf.Clamp(_pitch, MinPitch * 90f, MaxPitch * 90f);
    }

    /// <summary>Feed scroll / pinch zoom input.</summary>
    public void FeedZoom(float scroll)
    {
        Distance = Mathf.Clamp(Distance - scroll * ZoomSpeed, MinDistance, MaxDistance);
    }

    /// <summary>Set lock-on target (null to clear).</summary>
    public void SetLockOn(Transform target)
    {
        LockOnTarget = target;
        _lockOnActive = target != null;
    }

    public bool IsLockedOn => _lockOnActive;

    private void LateUpdate()
    {
        if (Target == null)
            return;

        Vector3 pivotPos = Pivot != null ? Pivot.position : Target.position + Vector3.up * 1.5f;

        // When locked on, orient the camera toward the LockOnTarget so the
        // character (and camera) both face the enemy.
        Quaternion lookRot;
        if (_lockOnActive && LockOnTarget != null)
        {
            Vector3 toTarget = LockOnTarget.position - pivotPos;
            toTarget.y = 0f;
            if (toTarget.sqrMagnitude > 0.001f)
                _yaw = Mathf.Atan2(toTarget.x, toTarget.z) * Mathf.Rad2Deg;
            _pitch = 15f; // moderate upward angle toward the target
        }

        Quaternion rot = Quaternion.Euler(_pitch, _yaw, 0f);

        Vector3 desiredCamPos = pivotPos - rot * Vector3.forward * Distance;

        // Terrain / wall collision: push the camera out of any geometry so it
        // does not clip behind the ground.
        float finalDistance = Distance;
        if (Physics.SphereCast(
                pivotPos, CollisionRadius, -rot * Vector3.forward,
                out RaycastHit hit, Distance, CollisionMask, QueryTriggerInteraction.Ignore))
        {
            finalDistance = Mathf.Max(hit.distance - CollisionRadius, MinDistance * 0.5f);
            desiredCamPos = pivotPos - rot * Vector3.forward * finalDistance;
        }

        Vector3 smoothPos = Vector3.SmoothDamp(
            transform.position, desiredCamPos, ref _currentVelocity, PositionSmoothTime);

        transform.position = smoothPos;
        transform.rotation = Quaternion.Slerp(
            transform.rotation, Quaternion.LookRotation(pivotPos - transform.position), RotationSmoothTime);
    }
}
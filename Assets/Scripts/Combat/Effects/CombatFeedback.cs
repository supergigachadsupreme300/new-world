using UnityEngine;

/// <summary>
/// Coordinated combat feedback (Task 3.3): wires hit events from a HitboxSystem to
/// HitStop (time freeze), ScreenShake (camera), and floating DamageNumbers.
///
/// Attach to a player/weapon root, assign the Hitbox whose OnHit drives feedback; the
/// camera + HitStop components are located automatically (this object or main camera).
/// </summary>
public class CombatFeedback : MonoBehaviour
{
    [Header("Wiring")]
    [Tooltip("Hitbox whose hits trigger feedback. Auto-found as child if unset.")]
    public HitboxSystem SourceHitbox;
    public Camera TargetCamera;

    [Header("Intensity")]
    [Range(0f, 1f)] public float ShakeOnHit = 0.5f;
    public bool UseHitStop = true;
    public bool ShowDamageNumbers = true;

    private HitStop _hitStop;
    private ScreenShake _shake;

    private void Awake()
    {
        if (TargetCamera == null)
            TargetCamera = Camera.main;

        if (SourceHitbox == null)
            SourceHitbox = GetComponentInChildren<HitboxSystem>();

        // ScreenShake on a child rig so it survives the follow camera overwriting the root.
        if (TargetCamera != null)
        {
            Transform rig = EnsureCameraRig(TargetCamera);
            _shake = rig.GetComponent<ScreenShake>();
            if (_shake == null)
                _shake = rig.gameObject.AddComponent<ScreenShake>();
            _shake.Target = rig;
        }

        // HitStop on this object.
        _hitStop = GetComponent<HitStop>();
        if (_hitStop == null)
            _hitStop = gameObject.AddComponent<HitStop>();
    }

    private static Transform EnsureCameraRig(Camera cam)
    {
        Transform rig = cam.transform.Find("CameraShakeRig");
        if (rig == null)
        {
            var go = new GameObject("CameraShakeRig");
            go.transform.SetParent(cam.transform, false);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            rig = go.transform;
        }
        return rig;
    }

    private void OnEnable()
    {
        if (SourceHitbox != null)
            SourceHitbox.OnHit += HandleHit;
    }

    private void OnDisable()
    {
        if (SourceHitbox != null)
            SourceHitbox.OnHit -= HandleHit;
    }

    private void HandleHit(DamageCalculator.HitResult result, GameObject target)
    {
        _shake?.Shake(ShakeOnHit);
        if (UseHitStop && result.TotalDamage > 0f)
            _hitStop?.Trigger();

        if (ShowDamageNumbers && target != null && result.TotalDamage > 0f)
            DamageNumber.Spawn(target.transform.position, result.TotalDamage, result.IsCritical);
    }
}

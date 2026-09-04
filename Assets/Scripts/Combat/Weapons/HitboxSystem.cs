using UnityEngine;

/// <summary>
/// Generates and manages a weapon hitbox for an attack swing.
///
/// The hitbox is a sphere-cast or box overlap that tracks the weapon's arc
/// during the active frames of an attack animation. It reports every valid
/// target struck and applies damage via DamageCalculator.
///
/// Attach to the weapon's transform (or a hand bone). Activate the hitbox
/// during the active attack window via BeginSwing; it auto-deactivates after
/// Duration seconds.
/// </summary>
public class HitboxSystem : MonoBehaviour
{
    [Header("Shape")]
    public HitboxShape Shape = HitboxShape.Sphere;
    public float Radius = 0.4f;
    public Vector3 BoxSize = new Vector3(0.3f, 0.3f, 0.6f);

    [Header("Detection")]
    public LayerMask HitLayers = ~0;
    public QueryTriggerInteraction TriggerInteraction = QueryTriggerInteraction.Ignore;

    [Header("Damage")]
    public float AttackPower = 10f;
    public float SkillMultiplier = 1f;
    public DamageType Type = DamageType.Physical;
    public IDamageResistance Resistance;
    public float KnockbackForce = 3f;

    [Header("Timing")]
    public float Duration = 0.2f;
    public float Cooldown = 0f;

    private bool _active;
    private float _timer;
    private float _cooldownTimer;
    private Transform _owner;
    private readonly System.Collections.Generic.HashSet<EntityId> _hitThisSwing = new System.Collections.Generic.HashSet<EntityId>();

    public bool IsActive => _active;

    /// <summary>Fires with the DamageCalculator.HitResult for each valid target hit.</summary>
    public event System.Action<DamageCalculator.HitResult, GameObject> OnHit;

    public enum HitboxShape { Sphere, Box }

    /// <summary>
    /// Begin a hitbox sweep. The hitbox will exist for Duration seconds and
    /// report each unique target once per swing.
    /// </summary>
    public void BeginSwing(Transform owner, float powerOverride = -1f)
    {
        if (_cooldownTimer > 0f)
            return;

        _owner = owner;
        _active = true;
        _timer = Duration;
        _hitThisSwing.Clear();
        if (powerOverride > 0f)
            AttackPower = powerOverride;
    }

    public void CancelSwing()
    {
        _active = false;
        _cooldownTimer = Cooldown;
    }

    private void Update()
    {
        if (_cooldownTimer > 0f)
            _cooldownTimer -= Time.deltaTime;

        if (!_active)
            return;

        _timer -= Time.deltaTime;
        if (_timer <= 0f)
        {
            CancelSwing();
            return;
        }

        DetectTargets();
    }

    private void DetectTargets()
    {
        Collider[] hits;
        if (Shape == HitboxShape.Sphere)
        {
            hits = Physics.OverlapSphere(
                transform.position, Radius, HitLayers, TriggerInteraction);
        }
        else
        {
            hits = Physics.OverlapBox(
                transform.position, BoxSize * 0.5f,
                transform.rotation, HitLayers, TriggerInteraction);
        }

        foreach (Collider col in hits)
        {
            EntityId id = col.gameObject.GetEntityId();
            if (_hitThisSwing.Contains(id))
                continue;

            // Never hit ourselves.
            if (_owner != null && col.transform.root == _owner.root)
                continue;

            _hitThisSwing.Add(id);
            ResolveHit(col);
        }
    }

    private void ResolveHit(Collider target)
    {
        float targetDef = 5f;

        var hitCtx = new DamageCalculator.HitContext
        {
            AttackPower        = AttackPower,
            SkillMultiplier    = SkillMultiplier,
            Defense            = targetDef,
            DefenseMultiplier  = 1f,
            Type               = Type,
            Resistance         = Resistance ?? NeutralResistance.Instance,
            WeaknessMultiplier = 1f,
            CriticalMultiplier = 1f,
        };

        var result = DamageCalculator.Calculate(hitCtx, blocked: false);

        OnHit?.Invoke(result, target.gameObject);

        // Apply to the target's health if it implements IDamageable.
        if (target.TryGetComponent<IDamageable>(out var damageable))
            damageable.TakeDamage(Mathf.RoundToInt(result.TotalDamage));

        // Knockback: apply a simple impulse to Rigidbody if present.
        Rigidbody rb = target.attachedRigidbody;
        if (rb != null && KnockbackForce > 0f)
        {
            Vector3 dir = (target.transform.position - transform.position).normalized;
            dir.y = 0.3f; // slight upward pop
            rb.AddForce(dir * KnockbackForce, ForceMode.Impulse);
        }
    }

    // ── Gizmos ──────────────────────────────────────────────────────────────
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0f, 0f, 0.4f);
        if (Shape == HitboxShape.Sphere)
        {
            Gizmos.DrawWireSphere(transform.position, Radius);
        }
        else
        {
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawWireCube(Vector3.zero, BoxSize);
        }
    }
}
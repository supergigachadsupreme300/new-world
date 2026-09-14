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
    private readonly System.Collections.Generic.HashSet<int> _hitThisSwing = new System.Collections.Generic.HashSet<int>();
    private readonly Collider[] _detectBuffer = new Collider[64];

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
        int count;
        if (Shape == HitboxShape.Sphere)
        {
            count = Physics.OverlapSphereNonAlloc(
                transform.position, Radius, _detectBuffer, HitLayers, TriggerInteraction);
        }
        else
        {
            count = Physics.OverlapBoxNonAlloc(
                transform.position, BoxSize * 0.5f, transform.rotation,
                _detectBuffer, HitLayers, TriggerInteraction);
        }

        for (int i = 0; i < count; i++)
        {
            Collider col = _detectBuffer[i];
            int id = col.gameObject.GetInstanceID();
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
            CriticalMultiplier = BackstabMultiplier(target),
        };

        var result = DamageCalculator.Calculate(hitCtx, blocked: false);

        OnHit?.Invoke(result, target.gameObject);

        // Apply to the target's health if it implements IDamageable.
        if (target.TryGetComponent<IDamageable>(out var damageable))
            damageable.TakeDamage(Mathf.RoundToInt(result.TotalDamage));

        // Floating damage number for every hit (popups are self-contained via DamageNumber).
        if (result.TotalDamage > 0f)
            DamageNumber.Spawn(target.transform.position, result.TotalDamage, Type);

        // Knockback: apply a simple impulse to Rigidbody if present. Class StaggerResistMul
        // (Brawler/Monk) softens how easily the player is shoved around.
        Rigidbody rb = target.attachedRigidbody;
        if (rb != null && KnockbackForce > 0f)
        {
            float force = KnockbackForce;
            var resist = target.GetComponent<ClassPassiveManager>();
            if (resist != null) force /= Mathf.Max(resist.StaggerResistMul, 0.1f);

            Vector3 dir = (target.transform.position - transform.position).normalized;
            dir.y = 0.3f; // slight upward pop
            rb.AddForce(dir * force, ForceMode.Impulse);
        }
    }

    /// <summary>
    /// Class backstab multiplier (Rogue/Samurai §3.2.1): a player melee strike landing on an
    /// enemy's back (enemy facing away) gains the accumulated BackstabMul as a critical
    /// multiplier. Returns 1 when not applicable (no player owner, non-enemy target, frontal hit).
    /// </summary>
    private float BackstabMultiplier(Collider target)
    {
        if (_owner == null || target == null) return 1f;
        var stats = _owner.GetComponentInParent<PlayerStats>();
        if (stats == null) return 1f;
        var passives = _owner.GetComponentInParent<ClassPassiveManager>();
        float backMul = passives != null ? passives.BackstabMul : 1f;
        if (backMul <= 1f) return 1f;
        if (!target.TryGetComponent<EnemyController>(out _)) return 1f;
        Vector3 dirToOwner = (_owner.position - target.transform.position).normalized;
        return Vector3.Dot(target.transform.forward, dirToOwner) < 0f ? backMul : 1f;
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
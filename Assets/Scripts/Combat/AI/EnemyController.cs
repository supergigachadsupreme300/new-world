using System;
using UnityEngine;

/// <summary>
/// Open-world enemy brain (planning Task 5.1, game-design §7.1). A lightweight FSM —
/// Patrol → Alert → Chase → Attack → Flee → Dead — driven by visible config. Implements
/// <see cref="IDamageable"/> so the Phase 3/-4 combat pipeline (HitboxSystem, RangedProjectile,
/// SpellCaster, WeaponArtExecutor) damages it through the standard route.
/// </summary>
[RequireComponent(typeof(Collider))]
public class EnemyController : MonoBehaviour, IDamageable
{
    /// <summary>FSM states exposed for UI / debug.</summary>
    public enum EnemyState
    {
        Patrol,
        Alert,
        Chase,
        Attack,
        Flee,
        Dead
    }

    [Header("Identity")]
    [Tooltip("Pending-biome enemy type id (see game-design §7.1).")]
    public string EnemyId;

    [Header("Combat")]
    [SerializeField] private int _maxHealth = 50;
    public int Damage = 10;
    [Tooltip("Flat damage reduction applied after armor factor (blocked=false skips).")]
    public int Armor;
    [Tooltip("0..1 damage reduction multiplier (0 = none).")]
    [Range(0f, 1f)] public float DamageReduction;
    public float AttackRange = 1.5f;
    public float AttackCooldown = 1.2f;
    [Tooltip("DPS check: if the player's LevelUpSystem damage per hit exceeds this, we punish—reserved.")]
    public int AttackDamageThreshold;

    [Header("Movement & Perception")]
    public float MoveSpeed = 2.5f;
    public float ChaseRange = 8f;
    public float AlertRange = 6f;
    public float FleeHealthPercent = 0.2f;
    public float LeashRange = 16f;
    public float PatrolRadius = 6f;
    public float PatrolSpeed = 1.5f;

    [Header("Brain Options")]
    public bool CanFlee = true;
    [Tooltip("Biome this enemy spawns from — drives palette/tier lookup (optional).")]
    public BiomeType Biome;
    public Transform ModelRoot;
    public GameObject DropPrefab;
    [Tooltip("Weighted drop table rolled on death (planning Task 5.2).")]
    public LootTable Loot;

    public int CurrentHealth { get; private set; }
    public EnemyState State { get; private set; }
    public bool IsDead => State == EnemyState.Dead;

    /// <summary>MaxStat% bonus for this enemy tier (used by spawned variants).</summary>
    public float TierScale = 1f;

    private Transform _target;
    private readonly System.Collections.Generic.List<Transform> _targets = new System.Collections.Generic.List<Transform>();
    private Vector3 _origin;
    private Vector3 _patrolTarget;
    private float _attackTimer;
    private float _alertTimer;
    private static readonly Collider[] _scanBuffer = new Collider[16];
    private bool _scanningTargets;

    /// <summary>Night strength multiplier per §7.3 (enemies stronger at night, 1 = day).</summary>
    public static float NightMultiplier => 1.15f;
    public bool IsNight => false;

    private void Awake()
    {
        _origin = transform.position;
        _patrolTarget = PickPatrolTarget();
        CurrentHealth = Mathf.RoundToInt(_maxHealth * TierScale);
        var col = GetComponent<Collider>();
        if (col != null) col.enabled = true;

        if (!string.IsNullOrEmpty(EnemyId) && ModelRoot == null)
            ModelRoot = EnemyModelBuilder.BuildEnemy(transform, EnemyId);
    }

    /// <summary>Recompute max-derived health from the current <see cref="TierScale"/>.</summary>
    public void RefreshHealth() => CurrentHealth = Mathf.RoundToInt(_maxHealth * TierScale);

    private void Update()
    {
        if (IsDead) return;
        TickTargets();
        switch (State)
        {
            case EnemyState.Patrol: UpdatePatrol(); break;
            case EnemyState.Alert: UpdateAlert(); break;
            case EnemyState.Chase: UpdateChase(); break;
            case EnemyState.Attack: UpdateAttack(); break;
            case EnemyState.Flee: UpdateFlee(); break;
        }
    }

    private void TickTargets()
    {
        _targets.Clear();
        int n = Physics.OverlapSphereNonAlloc(transform.position, ChaseRange, _scanBuffer);
        if (!_scanningTargets)
        {
            for (int i = 0; i < n; i++)
            {
                var t = _scanBuffer[i].transform;
                if (t.CompareTag("Player"))
                    _targets.Add(t);
            }
        }
        else
        {
            for (int i = 0; i < n; i++)
            {
                var t = _scanBuffer[i].transform;
                if (t.CompareTag("Player") || t.CompareTag("Companion"))
                    _targets.Add(t);
            }
        }
        _target = ClosestTarget();
    }

    private Transform ClosestTarget()
    {
        Transform best = null;
        float bestD = float.MaxValue;
        foreach (var t in _targets)
        {
            if (t == null) continue;
            float d = Vector3.Distance(transform.position, t.position);
            if (d < bestD) { bestD = d; best = t; }
        }
        return best;
    }

    // --------------------------------------------------------------
    //  FSM STATES
    // --------------------------------------------------------------

    private void UpdatePatrol()
    {
        if (_target != null && Vector3.Distance(transform.position, _target.position) <= AlertRange)
        {
            EnterAlert();
            return;
        }
        if (Vector3.Distance(transform.position, _patrolTarget) < 0.3f)
            _patrolTarget = PickPatrolTarget();
        MoveToward(_patrolTarget, PatrolSpeed);
        Face(_patrolTarget);
    }

    private void UpdateAlert()
    {
        if (_target == null)
        {
            State = EnemyState.Patrol;
            return;
        }
        float d = Vector3.Distance(transform.position, _target.position);
        if (d <= AttackRange)
        {
            EnterAttack();
            return;
        }
        if (d <= ChaseRange)
        {
            State = EnemyState.Chase;
            return;
        }
        Face(_target.position);
        _alertTimer += Time.deltaTime;
        if (_alertTimer >= 3f)
        {
            _alertTimer = 0f;
            State = EnemyState.Patrol;
        }
    }

    private void UpdateChase()
    {
        if (_target == null) { State = EnemyState.Patrol; return; }
        float d = Vector3.Distance(transform.position, _target.position);
        if (d <= AttackRange)
        {
            EnterAttack();
            return;
        }
        if (d > LeashRange)
        {
            State = EnemyState.Patrol;
            _patrolTarget = PickPatrolTarget();
            return;
        }
        if (ShouldFlee())
        {
            EnterFlee();
            return;
        }
        MoveToward(_target.position, MoveSpeed * (IsNight ? NightMultiplier : 1f));
        Face(_target.position);
    }

    private void UpdateAttack()
    {
        if (_target == null) { State = EnemyState.Patrol; return; }
        float d = Vector3.Distance(transform.position, _target.position);
        if (d > AttackRange * 1.15f)
        {
            State = EnemyState.Chase;
            return;
        }
        if (ShouldFlee())
        {
            EnterFlee();
            return;
        }
        Face(_target.position);
        _attackTimer += Time.deltaTime;
        if (_attackTimer >= AttackCooldown)
        {
            _attackTimer = 0f;
            StrikeTarget(_target);
        }
    }

    private void UpdateFlee()
    {
        if (_target == null) { State = EnemyState.Patrol; return; }
        if (!ShouldFlee())
        {
            State = EnemyState.Chase;
            return;
        }
        // Run away from the target toward the spawn origin.
        Vector3 away = (transform.position - _target.position).normalized;
        Vector3 dest = transform.position + away * 10f;
        // Prefer to run toward home, not over cliffs.
        if (Vector3.Distance(dest, _origin) > LeashRange)
            dest = _origin;
        MoveToward(dest, MoveSpeed * 1.35f);
    }

    // --------------------------------------------------------------
    //  TRANSITIONS / HELPERS
    // --------------------------------------------------------------

    private void EnterAlert() { State = EnemyState.Alert; _alertTimer = 0f; Face(_target.position); }
    private void EnterAttack() { _attackTimer = 0f; State = EnemyState.Attack; }
    private void EnterFlee() { State = EnemyState.Flee; }

    private bool ShouldFlee()
    {
        if (!CanFlee) return false;
        return (float)CurrentHealth / Mathf.Max(1, Mathf.RoundToInt(_maxHealth * TierScale)) <= FleeHealthPercent;
    }

    private Vector3 PickPatrolTarget()
    {
        Vector3 p = _origin + UnityEngine.Random.insideUnitSphere * PatrolRadius;
        p.y = 0f;
        return p;
    }

    private void MoveToward(Vector3 dest, float speed)
    {
        transform.position = Vector3.MoveTowards(transform.position, dest, speed * Time.deltaTime);
    }

    private void Face(Vector3 point)
    {
        Vector3 to = point - transform.position;
        to.y = 0f;
        if (to.sqrMagnitude > 0.0001f)
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(to.normalized), Time.deltaTime * 10f);
    }

private void StrikeTarget(Transform target)
    {
        if (target.TryGetComponent<IPlayerDamageReceiver>(out var receiver))
            receiver.ReceiveDamage(Damage);
        else if (target.TryGetComponent<IDamageable>(out var damageable))
            damageable.TakeDamage(Damage);
    }

    // --------------------------------------------------------------
    //  DAMAGE ROUTE (Phase 3/4 pipeline calls through this)
    // --------------------------------------------------------------

    /// <summary>IDamageable — returns remaining health.</summary>
    public int TakeDamage(int amount)
    {
        if (IsDead) return 0;
        int final = Mathf.Max(0, amount - Armor);
        final = (int)(final * (1f - DamageReduction));
        CurrentHealth = Mathf.Max(0, CurrentHealth - final);
        if (CurrentHealth <= 0)
        {
            Die();
            return 0;
        }
        // First contact wakes patrol/alert to chase.
        if (State == EnemyState.Patrol || State == EnemyState.Alert)
            State = EnemyState.Chase;
        return CurrentHealth;
    }

    private void Die()
    {
        State = EnemyState.Dead;
        RollAndSpawnLoot();
        DropDrop();
        Destroy(gameObject, 0.2f);
    }

    private void DropDrop()
    {
        if (DropPrefab == null) return;
        var go = Instantiate(DropPrefab, transform.position + Vector3.up * 0.5f, Quaternion.identity);
        go.transform.SetParent(transform.parent);
    }

    /// <summary>Roll the loot table and spawn LootDrops, scaled by the target's luck.</summary>
    private void RollAndSpawnLoot()
    {
        if (Loot == null || Loot.Entries == null || Loot.Entries.Count == 0) return;

        float luck = 0f;
        var receiver = _target != null ? _target.GetComponent<ILootLuckProvider>() : null;
        if (receiver != null)
            luck = Mathf.Max(0f, receiver.GetLootQuality() - 1f);

        Vector3 origin = transform.position + Vector3.up * 0.6f;
        int index = 0;
        foreach (var pair in Loot.Roll(luck))
        {
            var go = new GameObject("LootDrop_" + index++);
            go.transform.SetParent(transform.parent);
            go.transform.position = origin + new Vector3(
                UnityEngine.Random.Range(-0.4f, 0.4f), 0f,
                UnityEngine.Random.Range(-0.4f, 0.4f));
            var drop = go.AddComponent<LootDrop>();
            drop.Item = pair.Key;
            drop.Count = pair.Value;
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(_origin, PatrolRadius);
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, AttackRange);
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, AlertRange);
        Gizmos.color = Color.magenta;
        Gizmos.DrawWireSphere(transform.position, ChaseRange);
    }
}

/// <summary>
/// Optional seam so enemies can damage the player without coupling to the concrete
/// player type. A player/companion adapter implementing <see cref="IDamageable"/> is
/// the standard pipeline route; this interface lets a custom receiver register instead.
/// </summary>
public interface IPlayerDamageReceiver
{
    void ReceiveDamage(int amount);
}
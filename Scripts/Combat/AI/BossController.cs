using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Multi-phase boss brain (planning Task 5.1, game-design §7.2 boss arenas / §8.1 boss bar).
/// Moves through a fixed number of phases as HP falls, switching attack patterns per phase,
/// and exposes a health-change event so a UI boss bar can subscribe. Implements
/// <see cref="IDamageable"/> so the standard combat pipeline damages it.
/// </summary>
[RequireComponent(typeof(Collider))]
public class BossController : MonoBehaviour, IDamageable
{
    [Header("Boss Identity")]
    public string BossId = "area_boss";
    public string DisplayName = "Boss";

    [Header("Health")]
    public int MaxHealth = 500;
    public int Armor;
    [Tooltip("Additive flat damage reduction applied each hit.")]
    public int FlatDamageReduction;

    [Header("Phase Tuning")]
    [Tooltip("HP fractions (e.g. .66, .33) at which the boss advances a phase. Length defines phase count.")]
    public float[] PhaseThresholds = { 0.66f, 0.33f };

    [Header("Basic Combat")]
    public float MoveSpeed = 2f;
    public float AttackRange = 2.5f;
    public float AttackCooldown = 1f;
    public int MeleeDamage = 20;

    [Header("Pattern Unlocks")]
    public bool CanCharge = true;
    public bool CanBarrage = true;
    public bool CanSlam = true;

    public int CurrentHealth { get; private set; }
    public int CurrentPhase { get; private set; }
    public bool IsDead { get; private set; }
    public float HealthPercent => MaxHealth > 0 ? (float)CurrentHealth / MaxHealth : 0f;

    /// <summary>Raised whenever health changes (for boss bar UI): (current, max).</summary>
    public event Action<int, int> OnHealthChanged;
    /// <summary>Raised once when the boss dies.</summary>
    public event Action<BossController> OnBossDefeated;

    private Transform _target;
    private Vector3 _home;
    private float _attackTimer;
    private bool _frozen;

    private void Awake()
    {
        _home = transform.position;
        CurrentHealth = MaxHealth;
        CurrentPhase = 0;
        var col = GetComponent<Collider>();
        if (col != null) col.enabled = true;
    }

    private void Update()
    {
        if (IsDead || _frozen) return;
        if (_target == null)
        {
            TryAcquireTarget(ref _frozen);
            return;
        }

        float d = Vector3.Distance(transform.position, _target.position);
        Face(_target.position);

        if (d <= AttackRange)
        {
            _attackTimer += Time.deltaTime;
            if (_attackTimer >= AttackCooldown)
            {
                _attackTimer = 0f;
                PickAndRunAttack();
            }
        }
        else
        {
            MoveToward(_target.position, MoveSpeed);
        }
    }

    private bool TryAcquireTarget(ref bool frozen)
    {
        // In a production wiring this scans for the player; kept as an explicit hook.
        return _target != null;
    }

    /// <summary>Assign the player/companion to pursue.</summary>
    public void SetTarget(Transform t) => _target = t;

    private void PickAndRunAttack()
    {
        // Phase 0 default melee; later phases grant patterns (§7-8 tuning).
        StartCoroutine(SwingAttack());
    }

    private IEnumerator SwingAttack()
    {
        float t = 0f;
        while (t < 0.45f)
        {
            t += Time.deltaTime;
            yield return null;
        }
        if (_target != null && Vector3.Distance(transform.position, _target.position) <= AttackRange * 1.15f)
            HitTarget(_target, MeleeDamage);
    }

    private void HitTarget(Transform target, int amount)
    {
        if (target.TryGetComponent<IPlayerDamageReceiver>(out var receiver))
            receiver.ReceiveDamage(amount);
        else if (target.TryGetComponent<IDamageable>(out var damageable))
            damageable.TakeDamage(amount);
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
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(to.normalized), Time.deltaTime * 8f);
    }

    /// <summary>IDamageable — returns remaining health.</summary>
    public int TakeDamage(int amount)
    {
        if (IsDead) return 0;
        int final = Mathf.Max(0, amount - Armor - FlatDamageReduction);
        CurrentHealth = Mathf.Max(0, CurrentHealth - final);
        UpdatePhase();
        OnHealthChanged?.Invoke(CurrentHealth, MaxHealth);
        if (CurrentHealth <= 0)
            Defeated();
        return CurrentHealth;
    }

    private void UpdatePhase()
    {
        for (int i = PhaseThresholds.Length - 1; i >= 0; i--)
        {
            if (HealthPercent <= PhaseThresholds[i])
            {
                if (i + 1 > CurrentPhase)
                {
                    CurrentPhase = i + 1;
                    OnPhaseChanged(CurrentPhase);
                }
                return;
            }
        }
    }

    private void OnPhaseChanged(int phase)
    {
        // Future: swap model tint, unlock skill behavior, speed up.
        MoveSpeed += 0.5f;
        AttackCooldown = Mathf.Max(0.4f, AttackCooldown - 0.15f);
    }

    private void Defeated()
    {
        IsDead = true;
        _frozen = true;
        OnBossDefeated?.Invoke(this);
        // Leave the corpse/cleared state to the owning area.
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.25f, 0.15f);
        Gizmos.DrawWireSphere(transform.position, AttackRange);
    }
}
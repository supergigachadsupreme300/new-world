using System;
using UnityEngine;

/// <summary>
/// Ranged weapon behavior (§3.6 Layer 3). Fires projectiles (or raycasts), consumes
/// ammo (arrows/bolts) via IAmmoProvider, applies accuracy from Dexterity, and deals
/// damage from weapon.base (weapon ceiling — NOT stat-scaled, §3.4).
///
/// Attach to the weapon/root; assign Muzzle (fire origin) and optional Projectile prefab.
/// If no projectile prefab is set, falls back to a hit-scan raycast with a tracer.
/// </summary>
public class RangedWeaponBehavior : MonoBehaviour, IWeaponBehavior
{
    [Header("Wiring")]
    public WeaponData Data;
    public Transform Muzzle;
    public GameObject ProjectilePrefab;

    [Header("Runtime")]
    public DamageType ShotType = DamageType.Physical;

    /// <summary>Ammo source; defaults to InfiniteAmmo until Inventory exists.</summary>
    public IAmmoProvider Ammo = InfiniteAmmo.Instance;

    /// <summary>Optional stat accessor supplying Dexterity for accuracy (wired Phase 4).</summary>
    public IStatProvider Stats;

    [Header("Projectile")]
    public float ProjectileSpeed = 30f;

    private bool _attacking;

    public event Action Completed;
    public bool IsAttacking => _attacking;

    public void BeginAttack(AttackCommand cmd)
    {
        if (_attacking) return;
        if (Data != null && Data.AmmoItemId != null && Ammo != null)
        {
            int count = Ammo.Count(Data.AmmoItemId);
            if (count == 0) return; // no ammo
        }

        _attacking = true;

        // Damage comes from weapon.base (weapon ceiling, not stat-scaled).
        float damage = Data != null ? Data.BaseDamage : 10f;
        ShotType = Data != null ? Data.Type : ShotType;

        Vector3 origin = Muzzle != null ? Muzzle.position : cmd.Origin != null ? cmd.Origin.position : transform.position;
        Vector3 dir = cmd.Direction.sqrMagnitude > 0.0001f
            ? cmd.Direction.normalized
            : transform.forward;

        // Accuracy from Dexterity — spread angle decreases as accuracy rises.
        float accuracy = 1f;
        if (Stats != null && Data != null)
            accuracy = 1f + Stats.GetStat(WeaponScalingStat.Dexterity) * Data.AccuracyFromDex;
        Vector3 aimed = ApplySpread(dir, Mathf.Clamp01(1f / Mathf.Max(accuracy, 0.01f)));

        // Consume ammo.
        if (Data != null && Data.AmmoItemId != null)
            Ammo?.Consume(Data.AmmoItemId);

        FireProjectile(damage, origin, aimed, cmd);

        // Ranged attacks complete immediately (projectile carries the damage).
        _attacking = false;
        Completed?.Invoke();
    }

    public void ActiveFrame()
    {
    }

    public void Cancel()
    {
        _attacking = false;
    }

    private Vector3 ApplySpread(Vector3 dir, float spread)
    {
        if (spread <= 0f) return dir;
        return (dir + UnityEngine.Random.insideUnitSphere * spread * 0.15f).normalized;
    }

    private void FireProjectile(float damage, Vector3 origin, Vector3 dir, AttackCommand cmd)
    {
        if (ProjectilePrefab != null)
        {
            GameObject go = Instantiate(ProjectilePrefab, origin, Quaternion.LookRotation(dir));
            var proj = go.GetComponent<RangedProjectile>();
            if (proj != null)
                proj.Launch(dir, ProjectileSpeed, damage, ShotType, cmd.Origin);
            else if (go.TryGetComponent<Rigidbody>(out var rb))
                rb.linearVelocity = dir * ProjectileSpeed;
        }
        else
        {
            // Hit-scan fallback with tracer over reach.
#if UNITY_EDITOR
            Debug.DrawRay(origin, dir * (Data != null ? Data.Reach : 60f), Color.yellow, 0.5f);
#endif
            if (Physics.Raycast(origin, dir, out RaycastHit hit, Data != null ? Data.Reach : 60f))
            {
                var ctx = new DamageCalculator.HitContext
                {
                    AttackPower = damage,
                    SkillMultiplier = 1f,
                    Defense = 5f,
                    DefenseMultiplier = 1f,
                    Type = ShotType,
                    Resistance = NeutralResistance.Instance,
                    WeaknessMultiplier = 1f,
                    CriticalMultiplier = 1f,
                };
                var result = DamageCalculator.Calculate(ctx, false);
                OnShot?.Invoke(result, hit.collider.gameObject);
                if (hit.collider.TryGetComponent<IDamageable>(out var damageable))
                    damageable.TakeDamage(Mathf.RoundToInt(result.TotalDamage));
            }
        }
    }

    /// <summary>Fires with the resolved hit for raycast-based shots.</summary>
    public event System.Action<DamageCalculator.HitResult, GameObject> OnShot;
}

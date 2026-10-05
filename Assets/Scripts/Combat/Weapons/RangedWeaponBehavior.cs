using System;
using UnityEngine;

/// <summary>
/// Ranged weapon behavior (§3.6 Layer 3). Fires projectiles (or raycasts), consumes
/// ammo (arrows/bolts) via IAmmoProvider, applies accuracy from Dexterity, and deals
/// damage from weapon.base (weapon ceiling — NOT stat-scaled, §3.4).
///
/// Attach to the weapon/root; assign Muzzle (fire origin) and optional Projectile prefab.
/// If no projectile prefab is set, a generated arrow projectile is built at runtime.
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

    /// <summary>Flight time of a fully-uncharged shot (draw extends it).</summary>
    public float BaseLifetime = 4f;

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

        // Charge/draw scales damage, projectile speed and flight distance: a full draw
        // (~charge 0..1) roughly doubles damage and reach while the bolt flies ~1.5x faster.
        float charge = Mathf.Clamp01(cmd.ChargeLevel);

        // Damage comes from weapon.base (weapon ceiling, not stat-scaled).
        float damage = Data != null ? Data.BaseDamage : 10f;
        damage *= Mathf.Lerp(1f, 2.5f, charge);
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

        // Draw extends the flight envelope: speed rises and the projectile destroys later so a
        // full draw carries roughly 2.4x the base distance (speed x lifetime).
        float speed = ProjectileSpeed * Mathf.Lerp(1f, 1.5f, charge);
        float lifetime = BaseLifetime * Mathf.Lerp(1f, 2f, charge);
        float reach = Data != null ? Data.Reach * Mathf.Lerp(1f, 2f, charge) : 60f;

        // Consume ammo.
        if (Data != null && Data.AmmoItemId != null)
            Ammo?.Consume(Data.AmmoItemId);

        FireProjectile(damage, origin, aimed, cmd, speed, lifetime, reach);

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

    private void FireProjectile(float damage, Vector3 origin, Vector3 dir, AttackCommand cmd, float speed, float lifetime, float reach)
    {
        if (ProjectilePrefab != null)
        {
            GameObject go = Instantiate(ProjectilePrefab, origin, Quaternion.LookRotation(dir));
            var proj = go.GetComponent<RangedProjectile>();
            if (proj != null)
            {
                proj.Lifetime = lifetime;
                proj.Launch(dir, speed, damage, ShotType, cmd.Origin);
            }
            else if (go.TryGetComponent<Rigidbody>(out var rb))
                rb.linearVelocity = dir * speed;
        }
        else
        {
            // No authored prefab: build a generated projectil at runtime so shots read as real
            // projectiles (not hit-scan) — an arrow for bows, a spinning hammer head for the
            // throwing hammer.
            Vector3 spawn = origin + dir * 0.3f;
            var go = BuildDefaultProjectileVisual();
            go.transform.position = spawn;
            go.transform.rotation = Quaternion.LookRotation(dir);
            var proj = go.AddComponent<RangedProjectile>();
            proj.Lifetime = lifetime;
            proj.Launch(dir, speed, damage, ShotType, cmd.Origin);
        }
    }

    /// <summary>Pick the runtime projectile visual for the equipped ranged weapon (no prefab case).
    /// <para>1jd: the arrow and hammer models moved to <see cref="WeaponProjectileModelBuilder"/>
    /// (under <c>Models/</c>), so "arrow model" and "projectile model" are findable by name. The PICK
    /// stays here: which projectile a weapon fires is firing behaviour, not geometry.</para></summary>
    private GameObject BuildDefaultProjectileVisual()
    {
        if (Data != null && Data.id == "throwing_hammer")
            return WeaponProjectileModelBuilder.BuildHammerVisual();
        return WeaponProjectileModelBuilder.BuildArrowVisual();
    }
}

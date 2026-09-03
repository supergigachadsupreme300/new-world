using System;
using UnityEngine;

/// <summary>
/// Simple projectile fired by RangedWeaponBehavior. Flies straight, damages the first
/// valid target hit, and destroys itself after a lifetime or on impact.
/// </summary>
public class RangedProjectile : MonoBehaviour
{
    public float Damage = 10f;
    public DamageType Type = DamageType.Physical;
    public float Lifetime = 4f;
    public LayerMask HitLayers = ~0;

    private Vector3 _dir;
    private float _speed;
    private Transform _owner;
    private bool _launched;

    /// <summary>Fires with each valid target collider the projectile strikes.</summary>
    public event Action<GameObject> OnHit;

    /// <summary>Configure and launch the projectile.</summary>
    public void Launch(Vector3 direction, float speed, float damage, DamageType type, Transform owner)
    {
        _dir = direction;
        _speed = speed;
        Damage = damage;
        Type = type;
        _owner = owner;
        _launched = true;
    }

    private void Update()
    {
        if (!_launched) return;

        Lifetime -= Time.deltaTime;
        if (Lifetime <= 0f)
        {
            Destroy(gameObject);
            return;
        }

        float step = _speed * Time.deltaTime;
        if (Physics.Raycast(transform.position, _dir, out RaycastHit hit, step, HitLayers))
        {
            if (_owner == null || hit.collider.transform.root != _owner.root)
            {
                OnHit?.Invoke(hit.collider.gameObject);
                ApplyDirectDamage(hit.collider.gameObject);
                Destroy(gameObject);
                return;
            }
        }

        transform.position += _dir * step;
    }

    private void ApplyDirectDamage(GameObject target)
    {
        var ctx = new DamageCalculator.HitContext
        {
            AttackPower = Damage,
            SkillMultiplier = 1f,
            Defense = 5f,
            DefenseMultiplier = 1f,
            Type = Type,
            Resistance = NeutralResistance.Instance,
            WeaknessMultiplier = 1f,
            CriticalMultiplier = 1f,
        };
        var result = DamageCalculator.Calculate(ctx, false);

        if (target.TryGetComponent<IDamageable>(out var damageable))
            damageable.TakeDamage(result.TotalDamage);
    }
}

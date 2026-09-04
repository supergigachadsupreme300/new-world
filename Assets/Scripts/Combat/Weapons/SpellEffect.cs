using System;
using UnityEngine;

/// <summary>
/// Flight effect for a projectile or zone spell (§3.8). Spawned by SpellCaster,
/// flies straight, and resolves the spell's damage on impact via the owning caster.
/// </summary>
public class SpellEffect : MonoBehaviour
{
    public float Speed = 20f;
    public float Lifetime = 4f;
    public bool IsZone;
    public float Radius = 1f;

    [Tooltip("Layers the projectile can collide with. Defaults to Everything when 0.")]
    public LayerMask HitLayers = ~0;

    private SpellData _spell;
    private float _power;
    private Vector3 _dir;
    private SpellCaster _caster;
    private bool _launched;

    /// <summary>Configure the effect with spell + resolved power. Returns this for chaining.</summary>
    public SpellEffect Initialize(SpellData spell, float power, Vector3 dir, SpellCaster caster)
    {
        _spell = spell;
        _power = power;
        _dir = dir;
        _caster = caster;
        return this;
    }

    /// <summary>Begin flight (projectiles only; zones resolve immediately).</summary>
    public void Launch(float speed)
    {
        if (_spell != null && _spell.Delivery == SpellDelivery.Zone)
        {
            IsZone = true;
            Radius = _spell.Radius;
            ResolveZone();
            Destroy(gameObject);
            return;
        }

        _launched = true;
        Speed = speed;
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

        float step = Speed * Time.deltaTime;
        // Raycast the full step to avoid tunneling and to respect HitLayers.
        if (Physics.Raycast(transform.position, _dir, out RaycastHit hit, step, HitLayers))
        {
            ResolveProjectileImpact(hit.collider.gameObject);
            return;
        }
        transform.position += _dir * step;
    }

    private void ResolveProjectileImpact(GameObject hitObject)
    {
        // Also affect everything in the splash radius factoring in the caster.
        Collider[] cols = Physics.OverlapSphere(transform.position,
            _spell != null && _spell.Radius > 0f ? _spell.Radius : 0.2f, HitLayers);
        foreach (var col in cols)
        {
            if (_caster != null && col.transform.root == _caster.transform.root) continue;
            _caster?.ResolveHitAt(col.gameObject, _spell, _power);
        }
        if (hitObject != null)
            _caster?.ResolveHitAt(hitObject, _spell, _power);
        if (_spell != null && _spell.ImpactEffectPrefab != null)
            Instantiate(_spell.ImpactEffectPrefab, transform.position, Quaternion.identity);
        Destroy(gameObject);
    }

    private void ResolveZone()
    {
        Collider[] cols = Physics.OverlapSphere(transform.position, Radius, HitLayers);
        foreach (var col in cols)
        {
            if (_caster != null && col.transform.root == _caster.transform.root) continue;
            _caster?.ResolveHitAt(col.gameObject, _spell, _power);
        }
    }
}

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

    [Tooltip("Constant-size center probe that is the ONLY detector for ground hits. Never scaled "
        + "by charge, so a giant charged projectile no longer detonates the instant its scaled "
        + "body clips the terrain at spawn.")]
    public float GroundProbeRadius = 0.2f;

    private SpellData _spell;
    private float _power;
    private Vector3 _dir;
    private SpellCaster _caster;
    private bool _launched;
    private float _radiusMult = 1f;
    private readonly Collider[] _groundHits = new Collider[8];

    /// <summary>Configure the effect with spell + resolved power. Returns this for chaining.
    /// <paramref name="radiusMult"/> scales the splash/zone radius (charged casts).</summary>
    public SpellEffect Initialize(SpellData spell, float power, Vector3 dir, SpellCaster caster,
        float radiusMult = 1f)
    {
        _spell = spell;
        _power = power;
        _dir = dir;
        _caster = caster;
        _radiusMult = Mathf.Max(radiusMult, 0.01f);
        return this;
    }

    /// <summary>Begin flight (projectiles only; zones resolve immediately).</summary>
    public void Launch(float speed)
    {
        if (_spell != null && _spell.Delivery == SpellDelivery.Zone)
        {
            IsZone = true;
            Radius = _spell.Radius * _radiusMult;
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

        // Hitbox 1 — the "normal" probe: a full-step raycast that detonates on enemies, walls,
        // and props (the caster's own body is excluded so a bolt spawned at the hand never
        // detonates on the caster). Ground colliders are skipped here — terrain contact is judged
        // only by the small center probe below, so a charged-up (giant) body no longer self-
        // triggers on the ground at spawn.
        if (Physics.Raycast(transform.position, _dir, out RaycastHit hit, step, HitLayers))
        {
            if (_caster == null || hit.collider.transform.root != _caster.transform.root)
            {
                if (!IsGroundCollider(hit.collider))
                {
                    ResolveProjectileImpact(hit.collider.gameObject);
                    return;
                }
            }
        }

        // Hitbox 2 — the ground probe: a small, constant-size overlap AT the projectile center
        // (never scaled by charge). It reacts ONLY to terrain, so the big charged body can graze
        // the ground without detonating; the bolt blows up only once the fixed center probe
        // reaches it.
        int groundCount = Physics.OverlapSphereNonAlloc(transform.position, GroundProbeRadius,
            _groundHits, HitLayers);
        for (int i = 0; i < groundCount; i++)
        {
            Collider col = _groundHits[i];
            if (_caster != null && col.transform.root == _caster.transform.root) continue;
            if (!IsGroundCollider(col)) continue;
            ResolveProjectileImpact(col.gameObject);
            return;
        }

        transform.position += _dir * step;
    }

    private void ResolveProjectileImpact(GameObject hitObject)
    {
        // Also affect everything in the splash radius factoring in the caster.
        Collider[] cols = Physics.OverlapSphere(transform.position,
            _spell != null && _spell.Radius > 0f ? _spell.Radius * _radiusMult : 0.2f, HitLayers);
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

    /// <summary>True when the collider belongs to the world terrain — the "Ground" plane, a field
    /// visual, or a streamed terrain chunk. Ground hits are tracked ONLY by the small center
    /// probe, never by the (possibly charge-scaled) normal probe.</summary>
    private static bool IsGroundCollider(Collider c)
    {
        if (c == null) return false;
        return c.name == "Ground" || c.name == "FieldVisual"
            || c.GetComponentInParent<ChunkObject>() != null;
    }

    private void ResolveZone()
    {
        Vector3 ground = transform.position;
        if (Physics.Raycast(transform.position + Vector3.up * 0.1f, Vector3.down, out RaycastHit hit, 4f))
            ground = hit.point;
        SkillFx.RingFlash(ground, Vector3.up,
            _spell != null ? DamageNumber.ColorFor(_spell.Type) : Color.white,
            Radius, 0.5f);

        Collider[] cols = Physics.OverlapSphere(transform.position, Radius, HitLayers);
        foreach (var col in cols)
        {
            if (_caster != null && col.transform.root == _caster.transform.root) continue;
            _caster?.ResolveHitAt(col.gameObject, _spell, _power);
        }
    }
}

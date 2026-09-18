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
    private Transform _casterRoot;
    private bool _launched;
    private float _radiusMult = 1f;
    private readonly Collider[] _groundHits = new Collider[8];
    private readonly Collider[] _splashBuffer = new Collider[128];
    private readonly Collider[] _homingBuffer = new Collider[32];

    private const float MissileTurnRate = 240f;
    private bool _homing;
    private bool _locked;
    private Transform _homingTarget;
    private Vector3 _homingAim;

    /// <summary>Configure the effect with spell + resolved power. Returns this for chaining.
    /// <paramref name="radiusMult"/> scales the splash/zone radius (charged casts).</summary>
    public SpellEffect Initialize(SpellData spell, float power, Vector3 dir, SpellCaster caster,
        float radiusMult = 1f)
    {
        _spell = spell;
        _power = power;
        _dir = dir;
        _caster = caster;
        _casterRoot = caster != null ? caster.transform.root : null;
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
        if (_spell != null && _spell.Shape == ProjectileShape.Missile)
            _homing = true;
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

        if (_homing)
        {
            UpdateMissileTargeting();
            SteerTowardTarget();
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

    /// <summary>
    /// Missile homing (§3.8.1, ProjectileShape.Missile): bends the flight toward a target so
    /// missiles chase rather than fly straight. Reevaluated every frame along the CURRENT
    /// trajectory (the direction we are bending right now), so whichever foe comes onto the
    /// flight path gets top priority. Priority — (1) the enemy sitting on the trajectory ahead;
    /// (2) otherwise keep chasing the locked target's last known spot; (3) if never locked (or
    /// the locked foe died), lock the nearest enemy in a forward cone. With no target at all the
    /// missile flies straight.
    /// </summary>
    private void UpdateMissileTargeting()
    {
        Transform onTrajectory = FirstEnemyOnTrajectory(Lookahead());
        if (onTrajectory != null)
        {
            LockOn(onTrajectory);
            return;
        }
        if (_locked && _homingTarget != null)
            return;

        Transform nearest = NearestEnemyInCone(Lookahead(), 50f);
        if (nearest != null)
            LockOn(nearest);
    }

    /// <summary>First enemy root laying on the trajectory probe (a ray down the current flight
    /// direction), ground and caster skipped — the target the missile is about to fly into.</summary>
    private Transform FirstEnemyOnTrajectory(float distance)
    {
        var hits = Physics.RaycastAll(transform.position, _dir, distance, HitLayers);
        Array.Sort(hits);
        foreach (var h in hits)
        {
            if (h.collider == null) continue;
            if (IsGroundCollider(h.collider)) continue;
            Transform root = h.collider.transform.root;
            if (root == _casterRoot) continue;
            if (!IsEnemyRoot(root)) continue;
            return root;
        }
        return null;
    }

    /// <summary>How far ahead to probe the trajectory: the spell's reach, at least one second of
    /// flight so the bend has time to engage.</summary>
    private float Lookahead()
    {
        float d = _spell != null && _spell.Range > 0f ? _spell.Range : 12f;
        return Mathf.Max(d, Speed);
    }

    /// <summary>Steer toward the locked target (or its last known spot), bending the flight path.</summary>
    private void SteerTowardTarget()
    {
        if (_homingTarget != null)
            _homingAim = _homingTarget.position + Vector3.up * 0.8f;
        if (!_locked)
            return;
        Vector3 to = _homingAim - transform.position;
        if (to.sqrMagnitude < 0.01f) return;
        Vector3 next = Vector3.RotateTowards(_dir, to.normalized,
            MissileTurnRate * Mathf.Deg2Rad * Time.deltaTime, 0f);
        _dir = next.sqrMagnitude < 0.001f ? to.normalized : next.normalized;
        transform.rotation = Quaternion.LookRotation(_dir);
    }

    private void LockOn(Transform root)
    {
        _locked = true;
        _homingTarget = root;
        _homingAim = root.position + Vector3.up * 0.8f;
    }

    /// <summary>Nearest hostile root roughly ahead of the missile (enemy/boss, not player or partner).</summary>
    private Transform NearestEnemyInCone(float radius, float maxAngleDeg)
    {
        int count = Physics.OverlapSphereNonAlloc(transform.position, radius, _homingBuffer, HitLayers);
        Transform best = null;
        float bestScore = float.MaxValue;
        for (int i = 0; i < count; i++)
        {
            var col = _homingBuffer[i];
            if (col == null) continue;
            Transform root = col.transform.root;
            if (root == transform.root || root == _casterRoot) continue;
            if (!IsEnemyRoot(root)) continue;
            Vector3 to = col.transform.position - transform.position;
            float ang = Vector3.Angle(_dir, to);
            if (ang > maxAngleDeg) continue;
            float score = to.sqrMagnitude * (1f + ang * 0.02f);
            if (score < bestScore)
            {
                bestScore = score;
                best = root;
            }
        }
        return best;
    }

    private static bool IsEnemyRoot(Transform root)
    {
        if (root == null) return false;
        if (root.CompareTag("Player") || root.CompareTag("Companion")) return false;
        return root.TryGetComponent<EnemyController>(out _) || root.TryGetComponent<BossController>(out _);
    }

    private void ResolveProjectileImpact(GameObject hitObject)
    {
        // Also affect everything in the splash radius factoring in the caster.
        int count = Physics.OverlapSphereNonAlloc(transform.position,
            _spell != null && _spell.Radius > 0f ? _spell.Radius * _radiusMult : 0.2f,
            _splashBuffer, HitLayers);
        for (int i = 0; i < count; i++)
        {
            var col = _splashBuffer[i];
            if (col == null) continue;
            if (_caster != null && col.transform.root == _caster.transform.root) continue;
            _caster?.ResolveHitAt(col.gameObject, _spell, _power);
        }
        if (hitObject != null)
            _caster?.ResolveHitAt(hitObject, _spell, _power);
        if (_spell != null && _spell.ImpactEffectPrefab != null)
            Instantiate(_spell.ImpactEffectPrefab, transform.position, Quaternion.identity);

        // Every magic projectile dents the ground where it strikes. Earth projectiles
        // (TerrainShape.Crater — the school signature) carve a full crater scaled to the
        // spell radius; every other projectile leaves a small uniform impact dent so any
        // bolt (fireball, frost, arcane, lightning, dark, wind, water…) visibly disturbs
        // the terrain. Both probe the ground beneath the impact (the pit is carved as
        // flat-topped slab steps that always keep a walkable floor — never a void) and
        // never carve at the caster's own feet at cast time (FireProjectile
        // only spawns the bolt; no launch-site pit).
        if (_spell != null)
        {
            float dentRadius = 1.4f;
            if (_spell.TerrainShape == TerrainShape.Crater)
                dentRadius = Mathf.Max(1.2f, _spell.Radius);
            Vector3 probe = transform.position + Vector3.up * 0.1f;
            if (Physics.Raycast(probe, Vector3.down, out RaycastHit groundHit, 30f))
                TerrainDeformer.Apply(groundHit.point, dentRadius * _radiusMult,
                    TerrainShape.Crater, _dir);
        }

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

        int count = Physics.OverlapSphereNonAlloc(transform.position, Radius, _splashBuffer, HitLayers);
        for (int i = 0; i < count; i++)
        {
            var col = _splashBuffer[i];
            if (col == null) continue;
            if (_caster != null && col.transform.root == _caster.transform.root) continue;
            _caster?.ResolveHitAt(col.gameObject, _spell, _power);
        }
    }
}

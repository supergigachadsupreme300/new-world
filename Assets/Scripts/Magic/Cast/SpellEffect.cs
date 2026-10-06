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
    private int _missileScanFrame;
    private readonly Collider[] _groundHits = new Collider[8];
    private readonly Collider[] _splashBuffer = new Collider[128];
    private readonly Collider[] _homingBuffer = new Collider[32];

    private const float MissileTurnRate = 240f;
    private bool _homing;
    private bool _locked;
    private Transform _homingTarget;
    private Vector3 _homingAim;

    // Collider-on-demand (1dq): the projectile keeps its current chunk in the
    // ColliderRequestRegistry so a long-range bolt still lands on far terrain, while every other
    // chunk beyond the player ring stays collider-free. Moved chunk-by-chunk as the bolt flies.
    private TerrainChunkCoord _requestedChunk;
    private bool _requestActive;

    /// <summary>
    /// 1ie/1if: resolved once in <see cref="Initialize"/>. A flight effect can only die once, so
    /// there is no reason to hash on the impact path — and the per-spell Core colour it carries is
    /// what both the impact flash and the zone-splash ring read.
    /// </summary>
    private SpellLook _look;

    /// <summary>1jg/1jq: flight distance accumulated since the last trail point was laid. Reset
    /// whenever the step threshold is crossed, so a slow projectile lays the same spacing as a
    /// fast one.</summary>
    private float _trailAccum;

    /// <summary>1jq: the in-flight exhaust strip. Created lazily on the first point, so nothing is
    /// spawned for a cast that never gets a metre into the air.
    /// <para>
    /// The strip is <b>unparented and world-space</b> on purpose, exactly as 1jg's voxels were: it must
    /// stay where the bolt WAS, which is the entire point of a trail. That is also why the
    /// <c>Destroy(gameObject)</c> on impact below needs no hand-off at all — the strip is not a child,
    /// so it survives the projectile and fades itself out on its own schedule.
    /// </para>
    /// <para>
    /// Safe to leave null-checked with no reset: this component is never pooled. Both spawn paths
    /// <c>AddComponent</c> it onto a freshly created GameObject
    /// (<c>SpellCaster.Projectiles.cs</c>), so every projectile starts with a null trail. If it is ever
    /// pooled, this field must be cleared in <c>Initialize</c> or a reused component would keep writing
    /// into the previous cast's still-fading strip.
    /// </para>
    /// </summary>
    private TrailStrip _trail;

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
        _look = SpellLook.Resolve(spell);
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

        // Keep the chunk we're flying over collider-enabled (1dq). Only track the single current
        // chunk: each boundary crossing releases the old one, so no stale requests can accumulate.
        TerrainChunkCoord here = TerrainChunkCoord.FromWorld(transform.position);
        if (!_requestActive || here != _requestedChunk)
        {
            if (_requestActive)
                ColliderRequestRegistry.Release(_requestedChunk);
            _requestedChunk = here;
            ColliderRequestRegistry.Request(_requestedChunk);
            _requestActive = true;
        }

        Lifetime -= Time.deltaTime;
        if (Lifetime <= 0f)
        {
            Destroy(gameObject);
            return;
        }

        if (_homing)
        {
            // 1e6: the guidance re-lock scan (RaycastAll + OverlapSphereNonAlloc, UpdateMissile-
            // Targeting) runs at ~1/3 frame rate — the detonation probes below stay per-frame, so
            // collision continuity is untouched and only the soft "who do I chase" read is thinned.
            if (_missileScanFrame++ % 3 == 0)
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

        // 1jg/1jq: trail. Gated on distance travelled (not time) so spacing is identical at any
        // speed, and emitted here - after the move, inside the flight loop - so only a genuinely
        // flying projectile trails: a zone resolves and dies in Launch, and the static model bench
        // and the turret's DecorateProjectile path never reach this Update at all. 1jq replaced the
        // per-step pooled CUBE with one camera-facing quad strip; the gate above it is unchanged, so
        // the "only what genuinely flies trails" property is preserved by construction rather than by
        // a new flag. Colour is the already-resolved _look, so the strip adds no second colour lookup.
        _trailAccum += step;
        if (_trailAccum >= TrailStrip.Step)
        {
            _trailAccum = 0f;
            if (_trail == null) _trail = TrailStrip.Spawn(transform.position, _look);
            else _trail.Push(transform.position);
        }
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

    private void OnDestroy()
    {
        // Release the collider request so the chunk can fall back to collider-free (1dq).
        if (_requestActive)
        {
            ColliderRequestRegistry.Release(_requestedChunk);
            _requestActive = false;
        }
    }

    /// <summary>1ir: the radius this bolt actually bursts at. An authored
    /// <see cref="SpellData.BoltSplashRadius"/> wins over the fallback value — that is the whole point
    /// of the field, letting a turret's bolt detonate smaller than the area the turret scans. 0 (every
    /// pre-1ir spell) falls back to the legacy <c>Radius</c>.
    /// <para><b>Both branches are multiplied by <c>radiusMult</c>, deliberately.</b> The field chooses
    /// WHICH authored number answers the question, not whether the charge ladder applies to it — so a
    /// chargeable projectile setting it would burst proportionally, instead of being the one thing
    /// on the spell that ignored the charge. Dropping the multiplier from this branch would have been
    /// legal-looking and free to ship: the fallback branch scales, so the inconsistency would only
    /// ever show on the single spell that sets the field.</para>
    /// <para><b>Today this is inert for the only spell that sets the field.</b> Summon bolts pass
    /// <c>radiusMult = 1f</c> (see <see cref="SpellSummon"/>), so Continuous Fireball's 1.6 m burst is
    /// fixed while its familiar grows — which is intentional: the field exists to keep a bolt's
    /// detonation smaller than the area it scans, and that ratio reads better held constant than
    /// widened on a charge. Changing the summon to pass its scale would also widen Ember Effigy's
    /// bolts, which is out of scope here.</para>
    /// <para>Written ONCE here because the impact walk and the terrain dent below both need it, and
    /// two copies of "how big is this burst" would be free to disagree.</para></summary>
    private float SplashRadius
    {
        get
        {
            if (_spell != null && _spell.BoltSplashRadius > 0f)
                return _spell.BoltSplashRadius * _radiusMult;
            return _spell != null && _spell.Radius > 0f ? _spell.Radius * _radiusMult : 0.2f;
        }
    }

    private void ResolveProjectileImpact(GameObject hitObject)
    {
        // Also affect everything in the splash radius factoring in the caster.
        int count = Physics.OverlapSphereNonAlloc(transform.position, SplashRadius,
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
            ObjectPooler.SpawnTransient(_spell.ImpactEffectPrefab, transform.position,
                Quaternion.identity, 3f);

        // Every magic projectile dents the ground where it strikes. Earth projectiles
        // (TerrainShape.Crater — the school signature) carve a full crater scaled to the
        // spell radius; every other projectile leaves a small uniform impact dent so any
        // bolt (fireball, frost, arcane, lightning, dark, wind, water…) visibly disturbs
        // the terrain. The dent stays a LOCAL bowl — never the blast splash nor the
        // (unbounded) hold-to-overcharge sizeScale, exactly like zone casts (1cv/1cw) — so a
        // charged bolt still carves an authored-sized crater instead of a whole chunk. Both
        // probe the ground beneath the impact (the pit is carved as a smooth shallow dish that
        // always keeps a walkable floor — never a void) and never carve at the caster's own
        // feet at cast time (FireProjectile only spawns the bolt; no launch-site pit).
        if (_spell != null)
        {
            float dentRadius = 1.4f;
            if (_spell.TerrainShape == TerrainShape.Crater)
                dentRadius = Mathf.Max(1.2f, _spell.Radius);
            Vector3 probe = transform.position + Vector3.up * 0.1f;
            Vector3 impactGround = transform.position;
            if (Physics.Raycast(probe, Vector3.down, out RaycastHit groundHit, 30f))
            {
                impactGround = groundHit.point;
                // emitDebris:false — this dent is a magic projectile impact, so it does NOT throw the
                // excavation burst: that burst is stratum-tinted dirt thrown upward (2.5-5 m/s of lift)
                // and reads as "3 objects floating up then disappear". 1jh replaces it with the grey
                // rock chips below, which are thrown FORWARD and die in 1.4 s. The dent is identical
                // either way — only who supplies the debris changed.
                TerrainDeformer.Apply(impactGround, dentRadius, TerrainShape.Crater, _dir, emitDebris: false);
            }

            // Every projectile impact plays an exploding, fading body at the hit point (1gb).
            // 1if: the family is now the spell's own SpellImpactStyle rather than a fixed sphere —
            // a fireball's Burst, a lance's Cross, a boulder storm's Pillar all read differently,
            // and the shape choice is per-spell while the fade/lifetime stay shared. The sphere
            // survives as the Sphere family, so no spell lost its old look by accident.
            SpellImpactFx.Spawn(transform.position, Vector3.up, _look, Mathf.Max(0.8f, _spell.Radius));

            // 1jh: rock chips off the impact, ADDITIVE to the sphere above rather than replacing it —
            // the sphere is the spell's identity (per-school family) and the chips are the world
            // reacting, so the two answer different questions and both belong. Spelled at the HIT
            // point, not the probed ground point, so a wall hit throws chips too.
            TerrainDeformer.ImpactRockDebris(transform.position, dentRadius, _dir);
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
            _spell != null ? _look.Core : Color.white,
            Radius, 0.5f, _look.Scale);

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

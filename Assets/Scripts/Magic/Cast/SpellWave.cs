using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 1l87: the ground-hugging wave front (<see cref="SpellDelivery.Wave"/>) — the delivery that used to
/// hide inside the projectile pipeline behind a flag on <c>ProjectileShape.Wave</c>. Spawned by
/// <c>SpellCaster.ResolveWave</c>, it flattens the aim to horizontal, rides the terrain at a fixed
/// lift, sweeps a damage band over every foe it passes, and dies after the authored
/// <see cref="SpellData.Range"/>.
///
/// <para><b>Why its own component.</b> A wave is not a projectile: it never detonates on contact, it
/// passes THROUGH foes shoving each one once, it does not home, its reach is <c>Range</c> rather than a
/// flight envelope, and it must not be dressed by the projectile charge/preview pipeline. Keeping it
/// inside <see cref="SpellEffect"/> meant every one of those differences was a hidden branch on
/// <c>spell.Shape</c>; giving it its own delivery makes the category explicit (rule 13).</para>
///
/// <para><b>The drawn floor body is the builder's, and only the per-frame ripple is here.</b> The
/// caller attaches the wave floor through the same <see cref="MagicProjectileModelBuilder"/> path a QA
/// pedestal uses (<c>AttachDefaultProjectileVisual</c>), and this component drives its
/// <see cref="WaveRipple"/> while the wave MOVES (rule 19): the ripple lives here, not in the builder,
/// because the builder also draws motionless pedestals.</para>
/// </summary>
public class SpellWave : MonoBehaviour
{
    private SpellData _spell;
    private float _power;
    private Vector3 _dir;
    private SpellCaster _caster;
    private float _radiusMult = 1f;
    private float _speed = 20f;
    private float _life = 4f;
    private SpellLook _look;

    /// <summary>1l85: the floor's ripple driver, found once from the body the builder attached. Null
    /// for a body built without one; every use null-checks it.</summary>
    private WaveRipple _ripple;

    /// <summary>1jy: roots this wave has already hit, so each foe takes ONE resolved hit + one
    /// knockback as the band passes over it — never a per-frame re-resolve.</summary>
    private readonly HashSet<Transform> _hit = new HashSet<Transform>();
    private float _tick;
    private readonly Collider[] _buffer = new Collider[128];

    // Collider-on-demand (1dq): the wave rides up to its authored Range (20 m for Fire Wave), well
    // past the player's collider ring, so the down-raycast that glues it to the terrain needs each
    // chunk it crosses kept collider-enabled — the same request SpellEffect makes for a long bolt.
    private TerrainChunkCoord _requestedChunk;
    private bool _requestActive;

    /// <summary>1l88: how high above the ground THIS wave rides. Randomised once per cast (see
    /// <see cref="Initialize"/>) so no two waves skim at the same height — the report was that every
    /// wave rode at one fixed height and read as "the same wave over and over". Was a fixed 0.35 (1jy).</summary>
    private float _lift = 0.35f;

    /// <summary>1l88: the ride-height range a cast draws from. Kept low enough that the floor still
    /// reads as hugging the ground.</summary>
    private const float LiftMin = 0.12f;
    private const float LiftMax = 0.75f;

    /// <summary>1jy: the coverage cadence — the band re-scans this often so a fast wave does not skip
    /// past half the field. The <see cref="_hit"/> set, not this, is what enforces "once".</summary>
    private const float TickInterval = 0.15f;

    /// <summary>The band's half-width: the spell's authored delivery Radius, charge-scaled.</summary>
    private float BandRadius => _spell != null && _spell.Radius > 0f ? _spell.Radius * _radiusMult : 0.2f;

    /// <summary>
    /// Configure the wave: its spell, resolved power, cast direction, owner and charge scale. Flattens
    /// the aim to horizontal and faces the floor along it, then kicks the sweep off with a ground ring
    /// at its TELLING point — on the ground the wave is about to ride, not at the hand height it spawned
    /// at. Reach is the authored Range / ProjectileSpeed, so the sweep is a number on the spell and never
    /// a leftover of a default lifetime.
    /// </summary>
    public void Initialize(SpellData spell, float power, Vector3 dir, SpellCaster caster,
        float radiusMult = 1f)
    {
        _spell = spell;
        _power = power;
        _caster = caster;
        _radiusMult = Mathf.Max(radiusMult, 0.01f);
        _look = SpellLook.Resolve(spell);

        _dir = dir;
        _dir.y = 0f;
        if (_dir.sqrMagnitude < 0.001f)
        {
            _dir = transform.forward;
            _dir.y = 0f;
        }
        if (_dir.sqrMagnitude < 0.001f) _dir = Vector3.forward;
        _dir.Normalize();
        transform.rotation = Quaternion.LookRotation(_dir);

        _speed = spell != null && spell.ProjectileSpeed > 0f ? spell.ProjectileSpeed : 20f;
        if (spell != null && spell.Range > 0f)
            _life = Mathf.Min(4f, spell.Range / Mathf.Max(1f, _speed));

        // 1l88: draw this cast's ride height once, so consecutive waves do not all skim at one height.
        _lift = Random.Range(LiftMin, LiftMax);

        _ripple = GetComponentInChildren<WaveRipple>();

        Vector3 ground = transform.position;
        if (Physics.Raycast(transform.position + Vector3.up * 0.5f, Vector3.down, out RaycastHit hit, 4f)
            && IsGroundCollider(hit.collider))
            ground = hit.point;
        SkillFx.RingFlash(ground, Vector3.up, _look.Core, BandRadius, 0.4f, _look.Scale);
    }

    private void Update()
    {
        _life -= Time.deltaTime;
        if (_life <= 0f)
        {
            // 1jy: a spent wave finishes with a burst where its reach ran out — the same pooled impact
            // family the spell's look resolves. No dent: the wave never strikes the ground, it rides it.
            if (_spell != null)
                SpellImpactFx.Spawn(transform.position, Vector3.up, _look, Mathf.Max(0.8f, BandRadius));
            Destroy(gameObject);
            return;
        }

        // Keep the chunk we're riding collider-enabled (1dq). Only the single current chunk is
        // tracked: each boundary crossing releases the old one, so no stale requests accumulate.
        TerrainChunkCoord here = TerrainChunkCoord.FromWorld(transform.position);
        if (!_requestActive || here != _requestedChunk)
        {
            if (_requestActive)
                ColliderRequestRegistry.Release(_requestedChunk);
            _requestedChunk = here;
            ColliderRequestRegistry.Request(_requestedChunk);
            _requestActive = true;
        }

        // Ride the terrain at this cast's (randomised) lift. Ground colliders are judged the same way
        // SpellEffect does.
        Vector3 pos = transform.position;
        if (Physics.Raycast(pos + Vector3.up * 0.5f, Vector3.down, out RaycastHit ground, 4f)
            && IsGroundCollider(ground.collider))
        {
            pos.y = ground.point.y + _lift;
            transform.position = pos;
        }

        Sweep();

        // 1l85: roll the crest across the floor. Driven here (not in the body component) so only a
        // MOVING wave ripples; the same floor on the QA pedestal stays flat (rule 19).
        if (_ripple != null) _ripple.Apply(Time.time);

        transform.position += _dir * (_speed * Time.deltaTime);
    }

    /// <summary>1jy: the wave's damage band — everything within <see cref="BandRadius"/> of the wave
    /// center, once per root per wave. The throttle is only a coverage cadence (so the band keeps up
    /// with a fast wave instead of skipping past half the field); <see cref="_hit"/> is what enforces
    /// "once", so no foe can be re-resolved (and re-knocked) on a later tick.</summary>
    private void Sweep()
    {
        _tick += Time.deltaTime;
        if (_tick < TickInterval) return;
        _tick -= TickInterval;

        int count = Physics.OverlapSphereNonAlloc(transform.position, BandRadius, _buffer, ~0);
        for (int i = 0; i < count; i++)
        {
            Collider col = _buffer[i];
            if (col == null) continue;
            if (_caster != null && col.transform.root == _caster.transform.root) continue;
            Transform root = col.transform.root;
            if (root == null || !_hit.Add(root)) continue;
            _caster?.ResolveHitAt(col.gameObject, _spell, _power);
        }
    }

    /// <summary>Same "is this collider the ground" test SpellEffect uses: the named ground/field
    /// visuals, or anything parented under a streamed <see cref="ChunkObject"/>.</summary>
    private static bool IsGroundCollider(Collider c)
    {
        if (c == null) return false;
        return c.name == "Ground" || c.name == "FieldVisual"
            || c.GetComponentInParent<ChunkObject>() != null;
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
}

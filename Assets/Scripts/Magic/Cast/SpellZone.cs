using UnityEngine;

/// <summary>
/// Persistent spell zone (SpellDelivery.Zone with Duration > 0 and SpellDelivery.Vortex):
/// ticks the spell's damage while it lasts and optionally drags enemies toward its center.
/// Per-tick damage = resolved power x <paramref name="tickMultiplier"/>; status effects
/// (Burn / Frost / Stagger) still apply through the shared SpellCaster pipeline on each tick.
/// Enemies are transform-driven, so the pull nudges roots rather than applying physics force.
/// Skips the caster's group and friendly (Player/Companion) tags.
/// </summary>
public class SpellZone : MonoBehaviour
{
    public float Radius = 3f;
    public float Lifetime = 4f;
    public float TickInterval = 0.5f;
    public float PullSpeed = 0f;

    private SpellCaster _caster;
    private SpellData _spell;
    private float _power;
    private float _tickMultiplier = 1f;
    private float _age;
    private float _tick;
    private Transform _casterRoot;
    private readonly Collider[] _tickBuffer = new Collider[128];

    /// <summary>1ie: the zone's resolved visual identity, resolved once in Initialize. Every
    /// RingFlash and every 1ih tick flash reads this, so the zone never re-derives its own look.</summary>
    private SpellLook _look;

    public void Initialize(SpellCaster caster, SpellData spell, float power,
        float radiusMult = 1f, float tickMultiplier = 1f, float pullSpeed = 0f)
    {
        _caster = caster;
        _spell = spell;
        _power = power;
        _tickMultiplier = Mathf.Max(tickMultiplier, 0f);
        PullSpeed = Mathf.Max(pullSpeed, 0f);
        _casterRoot = caster != null ? caster.transform.root : null;
        radiusMult = Mathf.Max(radiusMult, 0.01f);
        Radius = spell != null && spell.Radius > 0f ? spell.Radius * radiusMult : Radius;
        _look = spell != null ? SpellLook.Resolve(spell) : SpellLook.Resolve(DamageType.Wind, ProjectileShape.Auto);
        BuildVisual();

        if (_spell != null && Radius > 0f)
            SkillFx.RingFlash(transform.position, Vector3.up, _look.Core, Radius, 0.4f, _look.Scale);
    }

    private void Update()
    {
        _age += Time.deltaTime;
        if (PullSpeed > 0f)
            transform.Rotate(0f, 240f * Time.deltaTime, 0f, Space.Self);

        _tick -= Time.deltaTime;
        if (_tick <= 0f)
        {
            _tick = TickInterval;
            Tick();
        }

        if (_age >= Lifetime)
        {
            Destroy(gameObject);
        }
    }

    private void Tick()
    {
        if (_caster == null || _spell == null) return;

        int count = Physics.OverlapSphereNonAlloc(transform.position, Radius, _tickBuffer);
        bool struck = false;
        for (int i = 0; i < count; i++)
        {
            var col = _tickBuffer[i];
            if (col == null) continue;

            Transform root = col.transform.root;
            if (root == _casterRoot) continue;

            // Holy zones heal allies (IHealable) standing inside while damaging enemies.
            if (_spell.Heals && root.TryGetComponent<IHealable>(out _))
            {
                _caster.ResolveHeal(_spell, _power * _tickMultiplier, col.gameObject);
                continue;
            }

            if (root.CompareTag("Player") || root.CompareTag("Companion")) continue;

            var target = col.gameObject;
            if (!target.TryGetComponent<IDamageable>(out _)) continue;

            // Per-tick damage resolves through the shared damage pipeline (statuses too).
            float tickDamage = _power * _tickMultiplier;
            if (tickDamage > 0f)
                _caster.ResolveHitAt(target, _spell, tickDamage);
            struck = true;

            bool isEnemy = root.TryGetComponent<EnemyController>(out _)
                || root.TryGetComponent<BossController>(out _);
            if (isEnemy && PullSpeed > 0f && _age < Lifetime)
            {
                Vector3 to = root.position - transform.position;
                to.y = 0f;
                float dist = to.magnitude;
                if (dist > 0.1f)
                    root.position = Vector3.MoveTowards(root.position, transform.position,
                        Mathf.Min(PullSpeed * Time.deltaTime, dist));
            }
        }

        // 1ih: ONE flash per zone tick, not per collider. A blizzard (magic_blizzard) is a Zone with a
        // 0.5 s interval inside a 6 m radius, so an unpooled per-collider flash would fire ~20x per
        // second per victim — which is what SpellImpactFx's PerFrameBudget exists to bound.
        if (struck)
            SpellImpactFx.Spawn(transform.position, Vector3.up, _look, Radius);
    }

    /// <summary>1jd: the two bodies moved to <see cref="SpellZoneModelBuilder"/> (under
    /// <c>Models/Magic/</c>) — the pull funnel and the persistent ground disc. This method still owns
    /// the colour choice and the branch between them; the shapes and names are unchanged.</summary>
    private void BuildVisual()
    {
        // 1ie: the persistent zone body takes the per-spell Core colour, not the school colour, so a
        // zone is identifiable while it lives. Side effect worth naming: SharedSpriteMaterial is a
        // cache keyed by colour, so the key space grows from 10 school colours to ~172 spell
        // colours. That is 162 extra small Materials held for the session, not a leak (each is one
        // shader instance and they are all live-bounded by the zones that use them).
        //
        // 1ij: no ternary any more. `type` is redundant as a second source of identity — Initialize
        // already resolved the identity-less fallback INTO _look on the null-spell path (it passes
        // DamageType.Wind there too), so `_spell != null ? _look.Core : ColorFor(type)` was asking
        // two questions and could only disagree with itself. One resolved look, read once.
        Color color = _look.Core;
        Material sharedMat = SkillFx.SharedSpriteMaterial(color);

        // 1kc: an authored VortexCircle body is drawn regardless of pull — its rising edge swirl IS
        // the "wind" that drags foes in, so the pull branch below must not swap it back to the old
        // funnel. Every other zone resolves Funnel and keeps the pre-1kc shapes.
        if (_look.ZoneModel == ZoneBody.VortexCircle)
        {
            SpellZoneModelBuilder.BuildConflagration(transform, Radius, color, SpellLook.HotCore(color));
            return;
        }

        if (PullSpeed > 0f)
        {
            SpellZoneModelBuilder.BuildFunnel(transform, Radius, sharedMat);
            return;
        }

        SpellZoneModelBuilder.BuildGroundZone(transform, Radius, sharedMat);
    }
}
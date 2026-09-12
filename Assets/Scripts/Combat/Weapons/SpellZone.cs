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
        BuildVisual(spell != null ? spell.Type : DamageType.Wind);

        if (_spell != null && Radius > 0f)
            SkillFx.RingFlash(transform.position, Vector3.up, DamageNumber.ColorFor(_spell.Type), Radius, 0.4f);
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

        Collider[] cols = Physics.OverlapSphere(transform.position, Radius);
        for (int i = 0; i < cols.Length; i++)
        {
            var col = cols[i];
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
    }

    /// <summary>Funnel: tapering stack of spinning flat rings + orbiting debris (tornado / vortex).
    /// Disc: a single wide flat ring on the ground for persistent AoE zones.</summary>
    private void BuildVisual(DamageType type)
    {
        Shader shader = Shader.Find("Sprites/Default") ?? Shader.Find("Unlit/Color");
        if (shader == null) return;

        Color color = DamageNumber.ColorFor(type);

        if (PullSpeed > 0f)
        {
            const float height = 4.8f;
            const int rings = 7;
            for (int i = 0; i < rings; i++)
            {
                float t = i / (float)(rings - 1);
                float radius = Mathf.Lerp(Radius * 0.85f, 0.12f, t);

                var ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                ring.name = "SpellRing_" + i;
                Collider ringCol = ring.GetComponent<Collider>();
                if (ringCol != null) Destroy(ringCol);
                ring.transform.SetParent(transform, false);
                ring.transform.localPosition = new Vector3(0f, t * height, 0f);
                ring.transform.localScale = new Vector3(radius, 0.015f, radius);
                ring.GetComponent<MeshRenderer>().material = new Material(shader) { color = color };
            }

            for (int i = 0; i < 6; i++)
            {
                float t = i / 5f;
                var block = GameObject.CreatePrimitive(PrimitiveType.Cube);
                block.name = "SpellDebris_" + i;
                Collider blockCol = block.GetComponent<Collider>();
                if (blockCol != null) Destroy(blockCol);
                block.transform.SetParent(transform, false);
                float ang = i * 60f + 30f;
                float orbit = Mathf.Lerp(Radius * 0.7f, 0.25f, t);
                block.transform.localPosition = new Vector3(
                    Mathf.Cos(ang * Mathf.Deg2Rad) * orbit,
                    t * height * 0.8f,
                    Mathf.Sin(ang * Mathf.Deg2Rad) * orbit);
                block.transform.localScale = Vector3.one * Mathf.Lerp(0.22f, 0.08f, t);
                block.GetComponent<MeshRenderer>().material = new Material(shader) { color = color };
            }
            return;
        }

        var disc = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        disc.name = "SpellDisc";
        Collider discCol = disc.GetComponent<Collider>();
        if (discCol != null) Destroy(discCol);
        disc.transform.SetParent(transform, false);
        disc.transform.localScale = new Vector3(Radius * 2f, 0.02f, Radius * 2f);
        disc.GetComponent<MeshRenderer>().material = new Material(shader) { color = color };

        var halo = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        halo.name = "SpellHalo";
        Collider haloCol = halo.GetComponent<Collider>();
        if (haloCol != null) Destroy(haloCol);
        halo.transform.SetParent(transform, false);
        halo.transform.localPosition = new Vector3(0f, 0.9f, 0f);
        halo.transform.localScale = new Vector3(Radius * 1.6f, 0.03f, Radius * 1.6f);
        halo.GetComponent<MeshRenderer>().material = new Material(shader) { color = color };
    }
}
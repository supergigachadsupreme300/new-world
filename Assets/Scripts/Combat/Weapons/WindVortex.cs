using UnityEngine;

/// <summary>
/// Persistent wind spell zone (SpellDelivery.Vortex): a spinning funnel that ticks the
/// spell's damage over its lifetime and drags enemies toward its center (enemies are
/// transform-driven, so the pull nudges the root rather than applying physics force).
/// Damage resolves through the shared <see cref="SpellCaster"/> pipeline so Wisdom scaling,
/// resistances, damage numbers and impact FX all apply. Skips the caster's own group.
/// </summary>
public class WindVortex : MonoBehaviour
{
    public float Radius = 3f;
    public float Lifetime = 4f;
    public float TickInterval = 0.5f;
    public float PullSpeed = 3.5f;

    private SpellCaster _caster;
    private SpellData _spell;
    private float _power;
    private float _age;
    private float _tick;
    private Transform _casterRoot;

    public void Initialize(SpellCaster caster, SpellData spell, float power)
    {
        _caster = caster;
        _spell = spell;
        _power = power;
        _casterRoot = caster != null ? caster.transform.root : null;
        Radius = spell != null && spell.Radius > 0f ? spell.Radius : Radius;
        BuildVisual(spell != null ? spell.Type : DamageType.Wind);

        if (_spell != null && _spell.Radius > 0f)
            SkillFx.RingFlash(transform.position, Vector3.up, DamageNumber.ColorFor(_spell.Type), _spell.Radius, 0.4f);

        Destroy(gameObject, Lifetime);
    }

    private void Update()
    {
        _age += Time.deltaTime;
        transform.Rotate(0f, 240f * Time.deltaTime, 0f, Space.Self);

        _tick -= Time.deltaTime;
        if (_tick <= 0f)
        {
            _tick = TickInterval;
            Tick();
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
            if (root.CompareTag("Player") || root.CompareTag("Companion")) continue;

            var target = col.gameObject;
            if (!target.TryGetComponent<IDamageable>(out _)) continue;

            _caster.ResolveHitAt(target, _spell, _power);

            bool isEnemy = root.TryGetComponent<EnemyController>(out _)
                || root.TryGetComponent<BossController>(out _);
            if (isEnemy && _age < Lifetime)
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

    /// <summary>Tapering stack of spinning flat rings + a few lazily revolving blocks.</summary>
    private void BuildVisual(DamageType type)
    {
        Shader shader = Shader.Find("Sprites/Default") ?? Shader.Find("Unlit/Color");
        if (shader == null) return;

        Color color = DamageNumber.ColorFor(type);
        const float height = 4.8f;
        const int rings = 7;
        for (int i = 0; i < rings; i++)
        {
            float t = i / (float)(rings - 1);
            float radius = Mathf.Lerp(Radius * 0.85f, 0.12f, t);

            var ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            ring.name = "WindRing_" + i;
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
            block.name = "WindDebris_" + i;
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
    }
}
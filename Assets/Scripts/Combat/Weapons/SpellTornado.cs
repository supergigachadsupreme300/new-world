using UnityEngine;

/// <summary>
/// Great Tornado spell delivery (magic_tornado). Rebuilds the old environmental tornado
/// (MapBuilder.BuildTornado + TornadoBehavior) — a tall funnel of stacked debris blocks that
/// drifts and pulls objects around via physics — scaled down to the spell radius, and layers
/// the spell's damage ticks + enemy pull on top, mirroring the SpellZone/Vortex pipeline.
/// </summary>
public class SpellTornado : MonoBehaviour
{
    public float Radius = 3f;
    public float Lifetime = 5f;
    public float TickInterval = 0.5f;
    public float PullSpeed = 3.5f;

    private SpellCaster _caster;
    private SpellData _spell;
    private float _power;
    private float _tickMultiplier = 1f;
    private float _age;
    private float _tick;
    private Transform _casterRoot;
    private readonly Collider[] _tickBuffer = new Collider[128];

    public void Initialize(SpellCaster caster, SpellData spell, float power, float radiusMult = 1f, float tickMultiplier = 1f)
    {
        _caster = caster;
        _spell = spell;
        _power = power;
        _tickMultiplier = Mathf.Max(tickMultiplier, 0f);
        _casterRoot = caster != null ? caster.transform.root : null;

        radiusMult = Mathf.Max(radiusMult, 0.01f);
        Radius = spell != null && spell.Radius > 0f ? spell.Radius * radiusMult : Radius;
        TickInterval = spell != null && spell.TickInterval > 0f ? spell.TickInterval : 0.5f;

        // Scale the environmental tornado's fields down to the spell's footprint so pulled
        // objects don't ride up to the old 80-unit orbit height, and crank up the churn so the
        // funnel visibly spins like the old tornado.
        var tb = GetComponent<TornadoBehavior>();
        if (tb != null)
        {
            tb.PullRadius = Mathf.Max(Radius * 1.5f, 2f);
            tb.OrbitHeight = Mathf.Max(Radius * 1.6f, 4f);
            tb.PullForce = Mathf.Max(Radius * 0.8f, 2f);
            tb.MaxPullSpeed = Mathf.Max(Radius * 0.8f, 4f);
            tb.BaseRotateSpeed = 70f;
            tb.RotateSpeedVariation = 55f;
            // Orbiting debris swirl (the old tornado's visible spin): a handful of small chunks
            // that circle the funnel just above its base.
            for (int d = 0; d < 12; d++)
            {
                float s = Random.Range(0.4f, 1.1f);
                tb.AddDebrisBlock(new Vector3(s, Random.Range(0.3f, 0.8f), s), ColorPalette.StoneGray);
            }
            tb.AddDebrisBlock(new Vector3(0.9f, 0.5f, 0.9f), new Color(0.35f, 0.27f, 0.19f));
        }

        if (spell != null && Radius > 0f)
            SkillFx.RingFlash(transform.position, Vector3.up, DamageNumber.ColorFor(spell.Type), Radius * 2f, 0.4f);
    }

    private void Update()
    {
        _age += Time.deltaTime;
        _tick -= Time.deltaTime;
        if (_tick <= 0f)
        {
            _tick = Mathf.Max(TickInterval, 0.05f);
            Tick();
        }

        if (_age >= Lifetime)
            Destroy(gameObject);
    }

    private void Tick()
    {
        if (_caster == null || _spell == null) return;

        Vector3 center = transform.position;
        int count = Physics.OverlapSphereNonAlloc(center, Radius, _tickBuffer);
        for (int i = 0; i < count; i++)
        {
            var col = _tickBuffer[i];
            if (col == null) continue;

            Transform root = col.transform.root;
            if (root == _casterRoot) continue;
            if (root.CompareTag("Player") || root.CompareTag("Companion")) continue;
            if (!col.TryGetComponent<IDamageable>(out _)) continue;

            float tickDamage = _power * _tickMultiplier;
            if (tickDamage > 0f)
                _caster.ResolveHitAt(col.gameObject, _spell, tickDamage);

            bool isEnemy = root.TryGetComponent<EnemyController>(out _)
                || root.TryGetComponent<BossController>(out _);
            if (isEnemy)
            {
                Vector3 to = root.position - center;
                to.y = 0f;
                float dist = to.magnitude;
                if (dist > 0.1f)
                    root.position = Vector3.MoveTowards(root.position, center,
                        Mathf.Min(PullSpeed * Time.deltaTime, dist));
            }
        }
    }
}
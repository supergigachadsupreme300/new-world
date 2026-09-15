using UnityEngine;

/// <summary>
/// Summoned object (SpellDelivery.Summon): a persistent construct at the goal point that acts
/// until its lifetime ends. Damage summons become turrets that fire small bolts at the nearest
/// enemy inside their radius each tick (reusing the shared projectile flight); healing summons
/// become a persistent heal aura for allies inside the radius.
/// </summary>
public class SpellSummon : MonoBehaviour
{
    public float Radius = 6f;
    public float Lifetime = 6f;
    public float TickInterval = 0.5f;
    public float BoltPowerMultiplier = 0.6f;

    private SpellCaster _caster;
    private SpellData _spell;
    private float _power;
    private Transform _casterRoot;
    private float _age;
    private float _tick;
    private Transform _head;
    private Color _color;
    private Vector3 _headBaseScale;
    private readonly Collider[] _hitBuffer = new Collider[32];

    public void Initialize(SpellCaster caster, SpellData spell, float power, float radiusMult = 1f)
    {
        _caster = caster;
        _spell = spell;
        _power = power;
        _casterRoot = caster != null ? caster.transform.root : null;
        radiusMult = Mathf.Max(radiusMult, 0.01f);
        if (spell != null)
        {
            Radius = Mathf.Max(spell.Radius * radiusMult, 1f);
            if (spell.Duration > 0f) Lifetime = spell.Duration;
            if (spell.TickInterval > 0f) TickInterval = spell.TickInterval;
            _color = DamageNumber.ColorFor(spell.Type);
        }

        BuildVisual();
        SkillFx.RingFlash(transform.position, Vector3.up, _color, Radius * 0.8f, 0.45f);
    }

    private void Update()
    {
        if (_spell == null)
        {
            Destroy(gameObject);
            return;
        }

        _age += Time.deltaTime;
        if (_age >= Lifetime)
        {
            Destroy(gameObject);
            return;
        }

        if (_head != null)
            _head.localScale = _headBaseScale * (1f + 0.18f * Mathf.Sin(Time.time * 3.2f));

        _tick -= Time.deltaTime;
        if (_tick <= 0f)
        {
            _tick = TickInterval > 0f ? TickInterval : 0.5f;
            Tick();
        }
    }

    private void Tick()
    {
        if (_caster == null || _spell == null) return;

        if (_spell.Heals)
        {
            AuraHeal();
            return;
        }

        FireAtNearest();
    }

    private void AuraHeal()
    {
        int count = Physics.OverlapSphereNonAlloc(transform.position, Radius, _hitBuffer);
        for (int i = 0; i < count; i++)
        {
            var col = _hitBuffer[i];
            if (col == null) continue;
            Transform root = col.transform.root;
            if (root == _casterRoot) continue;
            if (!root.TryGetComponent<IHealable>(out _)) continue;
            _caster.ResolveHeal(_spell, _power, col.gameObject);
        }
    }

    private void FireAtNearest()
    {
        Transform target = NearestEnemy();
        if (target == null) return;

        Vector3 muzzle = _head != null ? _head.position : transform.position + Vector3.up * 1.6f;
        Vector3 to = target.position - muzzle;
        float h = Mathf.Max(0f, muzzle.y - target.position.y);
        Vector3 flat = to;
        flat.y = 0f;
        Vector3 dir = flat.sqrMagnitude > 0.0001f
            ? (flat.normalized + Vector3.up * Mathf.Clamp(h * 0.35f, 0f, 0.8f)).normalized
            : Vector3.up;

        var go = new GameObject("SummonBolt");
        go.transform.position = muzzle;
        go.transform.rotation = Quaternion.LookRotation(dir);
        _caster.DecorateProjectile(go, _spell.Type, _spell.Shape);
        float speed = _spell.ProjectileSpeed > 0f ? _spell.ProjectileSpeed : 18f;
        var fx = go.AddComponent<SpellEffect>().Initialize(_spell, _power * BoltPowerMultiplier, dir, _caster, 1f);
        fx.Launch(speed);
    }

    /// <summary>Nearest hostable enemy root inside the radius (EnemyController / BossController).</summary>
    private Transform NearestEnemy()
    {
        int count = Physics.OverlapSphereNonAlloc(transform.position, Radius, _hitBuffer);
        Transform best = null;
        float bestSqr = float.MaxValue;
        for (int i = 0; i < count; i++)
        {
            var col = _hitBuffer[i];
            if (col == null) continue;
            Transform root = col.transform.root;
            if (root == _casterRoot) continue;
            if (root.CompareTag("Player") || root.CompareTag("Companion")) continue;
            if (!root.TryGetComponent<EnemyController>(out _) &&
                !root.TryGetComponent<BossController>(out _)) continue;

            float dSqr = (root.position - transform.position).sqrMagnitude;
            if (dSqr < bestSqr)
            {
                bestSqr = dSqr;
                best = root;
            }
        }
        return best;
    }

    /// <summary>Totem: base disc + tapered pillar + pulsing head crystal + orbiting shards.</summary>
    private void BuildVisual()
    {
        Shader shader = Shader.Find("Sprites/Default") ?? Shader.Find("Unlit/Color");
        if (shader == null) return;

        var baseDisc = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        baseDisc.name = "SummonBase";
        DestroyCollider(baseDisc.transform);
        baseDisc.transform.SetParent(transform, false);
        baseDisc.transform.localPosition = new Vector3(0f, 0.08f, 0f);
        baseDisc.transform.localScale = new Vector3(0.9f, 0.07f, 0.9f);
        SetMaterial(baseDisc.transform, shader, _color);

        var pillar = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        pillar.name = "SummonPillar";
        DestroyCollider(pillar.transform);
        pillar.transform.SetParent(transform, false);
        pillar.transform.localPosition = new Vector3(0f, 1f, 0f);
        pillar.transform.localScale = new Vector3(0.55f, 0.95f, 0.55f);
        SetMaterial(pillar.transform, shader, _color);

        _head = GameObject.CreatePrimitive(PrimitiveType.Sphere).transform;
        _head.name = "SummonHead";
        DestroyCollider(_head);
        _head.SetParent(transform, false);
        _head.localPosition = new Vector3(0f, 2.1f, 0f);
        _head.localScale = Vector3.one * 0.5f;
        SetMaterial(_head, shader, _color);
        _headBaseScale = _head.localScale;

        for (int i = 0; i < 3; i++)
        {
            var shard = GameObject.CreatePrimitive(PrimitiveType.Cube);
            shard.name = "SummonOrbit_" + i;
            DestroyCollider(shard.transform);
            shard.transform.SetParent(transform, false);
            float ang = i * 120f;
            Vector2 c = new Vector2(Mathf.Cos(ang * Mathf.Deg2Rad), Mathf.Sin(ang * Mathf.Deg2Rad));
            shard.transform.localPosition = new Vector3(c.x * 0.75f, 1.2f, c.y * 0.75f);
            shard.transform.localScale = Vector3.one * 0.18f;
            SetMaterial(shard.transform, shader, _color);
        }
    }

    private static void DestroyCollider(Transform t)
    {
        Collider col = t.GetComponent<Collider>();
        if (col != null) Destroy(col);
    }

    private static void SetMaterial(Transform t, Shader shader, Color color)
    {
        var r = t.GetComponent<MeshRenderer>();
        if (r != null) r.material = new Material(shader) { color = color };
    }
}
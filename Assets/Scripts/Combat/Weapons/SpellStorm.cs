using System.Collections;
using UnityEngine;

/// <summary>
/// Storm delivery (SpellDelivery.Storm): a persistent area that repeatedly strikes at points inside
/// its radius for the spell's duration. Each strike flashes an element-styled bolt/burst, deals a
/// burst of the spell's power inside a small radius, and applies its status through the shared
/// pipeline. Strikes land with a small random stagger so the storm reads as chaotic, not pinging.
/// </summary>
public class SpellStorm : MonoBehaviour
{
    public float Radius = 3f;
    public float Lifetime = 3f;
    public float TickInterval = 0.5f;
    public int StrikesPerTick = 2;
    public float StrikePowerMultiplier = 0.8f;

    private SpellCaster _caster;
    private SpellData _spell;
    private float _power;
    private Transform _casterRoot;
    private float _age;
    private float _tick;
    private Color _color;
    private DamageType _type;

    public void Initialize(SpellCaster caster, SpellData spell, float power, float radiusMult = 1f)
    {
        _caster = caster;
        _spell = spell;
        _power = power;
        _casterRoot = caster != null ? caster.transform.root : null;
        radiusMult = Mathf.Max(radiusMult, 0.01f);
        if (spell != null)
        {
            Radius = Mathf.Max(spell.Radius * radiusMult, 1.5f);
            if (spell.Duration > 0f) Lifetime = spell.Duration;
            if (spell.TickInterval > 0f) TickInterval = spell.TickInterval;
            _color = DamageNumber.ColorFor(spell.Type);
            _type = spell.Type;
        }

        SkillFx.RingFlash(transform.position, Vector3.up, _color, Radius, 0.5f);
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

        _tick -= Time.deltaTime;
        if (_tick <= 0f)
        {
            _tick = TickInterval > 0f ? TickInterval : 0.5f;
            BurstStrikes();
        }
    }

    private void BurstStrikes()
    {
        int count = Mathf.Max(StrikesPerTick, 1);
        for (int i = 0; i < count; i++)
            StartCoroutine(StrikeDelayed(Random.Range(0f, 0.35f)));
    }

    private IEnumerator StrikeDelayed(float delay)
    {
        if (delay > 0f) yield return new WaitForSeconds(delay);
        if (this == null) yield break;

        Vector3 at = RandomStrikePoint();
        SpawnStrikeFx(at);
        ResolveStrike(at);
    }

    /// <summary>Largely prefer striking near an enemy inside the area; otherwise a random point.</summary>
    private Vector3 RandomStrikePoint()
    {
        Collider[] cols = Physics.OverlapSphere(transform.position, Radius);
        float bestSqr = float.MaxValue;
        Transform best = null;
        for (int i = 0; i < cols.Length && i < 24; i++)
        {
            var col = cols[i];
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
        if (best != null)
            return best.position;

        Vector2 r = Random.insideUnitCircle * Radius;
        return transform.position + new Vector3(r.x, 0.25f, r.y);
    }

    private void ResolveStrike(Vector3 at)
    {
        if (_caster == null || _spell == null) return;

        float strikeRadius = Mathf.Max(_spell.Radius * 0.55f, 1.2f);
        Collider[] cols = Physics.OverlapSphere(at, strikeRadius);
        for (int i = 0; i < cols.Length; i++)
        {
            var col = cols[i];
            if (col == null) continue;
            Transform root = col.transform.root;
            if (root == _casterRoot) continue;

            if (_spell.Heals && root.TryGetComponent<IHealable>(out _))
            {
                _caster.ResolveHeal(_spell, _power * StrikePowerMultiplier, col.gameObject);
                continue;
            }
            if (root.CompareTag("Player") || root.CompareTag("Companion")) continue;
            if (!col.gameObject.TryGetComponent<IDamageable>(out _)) continue;

            _caster.ResolveHitAt(col.gameObject, _spell, _power * StrikePowerMultiplier);
        }
    }

    private void SpawnStrikeFx(Vector3 at)
    {
        float groundY = transform.position.y;
        Vector3 ground = new Vector3(at.x, groundY, at.z);
        Color c = _color;

        if (_type == DamageType.Lightning)
        {
            // Crackling bolt column: two crossed tall thin bars.
            Shader shader = Shader.Find("Sprites/Default") ?? Shader.Find("Unlit/Color");
            if (shader != null)
            {
                GameObject bolt = GameObject.CreatePrimitive(PrimitiveType.Cube);
                bolt.name = "StormBoltA";
                DestroyCollider(bolt.transform);
                bolt.transform.position = at;
                bolt.transform.localScale = new Vector3(0.1f, 3.2f, 0.1f);
                SetMaterial(bolt.transform, shader, c);

                GameObject boltB = GameObject.CreatePrimitive(PrimitiveType.Cube);
                boltB.name = "StormBoltB";
                DestroyCollider(boltB.transform);
                boltB.transform.position = at;
                boltB.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);
                boltB.transform.localScale = new Vector3(0.1f, 3.2f, 0.1f);
                SetMaterial(boltB.transform, shader, c);
            }
        }

        StrikeFlash.Spawn(at, c, 1.6f);
        SkillFx.RingFlash(ground, Vector3.up, c, Random.Range(0.8f, 1.4f), 0.35f);
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

    /// <summary>Short-lived bright burst sphere that scales up and fades.</summary>
    private sealed class StrikeFlash : MonoBehaviour
    {
        private float _age;
        private float _lifetime = 0.3f;
        private float _scale = 1f;

        public static void Spawn(Vector3 at, Color color, float scale)
        {
            Shader shader = Shader.Find("Sprites/Default") ?? Shader.Find("Unlit/Color");
            if (shader == null) return;
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "StormFlash";
            DestroyCollider(go.transform);
            go.transform.position = at;
            var flash = go.AddComponent<StrikeFlash>();
            flash._scale = scale;
            SetMaterial(go.transform, shader, color);
        }

        private void Update()
        {
            _age += Time.deltaTime;
            float t = Mathf.Clamp01(_age / _lifetime);
            float s = Mathf.Lerp(0.3f, 1f, Mathf.SmoothStep(0f, 0.4f, t));
            transform.localScale = Vector3.one * (_scale * s);
            var r = GetComponent<MeshRenderer>();
            if (r != null && r.material != null)
            {
                Color c = r.material.color;
                c.a = 1f - t;
                r.material.color = c;
            }
            if (t >= 1f)
                Destroy(gameObject);
        }
    }
}
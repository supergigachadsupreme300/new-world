using System.Collections;
using System.Collections.Generic;
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
    private readonly Collider[] _strikeBuffer = new Collider[128];

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

        // Sky storms (summonFallingRock: Meteor Rain, Rockfall) send a big rock down to each strike
        // point; the strike's flash/damage/deform fire when it lands, so the storm reads as boulders
        // raining in rather than instant ground pings. Purely visual rock — no collider (1cx).
        if (_spell != null && _spell.SummonFallingRock)
        {
            float scale = Mathf.Max(_spell.Radius * 0.5f, 1.2f);
            Color tint = _color;
            SkillFx.FallRock(at, scale, tint, () =>
            {
                if (this == null) return;
                SpawnStrikeFx(at);
                ResolveStrike(at);
                DeformGround(at);
            });
            yield break;
        }

        SpawnStrikeFx(at);
        ResolveStrike(at);
        DeformGround(at);
    }

    /// <summary>Earth storms (TerrainShape, e.g. Rockfall) dent the ground under each strike —
    /// a permanent pit where every boulder lands (§3.8). Only runs for spells that carry a
    /// terrain shape; other elements no-op in TerrainDeformer.</summary>
    private void DeformGround(Vector3 at)
    {
        if (_spell == null || _spell.TerrainShape == TerrainShape.None) return;
        Vector3 ground = at;
        if (Physics.Raycast(at + Vector3.up * 0.5f, Vector3.down, out RaycastHit hit, 10f))
            ground = hit.point;
        float strikeRadius = Mathf.Max(_spell.Radius * 0.55f, 1.2f);
        TerrainDeformer.Apply(ground, strikeRadius, _spell.TerrainShape);
    }

    /// <summary>Largely prefer striking near an enemy inside the area; otherwise a random point.</summary>
    private Vector3 RandomStrikePoint()
    {
        int count = Physics.OverlapSphereNonAlloc(transform.position, Radius, _strikeBuffer);
        float bestSqr = float.MaxValue;
        Transform best = null;
        for (int i = 0; i < count && i < 24; i++)
        {
            var col = _strikeBuffer[i];
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
        int count = Physics.OverlapSphereNonAlloc(at, strikeRadius, _strikeBuffer);
        for (int i = 0; i < count; i++)
        {
            var col = _strikeBuffer[i];
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
            // Crackling bolt column: two crossed tall thin bars, faded out by BoltFader.
            // (The old code forgot to destroy these — each strike leaked two permanent cubes.)
            Material sharedMat = SkillFx.SharedSpriteMaterial(c);
            if (sharedMat != null)
            {
                GameObject bolt = GameObject.CreatePrimitive(PrimitiveType.Cube);
                bolt.name = "StormBoltA";
                DestroyCollider(bolt.transform);
                bolt.transform.position = at;
                bolt.transform.localScale = new Vector3(0.1f, 3.2f, 0.1f);
                AssembleBolt(bolt, sharedMat);

                GameObject boltB = GameObject.CreatePrimitive(PrimitiveType.Cube);
                boltB.name = "StormBoltB";
                DestroyCollider(boltB.transform);
                boltB.transform.position = at;
                boltB.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);
                boltB.transform.localScale = new Vector3(0.1f, 3.2f, 0.1f);
                AssembleBolt(boltB, sharedMat);
            }
        }

        StrikeFlash.Spawn(at, c, 1.6f);
        SkillFx.RingFlash(ground, Vector3.up, c, Random.Range(0.8f, 1.4f), 0.35f);
    }

    private static void AssembleBolt(GameObject bolt, Material sharedMat)
    {
        var r = bolt.GetComponent<MeshRenderer>();
        if (r != null && sharedMat != null)
            r.sharedMaterial = sharedMat;
        bolt.AddComponent<BoltFader>().Init(new Vector3(0.1f, 3.2f, 0.1f), 0.25f);
    }

    private static void DestroyCollider(Transform t)
    {
        Collider col = t.GetComponent<Collider>();
        if (col != null) Destroy(col);
    }

    /// <summary>Shrinks the lightning bars to nothing, then removes them (no lingering leak).</summary>
    private sealed class BoltFader : MonoBehaviour
    {
        private Vector3 _startScale;
        private float _lifetime = 0.25f;
        private float _age;

        public void Init(Vector3 startScale, float lifetime)
        {
            _startScale = startScale;
            _lifetime = lifetime;
        }

        private void Update()
        {
            _age += Time.deltaTime;
            float t = Mathf.Clamp01(_age / _lifetime);
            transform.localScale = _startScale * (1f - t);
            if (t >= 1f)
                Destroy(gameObject);
        }
    }

    /// <summary>Short-lived bright burst sphere that scales up and fades. Pooled so heavy
    /// storms (strikes every ~0.5s) stop allocating new spheres + materials per strike.</summary>
    private sealed class StrikeFlash : MonoBehaviour
    {
        private float _age;
        private float _lifetime = 0.3f;
        private float _scale = 1f;
        private Material _mat;
        private static readonly List<StrikeFlash> _pool = new List<StrikeFlash>();
        private const int PoolCap = 32;

        public static void Spawn(Vector3 at, Color color, float scale)
        {
            StrikeFlash flash = Acquire();
            if (flash == null) return;
            flash.transform.position = at;
            flash._scale = scale;
            flash._age = 0f;
            if (flash._mat == null)
                return;
            flash._mat.color = color;
            flash.gameObject.SetActive(true);
        }

        private static StrikeFlash Acquire()
        {
            for (int i = 0; i < _pool.Count; i++)
            {
                var f = _pool[i];
                if (f == null)
                {
                    _pool.RemoveAt(i);
                    i--;
                    continue;
                }
                if (f.gameObject.activeSelf) continue;
                _pool.RemoveAt(i);
                return f;
            }

            Shader shader = Shader.Find("Sprites/Default") ?? Shader.Find("Unlit/Color");
            if (shader == null) return null;
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "StormFlash";
            DestroyCollider(go.transform);
            var flash = go.AddComponent<StrikeFlash>();
            flash._mat = new Material(shader);
            var r = go.GetComponent<MeshRenderer>();
            if (r != null)
                r.material = flash._mat;
            return flash;
        }

        private void Update()
        {
            _age += Time.deltaTime;
            float t = Mathf.Clamp01(_age / _lifetime);
            float s = Mathf.Lerp(0.3f, 1f, Mathf.SmoothStep(0f, 0.4f, t));
            transform.localScale = Vector3.one * (_scale * s);
            if (_mat != null)
            {
                Color c = _mat.color;
                c.a = 1f - t;
                _mat.color = c;
            }
            if (t >= 1f)
            {
                gameObject.SetActive(false);
                if (_pool.Count < PoolCap)
                    _pool.Add(this);
                else
                    Destroy(gameObject);
            }
        }
    }
}
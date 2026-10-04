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
    private readonly Collider[] _strikeBuffer = new Collider[128];

    /// <summary>1ie/1ih: the storm's resolved look, cached at Initialize.</summary>
    private SpellLook _look;

    public void Initialize(SpellCaster caster, SpellData spell, float power, float radiusMult = 1f)
    {
        _caster = caster;
        _spell = spell;
        _power = power;
        _casterRoot = caster != null ? caster.transform.root : null;
        radiusMult = Mathf.Max(radiusMult, 0.01f);
        // 1ie: the storm's body colour and its spawn ring both come from the per-spell look, so a
        // lightning storm and a frost storm are told apart by their Core hue as well as by
        // their shape. _type is kept as-is — it still gates the bolt-vs-sphere FX branch.
        //
        // Resolved OUTSIDE the spell != null guard because RingFlash below reads _look.Scale
        // unconditionally. In the guard it would stay default(SpellLook), whose Scale is 0 —
        // so a spell-less storm spawned one ring at scale 0 (5x too small) while its body
        // used the real Radius. Zone/Beam/Tornado all seed the fallback for this reason;
        // read as a sibling rather than re-derived per class.
        _look = spell != null ? SpellLook.Resolve(spell) : SpellLook.Resolve(DamageType.Wind, ProjectileShape.Auto);
        if (spell != null)
        {
            Radius = Mathf.Max(spell.Radius * radiusMult, 1.5f);
            if (spell.Duration > 0f) Lifetime = spell.Duration;
            if (spell.TickInterval > 0f) TickInterval = spell.TickInterval;
            _color = _look.Core;
            _type = spell.Type;
        }

        SkillFx.RingFlash(transform.position, Vector3.up, _color, Radius, 0.5f, _look.Scale);
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
        // `_look` is already resolved above (:47), so the sky-rock style rides along with the tint
        // rather than being re-resolved here (1f7).
        if (_spell != null && _spell.SummonFallingRock)
        {
            float scale = Mathf.Max(_spell.Radius * 0.5f, 1.2f);
            Color tint = _color;
            SkillFx.FallRock(at, scale, tint, _look.SkyRock, () =>
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

        // 1ih: the per-strike flash is now the shared per-spell impact family instead of a
        // sphere-only StrikeFlash. 14 storm spells share it, which is what sized PoolCap at 96 —
        // StrikeFlash's 32 was sized for the same spells but is now retired, so its cap has no
        // remaining owner. The ring stays: a strike with a flash AND a ring is the strongest
        // on-screen read, and only the sphere is redundant.
        SpellImpactFx.Spawn(at, Vector3.up, _look, 1.6f);
        SkillFx.RingFlash(ground, Vector3.up, c, Random.Range(0.8f, 1.4f), 0.35f, _look.Scale);
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

}
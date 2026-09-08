using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Central spell-casting runtime (Â§3.8). Validates focus points (FP) + cooldowns,
/// plays the cast time, then delivers the spell (instant / projectile / zone) and
/// resolves damage through DamageCalculator scaled by Wisdom.
///
/// MagicWeaponBehavior and active skills both cast through this shared pipeline.
/// </summary>
public class SpellCaster : MonoBehaviour
{
    [Header("Focus Pool")]
    [Tooltip("Max FP. From Intelligence (IStatProvider.MaxFocusPoints); falls back to this if no provider.")]
    public float MaxFp = 50f;
    public float RegenRate = 5f;
    public float RegenDelay = 0.3f;

    [Header("Wiring")]
    [Tooltip("Optional stat provider for Wisdom scaling + FP pool (wired Phase 4).")]
    public IStatProvider Stats;

    public float CurrentFp { get; private set; }

    private float _regenTimer;
    private readonly Dictionary<string, float> _cooldowns = new Dictionary<string, float>();

    /// <summary>Fires with the spell data whenever a cast begins.</summary>
    public event Action<SpellData> OnCastStarted;
    /// <summary>Fires with the spell data + resolved results whenever a cast completes.</summary>
    public event Action<SpellData, DamageResult> OnCastComplete;

    /// <summary>Result of an executed spell.</summary>
    public struct DamageResult
    {
        public float TotalDamage;
        public bool HitTargets;
    }

    private void Awake()
    {
        CurrentFp = MaxFocusPoints();
    }

    private void Update()
    {
        // Regen FP (only when below max).
        float max = MaxFocusPoints();
        if (CurrentFp < max)
        {
            _regenTimer -= Time.deltaTime;
            if (_regenTimer <= 0f)
                CurrentFp = Mathf.Min(CurrentFp + RegenRate * Time.deltaTime, max);
        }

        // Tick cooldowns every frame regardless of FP level, so spells/arts are
        // never stuck while the pool is full.
        if (_cooldowns.Count > 0)
        {
            var keys = new List<string>(_cooldowns.Keys);
            foreach (var k in keys)
            {
                _cooldowns[k] -= Time.deltaTime;
                if (_cooldowns[k] <= 0f) _cooldowns.Remove(k);
            }
        }
    }

    private float MaxFocusPoints() =>
        Stats != null ? Mathf.Max(Stats.MaxFocusPoints, 0f) : MaxFp;

    /// <summary>True if the given FP amount is currently available.</summary>
    public bool HasFocusPoints(float amount) => CurrentFp >= amount;

    /// <summary>Spend focus points if available. Returns false if insufficient.</summary>
    public bool TrySpendFocus(float amount)
    {
        if (CurrentFp < amount) return false;
        CurrentFp -= amount;
        _regenTimer = RegenDelay;
        return true;
    }

    /// <summary>Whether the spell's cooldown has elapsed (true = ready to cast).</summary>
    public bool IsReady(SpellData spell)
    {
        if (spell == null) return false;
        return !_cooldowns.TryGetValue(spell.id, out float remaining) || remaining <= 0f;
    }

    /// <summary>Remaining cooldown seconds for the spell (0 = ready).</summary>
    public float RemainingCooldown(SpellData spell)
    {
        if (spell == null) return 0f;
        return _cooldowns.TryGetValue(spell.id, out float remaining) ? Mathf.Max(remaining, 0f) : 0f;
    }

    /// <summary>Remaining cooldown seconds for an arbitrary cooldown key (0 = ready). Used by Weapon Arts.</summary>
    public float CooldownRemaining(string key)
    {
        if (string.IsNullOrEmpty(key)) return 0f;
        return _cooldowns.TryGetValue(key, out float remaining) ? Mathf.Max(remaining, 0f) : 0f;
    }

    /// <summary>True if the cooldown key is ready (not cooling down).</summary>
    public bool CooldownReady(string key)
    {
        return CooldownRemaining(key) <= 0f;
    }

    /// <summary>Start a cooldown for an arbitrary key (used by Weapon Arts to gate reuse).</summary>
    public void StartCooldown(string key, float seconds)
    {
        if (string.IsNullOrEmpty(key)) return;
        _cooldowns[key] = Mathf.Max(seconds, 0f);
    }

    /// <summary>
    /// Begin casting a spell. Applies weapon magic-mods, validates FP + cooldown, plays
    /// cast time, then executes. Returns true if the cast began.
    /// </summary>
    public bool BeginCast(SpellData spell, Transform origin, MagicWeaponMods mods = default)
    {
        if (spell == null) return false;
        if (!IsReady(spell)) return false;

        float fpCost = Mathf.Max(spell.FpCost * mods.FpCostMult, 0f);
        if (!HasFocusPoints(fpCost)) return false;

        TrySpendFocus(fpCost);
        StartCoroutine(CastRoutine(spell, origin, mods));
        OnCastStarted?.Invoke(spell);
        return true;
    }

    private IEnumerator CastRoutine(SpellData spell, Transform origin, MagicWeaponMods mods)
    {
        // Cast time (modulated by weapon CastTimeMod).
        float castTime = spell.CastTime * Mathf.Max(mods.CastTimeMult, 0.05f);
        if (castTime > 0f)
        {
            float t = 0f;
            while (t < castTime)
            {
                t += Time.deltaTime;
                yield return null;
            }
        }

        // Execute the spell.
        DamageResult result = Execute(spell, origin, mods);
        OnCastComplete?.Invoke(spell, result);

        // Apply cooldown (modulated by weapon CooldownMod).
        _cooldowns[spell.id] = spell.Cooldown * Mathf.Max(mods.CooldownMult, 0.05f);
    }

    private DamageResult Execute(SpellData spell, Transform origin, MagicWeaponMods mods)
    {
        float basePower = spell.BasePower * mods.DamageMult;
        float wisdom = Stats != null ? Stats.MagicAttackPower : 0f;
        float totalPower = basePower + wisdom * 1f;

        Vector3 pos = origin != null ? origin.position : transform.position;
        Vector3 fwd = origin != null ? origin.forward : transform.forward;

        switch (spell.Delivery)
        {
            case SpellDelivery.Instant:
                return ResolveDirect(totalPower, spell, pos, fwd);
            case SpellDelivery.Projectile:
                return FireProjectile(totalPower, spell, pos, fwd);
            case SpellDelivery.Zone:
                return ResolveZone(totalPower, spell, pos);
            default:
                return new DamageResult();
        }
    }

    private DamageResult ResolveDirect(float power, SpellData spell, Vector3 pos, Vector3 fwd)
    {
        if (Physics.Raycast(pos, fwd, out RaycastHit hit, spell.Range))
        {
            return ApplyHit(spell, power, hit.collider.gameObject);
        }
        return new DamageResult();
    }

    private DamageResult FireProjectile(float power, SpellData spell, Vector3 pos, Vector3 fwd)
    {
        GameObject go;
        if (spell.CastEffectPrefab != null)
        {
            go = Instantiate(spell.CastEffectPrefab, pos, Quaternion.LookRotation(fwd));
            if (go.GetComponent<SpellEffect>() == null)
            {
                var fx = go.AddComponent<SpellEffect>();
                fx.Initialize(spell, power, fwd, this);
            }
        }
        else
        {
            go = new GameObject("SpellProjectile");
            go.transform.position = pos;
            go.transform.rotation = Quaternion.LookRotation(fwd);
            AttachDefaultProjectileVisual(go, spell.Type);
            go.AddComponent<SpellEffect>().Initialize(spell, power, fwd, this);
        }

        if (go.TryGetComponent<SpellEffect>(out var proj))
            proj.Launch(spell.ProjectileSpeed);

        return new DamageResult();
    }

    /// <summary>
    /// Build a per-type visible projectile body + trail for spells with no authored
    /// CastEffectPrefab, so magic skills read on screen. Renderer-only: the root keeps no
    /// collider so SpellEffect's flight raycast never self-hits.
    /// </summary>
    private void AttachDefaultProjectileVisual(GameObject go, DamageType type)
    {
        Color color = DamageNumber.ColorFor(type);
        Shader shader = Shader.Find("Sprites/Default") ?? Shader.Find("Unlit/Color");
        if (shader == null)
            return;

        var body = BuildProjectileBody(type, shader, color);
        body.SetParent(go.transform, false);

        var trail = go.GetComponent<TrailRenderer>();
        if (trail == null)
            trail = go.AddComponent<TrailRenderer>();
        trail.material = new Material(shader) { color = color };
        trail.time = TrailTime(type);
        trail.startWidth = TrailWidth(type);
        trail.endWidth = 0f;
        trail.minVertexDistance = 0.05f;
        var grad = new Gradient();
        grad.SetKeys(
            new[] { new GradientColorKey(color, 0f), new GradientColorKey(color, 1f) },
            new[] { new GradientAlphaKey(0.9f, 0f), new GradientAlphaKey(0f, 1f) });
        trail.colorGradient = grad;
    }

    /// <summary>Color-matched visual body for a projectile by damage type.</summary>
    private static Transform BuildProjectileBody(DamageType type, Shader shader, Color color)
    {
        switch (type)
        {
            case DamageType.Fire:
                return Orb("Fireball", PrimitiveType.Sphere, Vector3.one * 0.3f, shader, color,
                    OrbFx.Mode.Ember);
            case DamageType.Ice:
                return Shard("Frostbolt", shader, color);
            case DamageType.Lightning:
                return Spark("ChainBolt", shader, color);
            case DamageType.Dark:
                return Orb("DarkBolt", PrimitiveType.Sphere, Vector3.one * 0.26f, shader, color,
                    OrbFx.Mode.Wisp);
            default:
                return Orb("Orb", PrimitiveType.Sphere, Vector3.one * 0.22f, shader, color,
                    OrbFx.Mode.Plain);
        }
    }

    private static Transform Orb(string name, PrimitiveType shape, Vector3 scale, Shader shader,
        Color color, OrbFx.Mode mode)
    {
        var orb = GameObject.CreatePrimitive(shape);
        orb.name = name;
        Collider col = orb.GetComponent<Collider>();
        if (col != null)
            Destroy(col);
        orb.transform.localScale = scale;
        orb.GetComponent<MeshRenderer>().material = new Material(shader) { color = color };
        orb.AddComponent<OrbFx>().Pulse = mode;
        return orb.transform;
    }

    /// <summary>Diamond-shaped ice shard that drills forward.</summary>
    private static Transform Shard(string name, Shader shader, Color color)
    {
        var shard = GameObject.CreatePrimitive(PrimitiveType.Cube);
        shard.name = name;
        Collider col = shard.GetComponent<Collider>();
        if (col != null)
            Destroy(col);
        shard.transform.localScale = new Vector3(0.12f, 0.38f, 0.12f);
        shard.transform.localRotation = Quaternion.Euler(0f, 45f, 0f);
        shard.GetComponent<MeshRenderer>().material = new Material(shader) { color = color };
        shard.AddComponent<OrbFx>().Pulse = OrbFx.Mode.Shard;
        return shard.transform;
    }

    /// <summary>Two crossed thin bars forming a crackling X bolt.</summary>
    private static Transform Spark(string name, Shader shader, Color color)
    {
        var spark = new GameObject(name).transform;
        var a = GameObject.CreatePrimitive(PrimitiveType.Cube);
        a.name = "BarA";
        Collider colA = a.GetComponent<Collider>();
        if (colA != null)
            Destroy(colA);
        a.transform.SetParent(spark, false);
        a.transform.localScale = new Vector3(0.26f, 0.03f, 0.03f);
        a.GetComponent<MeshRenderer>().material = new Material(shader) { color = color };

        var b = GameObject.CreatePrimitive(PrimitiveType.Cube);
        b.name = "BarB";
        Collider colB = b.GetComponent<Collider>();
        if (colB != null)
            Destroy(colB);
        b.transform.SetParent(spark, false);
        b.transform.localScale = new Vector3(0.03f, 0.03f, 0.26f);
        b.GetComponent<MeshRenderer>().material = new Material(shader) { color = color };

        spark.gameObject.AddComponent<OrbFx>().Pulse = OrbFx.Mode.Bolt;
        return spark;
    }

    private static float TrailWidth(DamageType type)
    {
        switch (type)
        {
            case DamageType.Fire: return 0.20f;
            case DamageType.Ice: return 0.14f;
            case DamageType.Lightning: return 0.12f;
            case DamageType.Dark: return 0.12f;
            default: return 0.18f;
        }
    }

    private static float TrailTime(DamageType type)
    {
        switch (type)
        {
            case DamageType.Fire: return 0.50f;
            case DamageType.Ice: return 0.45f;
            case DamageType.Lightning: return 0.30f;
            case DamageType.Dark: return 0.50f;
            default: return 0.40f;
        }
    }

    /// <summary>
    /// Tiny flight animation for a projectile body: a per-type scale pulse (flicker / crackle /
    /// breathe) and, for the ice shard, a drill spin around its long axis.
    /// </summary>
    private sealed class OrbFx : MonoBehaviour
    {
        public enum Mode
        {
            Plain,  // gentle breathe
            Ember,  // fast irregular flicker
            Shard,  // slight breathe + drill spin
            Bolt,   // fast crackle pulse
            Wisp    // slow pulsing
        }

        public Mode Pulse;

        private Vector3 _baseScale;

        private void Start()
        {
            _baseScale = transform.localScale;
        }

        private void Update()
        {
            float t = Time.time;
            float pulse;
            float spin = 0f;
            switch (Pulse)
            {
                case Mode.Ember:
                    pulse = 1f + 0.14f * Mathf.Sin(t * 11f) + 0.08f * Mathf.Sin(t * 17.3f);
                    break;
                case Mode.Shard:
                    pulse = 1f + 0.04f * Mathf.Sin(t * 4.2f);
                    spin = 160f;
                    break;
                case Mode.Bolt:
                    pulse = 1f + 0.22f * Mathf.Sin(t * 24f) * Mathf.Sin(t * 7f);
                    break;
                case Mode.Wisp:
                    pulse = 1f + 0.10f * Mathf.Sin(t * 2.6f);
                    break;
                default:
                    pulse = 1f + 0.06f * Mathf.Sin(t * 3.4f);
                    break;
            }
            transform.localScale = _baseScale * Mathf.Max(0.1f, pulse);
            if (spin != 0f)
                transform.Rotate(0f, spin * Time.deltaTime, 0f, Space.Self);
        }
    }

    private DamageResult ResolveZone(float power, SpellData spell, Vector3 pos)
    {
        SpawnZoneRing(pos, spell);

        Collider[] cols = Physics.OverlapSphere(pos, spell.Radius);
        bool hitAny = false;
        float total = 0f;
        foreach (var col in cols)
        {
            if (col.transform.root == transform.root) continue;
            var hit = ApplyHit(spell, power, col.gameObject);
            total += hit.TotalDamage;
            hitAny |= hit.HitTargets;
        }
        return new DamageResult { TotalDamage = total, HitTargets = hitAny };
    }

    /// <summary>Ground ring flash sized to the spell's radius so zone spells read on screen.</summary>
    private static void SpawnZoneRing(Vector3 pos, SpellData spell)
    {
        if (spell == null || spell.Radius <= 0f) return;
        Vector3 ground = pos;
        if (Physics.Raycast(pos + Vector3.up * 0.1f, Vector3.down, out RaycastHit hit, 4f))
            ground = hit.point;
        SkillFx.RingFlash(ground, Vector3.up, DamageNumber.ColorFor(spell.Type), spell.Radius, 0.5f);
    }

    private DamageResult ApplyHit(SpellData spell, float power, GameObject target)
    {
        var ctx = new DamageCalculator.HitContext
        {
            AttackPower = power,
            SkillMultiplier = 1f,
            Defense = 5f,
            DefenseMultiplier = 1f,
            Type = spell.Type,
            Resistance = NeutralResistance.Instance,
            WeaknessMultiplier = 1f,
            CriticalMultiplier = 1f,
        };
        var result = DamageCalculator.Calculate(ctx, false);

        if (target.TryGetComponent<IDamageable>(out var damageable))
            damageable.TakeDamage(Mathf.RoundToInt(result.TotalDamage));
        if (result.TotalDamage > 0f)
            DamageNumber.Spawn(target.transform.position, result.TotalDamage, spell.Type);

        if (spell.ImpactEffectPrefab != null)
            Instantiate(spell.ImpactEffectPrefab, target.transform.position, Quaternion.identity);

        return new DamageResult
        {
            TotalDamage = result.TotalDamage,
            HitTargets = true
        };
    }

    /// <summary>
    /// Resolve a spell's damage against a specific target (used by SpellEffect on
    /// projectile/zone impact). Accessible so effects can route back through the shared
    /// DamageCalculator pipeline.
    /// </summary>
    public void ResolveHitAt(GameObject target, SpellData spell, float power)
    {
        if (target == null || spell == null) return;
        ApplyHit(spell, power, target);
    }
}

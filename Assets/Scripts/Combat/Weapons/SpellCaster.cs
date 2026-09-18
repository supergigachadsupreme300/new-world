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

    [Header("Charging (§3.8)")]
    [Tooltip("Extra focus-point cost per charge level (1.0 = up to +100% at full charge, so cost keeps pace with power).")]
    public float ChargeFpCostBonus = 1f;
    [Tooltip("Extra spell power per charge level (1.6 = up to +160% at full charge).")]
    public float ChargeDamageBonus = 1.6f;
    [Tooltip("Extra size/radius per charge level (1.2 = up to +120% at full charge).")]
    public float ChargeSizeBonus = 1.2f;
    [Tooltip("Focus points drained per second while charging at level 1 (real-time charge drain). Scales with the charge level, so overcharging burns FP faster.")]
    public float FpChargeDrainRate = 1.5f;

    [Header("Wiring")]
    [Tooltip("Optional stat provider for Wisdom scaling + FP pool (wired Phase 4).")]
    public IStatProvider Stats;

    public float CurrentFp { get; private set; }

    /// <summary>Total casts ever started on this caster (monotonic — used to track a specific cast).</summary>
    public int CastCount { get; private set; }

    /// <summary>True while at least one spell cast is still in progress (cast time / delivery).</summary>
    public bool IsCasting => _activeCasts > 0;

    /// <summary>True while a Beam channel is active (held by the user with LMB, draining FP).</summary>
    public bool IsChanneling => _activeBeam != null;

    private int _activeCasts;
    private SpellBeam _activeBeam;

    private float _regenTimer;
    private readonly Dictionary<string, float> _cooldowns = new Dictionary<string, float>();
    // Reused key list so the per-frame cooldown tick never allocates.
    private readonly List<string> _cooldownKeys = new List<string>();
    // Reused burst-damage overlap buffer (ResolveBurst below).
    private readonly Collider[] _overlapBuffer = new Collider[128];

    /// <summary>Spell id whose Vortex delivery is the Great Tornado (old environmental tornado model + function).</summary>
    private const string GreatTornadoSpellId = "magic_tornado_spell";

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

    private bool _poolInitialized;

    private void Awake()
    {
        // Don't snapshot the start pool here: the real max comes from Stats, which the combat-stack
        // builder wires AFTER AddComponent<SpellCaster>() (so Stats is still null during Awake).
        // Snapshoting it now would leave the player with the 50-FP fallback instead of full mana.
        // The pool is filled from the real max on the first Update once the provider is available.
    }

    private void Update()
    {
        if (!_poolInitialized)
        {
            _poolInitialized = true;
            CurrentFp = MaxFocusPoints();
        }

        // Regen FP (only when below max).
        float max = MaxFocusPoints();
        if (CurrentFp < max)
        {
            _regenTimer -= Time.deltaTime;
            if (_regenTimer <= 0f)
                CurrentFp = Mathf.Min(CurrentFp + RegenRate * FocusRegenMult() * Time.deltaTime, max);
        }

        // Tick cooldowns every frame regardless of FP level, so spells/arts are
        // never stuck while the pool is full. Iterate a reused key list (no per-frame alloc),
        // writing back the decremented value once instead of double-indexing the dictionary.
        if (_cooldowns.Count > 0)
        {
            _cooldownKeys.Clear();
            foreach (var k in _cooldowns.Keys)
                _cooldownKeys.Add(k);
            for (int i = 0; i < _cooldownKeys.Count; i++)
            {
                string k = _cooldownKeys[i];
                float rem = _cooldowns[k] - Time.deltaTime;
                if (rem <= 0f)
                    _cooldowns.Remove(k);
                else
                    _cooldowns[k] = rem;
            }
        }
    }

    private float MaxFocusPoints() =>
        Stats != null ? Mathf.Max(Stats.MaxFocusPoints, 0f) : MaxFp;

    /// <summary>Focus-regen multiplier: tree perks (§3.3) folded over the base regen rate.</summary>
    private float FocusRegenMult() => Stats != null ? Stats.FocusRegenMul : 1f;

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

    /// <summary>Dev/test convenience (magic test matrix): refill the focus pool to its max.</summary>
    public void TopUpFocus() => CurrentFp = MaxFocusPoints();

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
    /// <paramref name="charge"/> (0..1+; no upper cap) raises the focus cost and scales power/size —
    /// clamped so the cast always fires as strong as the caster can still afford (paid via the
    /// <paramref name="prepaidFocus"/> real-time charge drain plus the current pool) rather than
    /// dudding out. Only the remainder after the prepaid drain is spent.
    /// <paramref name="fast"/> skips both the cooldown gate and the cast-time wait (wheel-cast magic
    /// resolves instantly on every click; FP is the only limiter) and never starts a cooldown.
    /// </summary>
    public bool BeginCast(SpellData spell, Transform origin, MagicWeaponMods mods = default, float charge = 0f, float prepaidFocus = 0f, bool fast = false)
    {
        if (spell == null) return false;
        if (!fast && !IsReady(spell)) return false;
        charge = Mathf.Max(0f, charge);
        prepaidFocus = Mathf.Max(0f, prepaidFocus);

        if (mods.DamageMult <= 0f) mods.DamageMult = 1f;
        if (mods.CastTimeMult <= 0f) mods.CastTimeMult = 1f;
        if (mods.CooldownMult <= 0f) mods.CooldownMult = 1f;
        if (mods.FpCostMult <= 0f) mods.FpCostMult = 1f;
        if (mods.RadiusMult <= 0f) mods.RadiusMult = 1f;
        if (mods.RangeMult <= 0f) mods.RangeMult = 1f;

        float baseCost = Mathf.Max(spell.FpCost * mods.FpCostMult, 0f);
        if (charge > 0f)
            charge = ClampChargeToAffordable(baseCost, charge, prepaidFocus);

        float fpCost = baseCost * (1f + charge * ChargeFpCostBonus);
        float remainder = Mathf.Max(0f, fpCost - prepaidFocus);
        if (!HasFocusPoints(remainder)) return false;

        TrySpendFocus(remainder);
        // A successful new cast replaces the active beam channel (rejected casts leave it alone).
        StopChannel();
        _activeCasts++;
        CastCount++;
        StartCoroutine(CastRoutine(spell, origin, mods, charge, fast));
        OnCastStarted?.Invoke(spell);
        return true;
    }

    /// <summary>Reduce a held charge so its focus cost fits the total the caster can pay
    /// (the prepaid real-time drain plus the current pool).</summary>
    private float ClampChargeToAffordable(float baseCost, float charge, float prepaidFocus)
    {
        if (baseCost <= 0f) return charge;
        float available = prepaidFocus + CurrentFp;
        float maxCharge = (available / baseCost - 1f) / ChargeFpCostBonus;
        return Mathf.Min(charge, Mathf.Max(maxCharge, 0f));
    }

    private IEnumerator CastRoutine(SpellData spell, Transform origin, MagicWeaponMods mods, float charge, bool fast)
    {
        // Cast time (modulated by weapon CastTimeMod). A fast cast (wheel-cast magic) resolves
        // immediately — both charged casts (wind-up spent on the hold) and tap casts — so the burst
        // ring and the delivery land on the same frame. Only non-fast casts wait.
        if (!fast)
        {
            float castTime = spell.CastTime * Mathf.Max(mods.CastTimeMult, 0.05f);
            if (castTime > 0f && charge <= 0f)
            {
                float t = 0f;
                while (t < castTime)
                {
                    t += Time.deltaTime;
                    yield return null;
                }
            }
        }

        // Execute the spell.
        DamageResult result = Execute(spell, origin, mods, charge);
        OnCastComplete?.Invoke(spell, result);

        // Apply cooldown (modulated by weapon CooldownMod and tree cooldown-reduction perks §3.3).
        // Fast casts skip it — FP is the limiter.
        if (!fast)
            _cooldowns[spell.id] = spell.Cooldown * Mathf.Max(mods.CooldownMult, 0.05f)
                * (Stats != null ? Stats.CooldownReductionMult : 1f);
        _activeCasts = Mathf.Max(0, _activeCasts - 1);
    }

    private DamageResult Execute(SpellData spell, Transform origin, MagicWeaponMods mods, float charge)
    {
        float basePower = spell.BasePower * mods.DamageMult * (1f + charge * ChargeDamageBonus);
        float wisdom = Stats != null ? Stats.MagicAttackPower : 0f;
        float totalPower = basePower + wisdom * 1f;

        Vector3 pos = origin != null ? origin.position : transform.position;
        Vector3 fwd = origin != null ? origin.forward : transform.forward;

        Camera cam = Camera.main;
        if (cam != null)
        {
            Vector3 aim = cam.transform.position + cam.transform.forward * Mathf.Max(spell.Range, 5f);
            Vector3 dir = aim - pos;
            if (dir.sqrMagnitude > 0.0001f)
                fwd = dir.normalized;
        }

        switch (spell.Delivery)
        {
            case SpellDelivery.Instant:
                return ResolveDirect(totalPower, spell, pos, fwd, spell.Range * mods.RangeMult);
            case SpellDelivery.Projectile:
                return FireProjectile(totalPower, spell, pos, fwd, charge, SizeScale(charge) * mods.RadiusMult);
            case SpellDelivery.Zone:
                return ResolveZone(totalPower, spell, pos, fwd, charge, SizeScale(charge) * mods.RadiusMult, spell.Range * mods.RangeMult);
            case SpellDelivery.Vortex:
                return SpawnVortex(totalPower, spell, pos, fwd, charge, SizeScale(charge) * mods.RadiusMult, spell.Range * mods.RangeMult);
            case SpellDelivery.Beam:
                return ResolveBeam(totalPower, spell, pos, fwd, charge, SizeScale(charge) * mods.RadiusMult);
            case SpellDelivery.Summon:
                return ResolveSummon(totalPower, spell, pos, fwd, charge, SizeScale(charge) * mods.RadiusMult, spell.Range * mods.RangeMult);
            case SpellDelivery.Storm:
                return ResolveStorm(totalPower, spell, pos, fwd, charge, SizeScale(charge) * mods.RadiusMult, spell.Range * mods.RangeMult);
            default:
                return new DamageResult();
        }
    }

    /// <summary>End the active Beam channel (if any). Returns true when one was running.</summary>
    public bool StopChannel()
    {
        if (_activeBeam == null) return false;
        _activeBeam.StopChannel();
        _activeBeam = null;
        return true;
    }

    /// <summary>Clear the caster's channel reference after the beam destroys itself.</summary>
    internal void ForgetBeam(SpellBeam beam)
    {
        if (_activeBeam == beam) _activeBeam = null;
    }

    /// <summary>
    /// Spawn a channeled beam from the cast point toward the aim. The beam lives while the caster
    /// holds the sustain input (LMB) and can afford its per-second focus upkeep; it fades out on
    /// release or when the pool runs dry. Charge widens the beam and raises its tick power.
    /// </summary>
    private DamageResult ResolveBeam(float power, SpellData spell, Vector3 pos, Vector3 fwd, float charge, float sizeScale)
    {
        StopChannel();
        var go = new GameObject("SpellBeam");
        go.transform.position = pos + fwd * 0.5f + Vector3.up * 0.2f;
        go.transform.rotation = Quaternion.LookRotation(fwd);
        var beam = go.AddComponent<SpellBeam>();
        beam.Initialize(this, spell, power, fwd, sizeScale, sizeScale);
        _activeBeam = beam;
        return new DamageResult { HitTargets = true };
    }

    /// <summary>Drop the forward aim onto the ground — shared ground-placement for zone/summon/storm.</summary>
    private static Vector3 GroundTarget(Vector3 pos, Vector3 fwd, float range)
    {
        Vector3 at = pos;
        if (Physics.Raycast(pos, fwd, out RaycastHit aimHit, Mathf.Max(range, 0.1f)))
            at = aimHit.point;
        else
            at = pos + fwd * Mathf.Max(range, 0f);
        if (Physics.Raycast(at + Vector3.up * 0.1f, Vector3.down, out RaycastHit groundHit, 30f))
            at = groundHit.point;
        return at;
    }

    /// <summary>
    /// Summon a persistent object at the goal point (SpellDelivery.Summon). Damage summons act as
    /// turrets firing at the nearest enemy; healing summons become a persistent heal aura.
    /// </summary>
    private DamageResult ResolveSummon(float power, SpellData spell, Vector3 pos, Vector3 fwd, float charge, float sizeScale, float range)
    {
        Vector3 center = GroundTarget(pos, fwd, range);

        // Earth summons (the golem line) erupt a small rock field where the construct rises
        // (§3.8). Other schools carry no terrain shape and no-op in TerrainDeformer. A modest
        // radius so the bump reads as the construct breaking the surface, not a wide reshape.
        if (spell.TerrainShape != TerrainShape.None)
            TerrainDeformer.Apply(center, Mathf.Min(spell.Radius * 0.4f, 2.5f), spell.TerrainShape, fwd);

        var go = new GameObject("SpellSummon");
        go.transform.position = center;
        go.AddComponent<SpellSummon>().Initialize(this, spell, power, sizeScale);
        return new DamageResult { HitTargets = true };
    }

    /// <summary>
    /// Summon a storm over the goal point (SpellDelivery.Storm): repeated element-styled strikes
    /// inside the radius for the spell's duration.
    /// </summary>
    private DamageResult ResolveStorm(float power, SpellData spell, Vector3 pos, Vector3 fwd, float charge, float sizeScale, float range)
    {
        Vector3 center = GroundTarget(pos, fwd, range);
        var go = new GameObject("SpellStorm");
        go.transform.position = center;
        go.AddComponent<SpellStorm>().Initialize(this, spell, power, sizeScale);
        return new DamageResult { HitTargets = true };
    }

    /// <summary>Public access to the default projectile visual (used by summoned turrets).</summary>
    public void DecorateProjectile(GameObject go, DamageType type, ProjectileShape shape = ProjectileShape.Auto)
    {
        AttachDefaultProjectileVisual(go, type, shape);
    }

    /// <summary>Size multiplier applied to deliveries by charge level.</summary>
    private float SizeScale(float charge) => 1f + charge * ChargeSizeBonus;

    /// <summary>
    /// Spawn a tornado at the cast location (SpellDelivery.Vortex). The Great Tornado
    /// (magic_tornado) uses the old environmental tornado model + function — a tall drifting
    /// debris funnel (TornadoBehavior); other Vortex spells use a persistent
    /// <see cref="SpellZone"/>. Both tick the spell's damage over their lifetime and drag
    /// enemies toward the center — winds pulled in like the tornado. Positioned by raycasting
    /// along the cast direction up to <see cref="SpellData.Range"/>, then dropped to the
    /// ground so the funnel sits on terrain.
    /// </summary>
    private DamageResult SpawnVortex(float power, SpellData spell, Vector3 pos, Vector3 fwd, float charge, float sizeScale, float range)
    {
        Vector3 at = pos;
        if (Physics.Raycast(pos, fwd, out RaycastHit hit, Mathf.Max(range, 0.1f)))
            at = hit.point;
        else
            at = pos + fwd * Mathf.Max(range, 0f);

        Vector3 ground = at;
        if (Physics.Raycast(at + Vector3.up * 0.1f, Vector3.down, out RaycastHit groundHit, 30f))
            ground = groundHit.point;

        // The Great Tornado (magic_tornado) rebuilds the old environmental tornado — a tall
        // tapering funnel of debris blocks (MapBuilder.BuildTornado) that drifts and pulls
        // objects around via physics (TornadoBehavior) — scaled down to the spell radius,
        // instead of the small stationary SpellZone funnel used by other Vortex spells.
        if (spell != null && spell.id == GreatTornadoSpellId)
        {
            float radius = Mathf.Max(spell.Radius > 0f ? spell.Radius * sizeScale : 3f, 0.5f);
            float height = Mathf.Max(radius * 3.5f, 10f);
            float widthScale = Mathf.Max((radius * 2.2f) / 35.5f, 0.12f);

            var tornado = MapBuilder.BuildTornado(null, ground, height, widthScale);
            var spellTornado = tornado.AddComponent<SpellTornado>();
            spellTornado.Initialize(this, spell, power, sizeScale, 1f);
            spellTornado.Lifetime = Mathf.Max(spell.Duration > 0f ? spell.Duration : 5f, 1f);
            return new DamageResult { HitTargets = true };
        }

        var go = new GameObject("SpellVortex");
        go.transform.position = ground;
        var zone = go.AddComponent<SpellZone>();
        zone.Initialize(this, spell, power, sizeScale, 1f, 3.5f);
        zone.Lifetime = Mathf.Max(spell.Duration > 0f ? spell.Duration : 5f, 1f);

        return new DamageResult { HitTargets = true };
    }

    private DamageResult ResolveDirect(float power, SpellData spell, Vector3 pos, Vector3 fwd, float range)
    {
        if (spell.SelfBuff)
        {
            // Self-buff: grant the caster (the player) a timed effect — e.g. flight for the
            // Duration. No damage, no target raycast. Applied to whatever owns this caster.
            var pc = transform.root.GetComponent<PlayerController>();
            if (pc != null && spell.Duration > 0f)
            {
                pc.BeginFlight(spell.Duration);
                SkillFx.RingFlash(transform.position, Vector3.up, DamageNumber.ColorFor(spell.Type), 2.5f, 0.5f);
            }
            return new DamageResult { HitTargets = true };
        }
        if (spell.Heals)
        {
            // Instant restoration casts on the caster (the classic "holy touch").
            int healed = ResolveHeal(spell, power, transform.root.gameObject);
            return new DamageResult { TotalDamage = healed, HitTargets = healed > 0 };
        }
        if (Physics.Raycast(pos, fwd, out RaycastHit hit, range))
        {
            return ApplyHit(spell, power, hit.collider.gameObject);
        }
        return new DamageResult();
    }

    private DamageResult FireProjectile(float power, SpellData spell, Vector3 pos, Vector3 fwd, float charge, float sizeScale)
    {
        // Earth projectiles (TerrainShape.Crater) carve their crater where the shard STRIKES —
        // the impact point, not the caster's own footing. The carve lives in
        // SpellEffect.ResolveProjectileImpact (down-probed onto the ground there), so casting
        // never dents the ground under the player.

        // Spawn on the aim line only (no vertical lift) so the trajectory passes through the
        // casting circle's center (the circle is anchored on the same origin as this cast).
        // The small forward muzzle offset mirrors the ranged Muzzle and keeps the bolt clear of
        // the caster's own collider; the SpellEffect's caster-root skip and ground probe handle
        // self/terrain contacts from there.
        pos += fwd * 0.5f;
        GameObject go;
        if (spell.CastEffectPrefab != null)
        {
            go = Instantiate(spell.CastEffectPrefab, pos, Quaternion.LookRotation(fwd));
            if (go.GetComponent<SpellEffect>() == null)
            {
                var fx = go.AddComponent<SpellEffect>();
                fx.Initialize(spell, power, fwd, this, sizeScale);
            }
        }
        else
        {
            go = new GameObject("SpellProjectile");
            go.transform.position = pos;
            go.transform.rotation = Quaternion.LookRotation(fwd);
            AttachDefaultProjectileVisual(go, spell.Type, spell.Shape);
            go.AddComponent<SpellEffect>().Initialize(spell, power, fwd, this, sizeScale);
        }

        // Charge scales the whole projectile body (authored prefab or generated visual).
        if (charge > 0f)
            go.transform.localScale *= sizeScale;

        if (go.TryGetComponent<SpellEffect>(out var proj))
            proj.Launch(spell.ProjectileSpeed);

        return new DamageResult();
    }

    /// <summary>
    /// Build a shape-aware visible projectile body + comet-exhaust particles for spells with no
    /// authored CastEffectPrefab, so magic skills read on screen. `shape` is the ProjectileShape
    /// from SpellData (§3.8): Auto resolves to the element default so every projectile still has a
    /// sane look; explicit shapes follow the spell's NAME ("Frost Bolt" = a Bolt, "Ice Lance" = a
    /// Lance, "Stone Shard" = a Shard...). Renderer-only: the root keeps no collider so
    /// SpellEffect's flight raycast never self-hits.
    /// </summary>
    private void AttachDefaultProjectileVisual(GameObject go, DamageType type, ProjectileShape shape)
    {
        Color color = DamageNumber.ColorFor(type);
        Shader shader = Shader.Find("Sprites/Default") ?? Shader.Find("Unlit/Color");
        if (shader == null)
            return;

        var body = BuildProjectileBody(ResolveShape(type, shape), shader, color);
        body.SetParent(go.transform, false);

        AttachProjectileParticles(body, type, color);
    }

    /// <summary>Element default shape used when a spell leaves Shape = Auto.</summary>
    private static ProjectileShape AutoShapeFor(DamageType type)
    {
        switch (type)
        {
            case DamageType.Fire: return ProjectileShape.Sphere;    // fireball
            case DamageType.Ice: return ProjectileShape.Shard;      // generic frost chip
            case DamageType.Lightning: return ProjectileShape.Bolt; // crackling bolt
            case DamageType.Wind: return ProjectileShape.Blade;     // wind blade
            case DamageType.Water: return ProjectileShape.Splash;   // droplet
            case DamageType.Earth: return ProjectileShape.Shard;    // grey stone chip
            case DamageType.Physical: return ProjectileShape.Dart;  // arrow / bolt line
            default: return ProjectileShape.Sphere;
        }
    }

    private static ProjectileShape ResolveShape(DamageType type, ProjectileShape shape)
        => shape == ProjectileShape.Auto ? AutoShapeFor(type) : shape;

    /// <summary>Color-matched visual body for a projectile by resolved shape.</summary>
    private static Transform BuildProjectileBody(ProjectileShape shape, Shader shader, Color color)
    {
        switch (shape)
        {
            case ProjectileShape.Bolt: return Bolt("JaggedBolt", shader, color);
            case ProjectileShape.Shard: return Shard("Shard", shader, color);
            case ProjectileShape.Lance: return Lance("IceLance", shader, color);
            case ProjectileShape.Spear: return Spear("Spear", shader, color);
            case ProjectileShape.Blade: return Blade("WindBlade", shader, color);
            case ProjectileShape.Splash: return Splash("WaterSplash", shader, color);
            case ProjectileShape.Comet: return Comet("Comet", shader, color);
            case ProjectileShape.Missile: return Missile("ArcaneMissiles", shader, color);
            case ProjectileShape.Dart: return Dart("Dart", shader, color);
            default: return Orb("Orb", PrimitiveType.Sphere, Vector3.one * 0.22f, shader, color,
                OrbFx.Mode.Plain);
        }
    }

    /// <summary>Primitive with its collider stripped, parented to `parent` at local zero.</summary>
    private static Transform Primitive(PrimitiveType kind, string name, Transform parent)
    {
        var go = GameObject.CreatePrimitive(kind);
        go.name = name;
        Collider col = go.GetComponent<Collider>();
        if (col != null)
            Destroy(col);
        go.transform.SetParent(parent, false);
        return go.transform;
    }

    private static void Materialize(Transform t, Shader shader, Color color)
        => t.GetComponent<MeshRenderer>().material = new Material(shader) { color = color };

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

    /// <summary>Diamond-shaped shard that drills forward.</summary>
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

    /// <summary>
    /// Jagged segmented bolt laid down the flight line — the same segment technique as the
    /// thunder-storm event's lightning bolt (RandomEventManager.SpawnJaggedBolt), so a "Bolt"
    /// reads as lightning (element-colored) rather than a plain sphere. Parent rotates to the
    /// aim, so the jitter lives in the local XY plane and the bolt streaks +Z.
    /// </summary>
    private static Transform Bolt(string name, Shader shader, Color color)
    {
        var root = new GameObject(name).transform;
        int segments = 8;
        float length = 1.25f;
        float jitter = 0.09f;
        Vector3 prev = new Vector3(0f, 0f, -length * 0.5f);
        for (int i = 0; i < segments; i++)
        {
            float t = i / (float)(segments - 1);
            Vector3 next = i == segments - 1
                ? new Vector3(0f, 0f, length * 0.5f)
                : new Vector3(
                    UnityEngine.Random.Range(-jitter, jitter),
                    UnityEngine.Random.Range(-jitter, jitter),
                    Mathf.Lerp(-length * 0.5f, length * 0.5f, t));
            var seg = Primitive(PrimitiveType.Cube, "Seg" + i, root);
            seg.localPosition = (prev + next) * 0.5f;
            Vector3 segDir = (next - prev).normalized;
            seg.localScale = new Vector3(
                Mathf.Lerp(0.17f, 0.05f, t),
                Mathf.Lerp(0.17f, 0.05f, t),
                Mathf.Max(0.1f, Vector3.Distance(prev, next)));
            if (segDir.sqrMagnitude > 0.001f && Mathf.Abs(segDir.z) < 0.999f)
                seg.rotation = Quaternion.LookRotation(segDir, Vector3.up);
            Materialize(seg, shader, color);
            prev = next;
        }
        root.gameObject.AddComponent<OrbFx>().Pulse = OrbFx.Mode.Bolt;
        return root;
    }

    /// <summary>Long, straight pointed spike (ice lance line) oriented along the flight line.</summary>
    private static Transform Lance(string name, Shader shader, Color color)
    {
        var root = new GameObject(name).transform;
        var shaft = Primitive(PrimitiveType.Cube, "Shaft", root);
        shaft.localScale = new Vector3(0.1f, 0.1f, 1.1f);
        Materialize(shaft, shader, color);
        var tip = Primitive(PrimitiveType.Cube, "Tip", root);
        tip.localPosition = new Vector3(0f, 0f, 0.62f);
        tip.localScale = new Vector3(0.12f, 0.12f, 0.22f);
        Materialize(tip, shader, color);
        root.gameObject.AddComponent<OrbFx>().Pulse = OrbFx.Mode.Shard;
        return root;
    }

    /// <summary>Tapered spear: broad diamond head + trailing shaft (shadow/void spears).</summary>
    private static Transform Spear(string name, Shader shader, Color color)
    {
        var root = new GameObject(name).transform;
        var shaft = Primitive(PrimitiveType.Cube, "Shaft", root);
        shaft.localScale = new Vector3(0.045f, 0.045f, 0.95f);
        var shaftColor = new Color(color.r * 0.5f, color.g * 0.5f, color.b * 0.5f, 1f);
        Materialize(shaft, shader, shaftColor);
        var head = Primitive(PrimitiveType.Cube, "Head", root);
        head.localPosition = new Vector3(0f, 0f, 0.48f);
        head.localScale = new Vector3(0.24f, 0.07f, 0.44f);
        head.localRotation = Quaternion.Euler(0f, 45f, 0f);
        Materialize(head, shader, color);
        root.gameObject.AddComponent<OrbFx>().Pulse = OrbFx.Mode.Plain;
        return root;
    }

    /// <summary>Flat slashing cross-blade that spins in its own plane (wind blades / scissor).</summary>
    private static Transform Blade(string name, Shader shader, Color color)
    {
        var root = new GameObject(name).transform;
        var a = Primitive(PrimitiveType.Cube, "BladeA", root);
        a.localScale = new Vector3(0.42f, 0.05f, 0.03f);
        Materialize(a, shader, color);
        var b = Primitive(PrimitiveType.Cube, "BladeB", root);
        b.localScale = new Vector3(0.05f, 0.42f, 0.03f);
        Materialize(b, shader, color);
        root.gameObject.AddComponent<OrbFx>().Pulse = OrbFx.Mode.Swirl;
        return root;
    }

    /// <summary>Water droplet (oblate sphere) with a short trailing splash of smaller drops.</summary>
    private static Transform Splash(string name, Shader shader, Color color)
    {
        var root = new GameObject(name).transform;
        var drop = Primitive(PrimitiveType.Sphere, "Drop", root);
        drop.localScale = new Vector3(0.26f, 0.2f, 0.26f);
        Materialize(drop, shader, color);
        for (int i = 0; i < 3; i++)
        {
            var trail = Primitive(PrimitiveType.Sphere, "Trail" + i, root);
            trail.localPosition = new Vector3(
                UnityEngine.Random.Range(-0.05f, 0.05f),
                UnityEngine.Random.Range(-0.04f, 0.04f),
                -0.28f - i * 0.15f);
            trail.localScale = Vector3.one * Mathf.Lerp(0.09f, 0.04f, i / 2f);
            Materialize(trail, shader, color);
        }
        root.gameObject.AddComponent<OrbFx>().Pulse = OrbFx.Mode.Plain;
        return root;
    }

    /// <summary>Streaking fire/energy comet: bright core + fading tail (hard to miss on screen).</summary>
    private static Transform Comet(string name, Shader shader, Color color)
    {
        var root = new GameObject(name).transform;
        var core = Primitive(PrimitiveType.Sphere, "Core", root);
        core.localScale = new Vector3(0.2f, 0.2f, 0.28f);
        Materialize(core, shader, color);
        var streak = Primitive(PrimitiveType.Cube, "Streak", root);
        streak.localPosition = new Vector3(0f, 0f, -0.35f);
        streak.localScale = new Vector3(0.07f, 0.07f, 0.6f);
        Materialize(streak, shader, color * 0.6f);
        root.gameObject.AddComponent<OrbFx>().Pulse = OrbFx.Mode.Ember;
        return root;
    }

    /// <summary>Cluster of small darts representing a volley (arcane missiles / darts).</summary>
    private static Transform Missile(string name, Shader shader, Color color)
    {
        var root = new GameObject(name).transform;
        for (int i = 0; i < 3; i++)
        {
            var m = Primitive(PrimitiveType.Sphere, "Missile" + i, root);
            m.localPosition = new Vector3(i * 0.16f - 0.16f, 0f, 0f);
            m.localScale = Vector3.one * 0.12f;
            Materialize(m, shader, color);
        }
        root.gameObject.AddComponent<OrbFx>().Pulse = OrbFx.Mode.Bolt;
        return root;
    }

    /// <summary>Small sleek bolt-line for quick shots (ranged darts / talisman trails).</summary>
    private static Transform Dart(string name, Shader shader, Color color)
    {
        var root = new GameObject(name).transform;
        var body = Primitive(PrimitiveType.Cube, "Body", root);
        body.localScale = new Vector3(0.06f, 0.06f, 0.6f);
        Materialize(body, shader, color);
        var tip = Primitive(PrimitiveType.Cube, "Tip", root);
        tip.localPosition = new Vector3(0f, 0f, 0.32f);
        tip.localScale = new Vector3(0.08f, 0.08f, 0.12f);
        Materialize(tip, shader, color);
        root.gameObject.AddComponent<OrbFx>().Pulse = OrbFx.Mode.Bolt;
        return root;
    }

    private static float EmissionRate(DamageType type)
    {
        switch (type)
        {
            case DamageType.Fire: return 90f;
            case DamageType.Ice: return 45f;
            case DamageType.Lightning: return 120f;
            case DamageType.Dark: return 30f;
            case DamageType.Wind: return 40f;
            default: return 60f;
        }
    }

    private static float StartLifetime(DamageType type)
    {
        switch (type)
        {
            case DamageType.Fire: return 0.45f;
            case DamageType.Ice: return 0.70f;
            case DamageType.Lightning: return 0.25f;
            case DamageType.Dark: return 0.65f;
            case DamageType.Wind: return 0.80f;
            default: return 0.50f;
        }
    }

    private static float StartSpeed(DamageType type)
    {
        switch (type)
        {
            case DamageType.Fire: return 4f;
            case DamageType.Ice: return 2f;
            case DamageType.Lightning: return 6f;
            case DamageType.Dark: return 1.5f;
            case DamageType.Wind: return 1.5f;
            default: return 3f;
        }
    }

    private static float StartSize(DamageType type)
    {
        switch (type)
        {
            case DamageType.Fire: return 0.09f;
            case DamageType.Ice: return 0.06f;
            case DamageType.Lightning: return 0.04f;
            case DamageType.Dark: return 0.14f;
            case DamageType.Wind: return 0.18f;
            default: return 0.08f;
        }
    }

    private static int MaxParticles(DamageType type)
    {
        switch (type)
        {
            case DamageType.Lightning: return 300;
            case DamageType.Fire: return 400;
            default: return 250;
        }
    }

    /// <summary>
    /// Comet-exhaust particle stream on a default projectile: a cone shaped exhaust trailing
    /// backward from the body so the bolt reads as an energetic magic projectile while flying.
    /// Emits from the body local origin; the cone is flipped -Z so particles stream behind it.
    /// </summary>
    private static void AttachProjectileParticles(Transform body, DamageType type, Color color)
    {
        var fxGo = new GameObject(body.name + "_Fx");
        fxGo.transform.SetParent(body, false);

        var ps = fxGo.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.loop = true;
        main.playOnAwake = true;
        main.startLifetime = StartLifetime(type);
        main.startSpeed = StartSpeed(type);
        main.startSize = StartSize(type);
        main.gravityModifier = 0f;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.maxParticles = MaxParticles(type);

        var emission = ps.emission;
        emission.rateOverTime = EmissionRate(type);

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 18f;
        shape.radius = 0.1f;
        shape.rotation = new Vector3(0f, 0f, 180f);

        var colorOverLifetime = ps.colorOverLifetime;
        colorOverLifetime.enabled = true;
        var grad = new Gradient();
        grad.SetKeys(
            new[] { new GradientColorKey(color, 0f), new GradientColorKey(color, 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
        colorOverLifetime.color = grad;

        var sizeOverLifetime = ps.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f,
            new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 0.4f)));

        Shader additive = Shader.Find("Particles/Additive") ?? Shader.Find("Sprites/Default");
        if (additive == null) return;
        var renderer = ps.GetComponent<ParticleSystemRenderer>();
        renderer.material = new Material(additive) { color = color };
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
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
            Wisp,   // slow pulsing
            Swirl   // gentle pulse + fast funnel spin
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
                case Mode.Swirl:
                    pulse = 1f + 0.10f * Mathf.Sin(t * 5.6f);
                    spin = 220f;
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

    private DamageResult ResolveZone(float power, SpellData spell, Vector3 pos, Vector3 fwd, float charge, float sizeScale, float range)
    {
        float radius = spell.Radius * sizeScale;

        // Aim at the ground the player is pointing at, skipping any already-raised terrain (a wall
        // this spell itself reared) so a repeat cast targets the intended ground, not the wall face.
        Vector3 center = TerrainDeformer.ResolveGroundTarget(pos, fwd, Mathf.Max(range, 0.1f));

        // Earth spells reshape the ground at the impact point before damage resolves (§3.8).
        // `fwd` orients directional shapes (e.g. the Wall ridge) along the cast axis.
        TerrainDeformer.Apply(center, radius, spell.TerrainShape, fwd);

        // Duration > 0 keeps the zone alive: it ticks the spell's damage while it lasts.
        if (spell.Duration > 0f)
        {
            SpawnZoneRing(center, spell, radius);
            var go = new GameObject("SpellZone");
            go.transform.position = center;
            var zone = go.AddComponent<SpellZone>();
            zone.Initialize(this, spell, power, sizeScale, 0.4f, 0f);
            zone.Lifetime = Mathf.Max(spell.Duration, 0.5f);
            return new DamageResult { HitTargets = true };
        }

        SpawnZoneRing(center, spell, radius);

        int count = Physics.OverlapSphereNonAlloc(center, radius, _overlapBuffer);
        bool hitAny = false;
        float total = 0f;
        for (int i = 0; i < count; i++)
        {
            var col = _overlapBuffer[i];
            if (col == null) continue;
            if (col.transform.root == transform.root)
            {
                // Holy bursts heal the caster too when standing inside the light.
                if (spell.Heals)
                {
                    int healed = ResolveHeal(spell, power, col.gameObject);
                    total += healed;
                    hitAny |= healed > 0;
                }
                continue;
            }
            if (spell.Heals && TryFindHealable(col.gameObject, out _))
            {
                int healed = ResolveHeal(spell, power, col.gameObject);
                total += healed;
                hitAny |= healed > 0;
                continue;
            }
            var hit = ApplyHit(spell, power, col.gameObject);
            total += hit.TotalDamage;
            hitAny |= hit.HitTargets;
        }
        return new DamageResult { TotalDamage = total, HitTargets = hitAny };
    }

    /// <summary>Ground ring flash sized to the spell's radius so zone spells read on screen.</summary>
    private static void SpawnZoneRing(Vector3 pos, SpellData spell, float radius)
    {
        if (spell == null || radius <= 0f) return;
        Vector3 ground = pos;
        if (Physics.Raycast(pos + Vector3.up * 0.1f, Vector3.down, out RaycastHit hit, 4f))
            ground = hit.point;
        SkillFx.RingFlash(ground, Vector3.up, DamageNumber.ColorFor(spell.Type), radius, 0.5f);
    }

    private DamageResult ApplyHit(SpellData spell, float power, GameObject target)
    {
        // Friendly-heal delivery: restore health on allies instead of damaging them.
        if (spell.Heals && TryFindHealable(target, out var healable))
        {
            int healed = Mathf.Max(1, Mathf.RoundToInt(power));
            healable.Heal(healed);
            DamageNumber.Spawn(target.transform.position, healed, spell.Type);
            return new DamageResult { TotalDamage = healed, HitTargets = true };
        }

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

        // Wet conduction (§3.7): a soaked foe takes bonus Ice/Lightning spell damage.
        // Melee/ranged attacks ignore the amp — water combos with frost/shock magic only.
        if ((spell.Type == DamageType.Ice || spell.Type == DamageType.Lightning)
            && WetStatus.IsWet(target))
        {
            ctx.WeaknessMultiplier = WetStatus.IceLightningDamageBonus;
        }

        var result = DamageCalculator.Calculate(ctx, false);

        if (target.TryGetComponent<IDamageable>(out var damageable))
            damageable.TakeDamage(Mathf.RoundToInt(result.TotalDamage));
        if (result.TotalDamage > 0f)
            DamageNumber.Spawn(target.transform.position, result.TotalDamage, spell.Type);

        ApplyStatus(spell, power, target);
        ApplyKnockback(spell, target);

        if (spell.ImpactEffectPrefab != null)
            Instantiate(spell.ImpactEffectPrefab, target.transform.position, Quaternion.identity);

        return new DamageResult
        {
            TotalDamage = result.TotalDamage,
            HitTargets = true
        };
    }

    /// <summary>
    /// Resolve a spell's damage against a specific target (used by SpellEffect / SpellZone on
    /// projectile/zone impact). Accessible so effects can route back through the shared
    /// DamageCalculator pipeline.
    /// </summary>
    public void ResolveHitAt(GameObject target, SpellData spell, float power)
    {
        if (target == null || spell == null) return;
        ApplyHit(spell, power, target);
    }

    /// <summary>Restore health to a friendly target via an <see cref="IHealable"/> (used by
    /// SpawnZone/SelfHeal). Returns the amount healed, or 0 when the target cannot heal.</summary>
    public int ResolveHeal(SpellData spell, float power, GameObject target)
    {
        if (spell == null || target == null) return 0;
        if (!TryFindHealable(target, out var healable)) return 0;
        int healed = Mathf.Max(1, Mathf.RoundToInt(power));
        healable.Heal(healed);
        DamageNumber.Spawn(target.transform.position, healed, spell.Type);
        return healed;
    }

    /// <summary>Friendly target check for healing: the object or its root implements <see cref="IHealable"/>.</summary>
    private static bool TryFindHealable(GameObject target, out IHealable healable)
    {
        healable = null;
        if (target == null) return false;
        if (target.TryGetComponent<IHealable>(out healable)) return true;
        return target.transform.root != null && target.transform.root.TryGetComponent<IHealable>(out healable);
    }

    /// <summary>Apply a spell's status effect on a damage hit. DoT statuses attach a SpellDoT to
    /// the target's root; Chill/Frost slow, Stagger stuns and Blind cloaks the victim in fog via
    /// the enemy controller / <see cref="BlindStatus"/>; Wet soaks the target (WetStatus) so
    /// Ice/Lightning follow-ups hit harder. When the skill declares no explicit status, the
    /// element's signature status (§3.7) is applied automatically.</summary>
    private static void ApplyStatus(SpellData spell, float power, GameObject target)
    {
        if (spell == null) return;

        // Explicit per-skill status wins; otherwise the element's signature status is applied to
        // every magic attack automatically. Wind/Earth/Holy/Physical have no signature status.
        StatusEffectType effect;
        if (spell.AppliesStatus)
        {
            effect = spell.StatusEffect;
        }
        else
        {
            var signature = ElementSignatureStatus.For(spell.Type);
            if (!signature.HasValue) return;
            effect = signature.Value;
        }

        if (spell.StatusProcChance > 0f && UnityEngine.Random.value > spell.StatusProcChance) return;

        Transform root = target != null ? target.transform.root : null;
        switch (effect)
        {
            case StatusEffectType.Bleed:
            case StatusEffectType.Poison:
            case StatusEffectType.Rot:
                SpellDoT.Apply(target, power * 0.12f, 4f, 0.5f, spell.Type);
                break;
            case StatusEffectType.Burn:
                // Fire-vs-water tug of war (§3.7): fire always melts any cold buildup, but a
                // soaked target can't be ignited — burn only takes hold if it isn't wet.
                ChillStatus.Melt(target);
                if (!WetStatus.IsWet(target))
                    SpellDoT.Apply(target, power * 0.12f, 4f, 0.5f, DamageType.Fire);
                break;
            case StatusEffectType.Chill:
                // Chill accumulates on the target's root; Wet conducts (+2 stacks) and reaching
                // the frost threshold converts it into a full Frost freeze (§3.7).
                ChillStatus.Apply(target);
                break;
            case StatusEffectType.Frost:
                if (root != null && root.TryGetComponent<EnemyController>(out var frostEnemy))
                    frostEnemy.ApplySlow(0.5f, 3.5f);
                break;
            case StatusEffectType.Stagger:
                if (root != null && root.TryGetComponent<EnemyController>(out var staggerEnemy))
                    staggerEnemy.ApplyStun(0.35f);
                break;
            case StatusEffectType.Wet:
                WetStatus.Apply(target, 4f);
                break;
            case StatusEffectType.Blind:
                BlindStatus.Apply(target, 4f);
                break;
        }
    }

    /// <summary>Outward shove on a damage hit — transform-driven (enemies hold no rigidbody).</summary>
    private void ApplyKnockback(SpellData spell, GameObject target)
    {
        if (spell == null || spell.Knockback <= 0f || target == null) return;
        Transform root = target.transform.root;
        Vector3 dir = root.position - transform.position;
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.0001f) return;
        root.position += dir.normalized * spell.Knockback;
    }
}

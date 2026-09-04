using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Central spell-casting runtime (§3.8). Validates focus points (FP) + cooldowns,
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
            go.AddComponent<SpellEffect>().Initialize(spell, power, fwd, this);
        }

        if (go.TryGetComponent<SpellEffect>(out var proj))
            proj.Launch(spell.ProjectileSpeed);

        return new DamageResult();
    }

    private DamageResult ResolveZone(float power, SpellData spell, Vector3 pos)
    {
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

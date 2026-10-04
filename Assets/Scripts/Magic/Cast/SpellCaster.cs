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
public partial class SpellCaster : MonoBehaviour
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

    /// <summary>
    /// Practical aim cap for GROUND deliveries (Zone / Vortex / Summon / Storm): the landing point
    /// follows the camera line-of-sight out to ~GroundAimMax instead of the spell's own Range, so
    /// AoE magic can be placed anywhere in the open world. Ranged/instant/beam deliveries keep their
    /// weapon/spell range cap — only ground placement is unbounded (to this large, safe distance).
    /// </summary>
    public const float GroundAimMax = 1200f;

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
}
using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The player's 11 core stats (game-design §3.4). Raw stat points (from starting package +
/// level-ups + items) are multiplied on-the-fly by the active race's percentage modifiers,
/// so a racial bonus compounds as the player levels that stat. All derived gameplay values
/// (MaxHP, MoveSpeed, MaxStamina, …) are computed from the TOTAL (modified) stat.
///
/// Implements <see cref="IStatProvider"/> so MeleeWeaponBehavior / RangedWeaponBehavior /
/// SpellCaster / WeaponSkillExecutor query stats through this single source.
/// </summary>
[DisallowMultipleComponent]
public class PlayerStats : MonoBehaviour, IStatProvider, ILootLuckProvider
{
    // k_* scaling knobs (§3.4) — balance numbers finalized during tuning.
    public const float K_Move = 0.5f;
    public const float K_Dodge = 0.02f;
    public const float K_As = 0.04f;
    public const float K_AsSpeed = 0.005f;
    public const float K_AsDex = 0.004f;
    public const float K_Str = 1f;
    public const float K_Stag = 0.5f;
    public const float K_Lt = 1f;
    public const float K_Racc = 0.04f;
    public const float BaseParrySeconds = 0.2f;
    public const float K_Parry = 0.01f;
    public const float K_Def = 0.01f;
    public const float K_Cool = 0.01f;
    public const float K_Mag = 1f;
    public const float K_Heal = 0.05f;
    public const float K_Buff = 0.03f;
    public const float K_Status = 2f;
    public const float K_Loot = 0.01f;

    /// <summary>Ceiling for the attack-speed scale that drives attack animation/action timing.</summary>
    public const float MaxAttackSpeedMult = 3f;

    [Header("Base Stat Points")]
    [Tooltip("Invested stat points (starting + leveled), one per StatType index.")]
    [SerializeField] private float[] _baseStats = new float[StatCount];

    [Tooltip("Active race whose % modifiers apply to total stats on-the-fly.")]
    public RaceData Race;

    // ── Temporary stat buffs (racial active abilities, potion effects, etc.) ───
    private struct TempBuff
    {
        public float Amount;
        public float ExpireTime;
    }
    private readonly Dictionary<StatType, List<TempBuff>> _tempBuffs = new Dictionary<StatType, List<TempBuff>>();

    /// <summary>Add a temporary stat buff that expires after <paramref name="duration"/> seconds.
    /// Multiple buffs on the same stat stack additively until they expire independently.</summary>
    public void AddTemporaryStatBuff(StatType stat, float amount, float duration)
    {
        if (amount <= 0f || duration <= 0f) return;
        if (!_tempBuffs.TryGetValue(stat, out var list))
        {
            list = new List<TempBuff>(2);
            _tempBuffs[stat] = list;
        }
        list.Add(new TempBuff { Amount = amount, ExpireTime = Time.time + duration });
    }

    private float GetTempBuffSum(StatType stat)
    {
        if (!_tempBuffs.TryGetValue(stat, out var list) || list.Count == 0) return 0f;
        float now = Time.time;
        float sum = 0f;
        for (int i = list.Count - 1; i >= 0; i--)
        {
            if (now >= list[i].ExpireTime) { list.RemoveAt(i); continue; }
            sum += list[i].Amount;
        }
        return sum;
    }

    [Header("Derived Tuning")]
    public float BaseMoveSpeed = 4f;
    public float BaseMeleeAtkPower = 10f;
    public float BaseLightAtkPower = 10f;
    public float BaseMagicAtkPower = 10f;
    public float BaseHealPower = 10f;

    public const int StatCount = 11;

    private void Awake()
    {
        if (_baseStats.Length != StatCount)
            Array.Resize(ref _baseStats, StatCount);
    }

    // ── Class modifiers (§3.2.1) ─────────────────────────────────────────────
    // Baked into the derived getters below so every consumer (weapons, spells, healing,
    // parry, block, cooldowns) picks up the active class's modifiers through the same
    // stat path. Safe fallback (1f/0f) when the class system is absent.

    private ClassPassiveManager _classMods;
    private RaceSkillPassiveManager _raceSkillMods;
    private PassivePerkManager _treePerks;
    private TalentTracker _talents;

    private ClassPassiveManager ActiveClassMods
    {
        get
        {
            if (_classMods == null) _classMods = GetComponent<ClassPassiveManager>();
            return _classMods;
        }
    }

    private RaceSkillPassiveManager ActiveRaceSkillMods
    {
        get
        {
            if (_raceSkillMods == null) _raceSkillMods = GetComponent<RaceSkillPassiveManager>();
            return _raceSkillMods;
        }
    }

    /// <summary>Learned skill-tree passive perks (§3.3), aggregated by <see cref="PassivePerkManager"/>.
    /// Re-queried until present (rigged later than Awake), then cached.</summary>
    private PassivePerkManager ActiveTreePerks
    {
        get
        {
            if (_treePerks == null) _treePerks = GetComponent<PassivePerkManager>();
            return _treePerks;
        }
    }

    private float TreeMul(PassivePerkType kind) => ActiveTreePerks?.Mul(kind) ?? 1f;
    private float TreeSum(PassivePerkType kind) => ActiveTreePerks?.Sum(kind) ?? 0f;

    /// <summary>Ranked Stat talents — a flat point bonus layered on every stat total.</summary>
    private TalentTracker ActiveTalents
    {
        get
        {
            if (_talents == null) _talents = GetComponent<TalentTracker>();
            return _talents;
        }
    }

    // ── Raw stat access ────────────────────────────────────────────────────

    /// <summary>The invested (unmodified) stat points for the given stat.</summary>
    public float GetBaseStat(StatType stat) => _baseStats[(int)stat];

    /// <summary>Base stat × (1 + race % + race skill bonuses) + talent stat points + temp buffs — the total stat used by all derived formulas.</summary>
    public float GetTotal(StatType stat)
    {
        float modifier = Race != null ? Race.GetStatModifier(stat) : 0f;
        var rsm = ActiveRaceSkillMods;
        if (rsm != null) modifier += rsm.GetStatBonus(stat);
        return GetBaseStat(stat) * (1f + modifier / 100f)
            + (ActiveTalents?.StatBonus(stat) ?? 0f)
            + GetTempBuffSum(stat);
    }

    /// <summary>Set base stat points directly (character creation / level-ups / items).</summary>
    public void SetBaseStat(StatType stat, float value)
    {
        _baseStats[(int)stat] = Mathf.Max(0f, value);
    }

    /// <summary>Add stat points (level-up grants).</summary>
    public void AddStatPoints(StatType stat, float amount)
    {
        _baseStats[(int)stat] = Mathf.Max(0f, _baseStats[(int)stat] + amount);
    }

    /// <summary>Recalculate any caches after a race change / level up (no-op; total is computed live).</summary>
    public void Refresh()
    {
        // Total stats are resolved on-the-fly, so nothing to cache.
    }

    // ── Derived formulas (§3.4) ────────────────────────────────────────────

    public float MaxHP => (100f + GetTotal(StatType.Health) * 12f) * TreeMul(PassivePerkType.MaxHealthPercent);

    public float MaxMoveSpeed => BaseMoveSpeed * (1f + GetTotal(StatType.Speed) * K_Move)
        * TreeMul(PassivePerkType.MovementSpeedPercent);

    public float DodgeSpeedMultiplier => 1f + GetTotal(StatType.Speed) * K_Dodge;

    /// <summary>Combined multiplicative attack-speed modifier from AttackSpeed, Speed, Dexterity, class, and race.</summary>
    public float AttackSpeedMultiplier =>
        (1f + GetTotal(StatType.AttackSpeed) * K_As)
        * (1f + GetTotal(StatType.Speed) * K_AsSpeed)
        * (1f + GetTotal(StatType.Dexterity) * K_AsDex)
        * (ActiveClassMods?.AttackSpeedMul ?? 1f)
        * (ActiveRaceSkillMods?.AttackSpeedMul ?? 1f)
        * TreeMul(PassivePerkType.AttackSpeedPercent);

    /// <summary>The attack-speed scale used for attack animation/action timing (capped). Driven
    /// by the AttackSpeed stat alone so a value of 1 plays at the authored tempo.</summary>
    public float AttackSpeedScale => Mathf.Clamp(
        (1f + GetTotal(StatType.AttackSpeed) * K_As)
        * (ActiveClassMods?.AttackSpeedMul ?? 1f)
        * (ActiveRaceSkillMods?.AttackSpeedMul ?? 1f)
        * TreeMul(PassivePerkType.AttackSpeedPercent),
        1f, MaxAttackSpeedMult);

    public float MaxStamina => (100f + GetTotal(StatType.Endurance) * 10f)
        * TreeMul(PassivePerkType.StaminaMaxPercent);

    /// <summary>Stamina-regen multiplier from tree perks + ranked talents (§3.3, §3.9) — consumed by PlayerController.</summary>
    public float StaminaRegenMul => TreeMul(PassivePerkType.StaminaRegenPercent)
        + (ActiveTalents?.StaminaRegenBonus ?? 0f) / 100f;

    /// <summary>Focus-regen multiplier from tree perks + ranked talents (§3.3, §3.9) — consumed by SpellCaster.</summary>
    public float FocusRegenMul => TreeMul(PassivePerkType.FocusRegenPercent)
        + (ActiveTalents?.FocusRegenBonus ?? 0f) / 100f;

    /// <summary>Flat HP-regen fraction of max HP per second from tree perks (§3.3) — consumed by PlayerController.</summary>
    public float HealthRegenPerSecondFlat => TreeSum(PassivePerkType.HealthRegenPerSecond);

    /// <summary>Flat damage-reduction fraction from tree perks (§3.3) — consumed by PlayerController.TakeDamage.</summary>
    public float DamageReductionPerkFlat => TreeSum(PassivePerkType.DamageReductionFlat);

    /// <summary>Cooldown-reduction multiplier from tree perks only (§3.3) — consumed by SpellCaster / SkillProfile.</summary>
    public float CooldownReductionMult => TreeMul(PassivePerkType.CooldownReductionPercent);

    /// <summary>Tree physical-attack-power multiplier (§3.3) — consumed by HitboxSystem alongside MeleeAtkPower.</summary>
    public float TreeAttackPowerMul => TreeMul(PassivePerkType.AttackPowerPercent);

    /// <summary>Tree + talent backstab multiplier (§3.3, §3.9) — compounds class backstab (HitboxSystem).</summary>
    public float TreeBackstabMul => TreeMul(PassivePerkType.BackstabPercent)
        + (ActiveTalents?.BackstabBonus ?? 0f) / 100f;

    /// <summary>Tree + talent stagger-resist multiplier (§3.3, §3.9) — compounds class stagger resistance (HitboxSystem).</summary>
    public float TreeStaggerResistMul => TreeMul(PassivePerkType.StaggerResistPercent)
        + (ActiveTalents?.StaggerResistBonus ?? 0f) / 100f;

    /// <summary>Tree + talent block-efficiency multiplier (§3.3, §3.9) — less stamina drained per blocked hit (CombatController).</summary>
    public float TreeBlockEfficiencyMul => TreeMul(PassivePerkType.BlockEfficiencyPercent)
        + (ActiveTalents?.BlockEfficiencyBonus ?? 0f) / 100f;

    /// <summary>Tree + talent crit-damage multiplier (§3.3, §3.9) — applied on top of a crit's base 2× (HitboxSystem).</summary>
    public float TreeCritDamageMul => TreeMul(PassivePerkType.CritDamagePercent)
        + (ActiveTalents?.CritDamageBonus ?? 0f) / 100f;

    public float EquipLoad => 40f + GetTotal(StatType.Endurance) * 2f
        + (ActiveClassMods?.EquipLoadBonus ?? 0f)
        + (ActiveRaceSkillMods?.EquipLoadBonus ?? 0f);

    public float MeleeAtkPower => (BaseMeleeAtkPower + GetTotal(StatType.Strength) * K_Str)
        * (ActiveClassMods?.EffectiveMeleeMul ?? 1f)
        * (ActiveRaceSkillMods?.MeleePowerMul ?? 1f)
        * TreeMul(PassivePerkType.AttackPowerPercent);

    public float StaggerPower => BaseMeleeAtkPower + GetTotal(StatType.Strength) * K_Stag;

    /// <summary>Backstab multiplier from race skill tree (applied in combat pipeline).</summary>
    public float RaceBackstabMul => ActiveRaceSkillMods?.BackstabMul ?? 1f;

    /// <summary>Blocking multiplier from race skill tree (applied in combat pipeline).</summary>
    public float RaceBlockingMul => ActiveRaceSkillMods?.BlockingMul ?? 1f;

    /// <summary>Stagger resist multiplier from race skill tree (applied in combat pipeline).</summary>
    public float RaceStaggerResistMul => ActiveRaceSkillMods?.StaggerResistMul ?? 1f;

    public float LightAtkPower => (BaseLightAtkPower + GetTotal(StatType.Dexterity) * K_Lt)
        * (ActiveClassMods?.MeleePowerMul ?? 1f)
        * (ActiveRaceSkillMods?.MeleePowerMul ?? 1f)
        * TreeMul(PassivePerkType.AttackPowerPercent);

    public float RangedAccuracy => (1f + GetTotal(StatType.Dexterity) * K_Racc)
        * (ActiveClassMods?.RangedHandlingMul ?? 1f);

    public float ParryWindow => (BaseParrySeconds + GetTotal(StatType.Dexterity) * K_Parry)
        * (ActiveClassMods?.ParryWindowMul ?? 1f)
        * (ActiveRaceSkillMods?.ParryWindowMul ?? 1f)
        * TreeMul(PassivePerkType.ParryWindowPercent);

    /// <summary>Flat physical damage reduction (Defense- and perk-derived), capped at 80%.</summary>
    public float DamageReduction => Mathf.Clamp(
        GetTotal(StatType.Defense) * K_Def
        * (ActiveClassMods?.DefenseMeleeMul ?? 1f)
        * (ActiveRaceSkillMods?.DefenseMeleeMul ?? 1f)
        + TreeSum(PassivePerkType.DamageReductionFlat), 0f, 0.8f);

    public float MaxFocusPoints => (50f + GetTotal(StatType.Intelligence) * 10f)
        * TreeMul(PassivePerkType.FocusMaxPercent);

    public float CooldownMultiplier => Mathf.Max(0.2f,
        (1f - GetTotal(StatType.Intelligence) * K_Cool)
        / (ActiveClassMods?.CooldownMul ?? 1f)
        / Mathf.Max(0.2f, ActiveRaceSkillMods?.CooldownMul ?? 1f)
        / TreeMul(PassivePerkType.CooldownReductionPercent));

    public float MagicAttackPower => (BaseMagicAtkPower + GetTotal(StatType.Wisdom) * K_Mag)
        * (ActiveClassMods?.SpellPowerMul ?? 1f)
        * (ActiveRaceSkillMods?.SpellPowerMul ?? 1f)
        * TreeMul(PassivePerkType.SpellDamagePercent);

    public float HealPowerMultiplier => (1f + GetTotal(StatType.Faith) * K_Heal)
        * (ActiveClassMods?.HealPowerMul ?? 1f)
        * (ActiveRaceSkillMods?.HealPowerMul ?? 1f)
        * TreeMul(PassivePerkType.HealPowerPercent);

    public float BuffDurationMultiplier => 1f + GetTotal(StatType.Faith) * K_Buff;

    /// <summary>Crit chance % = 5% base + Luck × 0.15% + tree crit perks + Critical Eye talent.</summary>
    public float CritChance => 5f + GetTotal(StatType.Luck) * 0.15f
        + TreeSum(PassivePerkType.CritChanceFlat)
        + (ActiveTalents?.CritChanceBonus ?? 0f);

    public float StatusProcLuck => GetTotal(StatType.Luck) * K_Status;

    /// <summary>Loot quality multiplier (game-design §7.1): Luck and tree loot luck raise drop payout chances.</summary>
    public float LootQuality => (1f + GetTotal(StatType.Luck) * K_Loot)
        * TreeMul(PassivePerkType.LootLuckPercent);

    // ── IStatProvider bridge ───────────────────────────────────────────────

    float IStatProvider.GetStat(WeaponScalingStat stat)
    {
        switch (stat)
        {
            case WeaponScalingStat.Strength: return GetTotal(StatType.Strength);
            case WeaponScalingStat.Dexterity: return GetTotal(StatType.Dexterity);
            case WeaponScalingStat.Intelligence: return GetTotal(StatType.Intelligence);
            case WeaponScalingStat.Wisdom: return GetTotal(StatType.Wisdom);
            default: return 0f;
        }
    }

    float IStatProvider.MagicAttackPower => MagicAttackPower;

    float IStatProvider.MaxFocusPoints => MaxFocusPoints;

    float IStatProvider.StatusProcLuck => StatusProcLuck;

    float ILootLuckProvider.GetLootQuality() => LootQuality;
}
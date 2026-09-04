using System;
using UnityEngine;

/// <summary>
/// The player's 11 core stats (game-design §3.4). Raw stat points (from starting package +
/// level-ups + items) are multiplied on-the-fly by the active race's percentage modifiers,
/// so a racial bonus compounds as the player levels that stat. All derived gameplay values
/// (MaxHP, MoveSpeed, MaxStamina, …) are computed from the TOTAL (modified) stat.
///
/// Implements <see cref="IStatProvider"/> so MeleeWeaponBehavior / RangedWeaponBehavior /
/// SpellCaster / WeaponArtExecutor query stats through this single source.
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

    [Header("Base Stat Points")]
    [Tooltip("Invested stat points (starting + leveled), one per StatType index.")]
    [SerializeField] private float[] _baseStats = new float[StatCount];

    [Tooltip("Active race whose % modifiers apply to total stats on-the-fly.")]
    public RaceData Race;

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

    // ── Raw stat access ────────────────────────────────────────────────────

    /// <summary>The invested (unmodified) stat points for the given stat.</summary>
    public float GetBaseStat(StatType stat) => _baseStats[(int)stat];

    /// <summary>Base stat × (1 + race %) — the total stat used by all derived formulas.</summary>
    public float GetTotal(StatType stat)
    {
        float modifier = Race != null ? Race.GetStatModifier(stat) : 0f;
        return GetBaseStat(stat) * (1f + modifier / 100f);
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

    public float MaxHP => 100f + GetTotal(StatType.Health) * 12f;

    public float MaxMoveSpeed => BaseMoveSpeed * (1f + GetTotal(StatType.Speed) * K_Move);

    public float DodgeSpeedMultiplier => 1f + GetTotal(StatType.Speed) * K_Dodge;

    /// <summary>Combined multiplicative attack-speed modifier from AttackSpeed, Speed, Dexterity.</summary>
    public float AttackSpeedMultiplier =>
        (1f + GetTotal(StatType.AttackSpeed) * K_As)
        * (1f + GetTotal(StatType.Speed) * K_AsSpeed)
        * (1f + GetTotal(StatType.Dexterity) * K_AsDex);

    public float MaxStamina => 100f + GetTotal(StatType.Endurance) * 10f;

    public float EquipLoad => 40f + GetTotal(StatType.Endurance) * 2f;

    public float MeleeAtkPower => BaseMeleeAtkPower + GetTotal(StatType.Strength) * K_Str;

    public float StaggerPower => BaseMeleeAtkPower + GetTotal(StatType.Strength) * K_Stag;

    public float LightAtkPower => BaseLightAtkPower + GetTotal(StatType.Dexterity) * K_Lt;

    public float RangedAccuracy => 1f + GetTotal(StatType.Dexterity) * K_Racc;

    public float ParryWindow => BaseParrySeconds + GetTotal(StatType.Dexterity) * K_Parry;

    /// <summary>Flat physical damage reduction, capped at 80%.</summary>
    public float DamageReduction => Mathf.Clamp(GetTotal(StatType.Defense) * K_Def, 0f, 0.8f);

    public float MaxFocusPoints => 50f + GetTotal(StatType.Intelligence) * 10f;

    public float CooldownMultiplier => Mathf.Max(0.2f, 1f - GetTotal(StatType.Intelligence) * K_Cool);

    public float MagicAttackPower => BaseMagicAtkPower + GetTotal(StatType.Wisdom) * K_Mag;

    public float HealPowerMultiplier => 1f + GetTotal(StatType.Faith) * K_Heal;

    public float BuffDurationMultiplier => 1f + GetTotal(StatType.Faith) * K_Buff;

    /// <summary>Crit chance % = 5% base + Luck × 0.15%.</summary>
    public float CritChance => 5f + GetTotal(StatType.Luck) * 0.15f;

    public float StatusProcLuck => GetTotal(StatType.Luck) * K_Status;

    /// <summary>Loot quality multiplier (game-design §7.1): Luck raises drop payout chances.</summary>
    public float LootQuality => 1f + GetTotal(StatType.Luck) * K_Loot;

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
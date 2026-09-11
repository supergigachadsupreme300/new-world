using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Aggregates the ACTIVE race's race-skill passive modifiers into a cached multiplier set.
/// Mirrors <see cref="ClassPassiveManager"/>: reads the active race from
/// <see cref="RaceChangeManager"/>, recomputes caches on race change, and exposes the
/// modifiers combat/utility code queries each frame. All race skills are auto-granted for
/// testing — the entire tree's modifiers apply simultaneously.
/// </summary>
[DisallowMultipleComponent]
public class RaceSkillPassiveManager : MonoBehaviour
{
    private PlayerStats _stats;
    private string _lastRaceId = string.Empty;
    private readonly Dictionary<RaceModType, float> _mods = new Dictionary<RaceModType, float>();

    private void Awake()
    {
        _stats = GetComponent<PlayerStats>();
    }

    private void Update()
    {
        string raceId = _stats != null && _stats.Race != null ? _stats.Race.raceId : string.Empty;
        if (raceId == _lastRaceId) return;
        _lastRaceId = raceId;
        Refresh();
    }

    /// <summary>Rebuild the cached modifier sum from the active race's tree.</summary>
    public void Refresh()
    {
        _mods.Clear();
        _lastRaceId = _stats != null && _stats.Race != null ? _stats.Race.raceId : string.Empty;

        if (string.IsNullOrEmpty(_lastRaceId)) return;

        foreach (var skill in RaceSkillCatalog.ForRace(_lastRaceId))
        {
            if (skill == null || skill.Mods == null) continue;
            foreach (var mod in skill.Mods)
            {
                if (mod.amount == 0f) continue;
                if (_mods.TryGetValue(mod.kind, out float cur)) _mods[mod.kind] = cur + mod.amount;
                else _mods[mod.kind] = mod.amount;
            }
        }
    }

    private float Sum(RaceModType kind) => _mods.TryGetValue(kind, out float v) ? v : 0f;
    private float Mul(RaceModType kind) => 1f + Sum(kind);

    // ── Stat bonuses (flat add to race's stat % modifier) ───────────────────
    public float GetStatBonus(StatType stat)
    {
        switch (stat)
        {
            case StatType.Health: return Sum(RaceModType.HealthBonus);
            case StatType.Speed: return Sum(RaceModType.SpeedBonus);
            case StatType.Endurance: return Sum(RaceModType.EnduranceBonus);
            case StatType.Strength: return Sum(RaceModType.StrengthBonus);
            case StatType.Dexterity: return Sum(RaceModType.DexterityBonus);
            case StatType.AttackSpeed: return Sum(RaceModType.AttackSpeedBonus);
            case StatType.Defense: return Sum(RaceModType.DefenseBonus);
            case StatType.Intelligence: return Sum(RaceModType.IntelligenceBonus);
            case StatType.Wisdom: return Sum(RaceModType.WisdomBonus);
            case StatType.Faith: return Sum(RaceModType.FaithBonus);
            case StatType.Luck: return Sum(RaceModType.LuckBonus);
            default: return 0f;
        }
    }

    // ── Modifier getters (all read-only; safe to call every frame) ──────────
    public float MoveSpeedMultiplier => Mul(RaceModType.MoveSpeedMul);
    public float DamageReductionBonus => Sum(RaceModType.DamageReductionBonus);
    public float HpRegenPerSecond => Sum(RaceModType.HpRegenPerSecond);
    public float LifestealBonus => Sum(RaceModType.LifestealBonus);
    public float StaminaRegenMul => Mul(RaceModType.StaminaRegenMul);

    public float MeleePowerMul => Mul(RaceModType.MeleePowerMul);
    public float SpellPowerMul => Mul(RaceModType.SpellPowerMul);
    public float BackstabMul => Mul(RaceModType.BackstabMul);
    public float HealPowerMul => Mul(RaceModType.HealPowerMul);
    public float CooldownMul => Mul(RaceModType.CooldownMul);
    public float ParryWindowMul => Mul(RaceModType.ParryWindowMul);
    public float BlockingMul => Mul(RaceModType.BlockingMul);
    public float StaggerResistMul => Mul(RaceModType.StaggerResistMul);
    public float AttackSpeedMul => Mul(RaceModType.AttackSpeedMul);
    public float DefenseMeleeMul => Mul(RaceModType.DefenseMeleeMul);

    public float XpBonusMul => Mul(RaceModType.XpBonusMul);
    public float EquipLoadBonus => Sum(RaceModType.EquipLoadBonus);
    public float ConsumablePotencyMul => Mul(RaceModType.ConsumablePotencyMul);
    public float AuraStrengthMul => 1f; // reserved for future use

    /// <summary>True if any mods are present for the active race.</summary>
    public bool HasAnyMods => _mods.Count > 0;
}

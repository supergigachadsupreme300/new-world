using UnityEngine;

/// <summary>
/// Calculates the final damage dealt by an attack after applying scaling,
/// defense, elemental modifiers, and critical bonuses.
///
/// Based on the Elden Ring-inspired formula from the design document:
///   Final = (Attack × Skill Multiplier × Weakness)
///          − (Defense × Defense Multiplier)
///          × Elemental Modifier
///          × Critical Modifier
/// </summary>
public static class DamageCalculator
{
    /// <summary>Full context describing an incoming hit.</summary>
    public struct HitContext
    {
        public float AttackPower;
        public float SkillMultiplier;
        public float Defense;

        public float DefenseMultiplier;

        // Elemental damage: the element's attack power, the target's resistance,
        // and a multiplier (0 = immune, 1 = neutral, >1 = weak).
        public float ElementalPower;
        public float ElementalResistance;
        public float WeaknessMultiplier;

        // Critical (backstab/riposte) bonus multiplier.
        public float CriticalMultiplier;
    }

    /// <summary>Resulting stats after a hit is resolved.</summary>
    public struct HitResult
    {
        public float PhysicalDamage;
        public float ElementalDamage;
        public float TotalDamage;
        public bool IsCritical;
        public bool IsBlocked;
    }

    // ── Constants ───────────────────────────────────────────────────────────
    private const float MinDamage = 0f;
    private const float MinMultiplier = 0.01f;
    private const float DefFloor = 0f;

    /// <summary>
    /// Resolve a full hit with blocking taken into account.
    /// </summary>
    public static HitResult Calculate(HitContext ctx, bool blocked)
    {
        // ── Physical component ─────────────────────────────────────────────
        float rawPhysical = ctx.AttackPower * Mathf.Max(ctx.SkillMultiplier, MinMultiplier);
        float reducedByDefense = ctx.Defense * Mathf.Max(ctx.DefenseMultiplier, MinMultiplier);
        float physical = Mathf.Max(rawPhysical - reducedByDefense, MinDamage);

        // ── Elemental component ────────────────────────────────────────────
        float elemental = Mathf.Max(ctx.ElementalPower - ctx.ElementalResistance, MinDamage);
        elemental *= Mathf.Max(ctx.WeaknessMultiplier, MinMultiplier);

        // ── Critical bonus (applies to total) ──────────────────────────────
        float totalBeforeCritical = physical + elemental;
        float total = totalBeforeCritical * Mathf.Max(ctx.CriticalMultiplier, MinMultiplier);

        // ── Block reduction ────────────────────────────────────────────────
        bool isBlocked = false;
        if (blocked)
        {
            // Block absorbs ~70% of damage by default (defense multiplier scales).
            float blockReduction = 0.70f;
            total *= (1f - blockReduction);
            isBlocked = true;
        }

        return new HitResult
        {
            PhysicalDamage = Mathf.Round(Mathf.Max(physical, 0f)),
            ElementalDamage = Mathf.Round(Mathf.Max(elemental, 0f)),
            TotalDamage    = Mathf.Round(Mathf.Max(total, 0f)),
            IsCritical     = ctx.CriticalMultiplier > 1.5f,
            IsBlocked      = isBlocked
        };
    }

    /// <summary>
    /// Simplified overload for most melee attacks without elemental data.
    /// </summary>
    public static HitResult CalculateMelee(float attack, float skillMult, float defense, bool blocked, bool critical = false)
    {
        return Calculate(new HitContext
        {
            AttackPower        = attack,
            SkillMultiplier    = skillMult,
            Defense            = defense,
            DefenseMultiplier  = 1f,
            ElementalPower     = 0f,
            ElementalResistance= 0f,
            WeaknessMultiplier = 1f,
            CriticalMultiplier = critical ? 2.5f : 1f,
        }, blocked);
    }
}
using UnityEngine;

/// <summary>
/// Calculates the final damage dealt by an attack after applying scaling,
/// defense, per-type resistance, and critical bonuses.
///
/// Based on the Elden Ring-inspired formula from game-design §3.1:
///   Final = (Attack × Skill Multiplier × Weakness)
///          − (Defense × Defense Multiplier)
///          × Damage-Type Modifier      # attacker's DamageType vs equipment resistance (§3.7)
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

        // Damage type (§3.7): the attacker's single type, resolved against the
        // target's equipment-backed resistance (equipment-only rule, §3.4).
        public DamageType Type;
        public IDamageResistance Resistance;

        // Extra source of resistance/weak (e.g. splash or per-type modifiers).
        public float WeaknessMultiplier;

        // Critical (backstab/riposte) bonus multiplier.
        public float CriticalMultiplier;
    }

    /// <summary>Resulting stats after a hit is resolved.</summary>
    public struct HitResult
    {
        public float PhysicalDamage;
        public float TypeDamage;
        public float TotalDamage;
        public bool IsCritical;
        public bool IsBlocked;
    }

    // ── Constants ───────────────────────────────────────────────────────────
    private const float MinDamage = 0f;
    private const float MinMultiplier = 0.01f;
    private const float PhysicalNeutral = 1f;

    /// <summary>
    /// Resolve a full hit with blocking taken into account.
    /// </summary>
    public static HitResult Calculate(HitContext ctx, bool blocked)
    {
        // ── Base component (reduces through defense / armor) ──────────────
        float rawBase = ctx.AttackPower * Mathf.Max(ctx.SkillMultiplier, MinMultiplier);
        float reducedByDefense = ctx.Defense * Mathf.Max(ctx.DefenseMultiplier, MinMultiplier);
        float physical = Mathf.Max(rawBase - reducedByDefense, MinDamage);

        // ── Type component (§3.7): attacker type vs equipment resistance ──
        float resistanceMult = ctx.GetResistanceMultiplier();
        float typeComponent = physical * resistanceMult;

        // ── Extra weakness modifier ────────────────────────────────────────
        typeComponent *= Mathf.Max(ctx.WeaknessMultiplier, MinMultiplier);

        // ── Critical bonus (applies to total) ──────────────────────────────
        float total = typeComponent * Mathf.Max(ctx.CriticalMultiplier, MinMultiplier);

        // ── Block reduction ────────────────────────────────────────────────
        bool isBlocked = false;
        if (blocked)
        {
            float blockReduction = 0.70f;
            total *= (1f - blockReduction);
            isBlocked = true;
        }

        return new HitResult
        {
            PhysicalDamage = Mathf.Round(Mathf.Max(physical, 0f)),
            TypeDamage     = Mathf.Round(Mathf.Max(typeComponent, 0f)),
            TotalDamage    = Mathf.Round(Mathf.Max(total, 0f)),
            IsCritical     = ctx.CriticalMultiplier > 1.5f,
            IsBlocked      = isBlocked
        };
    }

    /// <summary>
    /// Simplified overload for most melee attacks without a typed damage source.
    /// Uses a neutral resistance so the result equals the pure physical formula.
    /// </summary>
    public static HitResult CalculateMelee(float attack, float skillMult, float defense, bool blocked, bool critical = false)
    {
        return Calculate(new HitContext
        {
            AttackPower        = attack,
            SkillMultiplier    = skillMult,
            Defense            = defense,
            DefenseMultiplier  = 1f,
            Type               = DamageType.Physical,
            Resistance         = NeutralResistance.Instance,
            WeaknessMultiplier = 1f,
            CriticalMultiplier = critical ? 2.5f : 1f,
        }, blocked);
    }

    private static float GetResistanceMultiplier(this HitContext ctx)
    {
        if (ctx.Resistance == null)
            return PhysicalNeutral;
        return Mathf.Max(ctx.Resistance.GetMultiplier(ctx.Type), 0f);
    }
}

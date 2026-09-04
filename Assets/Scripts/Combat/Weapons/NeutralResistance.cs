using UnityEngine;

/// <summary>
/// Placeholder resistance source that treats every damage type as neutral (1.0).
///
/// Real resistance is equipment-only (§3.4) and is wired to the Inventory/equipment
/// system in a later phase. Until then this keeps DamageCalculator functional.
/// </summary>
public sealed class NeutralResistance : IDamageResistance
{
    /// <summary>Shared neutral instance (all multipliers = 1.0).</summary>
    public static readonly NeutralResistance Instance = new NeutralResistance();

    public float GetMultiplier(DamageType type) => 1f;
}

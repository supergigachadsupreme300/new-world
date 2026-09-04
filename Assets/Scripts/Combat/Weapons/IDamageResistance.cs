using UnityEngine;

/// <summary>
/// Supplies per-type damage resistance. Resistance is provided by armor/gear only (§3.4);
/// no stat grants resistance.
///
/// Returns the multiplier applied to incoming damage of the given type:
///   0.0 = immune, 1.0 = neutral, &lt;1.0 = resistant, &gt;1.0 = weak.
/// </summary>
public interface IDamageResistance
{
    /// <summary>Multiplier applied to damage of the given type.</summary>
    float GetMultiplier(DamageType type);
}

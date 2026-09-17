using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Aggregates the learned skill-tree passive perks (§3.3) into a cached modifier set. Mirrors
/// <see cref="ClassPassiveManager"/>: <see cref="PassivePerkEffect"/> feeds perks in on learn/restore,
/// and <see cref="PlayerStats"/> plus the combat pipeline read them each frame. Perks only accumulate
/// (skills are never unlearned), so no refresh/teardown pass is needed.
/// </summary>
[DisallowMultipleComponent]
public class PassivePerkManager : MonoBehaviour
{
    private readonly Dictionary<PassivePerkType, float> _perks = new Dictionary<PassivePerkType, float>();

    /// <summary>Add (or stack) a perk amount. Percent kinds pass their percent value; flats raw points.</summary>
    public void AddPerk(PassivePerkType kind, float amount)
    {
        if (amount == 0f) return;
        if (!_perks.TryGetValue(kind, out float cur)) _perks[kind] = amount;
        else _perks[kind] = cur + amount;
    }

    /// <summary>Accumulated flat sum for <paramref name="kind"/> (flats, and checked by Sum-based getters).</summary>
    public float Sum(PassivePerkType kind) => _perks.TryGetValue(kind, out float v) ? v : 0f;

    /// <summary>1 + accumulated percent for <paramref name="kind"/> — the multiplier consumers apply.</summary>
    public float Mul(PassivePerkType kind) => 1f + Sum(kind);

    /// <summary>How many distinct perk kinds the player has invested in.</summary>
    public int Count => _perks.Count;
}
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Player talent ownership (account/creation perks that boost XP gain, stats, or combat/regen).
/// A new player is granted ONE random talent at game creation; any talent can then be handed ranks
/// freely (no currency) up to its max rank. XP bonuses are read live (additive), stat bonuses are flat
/// additions resolved through <see cref="PlayerStats.GetTotal"/>, and the combat/regen percent bonuses
/// fold additively into the same PlayerStats getters as the skill-tree perks, so restore never
/// double-applies anything.
/// </summary>
[DisallowMultipleComponent]
public class TalentTracker : MonoBehaviour
{
    [System.Serializable]
    public sealed class TalentRank
    {
        public string TalentId;
        public int Ranks;
    }

    /// <summary>Ranked talents (id → rank count).</summary>
    public List<TalentRank> Owned = new List<TalentRank>();

    /// <summary>Whether the random first talent was already granted for this run.</summary>
    public bool FirstGranted;

    /// <summary>
    /// Ensure a talent tracker (and its level-up dependency) exists on a object, then return it.
    /// Used by boot paths that grant the account-creation talent before the character panel has
    /// ever ensured the progression systems.
    /// </summary>
    public static TalentTracker EnsureOn(GameObject root)
    {
        var existing = root.GetComponent<TalentTracker>();
        if (existing != null) return existing;
        if (root.GetComponent<LevelUpSystem>() == null)
            root.AddComponent<LevelUpSystem>();
        return root.AddComponent<TalentTracker>();
    }

    /// <summary>Grant the account-creation talent: one randomized talent at rank 1 (once only).</summary>
    public void GrantRandomFirstTalent()
    {
        if (FirstGranted) return;
        TalentCatalog.EnsureBuilt();
        var pool = TalentCatalog.All;
        if (pool == null || pool.Count == 0) return;
        FirstGranted = true;
        string id = pool[Random.Range(0, pool.Count)].Id;
        GainRank(id, 1);
    }

    /// <summary>Current rank of a talent (0 when never taken).</summary>
    public int RankOf(string id)
    {
        foreach (var o in Owned)
            if (o.TalentId == id) return o.Ranks;
        return 0;
    }

    /// <summary>Whether the given talent can take another rank right now (free, capped at max).</summary>
    public bool CanRank(string id)
    {
        var talent = TalentCatalog.Find(id);
        return talent != null && RankOf(id) < talent.MaxRanks;
    }

    /// <summary>Hand the given talent its next rank (free, capped at max). Returns true when ranked.</summary>
    public bool RankUp(string id)
    {
        if (!CanRank(id)) return false;
        GainRank(id, 1);
        return true;
    }

    private void GainRank(string id, int amount)
    {
        foreach (var o in Owned)
        {
            if (o.TalentId != id) continue;
            o.Ranks += amount;
            return;
        }
        Owned.Add(new TalentRank { TalentId = id, Ranks = amount });
    }

    // ── XP / stat bonus reads (summed additive over ranked talents) ───────

    /// <summary>Combined +% character XP from ranked PlayerXp talents.</summary>
    public float PlayerXpBonus => SumKind(TalentKind.PlayerXp);

    /// <summary>Combined +% XP for the given skill type (per-skill + category bar).</summary>
    public float TypeXpBonus(SkillType type)
    {
        float sum = 0f;
        foreach (var o in Owned)
        {
            var t = TalentCatalog.Find(o.TalentId);
            if (t != null && t.Kind == TalentKind.SkillTypeXp && t.Scope == type)
                sum += t.PerRank * o.Ranks;
        }
        return sum;
    }

    /// <summary>Combined +flat stat points for the given stat from ranked Stat talents.</summary>
    public float StatBonus(StatType stat)
    {
        float sum = 0f;
        foreach (var o in Owned)
        {
            var t = TalentCatalog.Find(o.TalentId);
            if (t != null && t.Kind == TalentKind.Stat && t.Stat == stat) sum += t.PerRank * o.Ranks;
        }
        return sum;
    }

    /// <summary>Combined +% critical-hit chance (flat percent, e.g. 6 = +6%) from ranked CritChance talents.</summary>
    public float CritChanceBonus => SumKind(TalentKind.CritChance);

    /// <summary>Combined +% crit damage (percent units) from ranked CritDamage talents.</summary>
    public float CritDamageBonus => SumKind(TalentKind.CritDamage);

    /// <summary>Combined +% backstab damage from ranked Backstab talents.</summary>
    public float BackstabBonus => SumKind(TalentKind.Backstab);

    /// <summary>Combined +% block stamina efficiency (less stamina drained per blocked hit) from ranked talents.</summary>
    public float BlockEfficiencyBonus => SumKind(TalentKind.BlockEfficiency);

    /// <summary>Combined +% stagger/knockback resistance from ranked StaggerResist talents.</summary>
    public float StaggerResistBonus => SumKind(TalentKind.StaggerResist);

    /// <summary>Combined +% stamina regeneration from ranked StaminaRegen talents.</summary>
    public float StaminaRegenBonus => SumKind(TalentKind.StaminaRegen);

    /// <summary>Combined +% focus (FP) regeneration from ranked FocusRegen talents.</summary>
    public float FocusRegenBonus => SumKind(TalentKind.FocusRegen);

    /// <summary>Sum of PerRank × Ranks across every owned talent of the given kind.</summary>
    private float SumKind(TalentKind kind)
    {
        float sum = 0f;
        foreach (var o in Owned)
        {
            var t = TalentCatalog.Find(o.TalentId);
            if (t != null && t.Kind == kind) sum += t.PerRank * o.Ranks;
        }
        return sum;
    }

    [System.Serializable]
    private sealed class TalentSave
    {
        public List<TalentRank> Owned;
        public bool FirstGranted;
    }

    /// <summary>Serialize ownership to JSON for the save file.</summary>
    public string Serialize()
    {
        return JsonUtility.ToJson(new TalentSave
        {
            Owned = Owned,
            FirstGranted = FirstGranted
        });
    }

    /// <summary>Restore ownership from a saved JSON blob (empty → clean slate).</summary>
    public void Restore(string json)
    {
        Owned = new List<TalentRank>();
        FirstGranted = false;
        if (string.IsNullOrEmpty(json)) return;
        try
        {
            var save = JsonUtility.FromJson<TalentSave>(json);
            if (save == null) return;
            FirstGranted = save.FirstGranted;
            if (save.Owned != null)
                foreach (var o in save.Owned)
                    if (o != null && !string.IsNullOrEmpty(o.TalentId) && TalentCatalog.Find(o.TalentId) != null)
                        Owned.Add(o);
        }
        catch
        {
            // Malformed save → keep a clean slate rather than crashing the load.
        }
    }
}
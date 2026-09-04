using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Persistent race unlock state (game-design §3.5, planning Task 4.3). Human is always
/// unlocked; others unlock via world Race Discovery Points (and are manually pickable at
/// character creation). Running as a MonoSingleton, the unlock set persists across scenes
/// and characters via serialized state.
/// </summary>
public class RaceUnlockManager : MonoSingleton<RaceUnlockManager>
{
    [Header("Persistent Unlocks")]
    [Tooltip("Race ids the player has discovered/unlocked.")]
    public List<string> UnlockedRaceIds = new List<string>();

    private readonly HashSet<string> _unlocked = new HashSet<string>();

    protected override void Awake()
    {
        base.Awake();
        RebuildSet();
        // Human is always available.
        if (!_unlocked.Contains("human"))
            UnlockRace("human");
    }

    private void RebuildSet()
    {
        _unlocked.Clear();
        foreach (var id in UnlockedRaceIds)
            if (!string.IsNullOrEmpty(id)) _unlocked.Add(id);
    }

    public bool IsUnlocked(RaceData race)
    {
        if (race == null) return false;
        if (string.Equals(race.raceId, "human", System.StringComparison.OrdinalIgnoreCase)) return true;
        return _unlocked.Contains(race.raceId);
    }

    /// <summary>Unlock a race (discovery or character creation pick). Persisted to UnlockedRaceIds.</summary>
    public void UnlockRace(string raceId)
    {
        if (string.IsNullOrEmpty(raceId)) return;
        if (_unlocked.Add(raceId))
        {
            UnlockedRaceIds.Add(raceId);
        }
    }

    /// <summary>All currently unlocked race ids.</summary>
    public IEnumerable<string> UnlockedIds => _unlocked;
}
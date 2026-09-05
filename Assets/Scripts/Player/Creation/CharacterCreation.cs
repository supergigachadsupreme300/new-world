using UnityEngine;

/// <summary>
/// Character creation (game-design §3.5 "Race Selection & Weighted Random", planning Task 4.4).
/// Performs a weighted random roll (Human ≈ 50%, each other race ≈ 2.38%) that auto-commits,
/// or accepts an explicit manual race pick. On commit it applies the race to PlayerStats,
/// spawns the RaceRig, registers the unlock, and seeds starting stat points.
/// </summary>
public class CharacterCreation : MonoBehaviour
{
    [Header("Sources")]
    [Tooltip("Race catalog. Falls back to RaceDatabase default roster if unassigned.")]
    public RaceDatabase Database;

    [Header("Starting Stats")]
    [Tooltip("Base stat points granted before the player allocates level-ups.")]
    public float[] StartingStats = { 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10 };

    private PlayerStats _player;

    private void Awake()
    {
        _player = GetComponent<PlayerStats>();
    }

    /// <summary>Roll a random race (weighted) and commit it. Returns the chosen race.</summary>
    public RaceData RollRandomRace()
    {
        var db = ResolveDatabase();
        if (db == null) return null;
        var race = db.Roll();
        CommitRace(race);
        return race;
    }

    /// <summary>Explicitly commit the given race (manual pick).</summary>
    public void CommitRace(RaceData race)
    {
        if (race == null || _player == null) return;

        // Unlock (persistent) and apply.
        if (RaceUnlockManager.Instance != null)
            RaceUnlockManager.Instance.UnlockRace(race.raceId);

        ApplyStartingStats();

        var mgr = _player.GetComponent<RaceChangeManager>();
        if (mgr == null) mgr = _player.gameObject.AddComponent<RaceChangeManager>();
        mgr.SetActiveRace(race, requireStone: false, unlockIfNeeded: true);
    }

    private void ApplyStartingStats()
    {
        PlayerStats ps = _player;
        for (int i = 0; i < (int)StatType.Luck + 1 && i < StartingStats.Length; i++)
            ps.SetBaseStat((StatType)i, StartingStats[i]);
    }

    private RaceDatabase ResolveDatabase()
    {
        if (Database != null) return Database;
        // Fall back to a runtime default roster so the game runs asset-free.
        var fallback = ScriptableObject.CreateInstance<RaceDatabase>();
        fallback.Races = RaceDatabase.BuildDefaultRoster();
        return fallback;
    }
}
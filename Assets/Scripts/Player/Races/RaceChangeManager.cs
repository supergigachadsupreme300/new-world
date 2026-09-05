using UnityEngine;

/// <summary>
/// Player-side active-race manager (game-design §3.5, planning Task 4.3). Mirrors the
/// <see cref="ClassUnlocker"/> pattern: reads the persistent unlock set from
/// <see cref="RaceUnlockManager"/>, tracks which race the player currently identifies with
/// (<see cref="ActiveRaceId"/>), and applies a race by refreshing <see cref="PlayerStats.Race"/>
/// modifiers, the <see cref="RaceRig"/>, and the <see cref="RacePassiveManager"/> kit.
///
/// Non-Human changes consume a Ritual Stone from the <see cref="ToolManager"/> inventory
/// (§3.5 transform cost); Human is always free. Fires <see cref="OnActiveRaceChanged"/> so UI
/// and combat/utility code can react. Attach to the player root.
/// </summary>
[DisallowMultipleComponent]
public class RaceChangeManager : MonoBehaviour
{
    /// <summary>Id of the race the player currently identifies with.</summary>
    [Tooltip("Current race id; the player's Race is re-applied from this on Start.")]
    public string ActiveRaceId = "human";

    /// <summary>The item consumed when the player changes to a non-Human race.</summary>
    public const string RitualStoneItemId = "ritual_stone";

    private PlayerStats _stats;
    private RaceRig _rig;
    private RacePassiveManager _passive;

    /// <summary>Fires when the active race changes (after applying reflex).</summary>
    public event System.Action<RaceData> OnActiveRaceChanged;

    private void Awake()
    {
        _stats = GetComponent<PlayerStats>();
        _rig = GetComponent<RaceRig>();
        _passive = GetComponent<RacePassiveManager>();
    }

    private void Start()
    {
        if (string.IsNullOrEmpty(ActiveRaceId))
            ActiveRaceId = "human";
        ApplyActiveRace();
    }

    /// <summary>
    /// The currently-read <see cref="RaceData"/> for <see cref="ActiveRaceId"/> from the default
    /// roster, or null if unknown.
    /// </summary>
    public RaceData ActiveRace
    {
        get
        {
            if (string.IsNullOrEmpty(ActiveRaceId))
                return null;
            var roster = RaceDatabase.BuildDefaultRoster();
            if (roster == null) return null;
            for (int i = 0; i < roster.Count; i++)
            {
                var r = roster[i];
                if (r != null && string.Equals(r.raceId, ActiveRaceId, System.StringComparison.OrdinalIgnoreCase))
                    return r;
            }
            return null;
        }
    }

    /// <summary>The race the player's stats currently resolve against (<see cref="PlayerStats.Race"/>).</summary>
    public RaceData CurrentRace => _stats != null ? _stats.Race : null;

    /// <summary>How many Ritual Stones the player holds (0 if no ToolManager).</summary>
    public int RitualStoneCount
    {
        get
        {
            var tm = ToolManager.Instance;
            return tm != null ? tm.CountItem(RitualStoneItemId) : 0;
        }
    }

    /// <summary>True if the player holds at least one Ritual Stone (or has no inventory at all).</summary>
    public bool HasRitualStone
    {
        get
        {
            var tm = ToolManager.Instance;
            return tm == null || tm.CountItem(RitualStoneItemId) > 0;
        }
    }

    /// <summary>Change the active race. Only unlocked races can be selected (Human is always free).</summary>
    /// <param name="race">Target race.</param>
    /// <param name="requireStone">Consume a Ritual Stone for non-Human changes (default true).
    /// Character creation and free starting races pass false.</param>
    /// <param name="unlockIfNeeded">Auto-unlock the race (discovery/creation path) when locked.</param>
    /// <returns>False (and leaves the current race) when the race is unknown/locked or the stone is missing.</returns>
    public bool SetActiveRace(RaceData race, bool requireStone = true, bool unlockIfNeeded = false)
    {
        if (race == null) return false;

        // No-op change: same race is free and consumes nothing.
        if (string.Equals(ActiveRaceId, race.raceId, System.StringComparison.OrdinalIgnoreCase))
            return true;

        bool isHuman = string.Equals(race.raceId, "human", System.StringComparison.OrdinalIgnoreCase);
        if (!isHuman && unlockIfNeeded)
        {
            var rm = RaceUnlockManager.Instance;
            if (rm != null) rm.UnlockRace(race.raceId);
        }
        if (!IsSelectable(race)) return false;

        if (!isHuman && requireStone)
        {
            var tm = ToolManager.Instance;
            if (tm == null || !tm.RemoveItemAmount(RitualStoneItemId, 1))
                return false;
        }

        ActiveRaceId = race.raceId;
        ApplyRace(race);
        OnActiveRaceChanged?.Invoke(race);
        return true;
    }

    /// <summary>Re-apply the currently-active race's modifiers/rig/passive (spawn, scene load).</summary>
    public void ApplyActiveRace()
    {
        var race = ActiveRace;
        if (race == null) return;
        ApplyRace(race);
    }

    /// <summary>True when the race is Human or currently unlocked in the persistent unlock set.</summary>
    private bool IsSelectable(RaceData race)
    {
        if (race == null) return false;
        if (string.Equals(race.raceId, "human", System.StringComparison.OrdinalIgnoreCase))
            return true;
        var rm = RaceUnlockManager.Instance;
        return rm != null && rm.IsUnlocked(race);
    }

    private void ApplyRace(RaceData race)
    {
        if (_stats != null)
        {
            _stats.Race = race;
            _stats.Refresh();
        }

        if (_rig == null) _rig = GetComponent<RaceRig>();
        if (_rig == null) _rig = gameObject.AddComponent<RaceRig>();
        _rig.ApplyRace(race);

        if (_passive == null) _passive = GetComponent<RacePassiveManager>();
        if (_passive == null) _passive = gameObject.AddComponent<RacePassiveManager>();
        else _passive.enabled = true; // triggers passive refresh next Update
    }
}
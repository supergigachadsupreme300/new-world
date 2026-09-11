using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Tracks which race skills are "learned" for the active race. For testing, all skills are
/// auto-granted on race selection (no point economy). The UI reads from this to know which
/// nodes to highlight as learned/available.
/// </summary>
[DisallowMultipleComponent]
public class RaceSkillState : MonoBehaviour
{
    private PlayerStats _stats;
    private string _lastRaceId = string.Empty;
    private readonly HashSet<string> _learned = new HashSet<string>();
    private readonly List<string> _learnedList = new List<string>();

    private void Awake()
    {
        _stats = GetComponent<PlayerStats>();
    }

    private void Update()
    {
        string raceId = _stats != null && _stats.Race != null ? _stats.Race.raceId : string.Empty;
        if (raceId == _lastRaceId) return;
        SetRace(raceId);
    }

    /// <summary>Grant all skills for the given race. Called automatically on race change.</summary>
    public void SetRace(string raceId)
    {
        _lastRaceId = raceId;
        _learned.Clear();
        _learnedList.Clear();

        if (string.IsNullOrEmpty(raceId)) return;

        RaceSkillCatalog.EnsureBuilt();
        foreach (var skill in RaceSkillCatalog.ForRace(raceId))
        {
            if (skill == null || string.IsNullOrEmpty(skill.id)) continue;
            _learned.Add(skill.id);
            _learnedList.Add(skill.id);
        }
    }

    /// <summary>True if the given race skill is learned (auto-granted).</summary>
    public bool IsLearned(string skillId) => _learned.Contains(skillId);

    /// <summary>All learned skill ids for the current race.</summary>
    public IReadOnlyList<string> LearnedIds => _learnedList;

    /// <summary>Number of learned skills.</summary>
    public int LearnedCount => _learned.Count;

    /// <summary>Id of the current race.</summary>
    public string ActiveRaceId => _lastRaceId;
}

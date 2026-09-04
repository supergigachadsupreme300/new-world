using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Phase 8 (Task 8.2): Race / Stat Sheet UI. A modal panel that reads the player's active
/// <see cref="RaceData"/> and the 11 <see cref="PlayerStats"/> totals, lets the player allocate
/// a point budget as a live preview, and commits with <see cref="PlayerStats.SetBaseStat"/>.
/// Composes existing <see cref="PlayerStats"/> / <see cref="RaceData"/> without rewriting the
/// live HUD.
/// </summary>
public sealed class RaceStatSheetUI : MenuPanelBase
{
    public int Budget = 5;

    private readonly Dictionary<int, int> _alloc = new Dictionary<int, int>();
    private TMP_Text _raceLine;
    private TMP_Text _statsLines;
    private TMP_Text _budgetLine;
    private PlayerStats _stats;

    private static readonly string[] StatNames =
    {
        "HP", "Speed", "Endurance", "Strength", "Dexterity", "AttackSpeed",
        "Defense", "Intelligence", "Wisdom", "Faith", "Luck"
    };

    private void OnEnable()
    {
        Build(Localization.T("RACE / STAT SHEET"));
        _raceLine = MakeBodyText(BodyRow.transform, "RaceLine", new Vector2(-180f, 150f));
        _statsLines = MakeBodyText(BodyRow.transform, "StatsLines", new Vector2(-180f, 60f));
        _budgetLine = MakeBodyText(BodyRow.transform, "BudgetLine", new Vector2(-180f, -140f));
        MakeBodyText(BodyRow.transform, "Hint", new Vector2(30f, -140f))
            .text = Localization.T("[ + ] allocate points / use buttons to commit");
    }

    private TMP_Text MakeBodyText(RectTransform parent, string name, Vector2 pos)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = new Vector2(420f, 200f);
        var tmp = go.AddComponent<TextMeshProUGUI>();
        GameManager.Instance?.UIManager?.ApplyDefaultFont(tmp);
        tmp.fontSize = Mathf.Max(12f, Screen.height / 62f);
        tmp.color = Color.white;
        tmp.alignment = TextAlignmentOptions.Left;
        return tmp;
    }

    protected override void Refresh()
    {
        var gm = GameManager.Instance;
        var player = gm != null ? gm.Player : null;
        _stats = player != null ? player.GetComponent<PlayerStats>() : null;
        if (_stats == null) return;

        var race = _stats.Race;
        string raceName = race != null ? race.displayName : Localization.T("Human");
        _raceLine.text = raceName + (race != null && !string.IsNullOrEmpty(race.lore) ? "\n" + race.lore : "");

        StringBuilder sb = new StringBuilder();
        for (int i = 0; i < PlayerStats.StatCount; i++)
        {
            StatType st = (StatType)i;
            float alloc = _alloc.TryGetValue(i, out var a) ? a : 0f;
            float total = _stats.GetTotal(st) + Mathf.RoundToInt(alloc);
            sb.Append(StatNames[i]).Append("  ").Append(Mathf.RoundToInt(_stats.GetBaseStat(st)))
              .Append(" → ").Append(total);
            if (i != PlayerStats.StatCount - 1) sb.Append("\n");
        }
        _statsLines.text = sb.ToString();

        int used = 0;
        foreach (var e in _alloc) used += e.Value;
        int remain = Mathf.Max(0, Budget - used);
        _budgetLine.text = Localization.F("Points Remaining: {0}", remain);
    }

    /// <summary>Allocate one preview point to a stat (0-10) if budget remains.</summary>
    public void Allocate(StatType stat, int amount)
    {
        if (_stats == null) return;
        int idx = (int)stat;
        int cur = _alloc.TryGetValue(idx, out var a) ? a : 0;
        int newCur = Mathf.Clamp(cur + amount, 0, 10);
        int used = 0;
        foreach (var e in _alloc) used += e.Value;
        int remain = Budget - used;
        if (newCur > cur && remain <= 0) return;
        if (newCur == cur) return;
        _alloc[idx] = newCur;
        Refresh();
    }

    /// <summary>Commit the preview allocation to the underlying stats.</summary>
    public void Commit()
    {
        if (_stats == null) return;
        foreach (var e in _alloc)
        {
            if (e.Value != 0) _stats.AddStatPoints((StatType)e.Key, e.Value);
        }
        _alloc.Clear();
        Refresh();
    }
}
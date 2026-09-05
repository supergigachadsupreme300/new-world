using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Phase 8 (Task 8.2): Character Creation UI (race select + stat preview, planning Phase 4).
/// Lists selectable races from <see cref="RaceUnlockManager"/> availability, previews the stat
/// delta the race grants, and applies the selection to <see cref="PlayerStats"/>. Composes the
/// existing race/passive system.
/// </summary>
public sealed class CharacterCreationUI : MenuPanelBase
{
    private TMP_Text _listLine;
    private TMP_Text _previewLine;
    private int _selected;
    private readonly List<RaceData> _pool = new List<RaceData>();
    private PlayerStats _stats;

    private void OnEnable()
    {
        Build(Localization.T("CHARACTER CREATION"));
        _listLine = MakeBodyText(BodyRow, "Races", new Vector2(-220f, 150f));
        _previewLine = MakeBodyText(BodyRow, "Preview", new Vector2(40f, 150f));
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
        rt.sizeDelta = new Vector2(460f, 240f);
        var tmp = go.AddComponent<TextMeshProUGUI>();
        GameManager.Instance?.UIManager?.ApplyDefaultFont(tmp);
        tmp.fontSize = Mathf.Max(12f, Screen.height / 60f);
        tmp.color = Color.white;
        tmp.alignment = TextAlignmentOptions.Left;
        return tmp;
    }

    protected override void Refresh()
    {
        var gm = GameManager.Instance;
        var player = gm != null ? gm.Player : null;
        _stats = player != null ? player.GetComponent<PlayerStats>() : null;
        RebuildPool();

        StringBuilder sb = new StringBuilder();
        for (int i = 0; i < _pool.Count; i++)
        {
            RaceData r = _pool[i];
            string tag = i == _selected ? "» " : "  ";
            sb.Append(tag).Append(r.displayName);
            if (i != _pool.Count - 1) sb.Append("\n");
        }
        _listLine.text = sb.ToString();

        RaceData sel = _selected >= 0 && _selected < _pool.Count ? _pool[_selected] : null;
        if (sel != null && _stats != null)
        {
            StringBuilder p = new StringBuilder();
            for (int i = 0; i < PlayerStats.StatCount; i++)
            {
                StatType st = (StatType)i;
                float mod = sel.GetStatModifier(st);
                if (mod != 0f)
                    p.Append(StatNames[i]).Append(" ").Append(FormatPercent(mod)).Append("\n");
            }
            if (sel.PassiveId != null && !string.IsNullOrEmpty(sel.PassiveId))
                p.Append("Passive: ").Append(sel.PassiveDescription).Append("\n");
            _previewLine.text = p.Length == 0 ? Localization.T("No racial bonuses.") : p.ToString();
        }
        else
        {
            _previewLine.text = "";
        }
    }

    private void RebuildPool()
    {
        _pool.Clear();
        var unlock = RaceUnlockManager.Instance;
        var roster = RaceDatabase.BuildDefaultRoster();
        if (roster != null)
        {
            foreach (RaceData r in roster)
            {
                if (unlock == null || unlock.IsUnlocked(r) || r.raceId == "human")
                    _pool.Add(r);
            }
        }
        if (_pool.Count == 0 && roster != null)
        {
            foreach (RaceData r in roster) _pool.Add(r); // fallback: list all
        }
        if (_selected >= _pool.Count) _selected = _pool.Count - 1;
        if (_selected < 0 && _pool.Count > 0) _selected = 0;
    }

    private static readonly string[] StatNames =
    {
        "HP", "Speed", "Endurance", "Strength", "Dexterity", "AttackSpeed",
        "Defense", "Intelligence", "Wisdom", "Faith", "Luck"
    };

    private static string FormatPercent(float v) => (v > 0 ? "+" : "") + v.ToString("0") + "%";

    /// <summary>Select the next/previous race.</summary>
    public void Cycle(int direction)
    {
        int n = _pool.Count;
        if (n == 0) return;
        _selected = Mathf.Max(0, Mathf.Min(n - 1, _selected + (direction > 0 ? 1 : -1)));
        Refresh();
    }

    /// <summary>Apply the selected race to the player.</summary>
    public void Confirm()
    {
        if (_pool.Count == 0) return;
        RaceData r = _pool[Mathf.Max(0, _selected)];
        if (_stats == null) return;

        var mgr = _stats.GetComponent<RaceChangeManager>();
        if (mgr == null) mgr = _stats.gameObject.AddComponent<RaceChangeManager>();
        mgr.SetActiveRace(r, requireStone: false, unlockIfNeeded: true);

        GameManager.Instance?.UIManager?.ShowMessage(
            Localization.F("Đã đọn cá gj: {0}", r.displayName), 2.5f);
    }
}
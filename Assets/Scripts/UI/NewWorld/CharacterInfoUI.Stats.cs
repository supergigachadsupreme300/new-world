/// <summary>Character Info - Info tab: level/XP / unspent stat points / 11-stat allocator and edit fields, class+race summary lines, and the talent rank list below the stat block (build + refresh).</summary>
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using TMPro;

public sealed partial class CharacterInfoUI
{

    // ── Info tab ──────────────────────────────────────────────────────────
    // Level, unspent stat points, XP progress bar, 11-stat "+" allocator lines,
    // and class/race change controls. (HP/FP/Stamina bars live on the HUD, not here.)
    // The whole block plus the TALENTS section below it share one vertical scroll.

    private void BuildInfoTabScroll(Transform parent)
    {
        var view = new GameObject("InfoScroll");
        view.transform.SetParent(parent, false);
        var vrt = view.AddComponent<RectTransform>();
        vrt.anchorMin = Vector2.zero;
        vrt.anchorMax = Vector2.one;
        vrt.offsetMin = Vector2.zero;
        vrt.offsetMax = Vector2.zero;
        var vImg = view.AddComponent<Image>();
        vImg.color = new Color(0f, 0f, 0f, 0f);
        vImg.raycastTarget = true;
        view.AddComponent<RectMask2D>();
        var sr = view.AddComponent<ScrollRect>();
        sr.horizontal = false;
        sr.vertical = true;
        sr.movementType = ScrollRect.MovementType.Clamped;
        sr.inertia = true;
        sr.scrollSensitivity = 48f;
        sr.viewport = vrt;

        _infoContent = new GameObject("InfoContent").AddComponent<RectTransform>();
        _infoContent.SetParent(view.transform, false);
        _infoContent.anchorMin = new Vector2(0f, 1f);
        _infoContent.anchorMax = new Vector2(1f, 1f);
        _infoContent.pivot = new Vector2(0.5f, 1f);
        _infoContent.sizeDelta = new Vector2(0f, InfoStatBlockHeight);
        sr.content = _infoContent;

        var statBlock = new GameObject("StatBlock").AddComponent<RectTransform>();
        statBlock.SetParent(_infoContent, false);
        statBlock.anchorMin = new Vector2(0f, 1f);
        statBlock.anchorMax = new Vector2(1f, 1f);
        statBlock.pivot = new Vector2(0.5f, 1f);
        statBlock.anchoredPosition = new Vector2(0f, 0f);
        statBlock.sizeDelta = new Vector2(0f, InfoStatBlockHeight);
        BuildInfoTab(statBlock);

        BuildTalentsView(_infoContent);

        // Size the scroll content to fit both the stat/level block and the talent section
        // so the full stack is reachable by scrolling (drag range = content - viewport).
        float talentH = 0f;
        if (_talentsView != null)
        {
            var srt = _talentsView.GetComponent<RectTransform>();
            if (srt != null) talentH = srt.rect.height;
        }
        _infoContent.sizeDelta = new Vector2(0f, InfoStatBlockHeight + talentH + 24f);
    }

    private void BuildInfoTab(Transform parent)
    {
        _levelText = MakeBodyText(parent, "Level", P(-330f, 210f), Sz(220f, 64f));
        _levelText.fontSize = Mathf.Max(34f, Screen.height / 28f);

        _xpFill = MakeBar(parent, "XpBar", P(-330f, 140f), Sz(360f, 32f),
            new Color(0.6f, 0.5f, 0.85f), out _xpLabel);

        _pointsText = MakeBodyText(parent, "StatPoints", P(-330f, 100f), Sz(240f, 32f));
        _pointsText.fontSize = Mathf.Max(20f, Screen.height / 40f);

        // Stat list with "+" allocator (two columns of 6 + 5 directly under the XP bar).
        // Rows start below the "Stat Points" label (y 80) so the first row never overlaps it.
        for (int i = 0; i < PlayerStats.StatCount; i++)
        {
            int col = i < 6 ? 0 : 1;
            float baseY = 80f - (i % 6) * 26f;
            float x0 = col == 0 ? -330f : -40f;

            var name = MakeBodyText(parent, "StatName_" + i, P(x0, baseY), Sz(130f, 26f));
            name.fontSize = Mathf.Max(18f, Screen.height / 46f);
            name.text = StatNames[i];

            _statValueTexts[i] = MakeBodyText(parent, "StatValue_" + i, P(x0 + 160f, baseY), Sz(50f, 26f));
            _statValueTexts[i].fontSize = Mathf.Max(18f, Screen.height / 46f);
            _statValueTexts[i].alignment = TextAlignmentOptions.TopRight;

            _plusButtons[i] = MakePlusButton(parent, "Plus_" + i,
                P(x0 + 222f, baseY), Sz(22f, 22f), i);

            _statEditFields[i] = MakeStatEditField(parent, "StatEdit_" + i, x0 + 252f, baseY, i);
        }

        // Class / race summaries + change buttons.
        _classLine = MakeBodyText(parent, "ClassLine", P(-330f, -96f), Sz(700f, 30f));
        _classLine.fontSize = Mathf.Max(16f, Screen.height / 48f);
        _raceLine = MakeBodyText(parent, "RaceLine", P(-330f, -130f), Sz(700f, 30f));
        _raceLine.fontSize = Mathf.Max(16f, Screen.height / 48f);
        var classBtn = MakeButton(parent, "ChangeClassBtn", "Change Class", P(-120f, -158f), () => OpenChangeDialog("class"));
        classBtn.GetComponent<RectTransform>().sizeDelta = Sz(150f, 32f);
        var raceBtn = MakeButton(parent, "ChangeRaceBtn", "Change Race", P(120f, -158f), () => OpenChangeDialog("race"));
        raceBtn.GetComponent<RectTransform>().sizeDelta = Sz(150f, 32f);
    }

    // ── Talents view (rankable XP/stat perks) ─────────────────────────────
    // Lives inside the Info panel's single vertical scroll, directly below the stat/level block.

    private void BuildTalentsView(Transform parent)
    {
        const float headerH = 28f;
        const float step = 56f;

        TalentCatalog.EnsureBuilt();
        int count = 0;
        foreach (var t in TalentCatalog.All)
            if (t != null) count++;

        var section = new GameObject("TalentsView");
        section.transform.SetParent(parent, false);
        var srt = section.AddComponent<RectTransform>();
        srt.anchorMin = new Vector2(0f, 1f);
        srt.anchorMax = new Vector2(1f, 1f);
        srt.pivot = new Vector2(0.5f, 1f);
        srt.anchoredPosition = new Vector2(0f, -InfoStatBlockHeight);
        srt.sizeDelta = new Vector2(0f, headerH + count * step + 16f);
        _talentsView = section;

        var titleGo = new GameObject("TalentsTitle");
        titleGo.transform.SetParent(section.transform, false);
        var tirt = titleGo.AddComponent<RectTransform>();
        tirt.anchorMin = new Vector2(0f, 1f);
        tirt.anchorMax = new Vector2(1f, 1f);
        tirt.pivot = new Vector2(0.5f, 1f);
        tirt.anchoredPosition = new Vector2(0f, 0f);
        tirt.sizeDelta = new Vector2(0f, headerH);
        var title = titleGo.AddComponent<TextMeshProUGUI>();
        GameManager.Instance?.UIManager?.ApplyDefaultFont(title);
        title.fontSize = Mathf.Max(17f, Screen.height / 40f);
        title.color = new Color(0.85f, 0.85f, 0.95f, 1f);
        title.alignment = TextAlignmentOptions.Center;
        title.text = "TALENTS";

        foreach (var talent in TalentCatalog.All)
        {
            if (talent == null) continue;
            var row = MakeTalentRow(section.transform, talent, _talentRows.Count, headerH);
            _talentRows.Add((talent, row.label, row.btn));
        }
    }

    private (TMP_Text label, Button btn) MakeTalentRow(Transform parent, Talent talent, int index, float startY)
    {
        const float step = 56f;
        var go = new GameObject("Row_" + talent.Id);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = new Vector2(0f, -(startY + index * step));
        rt.sizeDelta = new Vector2(0f, step - 6f);
        var bg = go.AddComponent<Image>();
        bg.raycastTarget = false;
        bg.color = new Color(1f, 1f, 1f, 0.06f);

        var label = new GameObject("Label");
        label.transform.SetParent(go.transform, false);
        var lr = label.AddComponent<RectTransform>();
        lr.anchorMin = Vector2.zero;
        lr.anchorMax = new Vector2(1f, 1f);
        lr.offsetMin = new Vector2(12f, 4f);
        lr.offsetMax = new Vector2(-168f, -4f);
        var tmp = label.AddComponent<TextMeshProUGUI>();
        GameManager.Instance?.UIManager?.ApplyDefaultFont(tmp);
        tmp.fontSize = Mathf.Max(14f, Screen.height / 52f);
        tmp.color = Color.white;
        tmp.alignment = TextAlignmentOptions.MidlineLeft;
        tmp.enableWordWrapping = false;
        tmp.overflowMode = TextOverflowModes.Ellipsis;

        var btnGo = new GameObject("RankUpBtn");
        btnGo.transform.SetParent(go.transform, false);
        var brt = btnGo.AddComponent<RectTransform>();
        brt.anchorMin = new Vector2(1f, 0.5f);
        brt.anchorMax = new Vector2(1f, 0.5f);
        brt.pivot = new Vector2(1f, 0.5f);
        brt.anchoredPosition = new Vector2(-10f, 0f);
        brt.sizeDelta = new Vector2(150f, 36f);
        var bImg = btnGo.AddComponent<Image>();
        ApplyMenuButtonSprite(bImg);
        var btn = btnGo.AddComponent<Button>();
        btn.targetGraphic = bImg;
        Talent captured = talent;
        btn.onClick.AddListener(() => RankUpTalent(captured));
        var bl = new GameObject("Label");
        bl.transform.SetParent(btnGo.transform, false);
        var blr = bl.AddComponent<RectTransform>();
        blr.anchorMin = Vector2.zero;
        blr.anchorMax = Vector2.one;
        blr.offsetMin = Vector2.zero;
        blr.offsetMax = Vector2.zero;
        var blt = bl.AddComponent<TextMeshProUGUI>();
        GameManager.Instance?.UIManager?.ApplyDefaultFont(blt);
        blt.text = "Rank Up";
        blt.fontSize = Mathf.Max(14f, Screen.height / 56f);
        blt.color = Color.white;
        blt.alignment = TextAlignmentOptions.Center;

        return (tmp, btn);
    }

    private void RefreshTalentsView()
    {
        if (_talentsView == null) return;
        var tracker = TalentTrackerOf();
        foreach (var (talent, label, btn) in _talentRows)
        {
            if (label == null || btn == null) continue;
            int rank = tracker != null ? tracker.RankOf(talent.Id) : 0;
            label.text = talent.DisplayName
                + "  ·  " + Localization.F("Rank {0}/{1}", rank, talent.MaxRanks)
                + "\n" + talent.EffectPerRank();
            btn.interactable = tracker != null && tracker.CanRank(talent.Id);
        }
    }

    private void RankUpTalent(Talent talent)
    {
        var tracker = TalentTrackerOf();
        if (tracker == null) return;
        if (tracker.RankUp(talent.Id))
            RefreshTalentsView();
    }

    private void RefreshInfo()
    {
        var stats = PlayerStatsOf();
        var level = LevelUpOf();

        if (level != null)
        {
            if (_levelText != null)
                _levelText.text = Localization.F("Level {0}", level.Level);
            if (_pointsText != null)
                _pointsText.text = Localization.F("Stat Points: {0}", level.AvailablePoints);

            float need = Mathf.Max(1f, level.XpToNextLevel);
            float frac = Mathf.Clamp01(level.Xp / need);
            if (_xpFill != null)
                _xpFill.fillAmount = frac;
            if (_xpLabel != null)
                _xpLabel.text = Localization.F("XP {0:0} / {1:0}", level.Xp, need);
        }

        // Stat totals + allocator enabled state.
        bool canSpend = level != null && level.AvailablePoints > 0;
        for (int i = 0; i < PlayerStats.StatCount; i++)
        {
            if (_statValueTexts[i] != null)
            {
                float total = stats != null ? stats.GetTotal((StatType)i) : 0f;
                _statValueTexts[i].text = Mathf.RoundToInt(total).ToString();
            }
            if (_plusButtons[i] != null)
            {
                _plusButtons[i].gameObject.SetActive(canSpend);
                _plusButtons[i].interactable = canSpend;
            }
        }

        // Class / race summary lines.
        if (_classLine != null)
        {
            var unlocker = ClassUnlockerOf();
            _classLine.text = unlocker != null ? CurrentClassLine(unlocker) : "";
        }
        if (_raceLine != null)
        {
            var raceMgr = RaceMgrOf();
            _raceLine.text = raceMgr != null ? CurrentRaceLine(raceMgr) : "";
        }
    }

    private static string CurrentClassLine(ClassUnlocker unlocker)
    {
        var active = unlocker.ActiveClass;
        if (active == null)
        {
            unlocker.EvaluateAll();
            active = unlocker.ActiveClass;
        }
        if (active == null) return "";
        string name = !string.IsNullOrEmpty(active.displayName) ? active.displayName : active.classId;
        string mech = active.UniqueMechanic;
        return string.IsNullOrEmpty(mech)
            ? Localization.F("Class: {0}", name)
            : Localization.F("Class: {0} — {1}", name, mech);
    }

    private static string CurrentRaceLine(RaceChangeManager mgr)
    {
        var active = mgr.ActiveRace;
        if (active == null) return "";
        return Localization.F("Race: {0} — {1}", active.displayName, active.PassiveDescription);
    }
}
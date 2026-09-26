/// <summary>Character Info - Faith tab (devotion bars, perk summary, switch-faith dialog) and the class/race/faith change dialog build + confirm flow.</summary>
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using TMPro;

public sealed partial class CharacterInfoUI
{

    // ── Class / Race change dialog ──────────────────────────────────────────
    private void EnsureChangeDialog()
    {
        if (_changeDialog != null || PaletteCanvas == null) return;

        _changeDialog = new GameObject("ChangeDialog");
        _changeDialog.transform.SetParent(PaletteCanvas, false);
        var rootRt = _changeDialog.AddComponent<RectTransform>();
        rootRt.anchorMin = Vector2.zero;
        rootRt.anchorMax = Vector2.one;
        rootRt.offsetMin = Vector2.zero;
        rootRt.offsetMax = Vector2.zero;
        var dim = _changeDialog.AddComponent<Image>();
        dim.color = new Color(0f, 0f, 0f, 0.55f);
        dim.raycastTarget = true;

        var box = new GameObject("DialogBox");
        box.transform.SetParent(_changeDialog.transform, false);
        var boxRt = box.AddComponent<RectTransform>();
        boxRt.anchorMin = new Vector2(0.5f, 0.5f);
        boxRt.anchorMax = new Vector2(0.5f, 0.5f);
        boxRt.pivot = new Vector2(0.5f, 0.5f);
        boxRt.anchoredPosition = Vector2.zero;
        boxRt.sizeDelta = Sz(580f, 440f);
        var boxImg = box.AddComponent<Image>();
        var menuTex = UiAssetCache.MenuTexture;
        if (menuTex != null)
        {
            boxImg.sprite = Sprite.Create(menuTex,
                new Rect(0, 0, menuTex.width, menuTex.height), new Vector2(0.5f, 0.5f));
            boxImg.type = Image.Type.Simple;
            boxImg.preserveAspect = false;
            boxImg.color = Color.white;
        }
        else
        {
            boxImg.color = ColorPalette.UIBackdrop;
        }

        _changeTitle = MakeDialogText(box.transform, "Title", P(0f, 196f), Sz(540f, 32f), TextAlignmentOptions.Center);
        _changeTitle.fontSize = Mathf.Max(18f, Screen.height / 44f);

        _changeOptions = new GameObject("Options").transform;
        _changeOptions.SetParent(box.transform, false);
        var or = _changeOptions.gameObject.AddComponent<RectTransform>();
        or.anchorMin = new Vector2(0.5f, 0.5f);
        or.anchorMax = new Vector2(0.5f, 0.5f);
        or.anchoredPosition = Vector2.zero;
        or.sizeDelta = Vector2.zero;

        _changeConfirmText = MakeDialogText(box.transform, "Confirm", P(0f, -184f), Sz(540f, 30f), TextAlignmentOptions.Center);

        MakeDialogButton(box.transform, "ConfirmBtn", "Confirm Change", P(-90f, -190f), ApplyPendingChange);
        MakeDialogButton(box.transform, "CancelBtn", "Cancel", P(90f, -190f), () => CloseChangeDialog(false));

        var close = MakeDialogButton(box.transform, "DialogClose", "X", P(256f, 196f), () => CloseChangeDialog(false));
        close.GetComponent<RectTransform>().sizeDelta = Sz(40f, 32f);

        var hook = _changeDialog.AddComponent<MenuPanelHook>();
        _changeDialog.SetActive(false);
    }

    private void OpenChangeDialog(string mode)
    {
        _changeMode = mode;
        _pendingChange = null;
        RebuildChangeDialog();
        if (_changeDialog != null)
            _changeDialog.SetActive(true);
    }

    public void CloseChangeDialog(bool applyStaged)
    {
        if (_changeDialog != null)
            _changeDialog.SetActive(false);
        _changeMode = null;
        _pendingChange = null;
    }

    private void RebuildChangeDialog()
    {
        if (_changeOptions == null) return;

        for (int i = _changeOptions.childCount - 1; i >= 0; i--)
            Destroy(_changeOptions.GetChild(i).gameObject);

        if (_changeMode == "class")
        {
            _changeTitle.text = Localization.T("Change Class — pick a new class");
            BuildClassOptions(_changeOptions);
        }
        else if (_changeMode == "faith")
        {
            _changeTitle.text = Localization.T("Switch Faith — pick a new faith (lose 30% devotion)");
            BuildFaithOptions(_changeOptions);
        }
        else
        {
            _changeTitle.text = Localization.T("Change Race — pick a new race");
            BuildRaceOptions(_changeOptions);
        }
        _changeConfirmText.text = "";
        _changeConfirmBtn = _changeConfirmBtn ?? FindConfirmButton();
        UpdateConfirmEnabled();
    }

    private Button FindConfirmButton()
    {
        if (_changeDialog == null) return null;
        var b = _changeDialog.transform.Find("DialogBox/ConfirmBtn");
        return b != null ? b.GetComponent<Button>() : null;
    }

    private void BuildFaithOptions(Transform parent)
    {
        var rm = GameManager.Instance?.ReligionManager;
        if (rm == null) return;

        var values = (ReligionManager.ReligionFaith[])System.Enum.GetValues(typeof(ReligionManager.ReligionFaith));
        int idx = 0;
        foreach (var f in values)
        {
            if (f == ReligionManager.ReligionFaith.None) continue;
            int col = idx % 2;
            int row = idx / 2;
            float x = col == 0 ? -270f : 10f;
            float y = 150f - row * 28f;
            bool isCurrent = rm.CurrentFaith == f;
            var captured = f;
            string option = FaithDisplayName(f) + (isCurrent ? "  (current)" : "");
            MakeDialogOption(parent, option, P(x, y), 270f, !isCurrent, () =>
            {
                _pendingChange = captured;
                _changeConfirmText.text = Localization.F("Switch faith to {0}? You lose 30% devotion in {1}.",
                    FaithDisplayName(captured), FaithDisplayName(rm.CurrentFaith));
                UpdateConfirmEnabled();
            });
            idx++;
        }
    }

    private void BuildClassOptions(Transform parent)
    {
        var unlocker = ClassUnlockerOf();
        List<ClassData> list = unlocker != null && unlocker.Classes != null && unlocker.Classes.Count > 0
            ? unlocker.Classes
            : ClassUnlocker.BuildDefaultClasses();
        string currentClassId = unlocker != null ? unlocker.ActiveClassId : "wanderer";

        for (int i = 0; i < list.Count; i++)
        {
            int col = i % 2;
            int row = i / 2;
            float x = col == 0 ? -270f : 10f;
            float y = 150f - row * 28f;
            var c = list[i];
            if (c == null) continue;
            string name = !string.IsNullOrEmpty(c.displayName) ? c.displayName : c.classId;
            bool isCurrent = string.Equals(currentClassId, c.classId, System.StringComparison.OrdinalIgnoreCase);
            MakeDialogOption(parent, name + (isCurrent ? "  (current)" : ""), P(x, y), 270f, !isCurrent, () =>
            {
                _pendingChange = c.classId;
                _changeConfirmText.text = Localization.F("Change class to {0}?", name);
                UpdateConfirmEnabled();
            });
        }
    }

    private void BuildRaceOptions(Transform parent)
    {
        var mgr = RaceMgrOf();
        if (mgr == null) return; // no race manager wired -> nothing to change
        string currentRaceId = mgr.ActiveRaceId;
        var roster = RaceDatabase.BuildDefaultRoster();
        if (roster == null) return;

        for (int i = 0; i < roster.Count; i++)
        {
            int col = i % 2;
            int row = i / 2;
            float x = col == 0 ? -270f : 10f;
            float y = 150f - row * 28f;
            var r = roster[i];
            if (r == null) continue;
            // Single-choice model (§3.5): only Human or an actually-discovered race is selectable.
            if (!mgr.CanSelectRace(r)) continue;
            bool isCurrent = string.Equals(currentRaceId, r.raceId, System.StringComparison.OrdinalIgnoreCase);
            MakeDialogOption(parent, r.displayName + (isCurrent ? "  (current)" : ""), P(x, y), 270f, !isCurrent, () =>
            {
                _pendingChange = r;
                _changeConfirmText.text = Localization.F("Change race to {0}? (requires a Ritual Stone)", r.displayName);
                UpdateConfirmEnabled();
            });
        }
    }

    private void UpdateConfirmEnabled()
    {
        _changeConfirmBtn = _changeConfirmBtn ?? FindConfirmButton();
        if (_changeConfirmBtn != null)
            _changeConfirmBtn.interactable = _pendingChange != null;
    }

    private void ApplyPendingChange()
    {
        if (_pendingChange == null)
        {
            CloseChangeDialog(false);
            return;
        }

        if (_changeMode == "class" && _pendingChange is string classId)
        {
            var unlocker = ClassUnlockerOf();
            if (unlocker != null)
            {
                unlocker.SetActiveClass(classId);
            }
        }
        else if (_changeMode == "faith" && _pendingChange is ReligionManager.ReligionFaith faith)
        {
            var rm = GameManager.Instance?.ReligionManager;
            if (rm != null)
            {
                if (!rm.SwitchFaith(faith))
                {
                    if (_changeConfirmText != null)
                        _changeConfirmText.text = Localization.T("Bạn đã đổi tín ngưỡng hôm nay — hãy thử lại vào ngày mai.");
                    return;
                }
                if (GameManager.Instance?.UIManager != null)
                    GameManager.Instance.UIManager.ShowMessage(Localization.F("Faith changed to {0}!", FaithDisplayName(faith)), 2f);
            }
        }
        else if (_changeMode == "race" && _pendingChange is RaceData race)
        {
            var mgr = RaceMgrOf();
            if (mgr != null)
            {
                // Single-choice model (§3.5): switching costs a Ritual Stone and never auto-unlocks.
                if (!mgr.SetActiveRace(race, requireStone: true, unlockIfNeeded: false))
                {
                    if (_changeConfirmText != null)
                        _changeConfirmText.text = Localization.T("Bạn cần Đá Nghi Thức để hóa thân sang chủng tộc khác.");
                    return;
                }
                if (GameManager.Instance?.UIManager != null &&
                    !string.Equals(race.raceId, "human", System.StringComparison.OrdinalIgnoreCase))
                    GameManager.Instance.UIManager.ShowMessage(
                        Localization.F("Bạn đã hóa thân thành {0}!", race.displayName), 2f);
            }
        }

        CloseChangeDialog(false);
        if (_current == Tab.Info)
            RefreshInfo();
        else if (_current == Tab.Faith)
            RefreshFaith();
        else
            RefreshInventoryUi();
    }

    // ── Faith tab ─────────────────────────────────────────────────────────
    // Current belief, devotion bars (0-10 per religion), perk summary for the
    // current faith, and the Switch-Faith dialog.
    private void BuildFaithTab(Transform parent)
    {
        _faithTitle = MakeBodyText(parent, "FaithTitle", P(0f, 238f), Sz(920f, 34f));
        _faithTitle.alignment = TextAlignmentOptions.Center;
        _faithTitle.fontSize = Mathf.Max(22f, Screen.height / 34f);

        // 192, not 206: the title's glyphs grow DOWN from y 238 (TopLeft in a 34 box), so at
        // 192 the status starts 8 below them. At the old 206 the two lines' ink overlapped by
        // ~6 units — invisible while the 84-tall tab band drew over the pair, a visible collision
        // once the band was raised. Same corridor rule as SkillsHeaderY: band bottom 250.
        _faithStatus = MakeBodyText(parent, "FaithStatus", P(0f, 192f), Sz(920f, 26f));
        _faithStatus.alignment = TextAlignmentOptions.Center;

        string[] rowNames = { "Taoism", "Buddhism", "Church" };
        Color[] fillColors =
        {
            new Color(0.35f, 0.75f, 0.55f),
            new Color(0.95f, 0.78f, 0.3f),
            new Color(0.4f, 0.55f, 0.95f)
        };
        for (int i = 0; i < 3; i++)
        {
            float y = 148f - i * 48f;
            var rowLabel = MakeBodyText(parent, "DevotionLabel_" + rowNames[i], P(-460f, y), Sz(230f, 26f));
            rowLabel.text = FaithDisplayName((ReligionManager.ReligionFaith)(i + 1));
            rowLabel.fontSize = Mathf.Max(15f, Screen.height / 54f);
            _devotionRowLabels[i] = rowLabel;
            _devotionFill[i] = MakeBar(parent, "DevotionBar_" + rowNames[i], P(-205f, y), Sz(300f, 24f),
                fillColors[i], out TMP_Text barLabel);
            _devotionBarLabels[i] = barLabel;
            barLabel.text = "0/" + ReligionManager.MaxDevotion;
        }

        var perksTitle = MakeBodyText(parent, "PerksTitle", P(-460f, -2f), Sz(420f, 26f));
        perksTitle.text = "Perks (current belief):";
        _perksText = MakeBodyText(parent, "PerksText", P(-460f, -34f), Sz(930f, 120f));
        _perksText.fontSize = Mathf.Max(14f, Screen.height / 58f);
        _perksText.lineSpacing = 1.25f;

        _switchFaithBtn = MakeButton(parent, "SwitchFaithBtn", "Switch Faith", P(0f, -208f), OpenFaithDialog);
        // MakeButton lays down the short "stats menu button" art; every other call site overrides
        // it with the full frame, and this one never did. Same convention as Change Class / Race.
        ApplyFullButtonSprite(_switchFaithBtn.GetComponent<Image>());
        var footer = MakeBodyText(parent, "FaithFooter", P(0f, -242f), Sz(920f, 22f));
        footer.alignment = TextAlignmentOptions.Center;
        footer.fontSize = Mathf.Max(12f, Screen.height / 68f);
        footer.text = "Switching faith removes 30% devotion from the abandoned faith and is limited to once per day.";
    }

    private void RefreshFaith()
    {
        var rm = GameManager.Instance?.ReligionManager;
        if (rm == null) return;
        var cur = rm.CurrentFaith;
        _faithTitle.text = "Faith — " + FaithDisplayName(cur);

        for (int i = 0; i < 3; i++)
        {
            var f = (ReligionManager.ReligionFaith)(i + 1);
            int dev = rm.GetDevotion(f);
            float frac = (float)dev / ReligionManager.MaxDevotion;
            _devotionRowLabels[i].text = FaithDisplayName(f) + (cur == f ? "   [current]" : "");
            _devotionRowLabels[i].color = cur == f ? new Color(0.4f, 1f, 0.6f) : Color.white;
            _devotionFill[i].fillAmount = frac;
            _devotionBarLabels[i].text = dev + "/" + ReligionManager.MaxDevotion;
        }

        bool blessed = rm.HasDailyBlessingToday;
        _faithStatus.text = cur == ReligionManager.ReligionFaith.None
            ? "You follow no religion yet. Worship at a pagoda, shrine, or church to join a faith."
            : blessed
                ? "Daily blessing active — you already worshiped at your faith's holy place today."
                : "No daily blessing today — worship at your faith's holy place.";
        _perksText.text = PerksText(rm);
    }

    private static string PerksText(ReligionManager rm)
    {
        switch (rm.CurrentFaith)
        {
            case ReligionManager.ReligionFaith.Taoism:
                return Localization.F("· Stamina regen +{0:0}%\n· Harvest yield +{1:0}%\n· Demon damage +{2:0}%\n· Qi blessing (daily): stamina regen x2",
                    rm.TaoistStaminaPassive * 100f, (rm.TaoistHarvestMult - 1f) * 100f, (rm.TaoistDemonDamageMult - 1f) * 100f);
            case ReligionManager.ReligionFaith.Buddhism:
                return Localization.F("· Karma gain +{0:0}%\n· Karma regen +{1:0}%\n· Max karma +{2:0}%\n· Rosary cost -{3:0}%\n· Demon damage taken -{4:0}%",
                    (rm.BuddhistKarmaGainMult - 1f) * 100f, (rm.BuddhistKarmaRegenMult - 1f) * 100f,
                    (rm.BuddhistMaxKarmaGainMult - 1f) * 100f, (1f - rm.BuddhistRosaryCost) * 100f,
                    (1f - rm.BuddhistDemonTakenMult) * 100f);
            case ReligionManager.ReligionFaith.Church:
                return Localization.F("· Holy damage +{0:0}%\n· Heal power +{1:0}%\n· Buff duration +{2:0}%\n· Demon damage taken -{3:0}%\n· Holy-water blessing (daily): full heal + heal power +{4:0}%",
                    (rm.ChurchHolyDamageMult - 1f) * 100f, (rm.ChurchHealPowerMult - 1f) * 100f,
                    (rm.ChurchBuffDurationMult - 1f) * 100f, (1f - rm.ChurchDemonTakenMult) * 100f,
                    (rm.ChurchBlessedHealMult - 1f) * 100f);
            default:
                return "Worship at a pagoda, shrine, or church to follow a faith and unlock its perks.\nDevotion +1 per worship day, up to " + ReligionManager.MaxDevotion + ".";
        }
    }

    private static string FaithDisplayName(ReligionManager.ReligionFaith f)
    {
        switch (f)
        {
            case ReligionManager.ReligionFaith.Taoism: return Localization.T("Đạo Giáo");
            case ReligionManager.ReligionFaith.Buddhism: return Localization.T("Phật Giáo");
            case ReligionManager.ReligionFaith.Church: return Localization.T("Công Giáo");
            default: return Localization.T("Không theo tín ngưỡng");
        }
    }

    private void OpenFaithDialog()
    {
        var rm = GameManager.Instance?.ReligionManager;
        if (rm == null) return;
        OpenChangeDialog("faith");
    }
}
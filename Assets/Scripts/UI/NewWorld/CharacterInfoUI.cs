using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Phase 10/11: Character Info menu — a stacked multi-panel window with a top button bar. Each
/// top button reveals one panel and hides the others (Stats / Skills / Inventory / Equipment /
/// Class / Race / Map). The Skills panel shows the per-category skill tree, lets the player spend skill
/// points and assign a hotkey to a castable skill via <see cref="SkillBindings"/>. The Equipment
/// panel is a humanoid 21-slot sheet (§5.4); the Class panel lists the 15 classes with unlock
/// status and lets the player change their active class; the Race panel lists the 22 races with
/// unlock status and lets the player change their active race (non-Human costs a Ritual Stone).
///
/// Composes existing PlayerStats / ToolManager / EquipmentSystem / GearCatalog / WeaponRigBuilder /
/// SkillProfile / SkillBindings / SkillCatalog / ClassUnlocker without rewriting them. Built on
/// MenuPanelBase.
/// </summary>
public sealed class CharacterInfoUI : MenuPanelBase
{
    public enum Tab { Stats = 0, Skills = 1, Inventory = 2, Equipment = 3, Class = 4, Race = 5, Map = 6 }

    public Tab ActiveTab = Tab.Stats;

    private readonly Dictionary<Tab, GameObject> _panels = new Dictionary<Tab, GameObject>();
    private Tab _current = Tab.Stats;
    private SkillType _skillView = SkillType.Melee;
    private bool _built;

    private TMP_Text _statsLine;
    private TMP_Text _skillListLine;
    private TMP_Text _skillPointsLine;
    private TMP_Text _invLine;
    private TMP_Text _equipSummary;
    private TMP_Text _classLine;
    private TMP_Text _raceLine;
    private TMP_Text _mapLine;
    private TMP_Text _captureLine;

    private readonly Dictionary<EquipSlot, TMP_Text> _equipSlotLabels = new Dictionary<EquipSlot, TMP_Text>();
    private readonly List<TMP_Text> _classRowLabels = new List<TMP_Text>();

    private static readonly string[] StatNames =
    {
        "HP", "Speed", "Endurance", "Strength", "Dexterity", "AttackSpeed",
        "Defense", "Intelligence", "Wisdom", "Faith", "Luck"
    };

    private void OnEnable()
    {
        if (_built) return;
        _built = true;

        Build(Localization.T("CHARACTER INFO"));
        _current = ActiveTab;

        BuildTopButtons();
        BuildPanels();
        ShowTab(_current);
    }

    private void BuildTopButtons()
    {
        string[] names = { "Stats", "Skills", "Inventory", "Equipment", "Class", "Race", "Map" };
        float w = PanelRect.rect.width;
        float bw = w / names.Length;
        for (int i = 0; i < names.Length; i++)
        {
            Tab tab = (Tab)i;
            string name = names[i];
            var go = new GameObject("Tab_" + name);
            go.transform.SetParent(PanelRect, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(bw * (0.5f + i), -56f);
            rt.sizeDelta = new Vector2(bw - 6f, 44f);
            var img = go.AddComponent<Image>();
            img.color = new Color(0.16f, 0.16f, 0.22f, 0.95f);
            var btn = go.AddComponent<Button>();
            btn.targetGraphic = img;
            Tab captured = tab;
            btn.onClick.AddListener(() => ShowTab(captured));

            var label = new GameObject("Label");
            label.transform.SetParent(go.transform, false);
            var lr = label.AddComponent<RectTransform>();
            lr.anchorMin = Vector2.zero;
            lr.anchorMax = Vector2.one;
            lr.offsetMin = Vector2.zero;
            lr.offsetMax = Vector2.zero;
            var lt = label.AddComponent<TextMeshProUGUI>();
            GameManager.Instance?.UIManager?.ApplyDefaultFont(lt);
            lt.text = name;
            lt.fontSize = Mathf.Max(18f, Screen.height / 52f);
            lt.color = Color.white;
            lt.alignment = TextAlignmentOptions.Center;
        }
    }

    private void BuildPanels()
    {
        // Stats panel.
        _panels[Tab.Stats] = MakePanel("StatsPanel");
        _statsLine = MakeBodyText(_panels[Tab.Stats].transform, "Stats", new Vector2(-200f, 10f), 460f, 300f);

        // Skills panel.
        _panels[Tab.Skills] = MakePanel("SkillsPanel");
        BuildSkillTypeBar(_panels[Tab.Skills].transform);
        _skillPointsLine = MakeBodyText(_panels[Tab.Skills].transform, "SkillPoints", new Vector2(-200f, 140f), 420f, 40f);
        _skillListLine = MakeBodyText(_panels[Tab.Skills].transform, "SkillList", new Vector2(-200f, 60f), 460f, 300f);
        _captureLine = MakeBodyText(_panels[Tab.Skills].transform, "Capture", new Vector2(-200f, -150f), 420f, 40f);
        MakeButton(_panels[Tab.Skills].transform, "LearnBtn", "Learn Selected", new Vector2(150f, -170f), LearnSelected);
        MakeButton(_panels[Tab.Skills].transform, "AssignKeyBtn", "Assign Key", new Vector2(150f, -210f), AssignNextSkillKey);

        // Inventory panel.
        _panels[Tab.Inventory] = MakePanel("InventoryPanel");
        _invLine = MakeBodyText(_panels[Tab.Inventory].transform, "Inventory", new Vector2(-200f, 10f), 460f, 300f);

        // Equipment panel (humanoid 21-slot sheet, §5.4).
        _panels[Tab.Equipment] = MakePanel("EquipmentPanel");
        BuildEquipmentSheet(_panels[Tab.Equipment].transform);
        _equipSummary = MakeBodyText(_panels[Tab.Equipment].transform, "Equipment", new Vector2(-200f, -200f), 460f, 90f);

        // Class panel (the 15-class roster, §3.2 with active-class selection).
        _panels[Tab.Class] = MakePanel("ClassPanel");
        _classLine = MakeBodyText(_panels[Tab.Class].transform, "Classes", new Vector2(-200f, 10f), 460f, 320f);
        MakeButton(_panels[Tab.Class].transform, "CycleClassBtn", "Switch Active Class", new Vector2(150f, -300f), CycleActiveClass);

        // Race panel (the 22-race roster, §3.5 with active-race selection + ritual stone cost).
        _panels[Tab.Race] = MakePanel("RacePanel");
        _raceLine = MakeBodyText(_panels[Tab.Race].transform, "Races", new Vector2(-200f, 10f), 460f, 320f);
        MakeButton(_panels[Tab.Race].transform, "CycleRaceBtn", "Switch Active Race", new Vector2(150f, -300f), CycleActiveRace);

        // Map panel (placeholder summary; the dedicated WorldMapUI is separate).
        _panels[Tab.Map] = MakePanel("MapPanel");
        _mapLine = MakeBodyText(_panels[Tab.Map].transform, "Map", new Vector2(-200f, 10f), 460f, 300f);
    }

    // ── Humanoid 21-slot equipment sheet (§5.4) ────────────────────────────
    // Humanoid torso down the middle (Head → Necklace → Body → Belt → Legging →
    // Feet) with hands (L/R) at the shoulders and glove on the arm; the 10 finger
    // rings walk the left elbow column (Finger1-5) and right hand column
    // (Finger6-10), ears flanking the head. Each slot is a small button: click an
    // occupied slot to unequip, click an empty slot to equip the next free catalog
    // piece for it, click a hand to cycle the equipped weapon.
    private void BuildEquipmentSheet(Transform parent)
    {
        GearCatalog.EnsureBuilt();
        _equipSlotLabels.Clear();

        SlotButton(parent, "Ear1",        EquipSlot.Ear1,     new Vector2(-200f, -10f));
        SlotButton(parent, "Head",        EquipSlot.Head,     new Vector2(-105f, -10f));
        SlotButton(parent, "Ear2",        EquipSlot.Ear2,     new Vector2(-10f, -10f));
        SlotButton(parent, "Necklace",    EquipSlot.Necklace, new Vector2(-105f, -48f));
        SlotButton(parent, "LHand",       EquipSlot.LeftHand, new Vector2(-200f, -86f));
        SlotButton(parent, "Body",        EquipSlot.Body,     new Vector2(-105f, -86f));
        SlotButton(parent, "RHand",       EquipSlot.RightHand,new Vector2(-10f, -86f));
        SlotButton(parent, "Glove",       EquipSlot.Glove,    new Vector2(-200f, -124f));
        SlotButton(parent, "Belt",        EquipSlot.Belt,     new Vector2(-105f, -124f));
        SlotButton(parent, "Legging",     EquipSlot.Legging,  new Vector2(-105f, -162f));
        SlotButton(parent, "Feet",        EquipSlot.Feet,     new Vector2(-105f, -200f));
        SlotButton(parent, "Finger1",     EquipSlot.Finger1,  new Vector2(-250f, -10f));
        SlotButton(parent, "Finger2",     EquipSlot.Finger2,  new Vector2(-250f, -48f));
        SlotButton(parent, "Finger3",     EquipSlot.Finger3,  new Vector2(-250f, -86f));
        SlotButton(parent, "Finger4",     EquipSlot.Finger4,  new Vector2(-250f, -124f));
        SlotButton(parent, "Finger5",     EquipSlot.Finger5,  new Vector2(-250f, -162f));
        SlotButton(parent, "Finger6",     EquipSlot.Finger6,  new Vector2(40f, -10f));
        SlotButton(parent, "Finger7",     EquipSlot.Finger7,  new Vector2(40f, -48f));
        SlotButton(parent, "Finger8",     EquipSlot.Finger8,  new Vector2(40f, -86f));
        SlotButton(parent, "Finger9",     EquipSlot.Finger9,  new Vector2(40f, -124f));
        SlotButton(parent, "Finger10",    EquipSlot.Finger10, new Vector2(40f, -162f));
    }

    private void SlotButton(Transform parent, string name, EquipSlot slot, Vector2 pos)
    {
        var go = new GameObject(name + "Slot");
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = new Vector2(84f, 30f);
        var img = go.AddComponent<Image>();
        img.color = new Color(0.14f, 0.16f, 0.2f, 0.95f);
        var btn = go.AddComponent<Button>();
        btn.targetGraphic = img;
        EquipSlot captured = slot;
        btn.onClick.AddListener(() => ToggleEquipSlot(captured));

        var label = new GameObject("Label");
        label.transform.SetParent(go.transform, false);
        var lr = label.AddComponent<RectTransform>();
        lr.anchorMin = Vector2.zero;
        lr.anchorMax = Vector2.one;
        lr.offsetMin = Vector2.zero;
        lr.offsetMax = Vector2.zero;
        var lt = label.AddComponent<TextMeshProUGUI>();
        GameManager.Instance?.UIManager?.ApplyDefaultFont(lt);
        lt.text = EquipmentSystem.SlotLabel(slot);
        lt.fontSize = Mathf.Max(12f, Screen.height / 80f);
        lt.color = new Color(0.75f, 0.78f, 0.85f, 1f);
        lt.alignment = TextAlignmentOptions.Center;
        _equipSlotLabels[slot] = lt;
    }

    private void ToggleEquipSlot(EquipSlot slot)
    {
        if (slot == EquipSlot.LeftHand || slot == EquipSlot.RightHand)
        {
            CycleWeapon();
            return;
        }
        var equip = EquipmentOf();
        if (equip == null) return;
        if (GearCatalog.TrySlotFor(equip.Get(slot) ?? "", out _))
        {
            equip.Unequip(slot);
        }
        else
        {
            // Equip the next catalog piece for this slot that isn't already worn.
            foreach (var g in GearCatalog.All)
            {
                if (g == null || g.Slot != slot) continue;
                if (equip.Get(EquipmentSystem.GearSlotOf(g.id)) == g.id) continue;
                equip.Equip(g.id);
                break;
            }
        }
        RefreshEquipment();
    }

    private void CycleActiveClass()
    {
        var unlocker = ClassUnlockerOf();
        if (unlocker == null || unlocker.UnlockedClassIds == null || unlocker.UnlockedClassIds.Count == 0)
            return;
        int idx = unlocker.UnlockedClassIds.IndexOf(unlocker.ActiveClassId);
        idx = (idx + 1) % unlocker.UnlockedClassIds.Count;
        unlocker.SetActiveClass(unlocker.UnlockedClassIds[idx]);
        RefreshClasses();
    }

    private void CycleActiveRace()
    {
        var mgr = RaceMgrOf();
        if (mgr == null) return;

        var roster = RaceDatabase.BuildDefaultRoster();
        if (roster == null || roster.Count == 0) return;

        var unlock = RaceUnlockManager.Instance;
        string activeId = mgr.ActiveRace != null ? mgr.ActiveRace.raceId : "human";

        // Walk the roster from the current race and pick the next selectable one.
        int start = -1;
        for (int i = 0; i < roster.Count; i++)
            if (roster[i] != null && string.Equals(roster[i].raceId, activeId, System.StringComparison.OrdinalIgnoreCase))
            { start = i; break; }
        if (start < 0) start = 0;

        for (int step = 1; step <= roster.Count; step++)
        {
            int i = (start + step) % roster.Count;
            var r = roster[i];
            if (r == null) continue;
            if (unlock != null && !unlock.IsUnlocked(r)) continue;

            if (mgr.SetActiveRace(r, requireStone: true, unlockIfNeeded: false))
            {
                RefreshRaces();
                return;
            }
        }
        _raceLine.text = Localization.T("No other unlocked race — collect a Ritual Stone or discover races.");
    }

    private void BuildSkillTypeBar(Transform parent)
    {
        string[] names = { "Melee", "Ranged", "Magic", "Stealth", "Crafting", "Fortitude" };
        float w = 460f;
        float bw = w / names.Length;
        for (int i = 0; i < names.Length; i++)
        {
            SkillType st = (SkillType)i;
            string name = names[i];
            var go = new GameObject("ST_" + name);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(-200f + bw * (0.5f + i), 200f);
            rt.sizeDelta = new Vector2(bw - 4f, 34f);
            var img = go.AddComponent<Image>();
            img.color = new Color(0.14f, 0.16f, 0.2f, 0.95f);
            var btn = go.AddComponent<Button>();
            btn.targetGraphic = img;
            SkillType captured = st;
            btn.onClick.AddListener(() => { _skillView = captured; _captureLine.text = ""; SetSkillList(); });

            var label = new GameObject("Label");
            label.transform.SetParent(go.transform, false);
            var lr = label.AddComponent<RectTransform>();
            lr.anchorMin = Vector2.zero;
            lr.anchorMax = Vector2.one;
            lr.offsetMin = Vector2.zero;
            lr.offsetMax = Vector2.zero;
            var lt = label.AddComponent<TextMeshProUGUI>();
            GameManager.Instance?.UIManager?.ApplyDefaultFont(lt);
            lt.text = name;
            lt.fontSize = Mathf.Max(14f, Screen.height / 64f);
            lt.color = Color.white;
            lt.alignment = TextAlignmentOptions.Center;
        }
    }

    private void ShowTab(Tab tab)
    {
        _current = tab;
        foreach (var kv in _panels)
            kv.Value.SetActive(kv.Key == tab);
        Refresh();
    }

    private GameObject MakePanel(string name)
    {
        var go = new GameObject(name);
        go.transform.SetParent(BodyRow, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        return go;
    }

    private TMP_Text MakeBodyText(Transform parent, string name, Vector2 pos, float w, float h)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = new Vector2(w, h);
        var tmp = go.AddComponent<TextMeshProUGUI>();
        GameManager.Instance?.UIManager?.ApplyDefaultFont(tmp);
        tmp.fontSize = Mathf.Max(14f, Screen.height / 48f);
        tmp.color = Color.white;
        tmp.alignment = TextAlignmentOptions.TopLeft;
        return tmp;
    }

    private void MakeButton(Transform parent, string name, string label, Vector2 pos, UnityEngine.Events.UnityAction onClick)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = new Vector2(190f, 38f);
        var img = go.AddComponent<Image>();
        img.color = new Color(0.2f, 0.25f, 0.3f, 0.95f);
        var btn = go.AddComponent<Button>();
        btn.targetGraphic = img;
        btn.onClick.AddListener(onClick);
        var l = new GameObject("Label");
        l.transform.SetParent(go.transform, false);
        var lr = l.AddComponent<RectTransform>();
        lr.anchorMin = Vector2.zero;
        lr.anchorMax = Vector2.one;
        lr.offsetMin = Vector2.zero;
        lr.offsetMax = Vector2.zero;
        var lt = l.AddComponent<TextMeshProUGUI>();
        GameManager.Instance?.UIManager?.ApplyDefaultFont(lt);
        lt.text = label;
        lt.fontSize = Mathf.Max(16f, Screen.height / 56f);
        lt.color = Color.white;
        lt.alignment = TextAlignmentOptions.Center;
    }

    protected override void Refresh()
    {
        switch (_current)
        {
            case Tab.Stats: RefreshStats(); break;
            case Tab.Skills: SetSkillList(); break;
            case Tab.Inventory: RefreshInventory(); break;
            case Tab.Equipment: RefreshEquipment(); break;
            case Tab.Class: RefreshClasses(); break;
            case Tab.Race: RefreshRaces(); break;
            case Tab.Map: RefreshMap(); break;
        }
    }

    private void RefreshStats()
    {
        var stats = PlayerStatsOf();
        if (stats == null)
        {
            _statsLine.text = Localization.T("No player stats.");
            return;
        }
        StringBuilder sb = new StringBuilder();
        for (int i = 0; i < PlayerStats.StatCount; i++)
        {
            StatType st = (StatType)i;
            sb.Append(StatNames[i]).Append("  ").Append(Mathf.RoundToInt(stats.GetTotal(st)));
            if (i != PlayerStats.StatCount - 1) sb.Append("\n");
        }
        _statsLine.text = sb.ToString();
    }

    private void SetSkillList()
    {
        var profile = SkillProfileOf();
        if (profile == null) return;
        _skillPointsLine.text = Localization.F("Skill Points: {0}", profile.Points);

        StringBuilder sb = new StringBuilder();
        int n = 0;
        foreach (var skill in SkillCatalog.OfType(_skillView))
        {
            string learned = profile.HasLearned(skill.id) ? "✔" : "  ";
            string ready = profile.CanLearn(skill) ? "[Learn]" : "";
            sb.Append(learned).Append(" ").Append(skill.displayName)
              .Append(" (").Append(skill.IsPassive ? "P" : "C").Append(")")
              .Append(" ").Append(ready);
            if (n != 9) sb.Append("\n");
            n++;
        }
        _skillListLine.text = sb.ToString();
    }

    private void LearnSelected()
    {
        var profile = SkillProfileOf();
        if (profile == null) return;

        // Learn the first not-yet-learned skill in the current view that qualifies.
        foreach (var skill in SkillCatalog.OfType(_skillView))
        {
            if (profile.CanLearn(skill))
            {
                profile.Learn(skill);
                SetSkillList();
                return;
            }
        }
        _captureLine.text = Localization.T("No learnable skill in this view.");
    }

    private void AssignNextSkillKey()
    {
        var profile = SkillProfileOf();
        var bindings = BindingsOf();
        if (profile == null || bindings == null) return;

        string pending = null;
        foreach (var skill in SkillCatalog.OfType(_skillView))
            if (profile.HasLearned(skill.id) && !skill.IsPassive) { pending = skill.id; break; }
        if (pending == null)
        {
            _captureLine.text = Localization.T("No learned castable in this view.");
            return;
        }
        bindings.BeginCapture(pending);
        _captureLine.text = Localization.F("Press a key to bind: {0}", pending);
    }

    private void RefreshInventory()
    {
        var tm = ToolManager.Instance;
        var player = GameManager.Instance?.Player;
        StringBuilder sb = new StringBuilder();
        for (int i = 0; i < 10; i++)
        {
            var slot = tm != null ? tm.PeekSlot(i) : null;
            string label = slot == null || slot.Type == null || slot.Count <= 0
                ? "(trống)"
                : Localization.ItemName(slot.Type) + " x" + slot.Count;
            sb.Append(i + 1).Append(": ").Append(label);
            if (i != 9) sb.Append("\n");
        }
        sb.Append('\n').Append(Localization.F("Tiền: {0}", player != null ? player.Money : 0L));
        _invLine.text = sb.ToString();
    }

    private void RefreshEquipment()
    {
        var equip = EquipmentOf();
        if (equip == null)
        {
            _equipSummary.text = Localization.T("No equipment system present.");
            return;
        }

        // Slot buttons: label = slot name, value = equipped gear display name.
        foreach (var slot in equip.AllSlots)
        {
            if (!_equipSlotLabels.TryGetValue(slot, out var label)) continue;
            var id = equip.Get(slot);
            var g = id != null ? GearCatalog.Find(id) : null;
            string item = g != null && !string.IsNullOrEmpty(g.displayName) ? g.displayName : (id ?? "—");
            label.text = EquipmentSystem.SlotLabel(slot) + "\n" + item;
        }

        // Weapons are tracked by CombatController, not the gear sheet.
        var combat = CombatOf();
        _equipSlotLabels.TryGetValue(EquipSlot.LeftHand, out var lh);
        _equipSlotLabels.TryGetValue(EquipSlot.RightHand, out var rh);
        if (lh != null)
            lh.text = EquipmentSystem.SlotLabel(EquipSlot.LeftHand) + "\n" + HandName(combat != null ? combat.LeftHand : null);
        if (rh != null)
            rh.text = EquipmentSystem.SlotLabel(EquipSlot.RightHand) + "\n" + HandName(combat != null ? combat.RightHand : null);

        StringBuilder sb = new StringBuilder();
        sb.Append(Localization.F("Wield: {0}", combat != null ? combat.Wielding.ToString() : "—"));
        sb.Append(Localization.F("   Equipped: {0}/21", equip.Count));
        sb.Append(Localization.F("   Weight: {0:0.0}", equip.TotalWeight)).Append('\n');
        sb.Append(Localization.F("Physical DR: {0:0.#}%", equip.TotalPhysicalDR)).Append("   ");
        string[] resTypes = { "Fire", "Ice", "Lightning", "Holy", "Dark", "Wind", "Earth", "Water", "Arcane" };
        for (int i = 0; i < resTypes.Length; i++)
        {
            float r = equip.Resistance((DamageType)(i + 1));
            if (r > 0.01f)
                sb.Append(resTypes[i]).Append(" ").Append(r.ToString("0.#")).Append("% ");
        }
        _equipSummary.text = sb.ToString();
    }

    private static string HandName(GameObject hand)
    {
        var host = hand != null ? hand.GetComponent<WeaponRigHost>() : null;
        if (host != null && host.Data != null && !string.IsNullOrEmpty(host.Data.displayName))
            return host.Data.displayName;
        return hand != null ? hand.name : "—";
    }

    private void RefreshClasses()
    {
        var unlocker = ClassUnlockerOf();
        if (unlocker == null)
        {
            _classLine.text = Localization.T("No class system present on the player.");
            return;
        }
        unlocker.EvaluateAll();
        var active = unlocker.ActiveClass;
        string activeName = active != null && !string.IsNullOrEmpty(active.displayName) ? active.displayName : "—";

        StringBuilder sb = new StringBuilder();
        sb.Append(Localization.F("Active Class: {0}", activeName)).Append('\n');
        if (unlocker.Classes != null)
        {
            int n = 0;
            foreach (var c in unlocker.Classes)
            {
                if (c == null) continue;
                string status = unlocker.IsUnlocked(c.classId) ? "✔" : "🔒";
                string isActive = string.Equals(c.classId, unlocker.ActiveClassId, System.StringComparison.OrdinalIgnoreCase) ? "★" : " ";
                string req = unlocker.IsUnlocked(c.classId) ? "" : "  (" + c.RequirementSummary() + ")";
                sb.Append(status).Append(" ").Append(isActive).Append(" ")
                    .Append(c.displayName).Append(req);
                if (n != 14) sb.Append('\n');
                n++;
            }
        }
        _classLine.text = sb.ToString();
    }

    private void RefreshRaces()
    {
        var mgr = RaceMgrOf();
        var unlock = RaceUnlockManager.Instance;
        var roster = RaceDatabase.BuildDefaultRoster();
        if (mgr == null || roster == null)
        {
            _raceLine.text = Localization.T("No race system present on the player.");
            return;
        }

        var active = mgr.ActiveRace;
        string activeName = active != null && !string.IsNullOrEmpty(active.displayName) ? active.displayName : "—";
        string activeId = active != null ? active.raceId : "human";

        StringBuilder sb = new StringBuilder();
        string stoneMark = mgr.RitualStoneCount > 0
            ? Localization.F("   (Ritual Stones: {0})", mgr.RitualStoneCount)
            : "   (no Ritual Stone)";
        sb.Append(Localization.F("Active Race: {0}{1}", activeName, stoneMark)).Append('\n');
        sb.Append(Localization.T("Stone cost: non-Human changes consume 1 Ritual Stone.")).Append('\n');

        if (active != null)
        {
            sb.Append(active.displayName).Append(" — ").Append(active.PassiveDescription).Append('\n');
            if (active.StatModifiers != null)
            {
                int n = 0;
                for (int i = 0; i < PlayerStats.StatCount; i++)
                {
                    float mod = active.GetStatModifier((StatType)i);
                    if (mod == 0f) continue;
                    if (n != 0) sb.Append(", ");
                    sb.Append(StatNames[i]).Append(" ").Append((mod > 0 ? "+" : "")).Append(mod.ToString("0")).Append("%");
                    n++;
                }
                if (n > 0) sb.Append('\n');
            }
        }

        if (roster != null)
        {
            int n = 0;
            foreach (var r in roster)
            {
                if (r == null) continue;
                string status = unlock != null && unlock.IsUnlocked(r) ? "✔" : "🔒";
                string isActive = string.Equals(r.raceId, activeId, System.StringComparison.OrdinalIgnoreCase) ? "★" : " ";
                sb.Append(status).Append(" ").Append(isActive).Append(" ").Append(r.displayName);
                if (n != 21) sb.Append('\n');
                n++;
            }
        }
        _raceLine.text = sb.ToString();
    }

    private void RefreshMap()
    {
        _mapLine.text = Localization.T("World Map — see the dedicated Map menu.\nChar Info Map is a placeholder summary.");
    }

    private void CycleWeapon()
    {
        var player = GameManager.Instance?.Player;
        var combat = CombatOf();
        if (player == null || combat == null) return;

        var all = WeaponCatalog.All;
        WeaponCatalog.EnsureBuilt();
        all = WeaponCatalog.All;
        if (all == null || all.Count == 0) return;

        int idx = 0;
        var cur = combat.RightHand != null ? combat.RightHand.GetComponent<WeaponRigHost>() : null;
        if (cur != null && cur.Data != null)
            for (int i = 0; i < all.Count; i++)
                if (all[i] != null && all[i].id == cur.Data.id) { idx = i; break; }
        idx = (idx + 1) % all.Count;

        WeaponRigBuilder.EquipInto(player.gameObject, all[idx]);
        RefreshEquipment();
    }

    private PlayerStats PlayerStatsOf()
    {
        var p = GameManager.Instance?.Player;
        return p != null ? p.GetComponent<PlayerStats>() : null;
    }

    private SkillProfile SkillProfileOf()
    {
        var p = GameManager.Instance?.Player;
        return p != null ? p.GetComponent<SkillProfile>() : null;
    }

    private SkillBindings BindingsOf()
    {
        var p = GameManager.Instance?.Player;
        return p != null ? p.GetComponent<SkillBindings>() : null;
    }

    private CombatController CombatOf()
    {
        var p = GameManager.Instance?.Player;
        return p != null ? p.GetComponent<CombatController>() : null;
    }

    private EquipmentSystem EquipmentOf()
    {
        var p = GameManager.Instance?.Player;
        return p != null ? p.GetComponent<EquipmentSystem>() : null;
    }

    private ClassUnlocker ClassUnlockerOf()
    {
        var p = GameManager.Instance?.Player;
        return p != null ? p.GetComponent<ClassUnlocker>() : null;
    }

    private RaceChangeManager RaceMgrOf()
    {
        var p = GameManager.Instance?.Player;
        return p != null ? p.GetComponent<RaceChangeManager>() : null;
    }
}
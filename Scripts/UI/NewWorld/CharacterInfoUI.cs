using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Phase 10: Character Info menu — a stacked multi-panel window with a top button bar. Each
/// top button reveals one panel and hides the others (Stats / Skills / Inventory / Equipment /
/// Map). The Skills panel shows the per-category skill tree, lets the player spend skill points
/// and assign a hotkey to a castable skill via <see cref="SkillBindings"/>.
///
/// Composes existing PlayerStats / ToolManager / EquipmentSystem / WeaponRigBuilder /
/// SkillProfile / SkillBindings / SkillCatalog without rewriting them. Built on MenuPanelBase.
/// </summary>
public sealed class CharacterInfoUI : MenuPanelBase
{
    public enum Tab { Stats = 0, Skills = 1, Inventory = 2, Equipment = 3, Map = 4 }

    public Tab ActiveTab = Tab.Stats;

    private readonly Dictionary<Tab, GameObject> _panels = new Dictionary<Tab, GameObject>();
    private Tab _current = Tab.Stats;
    private SkillType _skillView = SkillType.Melee;
    private bool _built;

    private TMP_Text _statsLine;
    private TMP_Text _skillListLine;
    private TMP_Text _skillPointsLine;
    private TMP_Text _invLine;
    private TMP_Text _equipLine;
    private TMP_Text _mapLine;
    private TMP_Text _captureLine;

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
        string[] names = { "Stats", "Skills", "Inventory", "Equipment", "Map" };
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
            rt.anchoredPosition = new Vector2(bw * (0.5f + i), -48f);
            rt.sizeDelta = new Vector2(bw - 6f, 32f);
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
            lt.fontSize = 14;
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

        // Equipment panel.
        _panels[Tab.Equipment] = MakePanel("EquipmentPanel");
        _equipLine = MakeBodyText(_panels[Tab.Equipment].transform, "Equipment", new Vector2(-200f, 10f), 460f, 300f);
        MakeButton(_panels[Tab.Equipment].transform, "CycleWeaponBtn", "Cycle Weapon", new Vector2(150f, -60f), CycleWeapon);

        // Map panel (placeholder summary; the dedicated WorldMapUI is separate).
        _panels[Tab.Map] = MakePanel("MapPanel");
        _mapLine = MakeBodyText(_panels[Tab.Map].transform, "Map", new Vector2(-200f, 10f), 460f, 300f);
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
            rt.sizeDelta = new Vector2(bw - 4f, 26f);
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
            lt.fontSize = 11;
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
        tmp.fontSize = Mathf.Max(11f, Screen.height / 62f);
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
        rt.sizeDelta = new Vector2(150f, 30f);
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
        lt.fontSize = 12;
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
        var combat = CombatOf();
        string weapon = combat != null && combat.RightHand != null
            ? EquippedName(combat.RightHand)
            : "—";
        _equipLine.text = Localization.F("Equipped Weapon: {0}\nWielding: {1}",
            weapon, combat != null ? combat.Wielding.ToString() : "—");
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

    private static string EquippedName(GameObject hand)
    {
        var host = hand != null ? hand.GetComponent<WeaponRigHost>() : null;
        if (host != null && host.Data != null && !string.IsNullOrEmpty(host.Data.displayName))
            return host.Data.displayName;
        return hand != null ? hand.name : "—";
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
}
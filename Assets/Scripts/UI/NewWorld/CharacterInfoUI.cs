using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

/// <summary>
/// Info menu — a stacked multi-panel window with a top button bar. Each top button reveals one
/// panel and hides the others (Info / Skills / Inventory / Map).
///
/// The Info panel shows the character level, XP-to-next-level with a progress bar, unspent stat
/// points (each level-up grants <see cref="LevelUpSystem.PointsPerLevel"/>), HP/FP/Stamina bars,
/// an 11-stat readout with "+" allocator buttons (only assignment, no refund), and the current
/// class / race with change buttons.
///
/// The Skills panel renders a Skyrim-style per-category skill tree: a draggable hub-and-spokes
/// layout with connecting lines derived from each skill's prerequisite DAG (<see cref="Skill.PrereqSkillIds"/>).
/// Clicking a node opens a detail pane with condition + function + cost + prerequisites and a
/// Learn button. Category levels come from <see cref="SkillXpTracker"/>.
///
/// The Inventory panel merges the old Inventory + Equipment tabs: the humanoid equipment sheet on
/// the LEFT, storage grid + mirrored hotbar row on the RIGHT, drag/drop via
/// <see cref="ItemDragHandle"/>/<see cref="ItemDropTarget"/>.
///
/// Built on MenuPanelBase. All layout coordinates in this file are declared in legacy 1280x720
/// units and multiplied by <see cref="S"/> so the enlarged panel stays uniformly proportional.
/// </summary>
public sealed class CharacterInfoUI : MenuPanelBase
{
    /// <summary>Layout multiplier (1.0 = identity; coords are final canvas units).</summary>
    private const float S = 1.0f;

    /// <summary>Last shown instance (drag & drop targets resolve it via this).</summary>
    public static CharacterInfoUI Instance;

    public enum Tab { Info = 0, Skills = 1, Inventory = 2, Map = 3 }

    public Tab ActiveTab = Tab.Info;

    private readonly Dictionary<Tab, GameObject> _panels = new Dictionary<Tab, GameObject>();
    private Tab _current = Tab.Info;
    private SkillType _skillView = SkillType.Melee;
    private bool _built;

    // Info tab widgets.
    private TMP_Text _levelText;
    private TMP_Text _pointsText;
    private Image _xpFill;
    private TMP_Text _xpLabel;
    private readonly TMP_Text[] _statValueTexts = new TMP_Text[PlayerStats.StatCount];
    private readonly Button[] _plusButtons = new Button[PlayerStats.StatCount];

    // Skills tab widgets.
    private TMP_Text _skillPointsText;
    private TMP_Text _categoryLevelText;
    private TMP_Text _equipSummary;
    private TMP_Text _mapLine;
    private TMP_Text _moneyLine;
    private TMP_Text _classLine;
    private TMP_Text _raceLine;

    // Skill tree.
    private RectTransform _treeContent;
    private readonly List<Skill> _treeSkills = new List<Skill>();
    private readonly List<(Skill skill, Image image)> _treeNodes = new List<(Skill, Image)>();
    private readonly List<(Image image, Skill target)> _treeLines = new List<(Image, Skill)>();
    private Skill _selectedSkill;
    private TMP_Text _detailTitle;
    private TMP_Text _detailDesc;
    private TMP_Text _detailMeta;
    private TMP_Text _detailLearnHint;
    private Button _learnBtn;
    private Button _assignKeyBtn;

    // Backpack storage grid (30 slots) + mirrored hotbar row (10 slots).
    private readonly Image[] _storageImgs = new Image[ToolManager.StorageSlotCount];
    private readonly TMP_Text[] _storageLabels = new TMP_Text[ToolManager.StorageSlotCount];
    private readonly Image[] _invImgs = new Image[ToolManager.HotbarSlotCount];
    private readonly TMP_Text[] _invLabels = new TMP_Text[ToolManager.HotbarSlotCount];
    private int _invSelected = -1;

    private readonly Dictionary<EquipSlot, TMP_Text> _equipSlotLabels = new Dictionary<EquipSlot, TMP_Text>();

    private static readonly Color WeaponIdleColor = new Color(0.14f, 0.16f, 0.2f, 0.95f);
    private static readonly Color WeaponSelectedColor = new Color(0.3f, 0.6f, 0.9f, 0.95f);
    private static readonly Color SlotColor = new Color(0.14f, 0.16f, 0.2f, 0.95f);
    private static readonly Color SlotSelectedColor = new Color(0.35f, 0.55f, 0.75f, 0.95f);

    // Skill tree node states.
    private static readonly Color NodeSelected = new Color(0.93f, 0.82f, 0.4f, 1f);
    private static readonly Color NodeLearned = new Color(0.3f, 0.72f, 0.42f, 1f);
    private static readonly Color NodeAvailable = new Color(0.82f, 0.6f, 0.22f, 1f);
    private static readonly Color NodeLocked = new Color(0.3f, 0.32f, 0.38f, 1f);
    private static readonly Color LineActive = new Color(0.72f, 0.68f, 0.55f, 0.9f);
    private static readonly Color LineInert = new Color(0.4f, 0.42f, 0.48f, 0.75f);

    /// <summary>Horizontal shift applied to the humanoid sheet so the backpack uses the right half.</summary>
    private const float EquipShiftX = -55f;

    /// <summary>Scaled position helper (legacy units -> enlarged layout).</summary>
    private static Vector2 P(float x, float y) => new Vector2(x * S, y * S);

    /// <summary>Scaled size helper.</summary>
    private static Vector2 Sz(float x, float y) => new Vector2(x * S, y * S);

    private static Sprite _menuButtonSprite;
    private static Sprite MenuButtonSprite()
    {
        if (_menuButtonSprite == null)
        {
            var tex = Resources.Load<Texture2D>("stats menu button");
            if (tex != null)
                _menuButtonSprite = Sprite.Create(tex,
                    new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
        }
        return _menuButtonSprite;
    }

    private static void ApplyMenuButtonSprite(Image img)
    {
        var sprite = MenuButtonSprite();
        if (sprite != null)
        {
            img.sprite = sprite;
            img.type = Image.Type.Simple;
            img.preserveAspect = false;
            img.color = Color.white;
        }
        else
        {
            img.color = new Color(0.2f, 0.2f, 0.26f, 0.95f);
        }
    }

    private const int WeaponGridCols = 3;
    private const int WeaponGridRows = 3;
    private const int WeaponGridCount = WeaponGridCols * WeaponGridRows;
    private string _selectedWeaponId;
    private readonly GameObject[] _weaponGos = new GameObject[WeaponGridCount];
    private readonly Image[] _weaponImages = new Image[WeaponGridCount];
    private readonly TMP_Text[] _weaponLabels = new TMP_Text[WeaponGridCount];
    private readonly WeaponDragHandle[] _weaponDrags = new WeaponDragHandle[WeaponGridCount];
    private readonly List<string> _weaponGridIds = new List<string>();

    // Class / Race change dialog (picker + confirmation).
    private GameObject _changeDialog;
    private TMP_Text _changeTitle;
    private Transform _changeOptions;
    private TMP_Text _changeConfirmText;
    private Button _changeConfirmBtn;
    private string _changeMode;
    private object _pendingChange;

    private static readonly string[] StatNames =
    {
        "HP", "Speed", "Endurance", "Strength", "Dexterity", "AttackSpeed",
        "Defense", "Intelligence", "Wisdom", "Faith", "Luck"
    };

    private void OnEnable()
    {
        Instance = this;
        EnsurePlayerSystems();
        if (_built) return;
        _built = true;

        SuppressTitle = true;
        Build(Localization.T("CHARACTER INFO"));
        _current = ActiveTab;

        BuildTopButtons();
        BuildPanels();
        ShowTab(_current);
    }

    /// <summary>
    /// The Info / Skills tabs read the leveling + skill systems on the player. The test ground
    /// wires skill/class/race managers, but <see cref="LevelUpSystem"/> and
    /// <see cref="SkillXpTracker"/> are not attached by any bootstrap — ensure the full set
    /// exists (idempotent) so this UI works standalone.
    /// </summary>
    private void EnsurePlayerSystems()
    {
        var player = GameManager.Instance?.Player;
        if (player == null) return;
        if (player.GetComponent<PlayerStats>() == null)
            player.gameObject.AddComponent<PlayerStats>();
        if (player.GetComponent<LevelUpSystem>() == null)
            player.gameObject.AddComponent<LevelUpSystem>();
        // SkillXpTracker before SkillProfile so the profile's Awake subscribes to level-ups.
        if (player.GetComponent<SkillXpTracker>() == null)
            player.gameObject.AddComponent<SkillXpTracker>();
        if (player.GetComponent<SkillProfile>() == null)
            player.gameObject.AddComponent<SkillProfile>();
        if (player.GetComponent<SkillBindings>() == null)
            player.gameObject.AddComponent<SkillBindings>();
        if (player.GetComponent<EquipmentSystem>() == null)
            player.gameObject.AddComponent<EquipmentSystem>();
        if (player.GetComponent<ClassUnlocker>() == null)
            player.gameObject.AddComponent<ClassUnlocker>();
        if (player.GetComponent<RaceChangeManager>() == null)
            player.gameObject.AddComponent<RaceChangeManager>();
    }

    private void OnDisable()
    {
        if (Instance == this)
            Instance = null;
    }

    private void BuildTopButtons()
    {
        string[] names = { "Info", "Skills", "Inventory", "Map" };
        float w = PanelRect.rect.width;
        float bw = w / names.Length;
        for (int i = 0; i < names.Length; i++)
        {
            Tab tab = (Tab)i;
            string name = names[i];
            var go = new GameObject("Tab_" + name);
            go.transform.SetParent(PanelRect, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 1f);
            rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(-w * 0.5f + bw * (0.5f + i), 4f);
            rt.sizeDelta = new Vector2(bw - 6f, 46f * S);
            var img = go.AddComponent<Image>();
            ApplyMenuButtonSprite(img);
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
            lt.fontSize = Mathf.Max(20f, Screen.height / 48f);
            lt.color = Color.white;
            lt.alignment = TextAlignmentOptions.Center;
        }
    }

    private void BuildPanels()
    {
        // Info panel: level/XP/points/bars + stat allocator + class/race.
        _panels[Tab.Info] = MakePanel("InfoPanel");
        BuildInfoTab(_panels[Tab.Info].transform);

        // Skills panel: draggable skill tree + detail pane.
        _panels[Tab.Skills] = MakePanel("SkillsPanel");
        BuildSkillTypeBar(_panels[Tab.Skills].transform);
        _skillPointsText = MakeBodyText(_panels[Tab.Skills].transform, "SkillPoints", P(-300f, 112f), Sz(160f, 26f));
        _categoryLevelText = MakeBodyText(_panels[Tab.Skills].transform, "CategoryLevel", P(90f, 112f), Sz(200f, 26f));
        BuildSkillTree(_panels[Tab.Skills].transform);
        BuildSkillDetail(_panels[Tab.Skills].transform);

        // Merged Inventory + Equipment panel: equipment sheet LEFT, backpack + use bar RIGHT.
        _panels[Tab.Inventory] = MakePanel("InventoryPanel");
        BuildEquipmentSheet(_panels[Tab.Inventory].transform);
        _equipSummary = MakeBodyText(_panels[Tab.Inventory].transform, "Equipment", P(-288f, 178f), Sz(560f, 44f));
        BuildWeaponLane(_panels[Tab.Inventory].transform);
        BuildStorageGrid(_panels[Tab.Inventory].transform);
        BuildHotbarMirror(_panels[Tab.Inventory].transform);
        _moneyLine = MakeBodyText(_panels[Tab.Inventory].transform, "Money", P(8f, -160f), Sz(260f, 24f));

        // Map panel (placeholder summary; the dedicated WorldMapUI is separate).
        _panels[Tab.Map] = MakePanel("MapPanel");
        _mapLine = MakeBodyText(_panels[Tab.Map].transform, "Map", P(-270f, 160f), Sz(500f, 200f));

        EnsureChangeDialog();
    }

    // ── Info tab ──────────────────────────────────────────────────────────
    // Level, unspent stat points, XP progress bar, 11-stat "+" allocator lines,
    // and class/race change controls. (HP/FP/Stamina bars live on the HUD, not here.)
    private void BuildInfoTab(Transform parent)
    {
        _levelText = MakeBodyText(parent, "Level", P(-330f, 210f), Sz(220f, 64f));
        _levelText.fontSize = Mathf.Max(34f, Screen.height / 28f);

        _xpFill = MakeBar(parent, "XpBar", P(-330f, 140f), Sz(360f, 32f),
            new Color(0.6f, 0.5f, 0.85f), out _xpLabel);

        _pointsText = MakeBodyText(parent, "StatPoints", P(-330f, 100f), Sz(240f, 32f));
        _pointsText.fontSize = Mathf.Max(20f, Screen.height / 40f);

        // Stat list with "+" allocator (right column, 11 rows).
        for (int i = 0; i < PlayerStats.StatCount; i++)
        {
            float baseY = 215f - i * 20f;

            var name = MakeBodyText(parent, "StatName_" + i, P(80f, baseY), Sz(110f, 22f));
            name.fontSize = Mathf.Max(16f, Screen.height / 50f);
            name.text = StatNames[i];

            _statValueTexts[i] = MakeBodyText(parent, "StatValue_" + i, P(195f, baseY), Sz(60f, 22f));
            _statValueTexts[i].fontSize = Mathf.Max(16f, Screen.height / 50f);
            _statValueTexts[i].alignment = TextAlignmentOptions.TopRight;

            _plusButtons[i] = MakePlusButton(parent, "Plus_" + i,
                P(265f, baseY), Sz(28f, 26f), i);
        }

        // Class / race summaries + change buttons.
        _classLine = MakeBodyText(parent, "ClassLine", P(-330f, -120f), Sz(700f, 30f));
        _classLine.fontSize = Mathf.Max(16f, Screen.height / 48f);
        _raceLine = MakeBodyText(parent, "RaceLine", P(-330f, -155f), Sz(700f, 30f));
        _raceLine.fontSize = Mathf.Max(16f, Screen.height / 48f);
        var classBtn = MakeButton(parent, "ChangeClassBtn", "Change Class", P(-120f, -185f), () => OpenChangeDialog("class"));
        classBtn.GetComponent<RectTransform>().sizeDelta = Sz(150f, 32f);
        var raceBtn = MakeButton(parent, "ChangeRaceBtn", "Change Race", P(120f, -185f), () => OpenChangeDialog("race"));
        raceBtn.GetComponent<RectTransform>().sizeDelta = Sz(150f, 32f);
    }

    private Image MakeBar(Transform parent, string name, Vector2 pos, Vector2 size, Color fillColor, out TMP_Text label)
    {
        var root = new GameObject(name);
        root.transform.SetParent(parent, false);
        var rt = root.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;

        var bg = new GameObject("Bg");
        bg.transform.SetParent(rt, false);
        var br = bg.AddComponent<RectTransform>();
        br.anchorMin = Vector2.zero;
        br.anchorMax = Vector2.one;
        br.offsetMin = Vector2.zero;
        br.offsetMax = Vector2.zero;
        var bimg = bg.AddComponent<Image>();
        bimg.color = new Color(0f, 0f, 0f, 0.65f);
        bimg.raycastTarget = false;

        var fill = new GameObject("Fill");
        fill.transform.SetParent(rt, false);
        var fr = fill.AddComponent<RectTransform>();
        fr.anchorMin = Vector2.zero;
        fr.anchorMax = new Vector2(1f, 1f);
        fr.pivot = new Vector2(0f, 0.5f);
        fr.offsetMin = new Vector2(3f, 3f);
        fr.offsetMax = new Vector2(-3f, -3f);
        var fimg = fill.AddComponent<Image>();
        fimg.type = Image.Type.Filled;
        fimg.fillMethod = Image.FillMethod.Horizontal;
        fimg.fillAmount = 1f;
        fimg.color = fillColor;
        fimg.raycastTarget = false;

        var labelGo = new GameObject("Label");
        labelGo.transform.SetParent(rt, false);
        var lr = labelGo.AddComponent<RectTransform>();
        lr.anchorMin = Vector2.zero;
        lr.anchorMax = Vector2.one;
        lr.offsetMin = Vector2.zero;
        lr.offsetMax = Vector2.zero;
        var ltmp = labelGo.AddComponent<TextMeshProUGUI>();
        GameManager.Instance?.UIManager?.ApplyDefaultFont(ltmp);
        ltmp.fontSize = Mathf.Max(13f, Screen.height / 66f);
        ltmp.color = Color.white;
        ltmp.alignment = TextAlignmentOptions.Center;
        ltmp.raycastTarget = false;

        label = ltmp;
        return fimg;
    }

    private Button MakePlusButton(Transform parent, string name, Vector2 pos, Vector2 size, int statIndex)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        var img = go.AddComponent<Image>();
        img.color = new Color(0.28f, 0.48f, 0.32f, 0.95f);
        var btn = go.AddComponent<Button>();
        btn.targetGraphic = img;
        int captured = statIndex;
        btn.onClick.AddListener(() =>
        {
            var lvl = LevelUpOf();
            if (lvl == null) return;
            if (lvl.SpendPoint((StatType)captured))
                RefreshInfo();
        });

        var label = new GameObject("Label");
        label.transform.SetParent(go.transform, false);
        var lr = label.AddComponent<RectTransform>();
        lr.anchorMin = Vector2.zero;
        lr.anchorMax = Vector2.one;
        lr.offsetMin = Vector2.zero;
        lr.offsetMax = Vector2.zero;
        var lt = label.AddComponent<TextMeshProUGUI>();
        GameManager.Instance?.UIManager?.ApplyDefaultFont(lt);
        lt.text = "+";
        lt.fontSize = Mathf.Max(17f, Screen.height / 48f);
        lt.color = Color.white;
        lt.alignment = TextAlignmentOptions.Center;
        return btn;
    }

    // ── Skill tree ────────────────────────────────────────────────────────
    private void BuildSkillTree(Transform parent)
    {
        var vp = new GameObject("TreeViewport");
        vp.transform.SetParent(parent, false);
        var vrt = vp.AddComponent<RectTransform>();
        vrt.anchorMin = new Vector2(0.5f, 0.5f);
        vrt.anchorMax = new Vector2(0.5f, 0.5f);
        vrt.pivot = new Vector2(0.5f, 0.5f);
        vrt.anchoredPosition = P(-110f, -50f);
        vrt.sizeDelta = Sz(320f, 260f);
        var vimg = vp.AddComponent<Image>();
        vimg.color = new Color(0.09f, 0.1f, 0.13f, 0.9f);
        vp.AddComponent<RectMask2D>();

        var content = new GameObject("TreeContent");
        content.transform.SetParent(vp.transform, false);
        _treeContent = content.AddComponent<RectTransform>();
        _treeContent.anchorMin = new Vector2(0.5f, 0.5f);
        _treeContent.anchorMax = new Vector2(0.5f, 0.5f);
        _treeContent.pivot = new Vector2(0.5f, 0.5f);
        _treeContent.anchoredPosition = Vector2.zero;
        _treeContent.sizeDelta = Sz(680f, 540f);

        var pan = vp.AddComponent<TreePan>();
        pan.Content = _treeContent;
    }

    private void BuildSkillDetail(Transform parent)
    {
        var pane = new GameObject("SkillDetail");
        pane.transform.SetParent(parent, false);
        var pRt = pane.AddComponent<RectTransform>();
        pRt.anchorMin = new Vector2(0.5f, 0.5f);
        pRt.anchorMax = new Vector2(0.5f, 0.5f);
        pRt.pivot = new Vector2(0.5f, 0.5f);
        pRt.anchoredPosition = P(240f, -40f);
        pRt.sizeDelta = Sz(150f, 200f);
        var pImg = pane.AddComponent<Image>();
        pImg.color = new Color(0.12f, 0.13f, 0.16f, 0.9f);

        _detailTitle = MakeBodyText(pane.transform, "DetailTitle", P(-70f, 90f), Sz(140f, 28f));
        _detailTitle.fontSize = Mathf.Max(18f, Screen.height / 42f);
        _detailTitle.alignment = TextAlignmentOptions.Center;

        _detailDesc = MakeBodyText(pane.transform, "DetailDesc", P(-70f, 60f), Sz(138f, 70f));
        _detailDesc.enableWordWrapping = true;

        _detailMeta = MakeBodyText(pane.transform, "DetailMeta", P(-70f, -10f), Sz(138f, 70f));

        _detailLearnHint = MakeBodyText(pane.transform, "DetailHint", P(-70f, -78f), Sz(138f, 22f));
        _detailLearnHint.fontSize = Mathf.Max(12f, Screen.height / 72f);
        _detailLearnHint.enableWordWrapping = true;

        var learn = MakeButton(pane.transform, "LearnBtn", "Learn", P(-72f, -108f), LearnSelectedSkill);
        learn.GetComponent<RectTransform>().sizeDelta = Sz(120f, 30f);
        _learnBtn = learn;

        var assign = MakeButton(pane.transform, "AssignKeyBtn", "Bind Key", P(52f, -108f), AssignSelectedSkillKey);
        assign.GetComponent<RectTransform>().sizeDelta = Sz(120f, 30f);
        _assignKeyBtn = assign;
    }

    private void RefreshSkillTree()
    {
        var profile = SkillProfileOf();
        bool hasPoints = profile != null && profile.Points > 0;

        // Node colors by state.
        foreach (var (skill, image) in _treeNodes)
        {
            if (image == null) continue;
            if (skill == _selectedSkill) image.color = NodeSelected;
            else if (profile != null && profile.HasLearned(skill.id)) image.color = NodeLearned;
            else if (profile != null && profile.CanLearn(skill)) image.color = NodeAvailable;
            else image.color = NodeLocked;
        }

        // Lines: active when the target (child) or any prereq is learned.
        for (int i = 0; i < _treeLines.Count; i++)
        {
            var (line, target) = _treeLines[i];
            if (line == null) continue;
            bool active = profile != null && profile.HasLearned(target.id);
            if (!active && target.PrereqSkillIds != null && profile != null)
                foreach (var pid in target.PrereqSkillIds)
                    if (profile.HasLearned(pid)) { active = true; break; }
            line.color = active ? LineActive : LineInert;
        }

        if (_skillPointsText != null)
            _skillPointsText.text = profile != null
                ? Localization.F("Skill Points: {0}", profile.Points)
                : "";

        var xp = SkillXpOf();
        if (_categoryLevelText != null)
            _categoryLevelText.text = xp != null
                ? Localization.F("{0} Lv {1}", _skillView, xp.GetLevel(_skillView))
                : _skillView.ToString();

        RefreshSkillDetail();
        if (_learnBtn != null)
            _learnBtn.interactable = hasPoints && _selectedSkill != null &&
                profile != null && profile.CanLearn(_selectedSkill);
        if (_assignKeyBtn != null)
            _assignKeyBtn.interactable = CanBindSelectedSkill();
    }

    private void RefreshSkillDetail()
    {
        if (_detailTitle == null) return;
        var skill = _selectedSkill;
        if (skill == null)
        {
            _detailTitle.text = Localization.T("No skill selected");
            _detailDesc.text = "";
            _detailMeta.text = "";
            _detailLearnHint.text = "";
            return;
        }
        _detailTitle.text = skill.displayName;
        _detailDesc.text = skill.description;

        var meta = new StringBuilder();
        meta.Append(skill.IsPassive ? Localization.T("Type: Passive") : Localization.T("Type: Castable"));
        meta.Append('\n');

        if (!skill.IsPassive)
        {
            meta.Append("Cost: ");
            if (skill.SkillCost.Resource == ResourceKind.None) meta.Append("Free");
            else meta.Append(skill.SkillCost.Resource.ToString()).Append(' ').Append(skill.SkillCost.Amount.ToString("0.##"));
            if (skill.SkillCost.Cooldown > 0f)
                meta.Append('\n').Append("CD: ").Append(skill.SkillCost.Cooldown.ToString("0.#")).Append('s');
        }
        else
        {
            meta.Append(Localization.T("Cost: Free (always-on)"));
        }

        if (skill.PrereqSkillIds != null && skill.PrereqSkillIds.Length > 0)
        {
            meta.Append('\n').Append("Requires: ");
            for (int i = 0; i < skill.PrereqSkillIds.Length; i++)
            {
                var pre = SkillCatalog.Find(skill.PrereqSkillIds[i]);
                meta.Append(pre != null ? pre.displayName : skill.PrereqSkillIds[i]);
                if (i != skill.PrereqSkillIds.Length - 1) meta.Append(", ");
            }
        }
        _detailMeta.text = meta.ToString();

        var profile = SkillProfileOf();
        if (profile != null && profile.HasLearned(skill.id))
            _detailLearnHint.text = Localization.T("✔ Learned");
        else if (profile != null && !profile.CanLearn(skill))
            _detailLearnHint.text = Localization.T("Learnable (spend 1 pt)");
        else
            _detailLearnHint.text = "";
    }

    private bool CanBindSelectedSkill()
    {
        if (_selectedSkill == null || _selectedSkill.IsPassive) return false;
        var profile = SkillProfileOf();
        return profile != null && profile.HasLearned(_selectedSkill.id);
    }

    private void LearnSelectedSkill()
    {
        var profile = SkillProfileOf();
        if (_selectedSkill == null || profile == null) return;
        if (profile.Learn(_selectedSkill))
        {
            RefreshSkillTree();
        }
        else
        {
            _detailLearnHint.text = Localization.T("Cannot learn — prerequisites or points missing.");
        }
    }

    private void AssignSelectedSkillKey()
    {
        var bindings = BindingsOf();
        if (_selectedSkill == null || bindings == null) return;
        if (!CanBindSelectedSkill())
        {
            if (_detailLearnHint != null)
                _detailLearnHint.text = Localization.T("Select a learned castable to bind.");
            return;
        }
        bindings.BeginCapture(_selectedSkill.id);
        if (_detailLearnHint != null)
            _detailLearnHint.text = Localization.F("Press a key to bind: {0}", _selectedSkill.displayName);
    }

    private void RebuildSkillTree()
    {
        if (_treeContent == null) return;

        for (int i = _treeContent.childCount - 1; i >= 0; i--)
            Destroy(_treeContent.GetChild(i).gameObject);
        _treeNodes.Clear();
        _treeLines.Clear();
        _treeSkills.Clear();
        _selectedSkill = null;
        _treeContent.anchoredPosition = Vector2.zero;

        var list = new List<Skill>();
        foreach (var s in SkillCatalog.OfType(_skillView))
            if (s != null) list.Add(s);
        if (list.Count == 0) return;

        // Depth = longest prerequisite chain.
        var depth = new Dictionary<string, int>();
        bool changed;
        do
        {
            changed = false;
            foreach (var s in list)
            {
                int d = 0;
                if (s.PrereqSkillIds != null)
                    foreach (var pid in s.PrereqSkillIds)
                    {
                        if (depth.TryGetValue(pid, out int pd))
                            d = Mathf.Max(d, pd + 1);
                    }
                if (!depth.TryGetValue(s.id, out int cur) || cur != d)
                {
                    depth[s.id] = d;
                    changed = true;
                }
            }
        } while (changed);

        // Nodes per depth-tier.
        var byDepth = new Dictionary<int, List<Skill>>();
        foreach (var s in list)
        {
            int d = depth.TryGetValue(s.id, out int dv) ? dv : 0;
            if (!byDepth.TryGetValue(d, out var bucket))
            {
                bucket = new List<Skill>();
                byDepth[d] = bucket;
            }
            bucket.Add(s);
        }

        // Radial hub & spokes: tier 0 = inner hub ring, deeper tiers radiate outward.
        float hubRadius = 78f * S;
        float spokeStep = 118f * S;
        var posOf = new Dictionary<string, Vector2>();
        var angleOf = new Dictionary<string, float>();

        var depths = new List<int>(byDepth.Keys);
        depths.Sort();
        foreach (int d in depths)
        {
            var bucket = byDepth[d];
            bool allRoot = d == 0;
            for (int i = 0; i < bucket.Count; i++)
            {
                var s = bucket[i];
                float angle;
                if (allRoot)
                {
                    angle = -90f + (i / Mathf.Max(1, bucket.Count)) * 360f;
                    angleOf[s.id] = angle;
                }
                else
                {
                    // Mean angle of prerequisites, fanning out siblings slightly.
                    float sx = 0f, sy = 0f; int cnt = 0;
                    if (s.PrereqSkillIds != null)
                        foreach (var pid in s.PrereqSkillIds)
                            if (angleOf.TryGetValue(pid, out float pa))
                            {
                                float paRad = pa * Mathf.Deg2Rad;
                                sx += Mathf.Cos(paRad); sy += Mathf.Sin(paRad); cnt++;
                            }
                    if (cnt == 0) { angle = -90f; }
                    else angle = Mathf.Atan2(sy / cnt, sx / cnt) * Mathf.Rad2Deg;
                    angle += (i % 2 == 0 ? -1f : 1f) * 18f * (i / 2);
                    angleOf[s.id] = angle;
                }
                float radius = hubRadius + d * spokeStep;
                float rad = angle * Mathf.Deg2Rad;
                posOf[s.id] = new Vector2(Mathf.Cos(rad) * radius, Mathf.Sin(rad) * radius);
                _treeSkills.Add(s);
            }
        }

        // Connection lines (prereq -> child).
        float thick = 3f * S;
        foreach (var s in list)
        {
            if (s.PrereqSkillIds == null) continue;
            if (!posOf.TryGetValue(s.id, out Vector2 end)) continue;
            foreach (var pid in s.PrereqSkillIds)
            {
                if (!posOf.TryGetValue(pid, out Vector2 start)) continue;
                var line = MakeTreeLine(start, end, thick);
                _treeLines.Add((line, s));
            }
        }

        // Nodes.
        foreach (var s in list)
        {
            if (!posOf.TryGetValue(s.id, out Vector2 pos)) continue;
            var node = MakeTreeNode(s, pos);
            _treeNodes.Add((s, node));
        }

        RefreshSkillTree();
    }

    private Image MakeTreeNode(Skill skill, Vector2 pos)
    {
        var go = new GameObject("Node_" + skill.id);
        go.transform.SetParent(_treeContent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = Sz(64f, 44f);
        var img = go.AddComponent<Image>();
        img.color = NodeLocked;
        var btn = go.AddComponent<Button>();
        btn.targetGraphic = img;
        Skill captured = skill;
        btn.onClick.AddListener(() =>
        {
            _selectedSkill = captured;
            RefreshSkillTree();
        });

        var label = new GameObject("Label");
        label.transform.SetParent(go.transform, false);
        var lr = label.AddComponent<RectTransform>();
        lr.anchorMin = Vector2.zero;
        lr.anchorMax = Vector2.one;
        lr.offsetMin = new Vector2(2f, 1f);
        lr.offsetMax = new Vector2(-2f, -1f);
        var tmp = label.AddComponent<TextMeshProUGUI>();
        GameManager.Instance?.UIManager?.ApplyDefaultFont(tmp);
        tmp.text = skill.displayName;
        tmp.fontSize = Mathf.Max(10f, Screen.height / 128f);
        tmp.color = Color.white;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.enableWordWrapping = true;
        return img;
    }

    private Image MakeTreeLine(Vector2 start, Vector2 end, float thick)
    {
        var go = new GameObject("Line");
        go.transform.SetParent(_treeContent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        Vector2 mid = (start + end) * 0.5f;
        float len = Vector2.Distance(start, end);
        rt.anchoredPosition = mid;
        rt.sizeDelta = new Vector2(Mathf.Max(1f, len), thick);
        float angle = Mathf.Atan2(end.y - start.y, end.x - start.x) * Mathf.Rad2Deg;
        rt.localRotation = Quaternion.Euler(0f, 0f, angle);
        var img = go.AddComponent<Image>();
        img.color = LineInert;
        img.raycastTarget = false;
        return img;
    }

    /// <summary>Drag handler for the tree viewport — pans <see cref="Content"/> within the mask.</summary>
    private sealed class TreePan : MonoBehaviour, IPointerDownHandler, IDragHandler
    {
        public RectTransform Content;
        private Vector2 _startPointer;
        private Vector2 _startPos;

        public void OnPointerDown(PointerEventData e)
        {
            if (Content == null) return;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                (RectTransform)transform, e.position, e.pressEventCamera, out _startPointer);
            _startPos = Content.anchoredPosition;
        }

        public void OnDrag(PointerEventData e)
        {
            if (Content == null) return;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                (RectTransform)transform, e.position, e.pressEventCamera, out Vector2 current))
                return;
            Content.anchoredPosition = _startPos + (current - _startPointer);
        }
    }

    // ── Backpack storage grid (Inventory tab, right side) ───────────────────
    private void BuildStorageGrid(Transform parent)
    {
        MakeBodyText(parent, "StorageHeader", P(8f, 178f), Sz(280f, 28f))
            .text = Localization.T("Backpack (storage)");

        for (int i = 0; i < ToolManager.StorageSlotCount; i++)
        {
            int col = i % 5;
            int row = i / 5;
            int slot = ToolManager.StorageStart + i;

            var go = new GameObject("StorageSlot_" + i);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2((8f + col * 52f) * S, (146f - row * 50f) * S);
            rt.sizeDelta = Sz(46f, 46f);
            var img = go.AddComponent<Image>();
            img.color = SlotColor;
            go.AddComponent<ItemDragHandle>().Slot = slot;
            go.AddComponent<ItemDropTarget>().Slot = slot;

            var label = new GameObject("Label");
            label.transform.SetParent(go.transform, false);
            var lr = label.AddComponent<RectTransform>();
            lr.anchorMin = Vector2.zero;
            lr.anchorMax = Vector2.one;
            lr.offsetMin = Vector2.zero;
            lr.offsetMax = Vector2.zero;
            var tmp = label.AddComponent<TextMeshProUGUI>();
            GameManager.Instance?.UIManager?.ApplyDefaultFont(tmp);
            tmp.fontSize = Mathf.Max(9f, Screen.height / 96f);
            tmp.color = Color.white;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.text = (i + 1).ToString();

            _storageImgs[i] = img;
            _storageLabels[i] = tmp;
        }
    }

    // ── Hotbar mirror (Inventory tab, bottom-right of the grid) ─────────────
    private void BuildHotbarMirror(Transform parent)
    {
        MakeBodyText(parent, "UseBarHeader", P(8f, -182f), Sz(290f, 24f))
            .text = Localization.T("Use bar (1-0)");

        for (int i = 0; i < ToolManager.HotbarSlotCount; i++)
        {
            var go = new GameObject("HudInvSlot_" + i);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2((8f + i * 30f) * S, -206f * S);
            rt.sizeDelta = Sz(28f, 26f);
            var img = go.AddComponent<Image>();
            img.color = SlotColor;
            var btn = go.AddComponent<Button>();
            btn.targetGraphic = img;
            int captured = i;
            btn.onClick.AddListener(() => SelectInventorySlot(captured));
            go.AddComponent<ItemDragHandle>().Slot = i;
            go.AddComponent<ItemDropTarget>().Slot = i;

            var label = new GameObject("Label");
            label.transform.SetParent(go.transform, false);
            var lr = label.AddComponent<RectTransform>();
            lr.anchorMin = Vector2.zero;
            lr.anchorMax = Vector2.one;
            lr.offsetMin = Vector2.zero;
            lr.offsetMax = Vector2.zero;
            var tmp = label.AddComponent<TextMeshProUGUI>();
            GameManager.Instance?.UIManager?.ApplyDefaultFont(tmp);
            tmp.fontSize = Mathf.Max(9f, Screen.height / 100f);
            tmp.color = Color.white;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.text = (i % 10 == 0 ? "10" : i.ToString());

            _invImgs[i] = img;
            _invLabels[i] = tmp;
        }
    }

    private void SelectInventorySlot(int index)
    {
        _invSelected = index;
        ToolManager.Instance?.SelectSlot(index);
        RefreshInventory();
    }

    // ── Owned-weapon lane (Inventory tab, bottom-left) ──────────────────────
    private void BuildWeaponLane(Transform parent)
    {
        MakeBodyText(parent, "WeaponsHeader", P(-288f, -118f), Sz(300f, 24f))
            .text = Localization.T("Weapons (drag onto a hand)");

        for (int i = 0; i < WeaponGridCount; i++)
        {
            int col = i % WeaponGridCols;
            int row = i / WeaponGridCols;

            var go = new GameObject("Weapon_" + i);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2((-288f + col * 100f) * S, (-146f - row * 32f) * S);
            rt.sizeDelta = Sz(94f, 26f);
            var img = go.AddComponent<Image>();
            img.color = WeaponIdleColor;
            var btn = go.AddComponent<Button>();
            btn.targetGraphic = img;
            int captured = i;
            btn.onClick.AddListener(() => SelectOwnedWeaponAt(captured));

            var label = new GameObject("Label");
            label.transform.SetParent(go.transform, false);
            var lr = label.AddComponent<RectTransform>();
            lr.anchorMin = Vector2.zero;
            lr.anchorMax = Vector2.one;
            lr.offsetMin = Vector2.zero;
            lr.offsetMax = Vector2.zero;
            var tmp = label.AddComponent<TextMeshProUGUI>();
            GameManager.Instance?.UIManager?.ApplyDefaultFont(tmp);
            tmp.fontSize = Mathf.Max(11f, Screen.height / 80f);
            tmp.color = Color.white;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.text = "";

            var drag = go.AddComponent<WeaponDragHandle>();

            _weaponGos[i] = go;
            _weaponImages[i] = img;
            _weaponLabels[i] = tmp;
            _weaponDrags[i] = drag;
            go.SetActive(false);
        }
    }

    private void RefreshWeapons()
    {
        var player = GameManager.Instance?.Player;
        var inv = player != null ? player.GetComponent<WeaponInventory>() : null;
        _weaponGridIds.Clear();
        if (inv != null)
            _weaponGridIds.AddRange(inv.Owned);
        if (_weaponGridIds.Count == 0 || !_weaponGridIds.Contains(WeaponCatalog.StarterWeaponId))
            _weaponGridIds.Insert(0, WeaponCatalog.StarterWeaponId);

        for (int i = 0; i < WeaponGridCount; i++)
        {
            bool has = i < _weaponGridIds.Count;
            if (_weaponGos[i] == null) continue;
            _weaponGos[i].SetActive(has);
            if (!has) continue;

            string id = _weaponGridIds[i];
            var weapon = WeaponCatalog.Find(id);
            string name = weapon != null && !string.IsNullOrEmpty(weapon.displayName) ? weapon.displayName : id;
            _weaponLabels[i].text = name;
            _weaponDrags[i].WeaponId = id;
            _weaponImages[i].color = id == _selectedWeaponId ? WeaponSelectedColor : WeaponIdleColor;
        }
    }

    private void SelectOwnedWeaponAt(int index)
    {
        if (index < 0 || index >= _weaponGridIds.Count) return;
        _selectedWeaponId = _weaponGridIds[index];
        RefreshWeapons();
    }

    public void EquipWeaponFromDrop(string weaponId, EquipSlot slot)
    {
        if (string.IsNullOrEmpty(weaponId)) return;
        _selectedWeaponId = weaponId;
        EquipOwnedWeapon(weaponId, slot);
    }

    public void OnDragDropEnded()
    {
        RefreshWeapons();
    }

    public void OnItemDragEnded()
    {
        RefreshInventoryUi();
    }

    /// <summary>Refresh all inventory-side displays (storage grid, use bar, weapons, sheet).</summary>
    public void RefreshInventoryUi()
    {
        RefreshInventory();
        RefreshWeapons();
        RefreshEquipment();
    }

    private void EquipOwnedWeapon(string weaponId, EquipSlot slot)
    {
        var player = GameManager.Instance?.Player;
        var combat = CombatOf();
        var inv = player != null ? player.GetComponent<WeaponInventory>() : null;
        if (player == null || combat == null) return;
        if (inv != null && !inv.Has(weaponId)) return;

        var weapon = WeaponCatalog.Find(weaponId);
        if (weapon == null) return;

        var wielding = WeaponRigBuilder.WieldingFor(weapon);
        if (slot == EquipSlot.LeftHand && wielding == CombatController.WieldingState.Single)
        {
            var leftRig = WeaponRigBuilder.EquipInto(player.gameObject, weapon);
            if (leftRig == null) return;
            combat.LeftHand = leftRig;
            combat.RightHand = null;
            combat.Wielding = CombatController.WieldingState.Single;
        }
        else
        {
            WeaponRigBuilder.EquipInto(player.gameObject, weapon);
        }

        _selectedWeaponId = "";
        RefreshEquipment();
        RefreshWeapons();
    }

    // ── Humanoid 21-slot equipment sheet (§5.4) ────────────────────────────
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
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2((pos.x + EquipShiftX) * S, (pos.y + 90f) * S);
        rt.sizeDelta = Sz(84f, 30f);
        var img = go.AddComponent<Image>();
        img.color = new Color(0.14f, 0.16f, 0.2f, 0.95f);
        var btn = go.AddComponent<Button>();
        btn.targetGraphic = img;
        EquipSlot captured = slot;
        btn.onClick.AddListener(() => ToggleEquipSlot(captured));

        if (slot == EquipSlot.LeftHand || slot == EquipSlot.RightHand)
        {
            var drop = go.AddComponent<WeaponDropTarget>();
            drop.Slot = slot;
        }

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
            if (!string.IsNullOrEmpty(_selectedWeaponId))
                EquipOwnedWeapon(_selectedWeaponId, slot);
            else
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
        var menuTex = Resources.Load<Texture2D>("menu");
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

    /// <summary>Trivial helper so the ESC handler in this file can close the dialog.</summary>
    private sealed class MenuPanelHook : MonoBehaviour
    {
        private void Update()
        {
            if (CharacterInfoUI.Instance == null) return;
            if (UnityEngine.InputSystem.Keyboard.current != null &&
                UnityEngine.InputSystem.Keyboard.current.escapeKey.wasPressedThisFrame)
                CharacterInfoUI.Instance.CloseChangeDialog(false);
        }
    }

    private TMP_Text MakeDialogText(Transform parent, string name, Vector2 pos, Vector2 size, TextAlignmentOptions align)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        var tmp = go.AddComponent<TextMeshProUGUI>();
        GameManager.Instance?.UIManager?.ApplyDefaultFont(tmp);
        tmp.color = Color.white;
        tmp.alignment = align;
        return tmp;
    }

    private Button MakeDialogButton(Transform parent, string name, string label, Vector2 pos, UnityEngine.Events.UnityAction onClick)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = Sz(160f, 34f);
        var img = go.AddComponent<Image>();
        ApplyMenuButtonSprite(img);
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
        lt.fontSize = Mathf.Max(15f, Screen.height / 58f);
        lt.color = Color.white;
        lt.alignment = TextAlignmentOptions.Center;
        return btn;
    }

    private void MakeDialogOption(Transform parent, string label, Vector2 pos, float w, bool enabled, UnityEngine.Events.UnityAction onClick)
    {
        var go = new GameObject("Option");
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = new Vector2(w * S, 26f * S);
        var img = go.AddComponent<Image>();
        img.color = enabled ? new Color(0.16f, 0.42f, 0.62f, 0.95f) : new Color(0.14f, 0.14f, 0.18f, 0.95f);
        var btn = go.AddComponent<Button>();
        btn.targetGraphic = img;
        btn.interactable = enabled;
        if (enabled)
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
        lt.fontSize = Mathf.Max(11f, Screen.height / 80f);
        lt.color = enabled ? Color.white : new Color(0.6f, 0.6f, 0.62f, 1f);
        lt.alignment = TextAlignmentOptions.Center;
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

    private void BuildClassOptions(Transform parent)
    {
        var unlocker = ClassUnlockerOf();
        if (unlocker == null || unlocker.Classes == null) return;

        var list = new List<object>();
        foreach (var c in unlocker.Classes)
            if (c != null) list.Add(c);

        for (int i = 0; i < list.Count; i++)
        {
            int col = i % 2;
            int row = i / 2;
            float x = col == 0 ? -270f : 10f;
            float y = 150f - row * 28f;
            var c = list[i] as ClassData;
            if (c == null) continue;
            string name = !string.IsNullOrEmpty(c.displayName) ? c.displayName : c.classId;
            string req = unlocker.IsUnlocked(c.classId) ? "" : "  (" + c.RequirementSummary() + ")";
            MakeDialogOption(parent, name + req, P(x, y), 270f, unlocker.IsUnlocked(c.classId), () =>
            {
                _pendingChange = c.classId;
                _changeConfirmText.text = Localization.F("Change class to {0}?", name);
                UpdateConfirmEnabled();
            });
        }
    }

    private void BuildRaceOptions(Transform parent)
    {
        var unlock = RaceUnlockManager.Instance;
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
            bool unlocked = unlock == null || unlock.IsUnlocked(r);
            bool costsStone = !string.Equals(r.raceId, "human", System.StringComparison.OrdinalIgnoreCase);
            string cost = costsStone ? "  (1 Ritual Stone)" : "";
            MakeDialogOption(parent, r.displayName + cost, P(x, y), 270f, unlocked, () =>
            {
                _pendingChange = r;
                _changeConfirmText.text = Localization.F("Change race to {0}?{1}", r.displayName, costsStone ? "  Cost: 1 Ritual Stone." : "");
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
        else if (_changeMode == "race" && _pendingChange is RaceData race)
        {
            var mgr = RaceMgrOf();
            if (mgr != null)
            {
                if (!mgr.SetActiveRace(race, requireStone: true, unlockIfNeeded: false))
                {
                    if (_changeConfirmText != null)
                        _changeConfirmText.text = Localization.T("Need a Ritual Stone to change race.");
                    return;
                }
            }
        }

        CloseChangeDialog(false);
        if (_current == Tab.Info)
            RefreshInfo();
        else
            RefreshInventoryUi();
    }

    private void BuildSkillTypeBar(Transform parent)
    {
        string[] names = { "Melee", "Ranged", "Magic", "Stealth", "Crafting", "Fortitude" };
        float w = 300f * S;
        float bw = w / names.Length;
        for (int i = 0; i < names.Length; i++)
        {
            SkillType st = (SkillType)i;
            string name = names[i];
            var go = new GameObject("ST_" + name);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(-150f * S + bw * (0.5f + i), 176f * S);
            rt.sizeDelta = new Vector2(bw - 4f * S, 34f * S);
            var img = go.AddComponent<Image>();
            img.color = new Color(0.14f, 0.16f, 0.2f, 0.95f);
            var btn = go.AddComponent<Button>();
            btn.targetGraphic = img;
            SkillType captured = st;
            btn.onClick.AddListener(() => { _skillView = captured; RebuildSkillTree(); });

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
            lt.fontSize = Mathf.Max(15f, Screen.height / 60f);
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

    private TMP_Text MakeBodyText(Transform parent, string name, Vector2 pos, Vector2 size)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        var tmp = go.AddComponent<TextMeshProUGUI>();
        GameManager.Instance?.UIManager?.ApplyDefaultFont(tmp);
        tmp.fontSize = Mathf.Max(14f, Screen.height / 48f);
        tmp.color = Color.white;
        tmp.alignment = TextAlignmentOptions.TopLeft;
        return tmp;
    }

    private Button MakeButton(Transform parent, string name, string label, Vector2 pos, UnityEngine.Events.UnityAction onClick)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = Sz(160f, 38f);
        var img = go.AddComponent<Image>();
        ApplyMenuButtonSprite(img);
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
        lt.fontSize = Mathf.Max(15f, Screen.height / 52f);
        lt.color = Color.white;
        lt.alignment = TextAlignmentOptions.Center;
        return btn;
    }

    protected override void Refresh()
    {
        switch (_current)
        {
            case Tab.Info: RefreshInfo(); break;
            case Tab.Skills: RefreshSkillTree(); break;
            case Tab.Inventory: RefreshInventoryUi(); break;
            case Tab.Map: RefreshMap(); break;
        }
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
                _plusButtons[i].interactable = canSpend;
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

    private void RefreshInventory()
    {
        var tm = ToolManager.Instance;
        var player = GameManager.Instance?.Player;
        int selected = tm != null ? tm.SelectedSlotIndex : -1;

        for (int i = 0; i < ToolManager.StorageSlotCount; i++)
        {
            var slot = tm != null ? tm.PeekSlot(ToolManager.StorageStart + i) : null;
            string body = slot == null || slot.Type == null || slot.Count <= 0
                ? ""
                : Localization.ItemName(slot.Type) + " x" + slot.Count;
            if (_storageLabels[i] != null)
                _storageLabels[i].text = string.IsNullOrEmpty(body) ? (i + 1).ToString() : (i + 1) + " " + body;
            if (_storageImgs[i] != null)
                _storageImgs[i].color = SlotColor;
        }

        for (int i = 0; i < ToolManager.HotbarSlotCount; i++)
        {
            var slot = tm != null ? tm.PeekSlot(i) : null;
            string body = slot == null || slot.Type == null || slot.Count <= 0
                ? ""
                : Localization.ItemName(slot.Type) + " x" + slot.Count;
            if (_invLabels[i] != null)
                _invLabels[i].text = string.IsNullOrEmpty(body)
                    ? (i + 1).ToString()
                    : (i + 1) + "\n" + body;
            if (_invImgs[i] != null)
                _invImgs[i].color = selected == i || _invSelected == i
                    ? SlotSelectedColor
                    : SlotColor;
        }
        if (_moneyLine != null)
            _moneyLine.text = Localization.F("Tiền: {0}", player != null ? player.Money : 0L);
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

    private void RefreshMap()
    {
        _mapLine.text = Localization.T("World Map — see the dedicated Map menu.\nChar Info Map is a placeholder summary.");
    }

    private void CycleWeapon()
    {
        var player = GameManager.Instance?.Player;
        var combat = CombatOf();
        var inv = player != null ? player.GetComponent<WeaponInventory>() : null;
        if (player == null || combat == null) return;

        WeaponCatalog.EnsureBuilt();
        var all = WeaponCatalog.All;
        if (all == null || all.Count == 0) return;

        var owned = new List<string>();
        if (inv != null) owned.AddRange(inv.Owned);
        if (owned.Count == 0 || !owned.Contains(WeaponCatalog.StarterWeaponId))
            owned.Insert(0, WeaponCatalog.StarterWeaponId);

        int idx = 0;
        var cur = combat.RightHand != null ? combat.RightHand.GetComponent<WeaponRigHost>() : null;
        if (cur != null && cur.Data != null)
            for (int i = 0; i < owned.Count; i++)
            {
                var target = WeaponCatalog.Find(owned[i]);
                if (target != null && target.id == cur.Data.id) { idx = i; break; }
            }
        idx = (idx + 1) % owned.Count;

        var next = WeaponCatalog.Find(owned[idx]);
        if (next != null)
            WeaponRigBuilder.EquipInto(player.gameObject, next);
        RefreshEquipment();
    }

    private PlayerStats PlayerStatsOf()
    {
        var p = GameManager.Instance?.Player;
        return p != null ? p.GetComponent<PlayerStats>() : null;
    }

    private LevelUpSystem LevelUpOf()
    {
        var p = GameManager.Instance?.Player;
        return p != null ? p.GetComponent<LevelUpSystem>() : null;
    }

    private SkillProfile SkillProfileOf()
    {
        var p = GameManager.Instance?.Player;
        return p != null ? p.GetComponent<SkillProfile>() : null;
    }

    private SkillXpTracker SkillXpOf()
    {
        var p = GameManager.Instance?.Player;
        return p != null ? p.GetComponent<SkillXpTracker>() : null;
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
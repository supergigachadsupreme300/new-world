using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using TMPro;

/// <summary>
/// Info menu — a stacked multi-panel window with a top button bar. Each top button reveals one
/// panel and hides the others (Info / Skills / Inventory / Map).
///
/// The Info panel shows the character level, XP-to-next-level with a progress bar, unspent stat
/// points (each level-up grants <see cref="LevelUpSystem.PointsPerLevel"/>), HP/FP/Stamina bars,
/// an 11-stat readout with "+" allocator buttons (only assignment, no refund), and the current
/// class / race with change buttons. Below that stat/level block the same tab lists the talents
/// (<see cref="TalentCatalog"/>) with a free *Rank Up* button per talent (no talent-point currency).
///
/// The Skills panel renders ONE combined skill tree: all <see cref="SkillCatalog.All"/> skills on a
    /// single large pannable + zoomable board, grouped into six colored sectors (one per
    /// <see cref="SkillType"/>), hub-and-spokes layout with connecting lines from each skill's
    /// prerequisite DAG (<see cref="Skill.PrereqSkillIds"/>). Clicking a node opens a detail pane with
    /// condition + function + cost + prerequisites and a Learn button. A legend chip + sector label per
    /// category shows that skill line's current level from <see cref="SkillXpTracker"/>.
///
/// The Inventory panel merges the old Inventory + Equipment tabs: the humanoid equipment sheet on
/// the LEFT, storage grid + mirrored hotbar row on the RIGHT, drag/drop via
/// <see cref="ItemDragHandle"/>/<see cref="ItemDropTarget"/>.
///
/// Built on MenuPanelBase. All layout coordinates in this file are declared in legacy 1280x720
/// units and multiplied by <see cref="S"/> so the enlarged panel stays uniformly proportional.
/// </summary>
public sealed partial class CharacterInfoUI : MenuPanelBase
{
    /// <summary>Layout multiplier (1.0 = identity; coords are final canvas units).</summary>
    private const float S = 1.0f;

    /// <summary>Last shown instance (drag & drop targets resolve it via this).</summary>
    public static CharacterInfoUI Instance;

    public enum Tab { Info = 0, Skills = 1, Inventory = 2, Map = 3, Faith = 4 }
    public enum SkillSubTab { General = 0, Class = 1, Race = 2 }

    public Tab ActiveTab = Tab.Info;

    private readonly Dictionary<Tab, GameObject> _panels = new Dictionary<Tab, GameObject>();
    private readonly List<RectTransform> _tabButtonRects = new List<RectTransform>();
    private Tab _current = Tab.Info;
    private bool _built;

    // Info tab widgets.
    private TMP_Text _levelText;
    private TMP_Text _pointsText;
    private Image _xpFill;
    private TMP_Text _xpLabel;
    private readonly TMP_Text[] _statValueTexts = new TMP_Text[PlayerStats.StatCount];
    private readonly Button[] _plusButtons = new Button[PlayerStats.StatCount];
    private readonly TMP_InputField[] _statEditFields = new TMP_InputField[PlayerStats.StatCount];

    // Skills tab widgets.
    private TMP_Text _skillPointsText;
    private TMP_Text _categoryLevelText;
    private TMP_Text _equipSummary;
    private TMP_Text _mapLine;
    private TMP_Text _moneyLine;
    private TMP_Text _classLine;
    private TMP_Text _raceLine;

    // Faith tab widgets.
    private TMP_Text _faithTitle;
    private TMP_Text _faithStatus;
    private readonly TMP_Text[] _devotionRowLabels = new TMP_Text[3];
    private readonly Image[] _devotionFill = new Image[3];
    private readonly TMP_Text[] _devotionBarLabels = new TMP_Text[3];
    private TMP_Text _perksText;
    private Button _switchFaithBtn;

    // Skill tree.
    private SkillSubTab _skillSubTab = SkillSubTab.General;
    private RectTransform _treeContent;
    private GameObject _detailPane;
    private readonly List<Skill> _treeSkills = new List<Skill>();
    private readonly List<(Skill skill, Image image, TMP_Text label)> _treeNodes = new List<(Skill, Image, TMP_Text)>();
    private readonly List<(Image image, Skill target)> _treeLines = new List<(Image, Skill)>();
    private Skill _selectedSkill;
    private TMP_Text _detailTitle;
    private TMP_Text _detailDesc;
    private TMP_Text _detailMeta;
    private TMP_Text _detailLearnHint;
    private Button _learnBtn;
    private Button _assignKeyBtn;
    private readonly List<(SkillType type, TMP_Text label)> _sectorLabels = new List<(SkillType, TMP_Text)>();
    private readonly List<(SkillType type, Image image)> _categoryNodes = new List<(SkillType, Image)>();
    private readonly List<(SkillType type, Image swatch, TMP_Text label)> _legendChips = new List<(SkillType, Image, TMP_Text)>();

    // Class skill tree.
    private readonly List<ClassSkill> _classTreeSkills = new List<ClassSkill>();
    private readonly List<(ClassSkill skill, Image image)> _classTreeNodes = new List<(ClassSkill, Image)>();
    private readonly List<(Image image, ClassSkill target)> _classTreeLines = new List<(Image, ClassSkill)>();
    private ClassSkill _selectedClassSkill;
    private Button _generalTabBtn;
    private Button _classTabBtn;
    private Button _raceTabBtn;
    private RectTransform _infoContent;
    private GameObject _talentsView;
    private readonly List<(Talent talent, TMP_Text label, Button upBtn)> _talentRows = new List<(Talent, TMP_Text, Button)>();
    private GameObject _legendRoot;
    private readonly List<TMP_Text> _generalHeadings = new List<TMP_Text>();

    // Race skill tree.
    private readonly List<RaceSkill> _raceTreeSkills = new List<RaceSkill>();
    private readonly List<(RaceSkill skill, Image image)> _raceTreeNodes = new List<(RaceSkill, Image)>();
    private readonly List<(Image image, RaceSkill target)> _raceTreeLines = new List<(Image, RaceSkill)>();
    private RaceSkill _selectedRaceSkill;

    // Per-tree container roots: each tree is built once into its own root under _treeContent,
    // then shown/hidden on sub-tab switch instead of being destroyed and re-created (~3k GOs).
    private RectTransform _generalTreeRoot;
    private RectTransform _classTreeRoot;
    private RectTransform _raceTreeRoot;
    private string _classTreeBuildId;
    private string _raceTreeBuildId;
    private Transform _treeBuildRoot;

    // Backpack storage grid (30 slots) + mirrored hotbar row (10 slots).
    private readonly Image[] _storageImgs = new Image[ToolManager.StorageSlotCount];
    private readonly TMP_Text[] _storageLabels = new TMP_Text[ToolManager.StorageSlotCount];
    private readonly Image[] _invImgs = new Image[ToolManager.HotbarSlotCount];
    private readonly TMP_Text[] _invLabels = new TMP_Text[ToolManager.HotbarSlotCount];
    private int _invSelected = -1;

    private readonly Dictionary<EquipSlot, TMP_Text> _equipSlotLabels = new Dictionary<EquipSlot, TMP_Text>();

    private static readonly Color SlotColor = new Color(0.14f, 0.16f, 0.2f, 0.95f);
    private static readonly Color SlotSelectedColor = new Color(0.35f, 0.55f, 0.75f, 0.95f);

    // Skill tree node states.
    private static readonly Color NodeSelected = new Color(0.99f, 0.94f, 0.66f, 1f);
    private static readonly Color NodeLearned = new Color(0.93f, 0.82f, 0.4f, 1f);
    private static readonly Color NodeAvailable = new Color(0.4f, 0.75f, 0.46f, 1f);
    private static readonly Color NodeLocked = new Color(0.3f, 0.32f, 0.38f, 1f);

    private static readonly string[] CategoryNames = { "Melee", "Ranged", "Magic", "Stealth", "Crafting", "Defense", "Shield" };

    /// <summary>Per-category accent colors (legend chips, sector labels, node top strips).</summary>
    private static readonly Color[] CategoryColors =
    {
        new Color(0.9f, 0.42f, 0.33f, 1f),   // Melee
        new Color(0.35f, 0.82f, 0.5f, 1f),   // Ranged
        new Color(0.48f, 0.56f, 0.95f, 1f),  // Magic
        new Color(0.55f, 0.5f, 0.85f, 1f),   // Stealth
        new Color(0.92f, 0.72f, 0.3f, 1f),   // Crafting
        new Color(0.55f, 0.78f, 0.42f, 1f),  // Defense
        new Color(0.66f, 0.55f, 0.85f, 1f),  // Shield
    };
    private static readonly Color LineActive = Color.black;
    private static readonly Color LineInert = Color.black;
    private static readonly Color LineHighlight = Color.white;

    /// <summary>Horizontal shift applied to the humanoid sheet so the backpack uses the right half.</summary>
    private const float EquipShiftX = -145f;

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

    private static Sprite _fullButtonSprite;
    private static Sprite FullButtonSprite()
    {
        if (_fullButtonSprite == null)
        {
            var tex = Resources.Load<Texture2D>("stats menu full button");
            if (tex != null)
                _fullButtonSprite = Sprite.Create(tex,
                    new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
        }
        return _fullButtonSprite;
    }

    private static void ApplyFullButtonSprite(Image img)
    {
        var sprite = FullButtonSprite();
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

    private static Sprite _categoryNodeSprite;
    private static Sprite CategoryNodeSprite()
    {
        if (_categoryNodeSprite == null)
        {
            const int size = 128;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            float center = (size - 1) * 0.5f;
            float maxD = size * 0.5f;
            var cols = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x - center;
                    float dy = y - center;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = Mathf.Clamp01(maxD - d + 1f);
                    cols[y * size + x] = new Color(1f, 1f, 1f, a);
                }
            }
            tex.SetPixels(cols);
            tex.Apply();
            _categoryNodeSprite = Sprite.Create(tex,
                new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
        }
        return _categoryNodeSprite;
    }

    private string _selectedWeaponId;

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
        EnsureSkillBindingHook();
        if (_built) return;
        _built = true;

        SuppressTitle = true;
        SuppressCloseButton = true;
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
        // TalentTracker (free rank-ups, no level-up subscription) — order independent of the others.
        if (player.GetComponent<TalentTracker>() == null)
            player.gameObject.AddComponent<TalentTracker>();
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
        var bindings = BindingsOf();
        if (bindings != null) bindings.OnKeyCaptured -= OnSkillKeyCaptured;
        if (Instance == this)
            Instance = null;
    }

    /// <summary>
    /// Single-subscription hook: refresh the skill tree (and the selected skill's detail) the
    /// moment a "capture next key" assignment completes, so the new bound key shows immediately.
    /// </summary>
    private void EnsureSkillBindingHook()
    {
        var bindings = BindingsOf();
        if (bindings == null) return;
        bindings.OnKeyCaptured -= OnSkillKeyCaptured;
        bindings.OnKeyCaptured += OnSkillKeyCaptured;
    }

    private void OnSkillKeyCaptured(Key key)
    {
        RefreshSkillTree();
    }

    /// <summary>
    /// Top tab-bar metrics in design units — ONE source of truth. <see cref="BuildTopButtons"/>
    /// lays the row out and <see cref="OnLayoutFitted"/> re-lays it on every aspect change; they
    /// each used to carry their own copy of the height and of the top offset (36 here, 34 there),
    /// which is how a bar silently differs between the first frame and the first window resize.
    ///
    /// The row hangs from the canvas TOP (pivot 0.5, 1), so <see cref="TabBarTopY"/> is the top
    /// edge's distance BELOW the top edge and the band occupies [top - height, top]. Design space
    /// is 1066x600 (1280/720 over <see cref="MenuPanelBase.UiScale"/>), so the top edge is y +300
    /// and the band's own extent is y 250..290.
    ///
    /// The size is bounded by the panels' top rows, which this band must NOT cover: it is a later
    /// sibling of the body row, so it DRAWS OVER them. The tallest content row under it is the
    /// Skills sub-tab row at y 236 and the Faith title at y 238, so the band's bottom edge has to
    /// stay above ~250 — hence 40 tall, not 84, and why the panels were authored blind to it.
    /// </summary>
    private const float TabBarHeight = 40f;

    /// <summary>Top edge of the tab row, in design units below the canvas top edge.</summary>
    private const float TabBarTopY = 10f;

    /// <summary>Bottom inset of a tab label inside its button (text box = height - this).</summary>
    private const float TabLabelInsetY = 8f;

    /// <summary>
    /// Top edge (pivot is the top) of the Skills panel's header row: the Skill Points / Learned
    /// readouts plus the General / Class / Race sub-toggles. It lives in the corridor between the
    /// tab band's bottom edge (y 250) and the tree viewport's top edge (y 200) — 30 tall, so it
    /// clears the band by 14 and the viewport by 6. Keep it in that corridor: the viewport is a
    /// RectMask2D built AFTER this row, so any overlap clips the buttons' bottom edge.
    /// </summary>
    private const float SkillsHeaderY = 236f;

    /// <summary>
    /// Tab label size: the resolution-scaled size, but never taller than the box it sits in. The
    /// band is 40 units on a height-matched canvas, so a raw Screen.height/44 outgrows the button
    /// on a 1440p+ window and the glyphs spill past the border art.
    /// </summary>
    private static float TabLabelFontSize()
    {
        float fits = (TabBarHeight * S - TabLabelInsetY) * 0.95f;
        return Mathf.Min(fits, Mathf.Max(24f, Screen.height / 44f));
    }

    private void BuildTopButtons()
    {
        string[] names = { "Info", "Skills", "Inventory", "Map", "Faith" };
        _tabButtonRects.Clear();
        float w = SeenCanvasWidth() * 0.8f;
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
            rt.anchoredPosition = new Vector2(-w * 0.5f + bw * (0.5f + i), TabBarTopY);
            rt.sizeDelta = new Vector2(bw - 6f, TabBarHeight * S);
            _tabButtonRects.Add(rt);
            var img = go.AddComponent<Image>();
            ApplyFullButtonSprite(img);
            var btn = go.AddComponent<Button>();
            btn.targetGraphic = img;
            Tab captured = tab;
            btn.onClick.AddListener(() => ShowTab(captured));

            var label = new GameObject("Label");
            label.transform.SetParent(go.transform, false);
            var lr = label.AddComponent<RectTransform>();
            lr.anchorMin = Vector2.zero;
            lr.anchorMax = Vector2.one;
            lr.offsetMin = new Vector2(0f, -TabLabelInsetY);
            lr.offsetMax = new Vector2(0f, -TabLabelInsetY);
            var lt = label.AddComponent<TextMeshProUGUI>();
            GameManager.Instance?.UIManager?.ApplyDefaultFont(lt);
            lt.text = name;
            lt.fontSize = TabLabelFontSize();
            lt.color = Color.white;
            lt.alignment = TextAlignmentOptions.Center;
        }
    }

    private void BuildPanels()
    {
        // Info panel: one vertical scroll — stat/level block (level/XP/points/stat allocator/
        // class/race) on top, then the talent rank list below it in the same scroll content.
        _panels[Tab.Info] = MakePanel("InfoPanel");
        BuildInfoTabScroll(_panels[Tab.Info].transform);
        RegisterFit(_panels[Tab.Info].GetComponent<RectTransform>(), TabDesignBox(Tab.Info));

        // Skills panel: draggable skill tree + detail pane.
        _panels[Tab.Skills] = MakePanel("SkillsPanel");
        // SkillsHeaderY is the one datum for this panel's top row: the two readouts and the three
        // sub-toggles are horizontally disjoint (labels at x -450 / +250, buttons at -160/-30/100)
        // so they share a single row, sized to sit between the tab band's bottom edge (y 250) and
        // the tree viewport's top edge (y 200) without touching either. The row was at 250/222,
        // i.e. inside the old 180..264 band, which drew over it.
        _skillPointsText = MakeBodyText(_panels[Tab.Skills].transform, "SkillPoints", P(-450f, SkillsHeaderY), Sz(200f, 28f));
        _categoryLevelText = MakeBodyText(_panels[Tab.Skills].transform, "Learned", P(250f, SkillsHeaderY), Sz(220f, 28f));

        // General / Class / Race sub-toggle inside the skills panel.
        _generalTabBtn = MakeButton(_panels[Tab.Skills].transform, "GenTabBtn", "General", P(-160f, SkillsHeaderY), OnGeneralTab);
        _generalTabBtn.GetComponent<RectTransform>().sizeDelta = Sz(120f, 30f);
        ApplyFullButtonSprite(_generalTabBtn.GetComponent<Image>());
        _classTabBtn = MakeButton(_panels[Tab.Skills].transform, "ClassTabBtn", "Class", P(-30f, SkillsHeaderY), OnClassTab);
        _classTabBtn.GetComponent<RectTransform>().sizeDelta = Sz(120f, 30f);
        ApplyFullButtonSprite(_classTabBtn.GetComponent<Image>());
        _raceTabBtn = MakeButton(_panels[Tab.Skills].transform, "RaceTabBtn", "Race", P(100f, SkillsHeaderY), OnRaceTab);
        _raceTabBtn.GetComponent<RectTransform>().sizeDelta = Sz(120f, 30f);
        ApplyFullButtonSprite(_raceTabBtn.GetComponent<Image>());

        RectTransform treeVt = BuildSkillTree(_panels[Tab.Skills].transform);
        BuildSkillDetail(treeVt.transform);
        _legendRoot = treeVt.gameObject;
        BuildTreeLegend(treeVt);
        UpdateSubTabButtons();
        RegisterFit(_panels[Tab.Skills].GetComponent<RectTransform>(), TabDesignBox(Tab.Skills));

        // Merged Inventory + Equipment panel: equipment sheet LEFT, backpack + use bar RIGHT.
        _panels[Tab.Inventory] = MakePanel("InventoryPanel");
        BuildEquipmentSheet(_panels[Tab.Inventory].transform);
        _equipSummary = MakeBodyText(_panels[Tab.Inventory].transform, "Equipment", P(-450f, 222f), Sz(480f, 44f));
        BuildStorageGrid(_panels[Tab.Inventory].transform);
        BuildHotbarMirror(_panels[Tab.Inventory].transform);
        _moneyLine = MakeBodyText(_panels[Tab.Inventory].transform, "Money", P(210f, -112f), Sz(260f, 24f));
        RegisterFit(_panels[Tab.Inventory].GetComponent<RectTransform>(), TabDesignBox(Tab.Inventory));

        // Map panel (placeholder summary; the dedicated WorldMapUI is separate).
        _panels[Tab.Map] = MakePanel("MapPanel");
        _mapLine = MakeBodyText(_panels[Tab.Map].transform, "Map", P(-270f, 160f), Sz(500f, 200f));
        RegisterFit(_panels[Tab.Map].GetComponent<RectTransform>(), TabDesignBox(Tab.Map));

        // Faith panel: current belief, devotion bars, perk summary, switch dialog.
        _panels[Tab.Faith] = MakePanel("FaithPanel");
        BuildFaithTab(_panels[Tab.Faith].transform);
        RegisterFit(_panels[Tab.Faith].GetComponent<RectTransform>(), TabDesignBox(Tab.Faith));

        EnsureChangeDialog();
    }

    /// <summary>
    /// Design-space box (canvas units, centered on the body) each tab's fixed layout occupies.
    /// Generous so labels never touch the edges after aspect-fit scaling; ≤ ~1070 wide so a 16:9
    /// window keeps fit = 1 and the layout is pixel-identical to before.
    /// </summary>
    private static Rect TabDesignBox(Tab tab)
    {
        switch (tab)
        {
            case Tab.Info: return new Rect(-430f, -190f, 860f, 430f);
            case Tab.Skills: return new Rect(-520f, -290f, 1040f, 580f);
            case Tab.Inventory: return new Rect(-510f, -260f, 1020f, 520f);
            case Tab.Map: return new Rect(-310f, -100f, 620f, 300f);
            case Tab.Faith: return new Rect(-480f, -260f, 960f, 520f);
            default: return new Rect(-500f, -250f, 1000f, 500f);
        }
    }

    /// <summary>The design-space width actually visible on screen at the current window size.</summary>
    private static float SeenCanvasWidth()
    {
        float refH = 720f / UiScale;
        if (refH <= 0f || Screen.height <= 0f) return 1280f / UiScale;
        return Screen.width / (Screen.height / refH);
    }

    /// <summary>Keep the top tab bar inside the visible area while the window changes aspect.</summary>
    protected override void OnLayoutFitted(float availW, float availH)
    {
        if (_tabButtonRects.Count == 0) return;
        float w = availW * 0.8f;
        float bw = w / _tabButtonRects.Count;
        for (int i = 0; i < _tabButtonRects.Count; i++)
        {
            _tabButtonRects[i].anchoredPosition = new Vector2(-w * 0.5f + bw * (0.5f + i), TabBarTopY);
            _tabButtonRects[i].sizeDelta = new Vector2(bw - 6f, TabBarHeight * S);
        }
    }


    /// <summary>Height of the stat/level block above the talent list (design units).</summary>
    private const float InfoStatBlockHeight = 480f;


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
        lt.fontSize = Mathf.Max(14f, Screen.height / 56f);
        lt.color = Color.white;
        lt.alignment = TextAlignmentOptions.Center;
        return btn;
    }

    private TMP_InputField MakeStatEditField(Transform parent, string name, float x, float y, int statIndex)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(x, y);
        rt.sizeDelta = Sz(64f, 26f);

        var img = go.AddComponent<Image>();
        img.color = new Color(0.08f, 0.08f, 0.12f, 0.9f);

        var field = go.AddComponent<TMP_InputField>();
        field.targetGraphic = img;
        field.textViewport = rt;
        field.lineType = TMP_InputField.LineType.SingleLine;
        field.characterValidation = TMP_InputField.CharacterValidation.Decimal;
        field.textComponent = MakeInputChild(go.transform, "Text", true);
        field.pointSize = field.textComponent.fontSize;
        field.placeholder = MakeInputChild(go.transform, "Placeholder", false);

        var stats = PlayerStatsOf();
        field.SetTextWithoutNotify(stats != null ? Mathf.RoundToInt(stats.GetTotal((StatType)statIndex)).ToString() : "0");

        int captured = statIndex;
        field.onEndEdit.AddListener((string value) =>
        {
            var s = PlayerStatsOf();
            if (s == null) return;
            if (string.IsNullOrEmpty(value)
                || !float.TryParse(value, System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out float parsed))
            {
                field.SetTextWithoutNotify(Mathf.RoundToInt(s.GetTotal((StatType)captured)).ToString());
                return;
            }
            s.SetBaseStat((StatType)captured, parsed);
            RefreshInfo();
        });
        return field;
    }

    private TextMeshProUGUI MakeInputChild(Transform parent, string childName, bool isText)
    {
        var child = new GameObject(childName);
        child.transform.SetParent(parent, false);
        var crt = child.AddComponent<RectTransform>();
        crt.anchorMin = new Vector2(0f, 0f);
        crt.anchorMax = new Vector2(1f, 1f);
        crt.offsetMin = new Vector2(4f, 2f);
        crt.offsetMax = new Vector2(-4f, -2f);
        var tmp = child.AddComponent<TextMeshProUGUI>();
        GameManager.Instance?.UIManager?.ApplyDefaultFont(tmp);
        tmp.fontSize = Mathf.Max(15f, Screen.height / 50f);
        tmp.color = isText ? Color.white : new Color(0.7f, 0.7f, 0.75f, 0.6f);
        tmp.alignment = TextAlignmentOptions.MidlineLeft;
        tmp.raycastTarget = false;
        return tmp;
    }





    // Race tree node color: passive = teal, active = purple-blue.
    private static readonly Color RaceNodePassive = new Color(0.3f, 0.7f, 0.65f, 1f);
    private static readonly Color RaceNodeActive = new Color(0.45f, 0.55f, 0.85f, 1f);


    /// <summary>Pan/zoom handler for the tree viewport — drags <see cref="Content"/> and scroll-zooms
    /// it toward the cursor within the mask.</summary>
    private sealed class TreePan : MonoBehaviour, IPointerDownHandler, IDragHandler, IScrollHandler
    {
        public const float MinScale = 0.05f;
        public const float MaxScale = 20f;

        public RectTransform Content;
        public RectTransform Viewport;
        private Vector2 _startPointer;
        private Vector2 _startPos;

        private RectTransform Target => Viewport != null ? Viewport : (RectTransform)transform;

        public void OnPointerDown(PointerEventData e)
        {
            if (Content == null) return;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                Target, e.position, e.pressEventCamera, out _startPointer);
            _startPos = Content.anchoredPosition;
        }

        public void OnDrag(PointerEventData e)
        {
            if (Content == null) return;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                Target, e.position, e.pressEventCamera, out Vector2 current))
                return;
            Content.anchoredPosition = _startPos + (current - _startPointer);
        }

        public void OnScroll(PointerEventData e)
        {
            if (Content == null) return;
            float prev = Content.localScale.x;
            float next = prev * Mathf.Pow(1.25f, Mathf.Sign(e.scrollDelta.y));
            next = Mathf.Clamp(next, MinScale, MaxScale);
            if (Mathf.Approximately(next, prev)) return;

            var vp = Viewport != null ? Viewport : (RectTransform)transform;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                vp, e.position, e.pressEventCamera, out Vector2 local))
            {
                // Keep the content point under the cursor fixed while resizing about the center pivot.
                Content.anchoredPosition *= next / prev;
                Content.anchoredPosition += local * (1f - next / prev);
            }
            Content.localScale = new Vector3(next, next, 1f);
        }
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
            case Tab.Info:
            RefreshInfo();
            RefreshTalentsView();
            break;
            case Tab.Skills:
            if (_skillSubTab == SkillSubTab.Class)
            {
                if (_classTreeNodes.Count == 0) RebuildSkillTree();
                else RefreshSkillTree();
            }
            else if (_skillSubTab == SkillSubTab.Race)
            {
                if (_raceTreeNodes.Count == 0) RebuildSkillTree();
                else RefreshSkillTree();
            }
            else
            {
                if (_treeNodes.Count == 0) RebuildSkillTree();
                else RefreshSkillTree();
            }
            break;
            case Tab.Inventory: RefreshInventoryUi(); break;
            case Tab.Map: RefreshMap(); break;
            case Tab.Faith: RefreshFaith(); break;
        }
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

    private TalentTracker TalentTrackerOf()
    {
        var p = GameManager.Instance?.Player;
        return p != null ? p.GetComponent<TalentTracker>() : null;
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
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
/// class / race with change buttons.
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
public sealed class CharacterInfoUI : MenuPanelBase
{
    /// <summary>Layout multiplier (1.0 = identity; coords are final canvas units).</summary>
    private const float S = 1.0f;

    /// <summary>Last shown instance (drag & drop targets resolve it via this).</summary>
    public static CharacterInfoUI Instance;

    public enum Tab { Info = 0, Skills = 1, Inventory = 2, Map = 3, Faith = 4 }
    public enum SkillSubTab { General = 0, Class = 1 }

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
    private readonly List<(Skill skill, Image image)> _treeNodes = new List<(Skill, Image)>();
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
    private GameObject _legendRoot;

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

    private static readonly string[] CategoryNames = { "Melee", "Ranged", "Magic", "Stealth", "Crafting", "Defense" };

    /// <summary>Per-category accent colors (legend chips, sector labels, node top strips).</summary>
    private static readonly Color[] CategoryColors =
    {
        new Color(0.9f, 0.42f, 0.33f, 1f),   // Melee
        new Color(0.35f, 0.82f, 0.5f, 1f),   // Ranged
        new Color(0.48f, 0.56f, 0.95f, 1f),  // Magic
        new Color(0.55f, 0.5f, 0.85f, 1f),   // Stealth
        new Color(0.92f, 0.72f, 0.3f, 1f),   // Crafting
        new Color(0.55f, 0.78f, 0.42f, 1f),  // Defense
    };
    private static readonly Color LineActive = new Color(0.72f, 0.68f, 0.55f, 0.9f);
    private static readonly Color LineInert = new Color(0.4f, 0.42f, 0.48f, 0.75f);

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
            rt.anchoredPosition = new Vector2(-w * 0.5f + bw * (0.5f + i), 16f);
            rt.sizeDelta = new Vector2(bw - 6f, 56f * S);
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
            lr.offsetMin = new Vector2(0f, -8f);
            lr.offsetMax = new Vector2(0f, -8f);
            var lt = label.AddComponent<TextMeshProUGUI>();
            GameManager.Instance?.UIManager?.ApplyDefaultFont(lt);
            lt.text = name;
            lt.fontSize = Mathf.Max(24f, Screen.height / 44f);
            lt.color = Color.white;
            lt.alignment = TextAlignmentOptions.Center;
        }
    }

    private void BuildPanels()
    {
        // Info panel: level/XP/points/bars + stat allocator + class/race.
        _panels[Tab.Info] = MakePanel("InfoPanel");
        BuildInfoTab(_panels[Tab.Info].transform);
        RegisterFit(_panels[Tab.Info].GetComponent<RectTransform>(), TabDesignBox(Tab.Info));

        // Skills panel: draggable skill tree + detail pane.
        _panels[Tab.Skills] = MakePanel("SkillsPanel");
        _skillPointsText = MakeBodyText(_panels[Tab.Skills].transform, "SkillPoints", P(-450f, 222f), Sz(200f, 28f));
        _categoryLevelText = MakeBodyText(_panels[Tab.Skills].transform, "Learned", P(250f, 222f), Sz(220f, 28f));

        // General / Class sub-toggle inside the skills panel.
        _generalTabBtn = MakeButton(_panels[Tab.Skills].transform, "GenTabBtn", "General", P(-160f, 250f), OnGeneralTab);
        _generalTabBtn.GetComponent<RectTransform>().sizeDelta = Sz(120f, 30f);
        ApplyFullButtonSprite(_generalTabBtn.GetComponent<Image>());
        _classTabBtn = MakeButton(_panels[Tab.Skills].transform, "ClassTabBtn", "Class", P(-30f, 250f), OnClassTab);
        _classTabBtn.GetComponent<RectTransform>().sizeDelta = Sz(120f, 30f);
        ApplyFullButtonSprite(_classTabBtn.GetComponent<Image>());

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
            _tabButtonRects[i].anchoredPosition = new Vector2(-w * 0.5f + bw * (0.5f + i), 6f);
            _tabButtonRects[i].sizeDelta = new Vector2(bw - 6f, 56f * S);
        }
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

    // ── Skill tree ────────────────────────────────────────────────────────
    private RectTransform BuildSkillTree(Transform parent)
    {
        var vp = new GameObject("TreeViewport");
        vp.transform.SetParent(parent, false);
        var vrt = vp.AddComponent<RectTransform>();
        vrt.anchorMin = new Vector2(0.5f, 0.5f);
        vrt.anchorMax = new Vector2(0.5f, 0.5f);
        vrt.pivot = new Vector2(0.5f, 0.5f);
        vrt.anchoredPosition = P(0f, -80f);
        vrt.sizeDelta = Sz(1000f, 560f);
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
        _treeContent.sizeDelta = Sz(2200f, 2200f);

        var pan = vp.AddComponent<TreePan>();
        pan.Content = _treeContent;
        pan.Viewport = vrt;
        return vrt;
    }

    private void BuildSkillDetail(Transform parent)
    {
        var pane = new GameObject("SkillDetail");
        pane.transform.SetParent(parent, false);
        var pRt = pane.AddComponent<RectTransform>();
        pRt.anchorMin = new Vector2(0.5f, 0.5f);
        pRt.anchorMax = new Vector2(0.5f, 0.5f);
        pRt.pivot = new Vector2(0.5f, 0.5f);
        pRt.anchoredPosition = Vector2.zero;
        pRt.sizeDelta = Sz(380f, 300f);
        var pImg = pane.AddComponent<Image>();
        pImg.color = new Color(0.12f, 0.13f, 0.16f, 0.96f);
        _detailPane = pane;
        pane.SetActive(false);

        _detailTitle = MakeBodyText(pane.transform, "DetailTitle", P(-190f, 150f), Sz(360f, 32f));
        _detailTitle.fontSize = Mathf.Max(20f, Screen.height / 38f);
        _detailTitle.alignment = TextAlignmentOptions.Center;

        _detailDesc = MakeBodyText(pane.transform, "DetailDesc", P(-190f, 116f), Sz(360f, 60f));
        _detailDesc.enableWordWrapping = true;

        _detailMeta = MakeBodyText(pane.transform, "DetailMeta", P(-190f, 56f), Sz(360f, 100f));

        _detailLearnHint = MakeBodyText(pane.transform, "DetailHint", P(-190f, -46f), Sz(360f, 24f));
        _detailLearnHint.fontSize = Mathf.Max(14f, Screen.height / 64f);
        _detailLearnHint.enableWordWrapping = true;

        var learn = MakeButton(pane.transform, "LearnBtn", "Learn", P(-100f, -80f), LearnSelectedSkill);
        learn.GetComponent<RectTransform>().sizeDelta = Sz(160f, 38f);
        ApplyFullButtonSprite(learn.GetComponent<Image>());
        _learnBtn = learn;

        var assign = MakeButton(pane.transform, "AssignKeyBtn", "Assign Key", P(100f, -80f), AssignSelectedSkillKey);
        assign.GetComponent<RectTransform>().sizeDelta = Sz(160f, 38f);
        ApplyFullButtonSprite(assign.GetComponent<Image>());
        _assignKeyBtn = assign;

        // Red ✕ close: hides the detail pane (deselects) but keeps the tab menu open.
        MakeRedClose(pane.transform, "DetailCloseBtn",
            new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f),
            new Vector2(-32f, -32f), new Vector2(34f, 34f), DismissSkillDetail);
    }

    private void DismissSkillDetail()
    {
        _selectedSkill = null;
        _selectedClassSkill = null;
        RefreshSkillTree();
    }

    private void RefreshSkillTree()
    {
        if (_skillSubTab == SkillSubTab.Class)
        {
            RefreshClassSkillTree();
            return;
        }

        var profile = SkillProfileOf();
        bool hasPoints = profile != null && profile.Points > 0;

        if (_detailPane != null)
            _detailPane.SetActive(_selectedSkill != null);

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
        {
            int learned = 0;
            if (profile != null)
                foreach (var (skill, _) in _treeNodes)
                    if (profile.HasLearned(skill.id)) learned++;
            _categoryLevelText.text = Localization.F("Learned {0}/{1}", learned, _treeNodes.Count);
        }

        if (xp != null)
        {
            foreach (var (type, label) in _sectorLabels)
                if (label != null)
                    label.text = Localization.F("{0} Lv {1}", CategoryNames[(int)type], xp.GetLevel(type));
            foreach (var (type, _, label) in _legendChips)
                if (label != null)
                    label.text = Localization.F("{0} Lv {1}", CategoryNames[(int)type], xp.GetLevel(type));
        }

        RefreshSkillDetail();
        // Ensure Learn/Assign buttons are visible in General mode (Class mode hides them).
        if (_learnBtn != null)
        {
            _learnBtn.gameObject.SetActive(true);
            _learnBtn.interactable = hasPoints && _selectedSkill != null &&
                profile != null && profile.CanLearn(_selectedSkill);
        }
        if (_assignKeyBtn != null)
        {
            _assignKeyBtn.gameObject.SetActive(true);
            _assignKeyBtn.interactable = CanBindSelectedSkill();
        }
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

        // Currently-bound hotkey for the selected skill.
        if (!skill.IsPassive)
        {
            var bindings = BindingsOf();
            var key = bindings != null ? bindings.KeyOf(skill.id) : (Key?)null;
            if (key.HasValue)
                meta.Append('\n').Append("Key: ").Append(SkillBarHUD.KeyLabel(key.Value));
        }
        _detailMeta.text = meta.ToString();

        var profile = SkillProfileOf();
        if (profile != null && profile.HasLearned(skill.id))
        {
            _detailLearnHint.text = Localization.T("Learned");   // font-safe (no ✔ glyph needed)
            _detailLearnHint.color = NodeLearned;
        }
        else if (profile != null && profile.CanLearn(skill))
        {
            _detailLearnHint.text = Localization.T("Learnable (spend 1 pt)");
            _detailLearnHint.color = NodeAvailable;
        }
        else if (profile != null)
        {
            _detailLearnHint.text = Localization.T("Locked — prerequisites or points missing");
            _detailLearnHint.color = NodeLocked;
        }
        else
        {
            _detailLearnHint.text = "";
        }
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
        if (_skillSubTab == SkillSubTab.Class)
        {
            RebuildClassSkillTree();
            return;
        }

        if (_treeContent == null) return;

        for (int i = _treeContent.childCount - 1; i >= 0; i--)
            Destroy(_treeContent.GetChild(i).gameObject);
        _treeNodes.Clear();
        _treeLines.Clear();
        _treeSkills.Clear();
        _sectorLabels.Clear();
        _categoryNodes.Clear();
        _selectedSkill = null;
        _treeContent.anchoredPosition = Vector2.zero;
        _treeContent.localScale = Vector3.one;

        SkillCatalog.EnsureBuilt();
        var list = new List<Skill>();
        foreach (var s in SkillCatalog.All)
            if (s != null) list.Add(s);
        if (list.Count == 0) return;

        // Depth is read directly from each skill's Layer field (0 = base, 1 = branch, 2 = deep).
        var depth = new Dictionary<string, int>();
        foreach (var s in list)
            depth[s.id] = s.Layer;

        // Polar slot grid: each category fans out inside its own 60° wedge as a cone from the
        // central category wheels, and every node claims a distinct ring/angle cell whose arc is
        // sized to the node pitch, so no nodes ever overlap. When a category outgrows its current
        // rings the layout creates new rings further out (no cap) instead of stacking/colliding.
        const float sectorHalf = 0.52f;     // ±29.8° rad of fan — the full 60° wedge spacing (±30° = 0.524 rad)
                                            // minus a 0.2° clearance buffer so adjacent categories never touch.
        // Tier-band radii (px). Each layer owns a fixed band of rings instead of drifting outward,
        // and the wheel is sized OUT from the hub so every ring's arc has enough real estate:
        //   ring0      r=280  Layer 0 (base) — one ring, exactly sized to the category's base count
        //              (10 for most, 14 for Magic) so no slots go to waste.
        //   ring1-2    r=580,596  Layer 1 (branch) — the first ring holds 50 slots (pitch 12), so the
        //              full Layer-1 set of most categories sits on a single ring; only Magic's 70-node
        //              stack spills 20 onto the tight 16px second ring.
        //   ring3+     r=726,...  Layer 2 (deep) — starts a full 130px moat past the last branch ring.
        const float ring0 = 280f;
        const float moatBase = 300f;    // base ring -> first branch ring (r1 = 580).
        const float branchStep = 16f;   // headroom between the two branch-band rings (10px node).
        const float moatBranch = 130f;  // last branch ring -> first deep ring (r3 = 726).
        const float deepStep = 120f;    // spacing between deep rings.
        float[] layerPitch = { 20f, 12f, 12f }; // Per-layer node pitch: L0 base (18px node + 2px gap),
                                                // L1 branch (11px node + 1px gap), L2 deep (10px node + 2px gap).

        float RingRadius(int ring)
        {
            if (ring <= 0) return ring0;
            if (ring == 1) return ring0 + moatBase;
            if (ring == 2) return ring0 + moatBase + branchStep;
            return ring0 + moatBase + branchStep + moatBranch + (ring - 3) * deepStep;
        }
        int RingCapacity(int ring, float pitch) => Mathf.Max(1, Mathf.FloorToInt(RingRadius(ring) * (2f * sectorHalf) / pitch));

        var posOf = new Dictionary<string, Vector2>();
        const float hubR = 180f;    // category wheel radius — widened so the six hub bubbles sit close to the
                                    // base ring and give the outer bands more room to grow.

        for (int ci = 0; ci < 6; ci++)
        {
            SkillType type = (SkillType)ci;
            float center = (-90f + ci * 60f) * Mathf.Deg2Rad;

            var catList = new List<Skill>();
            foreach (var s in list)
                if (s.Type == type) catList.Add(s);
            if (catList.Count == 0) continue;

            // Layered tree layout. Groups every category's skills into concentric bands by layer:
            // layer 0 = skills that require no condition; layer L = skills that branch out from
            // layer L-1 (deepest prerequisite chain). The node count on a layer drives how many
            // rings its band claims (rings widen outward), and a ring never mixes two layers, so
            // unlock tiers read as clean onion layers instead of slots shared first-come-first-served.
            var childIndex = new Dictionary<string, List<Skill>>();
            foreach (var s in catList)
            {
                if (s.PrereqSkillIds == null) continue;
                foreach (var pid in s.PrereqSkillIds)
                {
                    if (!childIndex.TryGetValue(pid, out var kids))
                        childIndex[pid] = kids = new List<Skill>();
                    kids.Add(s);
                }
            }

            var layerOf = new Dictionary<string, int>();
            int maxLayer = 0;
            foreach (var s in catList)
            {
                int cd = depth.TryGetValue(s.id, out int v) ? v : 0;
                layerOf[s.id] = cd;
                maxLayer = Mathf.Max(maxLayer, cd);
            }

            // Group by layer. Order rule (rings kept, adjacency fixed): each layer is ordered so
            // every node's children are emitted right after their parent, and co-prereq roots —
            // skills that share a common child — are clustered into adjacent siblings. Walking the
            // wheel left→right then reads as prereq flow (Backstab and Sly Fox sit side by side
            // because Assassinate requires both) instead of catalog scatter.
            var layers = new List<List<Skill>>();

            var roots = new List<Skill>();
            foreach (var s in catList)
                if (layerOf[s.id] == 0) roots.Add(s);

            // Union co-prereq root groups so parents of a shared child are emitted adjacent.
            var groupOf = new Dictionary<string, int>();
            var groups = new Dictionary<int, List<Skill>>();
            int nextGroup = 0;
            foreach (var r in roots)
            {
                groupOf[r.id] = nextGroup;
                groups[nextGroup] = new List<Skill> { r };
                nextGroup++;
            }

            void MergeGroups(int into, int from)
            {
                foreach (var m in groups[from])
                {
                    groupOf[m.id] = into;
                    groups[into].Add(m);
                }
                groups.Remove(from);
            }

            foreach (var s in catList)
            {
                if (s.PrereqSkillIds == null || s.PrereqSkillIds.Length < 2) continue;
                int anchor = -1;
                foreach (var pid in s.PrereqSkillIds)
                {
                    if (!groupOf.TryGetValue(pid, out int g) || g == anchor) continue;
                    if (anchor < 0) anchor = g;
                    else MergeGroups(anchor, g);
                }
            }

            var layer0 = new List<Skill>();
            for (int g = 0; g < nextGroup; g++)
                if (groups.TryGetValue(g, out var members))
                    layer0.AddRange(members);
            layers.Add(layer0);

            for (int L = 1; L <= maxLayer; L++)
            {
                var layer = new List<Skill>();
                var placed = new HashSet<string>();
                foreach (var parent in layers[L - 1])
                {
                    if (!childIndex.TryGetValue(parent.id, out var kids)) continue;
                    kids.Sort((a, b) => string.CompareOrdinal(a.id, b.id));
                    foreach (var k in kids)
                        if (layerOf[k.id] == L && placed.Add(k.id))
                            layer.Add(k);
                }
                foreach (var s in catList)
                    if (layerOf[s.id] == L && placed.Add(s.id))
                        layer.Add(s);
                layers.Add(layer);
            }

            // Ring-band allocation: the amount of nodes needed on a layer determines how many
            // rings that band takes (rings widen outward). Every link then points from an inner
            // band ring to an outer band ring.
            var ringTotal = new List<int>();
            var ringFor = new Dictionary<string, int>();
            int ringCursor = 0;
            for (int li = 0; li < layers.Count; li++)
            {
                var layer = layers[li];
                float pitch = li < layerPitch.Length ? layerPitch[li] : 12f;
                if (layer.Count == 0) continue;
                // Pin each layer to its fixed band slots (L0->0, L1->1-2, L2->3+) so a partially
                // filled layer never shifts the next band inward onto a wrong radius.
                int ringIdx = Mathf.Max(ringCursor, li == 0 ? 0 : li == 1 ? 1 : 3);
                int onRing = 0;
                foreach (var s in layer)
                {
                    // Layer 0 is exactly sized to its node count (no reserved/spare slots).
                    int cap = li == 0 ? layer.Count : RingCapacity(ringIdx, pitch);
                    if (onRing >= cap)
                    {
                        ringIdx++;
                        onRing = 0;
                    }
                    while (ringTotal.Count <= ringIdx) ringTotal.Add(0);
                    ringFor[s.id] = ringIdx;
                    ringTotal[ringIdx]++;
                    onRing++;
                }
                ringCursor = li == 1 ? 3 : ringIdx + 1;
            }

            // Center partially filled rings so isolated outer nodes sit mid-wedge, never hugging
            // the low-angle (left) edge of the cone.
            var used = new List<int>(ringTotal.Count);
            for (int r = 0; r < ringTotal.Count; r++) used.Add(0);

            foreach (var layer in layers)
                foreach (var s in layer)
                {
                    int ring = ringFor[s.id];
                    float pitch = s.Layer < layerPitch.Length ? layerPitch[s.Layer] : 12f;
                    int slots = ring == 0 ? ringTotal[ring] : RingCapacity(ring, pitch);
                    int first = Mathf.Max(0, (slots - ringTotal[ring]) / 2);
                    float ang = center - sectorHalf +
                        (first + used[ring] + 0.5f) * (2f * sectorHalf) / slots;
                    used[ring]++;

                    float radial = RingRadius(ring);
                    posOf[s.id] = new Vector2(Mathf.Cos(ang) * radial, Mathf.Sin(ang) * radial);
                    _treeSkills.Add(s);
                }

            // Category hub node: a circle at the wedge center that acts as the root parent of
            // every root skill's spoke.
            var catGo = new GameObject("CategoryNode_" + CategoryNames[ci]);
            catGo.transform.SetParent(_treeContent, false);
            var crt = catGo.AddComponent<RectTransform>();
            crt.anchorMin = new Vector2(0.5f, 0.5f);
            crt.anchorMax = new Vector2(0.5f, 0.5f);
            crt.pivot = new Vector2(0.5f, 0.5f);
            crt.anchoredPosition = new Vector2(Mathf.Cos(center) * hubR, Mathf.Sin(center) * hubR);
            crt.sizeDelta = new Vector2(128f, 128f);
            var cimg = catGo.AddComponent<Image>();
            cimg.sprite = CategoryNodeSprite();
            cimg.type = Image.Type.Simple;
            cimg.preserveAspect = true;
            cimg.color = CategoryColors[ci];
            cimg.raycastTarget = false;
            _categoryNodes.Add((type, cimg));

            // Category name centered inside the hub bubble (drawn after -> on top of the node).
            var lbl = MakeBodyText(_treeContent, "Sector_" + CategoryNames[ci],
                new Vector2(Mathf.Cos(center) * hubR - 64f, Mathf.Sin(center) * hubR), Sz(128f, 28f));
            lbl.alignment = TextAlignmentOptions.Center;
            lbl.fontSize = Mathf.Max(16f, Screen.height / 66f);
            lbl.color = Color.black;
            _sectorLabels.Add((type, lbl));
        }
        if (posOf.Count == 0) return;

        // Connection lines (prereq -> child), plus a spoke from each root skill (no prereq) to its
        // category hub so no node ever floats unconnected.
        float thick = 1.5f * S;
        foreach (var s in list)
        {
            if (!posOf.TryGetValue(s.id, out Vector2 end)) continue;
            float lineThick = s.Layer == 0 ? thick * 1.3f : s.Layer == 1 ? thick : thick * 0.7f;
            if (s.PrereqSkillIds != null && s.PrereqSkillIds.Length > 0)
            {
                foreach (var pid in s.PrereqSkillIds)
                {
                    if (!posOf.TryGetValue(pid, out Vector2 start)) continue;
                    var line = MakeTreeLine(start, end, lineThick);
                    _treeLines.Add((line, s));
                }
            }
            else
            {
                float c = (-90f + (int)s.Type * 60f) * Mathf.Deg2Rad;
                Vector2 hub = new Vector2(Mathf.Cos(c) * hubR, Mathf.Sin(c) * hubR);
                var spoke = MakeTreeLine(hub, end, lineThick * 0.7f);
                var tint = CategoryColors[(int)s.Type];
                spoke.color = new Color(tint.r, tint.g, tint.b, 0.4f);
                _treeLines.Add((spoke, s));
            }
        }

        // Nodes.
        foreach (var s in list)
        {
            if (!posOf.TryGetValue(s.id, out Vector2 pos)) continue;
            var node = MakeTreeNode(s, pos);
            _treeNodes.Add((s, node));
        }

        FitTreeToViewport();
        RefreshSkillTree();
    }

    /// <summary>Scales the tree content so the whole wheel fits inside the viewport.</summary>
    private void FitTreeToViewport()
    {
        if (_treeContent == null || _treeContent.parent == null) return;
        float maxR = 0f;
        foreach (var (_, image) in _treeNodes)
        {
            if (image == null) continue;
            maxR = Mathf.Max(maxR, ((RectTransform)image.transform).anchoredPosition.magnitude);
        }
        foreach (var (_, image) in _classTreeNodes)
        {
            if (image == null) continue;
            maxR = Mathf.Max(maxR, ((RectTransform)image.transform).anchoredPosition.magnitude);
        }
        maxR += 40f;
        _treeContent.sizeDelta = new Vector2(maxR * 2f, maxR * 2f);
        var vp = _treeContent.parent as RectTransform;
        if (vp == null || maxR <= 0f) return;
        float scale = Mathf.Min(vp.rect.width / (maxR * 2f), vp.rect.height / (maxR * 2f));
        float fitFloor = Mathf.Min(TreePan.MinScale, scale);
        scale = Mathf.Clamp(scale, fitFloor, TreePan.MaxScale);
        _treeContent.localScale = new Vector3(scale, scale, 1f);
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
        float nw = skill.Layer == 0 ? 18f : skill.Layer == 1 ? 11f : 10f;
        float nh = skill.Layer == 0 ? 14f : skill.Layer == 1 ? 10f : 7f;
        rt.sizeDelta = Sz(nw, nh);
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

        var strip = new GameObject("CatStrip");
        strip.transform.SetParent(go.transform, false);
        var srt = strip.AddComponent<RectTransform>();
        srt.anchorMin = new Vector2(0f, 1f);
        srt.anchorMax = new Vector2(1f, 1f);
        srt.pivot = new Vector2(0.5f, 1f);
        srt.offsetMin = new Vector2(2f, -6f);
        srt.offsetMax = new Vector2(-2f, 0f);
        var stripImg = strip.AddComponent<Image>();
        stripImg.raycastTarget = false;
        stripImg.color = CategoryColors[(int)skill.Type];

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
        tmp.fontSize = skill.Layer == 0 ? Mathf.Max(8f, Screen.height / 150f)
                     : skill.Layer == 1 ? Mathf.Max(7f, Screen.height / 200f)
                     : Mathf.Max(6f, Screen.height / 280f);
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

    // ── General / Class sub-tab toggle ───────────────────────────────────

    private void OnGeneralTab() => SetSkillSubTab(SkillSubTab.General);
    private void OnClassTab() => SetSkillSubTab(SkillSubTab.Class);

    private void SetSkillSubTab(SkillSubTab tab)
    {
        if (_skillSubTab == tab) return;
        _skillSubTab = tab;
        _selectedSkill = null;
        _selectedClassSkill = null;
        if (_detailPane != null) _detailPane.SetActive(false);
        // Legend only shown in General view (6-category colors irrelevant to class tree).
        if (_legendRoot != null)
        {
            foreach (var chip in _legendChips)
                if (chip.swatch != null) chip.swatch.transform.parent.gameObject.SetActive(tab == SkillSubTab.General);
            foreach (var node in _categoryNodes)
                if (node.image != null) node.image.gameObject.SetActive(tab == SkillSubTab.General);
            foreach (var sec in _sectorLabels)
                if (sec.label != null) sec.label.gameObject.SetActive(tab == SkillSubTab.General);
        }
        UpdateSubTabButtons();
        RebuildSkillTree();
    }

    private void UpdateSubTabButtons()
    {
        bool gen = _skillSubTab == SkillSubTab.General;
        if (_generalTabBtn != null)
        {
            var img = _generalTabBtn.GetComponent<Image>();
            if (img != null) img.color = gen ? NodeLearned : NodeLocked;
        }
        if (_classTabBtn != null)
        {
            var img = _classTabBtn.GetComponent<Image>();
            if (img != null) img.color = !gen ? NodeLearned : NodeLocked;
        }
    }

    // ── Class skill tree builder ─────────────────────────────────────────

    private void RebuildClassSkillTree()
    {
        if (_treeContent == null) return;

        for (int i = _treeContent.childCount - 1; i >= 0; i--)
            Destroy(_treeContent.GetChild(i).gameObject);
        _classTreeNodes.Clear();
        _classTreeLines.Clear();
        _classTreeSkills.Clear();
        _selectedClassSkill = null;
        _treeNodes.Clear();
        _treeLines.Clear();
        _treeSkills.Clear();
        _selectedSkill = null;
        _treeContent.anchoredPosition = Vector2.zero;
        _treeContent.localScale = Vector3.one;

        ClassSkillCatalog.EnsureBuilt();
        var unlocker = ClassUnlockerOf();
        string classId = unlocker != null ? unlocker.ActiveClassId : "wanderer";
        var list = new List<ClassSkill>();
        foreach (var s in ClassSkillCatalog.ForClass(classId))
            if (s != null) list.Add(s);
        if (list.Count == 0) return;

        // Radial layout: hub at center, 3 paths fan out at 120° intervals.
        const float pathR = 200f;
        const float leafR = 420f;
        const float leafSpread = 50f;

        // Hub node.
        var hub = list.Find(s => s.Layer == 0);
        if (hub != null)
        {
            _classTreeSkills.Add(hub);
            _classTreeNodes.Add((hub, MakeClassTreeNode(hub, Vector2.zero, 24f)));
        }

        // Group Layer 1 nodes and their Layer 2 children by path order.
        var layer1 = new List<ClassSkill>();
        foreach (var s in list)
            if (s.Layer == 1) layer1.Add(s);
        layer1.Sort((a, b) => string.CompareOrdinal(a.id, b.id));

        int pathCount = Mathf.Max(1, layer1.Count);
        for (int pi = 0; pi < pathCount; pi++)
        {
            var pathNode = layer1[pi];
            float angle = (-90f + pi * (360f / pathCount)) * Mathf.Deg2Rad;
            Vector2 pos1 = new Vector2(Mathf.Cos(angle) * pathR, Mathf.Sin(angle) * pathR);

            _classTreeSkills.Add(pathNode);
            _classTreeNodes.Add((pathNode, MakeClassTreeNode(pathNode, pos1, 18f)));

            // Line from hub to path parent.
            var spoke = MakeTreeLine(Vector2.zero, pos1, 2.5f);
            spoke.color = LineActive;
            _classTreeLines.Add((spoke, pathNode));

            // Find Layer 2 children of this path node.
            var children = new List<ClassSkill>();
            foreach (var s in list)
            {
                if (s.Layer != 2 || s.PrereqSkillIds == null) continue;
                foreach (var pid in s.PrereqSkillIds)
                    if (pid == pathNode.id) { children.Add(s); break; }
            }
            children.Sort((a, b) => string.CompareOrdinal(a.id, b.id));

            for (int ci = 0; ci < children.Count; ci++)
            {
                var child = children[ci];
                float childAngle = angle + (ci - (children.Count - 1) * 0.5f) * (leafSpread * Mathf.Deg2Rad);
                Vector2 pos2 = new Vector2(Mathf.Cos(childAngle) * leafR, Mathf.Sin(childAngle) * leafR);

                _classTreeSkills.Add(child);
                _classTreeNodes.Add((child, MakeClassTreeNode(child, pos2, 14f)));

                var line = MakeTreeLine(pos1, pos2, 1.5f);
                _classTreeLines.Add((line, child));
            }
        }

        FitTreeToViewport();
        RefreshClassSkillTree();
    }

    private Image MakeClassTreeNode(ClassSkill skill, Vector2 pos, float size)
    {
        var go = new GameObject("CNode_" + skill.id);
        go.transform.SetParent(_treeContent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = Sz(size * 2f, size);
        var img = go.AddComponent<Image>();
        img.color = skill.IsPassive ? NodeAvailable : new Color(0.55f, 0.48f, 0.9f, 1f);
        var btn = go.AddComponent<Button>();
        btn.targetGraphic = img;
        ClassSkill captured = skill;
        btn.onClick.AddListener(() =>
        {
            _selectedClassSkill = captured;
            RefreshClassSkillTree();
        });

        // Label.
        var lbl = new GameObject("Label");
        lbl.transform.SetParent(go.transform, false);
        var lr = lbl.AddComponent<RectTransform>();
        lr.anchorMin = Vector2.zero;
        lr.anchorMax = Vector2.one;
        lr.offsetMin = Vector2.zero;
        lr.offsetMax = Vector2.zero;
        var tmp = lbl.AddComponent<TextMeshProUGUI>();
        GameManager.Instance?.UIManager?.ApplyDefaultFont(tmp);
        tmp.text = skill.displayName;
        tmp.fontSize = skill.Layer == 0 ? Mathf.Max(10f, Screen.height / 100f)
                     : skill.Layer == 1 ? Mathf.Max(8f, Screen.height / 140f)
                     : Mathf.Max(7f, Screen.height / 180f);
        tmp.color = Color.white;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.enableWordWrapping = true;
        return img;
    }

    private void RefreshClassSkillTree()
    {
        if (_detailPane != null)
            _detailPane.SetActive(_selectedClassSkill != null);

        foreach (var (skill, image) in _classTreeNodes)
        {
            if (image == null) continue;
            if (skill == _selectedClassSkill) image.color = NodeSelected;
            else image.color = skill.IsPassive ? NodeAvailable : new Color(0.55f, 0.48f, 0.9f, 1f);
        }

        foreach (var (line, target) in _classTreeLines)
        {
            if (line == null) continue;
            line.color = LineActive;
        }

        if (_skillPointsText != null)
        {
            var unlocker = ClassUnlockerOf();
            string cls = unlocker != null ? unlocker.ActiveClassId : "wanderer";
            _skillPointsText.text = Localization.F("Class: {0}", cls);
        }

        if (_categoryLevelText != null)
            _categoryLevelText.text = Localization.F("Skills: {0}", _classTreeNodes.Count);

        RefreshClassSkillDetail();
    }

    private void RefreshClassSkillDetail()
    {
        if (_detailTitle == null) return;
        var skill = _selectedClassSkill;
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

        if (skill.Mods != null && skill.Mods.Length > 0)
        {
            meta.Append('\n').Append(Localization.T("Passive Mods:"));
            foreach (var m in skill.Mods)
                meta.Append('\n').Append("  ").Append(m.kind.ToString()).Append(" +").Append((m.amount * 100f).ToString("0.#")).Append("%");
        }

        _detailMeta.text = meta.ToString();
        _detailLearnHint.text = Localization.T("Auto-granted when class is unlocked.");

        // Class castables run through ClassSkillCaster's own hotkey system — no manual binding.
        if (_learnBtn != null) _learnBtn.gameObject.SetActive(false);
        if (_assignKeyBtn != null) _assignKeyBtn.gameObject.SetActive(false);
    }

    /// <summary>Pan/zoom handler for the tree viewport — drags <see cref="Content"/> and scroll-zooms
    /// it toward the cursor within the mask.</summary>
    private sealed class TreePan : MonoBehaviour, IPointerDownHandler, IDragHandler, IScrollHandler
    {
        public const float MinScale = 0.28f;
        public const float MaxScale = 3f;

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
            float next = prev * Mathf.Pow(1.2f, Mathf.Sign(e.scrollDelta.y));
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

    // ── Backpack storage grid (Inventory tab, right side) ───────────────────
    private void BuildStorageGrid(Transform parent)
    {
        var headerRoot = MakeBodyText(parent, "StorageHeader", P(120f, 238f), Sz(280f, 28f));
        headerRoot.text = Localization.T("Backpack (storage)");

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
            rt.anchoredPosition = new Vector2((120f + col * 62f) * S, (214f - row * 68f) * S);
            rt.sizeDelta = Sz(58f, 64f);
            var img = go.AddComponent<Image>();
            img.color = SlotColor;
            go.AddComponent<ItemDragHandle>().Slot = slot;
            go.AddComponent<ItemDropTarget>().Slot = slot;
            go.AddComponent<TooltipSlot>().Bind(() => SlotItemId(slot));

            var label = new GameObject("Label");
            label.transform.SetParent(go.transform, false);
            var lr = label.AddComponent<RectTransform>();
            lr.anchorMin = Vector2.zero;
            lr.anchorMax = Vector2.one;
            lr.offsetMin = Vector2.zero;
            lr.offsetMax = Vector2.zero;
            var tmp = label.AddComponent<TextMeshProUGUI>();
            GameManager.Instance?.UIManager?.ApplyDefaultFont(tmp);
            tmp.fontSize = Mathf.Max(12f, Screen.height / 80f);
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
        MakeBodyText(parent, "UseBarHeader", P(40f, -162f), Sz(300f, 24f))
            .text = Localization.T("Use bar (1-0)");

        for (int i = 0; i < ToolManager.HotbarSlotCount; i++)
        {
            var go = new GameObject("HudInvSlot_" + i);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2((40f + i * 42f) * S, -199f * S);
            rt.sizeDelta = Sz(44f, 50f);
            var img = go.AddComponent<Image>();
            img.color = SlotColor;
            var btn = go.AddComponent<Button>();
            btn.targetGraphic = img;
            int captured = i;
            btn.onClick.AddListener(() => SelectInventorySlot(captured));
            go.AddComponent<ItemDragHandle>().Slot = i;
            go.AddComponent<ItemDropTarget>().Slot = i;
            go.AddComponent<TooltipSlot>().Bind(() => SlotItemId(i));

            var label = new GameObject("Label");
            label.transform.SetParent(go.transform, false);
            var lr = label.AddComponent<RectTransform>();
            lr.anchorMin = Vector2.zero;
            lr.anchorMax = Vector2.one;
            lr.offsetMin = Vector2.zero;
            lr.offsetMax = Vector2.zero;
            var tmp = label.AddComponent<TextMeshProUGUI>();
            GameManager.Instance?.UIManager?.ApplyDefaultFont(tmp);
            tmp.fontSize = Mathf.Max(11f, Screen.height / 88f);
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

    // ── Weapon equipping (hand slots) ───────────────────────────────────────
    // Weapons are normal inventory items now: drag one from the grid/hotbar onto
    // the LHand/RHand slot (WeaponDropTarget) or click a hand slot to equip/cycle.

    public void EquipWeaponFromDrop(string weaponId, EquipSlot slot)
    {
        if (string.IsNullOrEmpty(weaponId)) return;
        _selectedWeaponId = weaponId;
        EquipOwnedWeapon(weaponId, slot);
    }

    public void OnDragDropEnded()
    {
        RefreshInventoryUi();
    }

    public void OnItemDragEnded()
    {
        RefreshInventoryUi();
    }

    /// <summary>Refresh all inventory-side displays (storage grid, use bar, sheet).</summary>
    public void RefreshInventoryUi()
    {
        RefreshInventory();
        RefreshEquipment();
    }

    private void EquipOwnedWeapon(string weaponId, EquipSlot slot)
    {
        var player = GameManager.Instance?.Player;
        var combat = CombatOf();
        if (player == null || combat == null) return;
        // Fists are innate — never granted/equipped as a bag item.
        if (weaponId == WeaponCatalog.FistWeaponId) return;

        var weapon = WeaponCatalog.Find(weaponId);
        if (weapon == null) return;

        // Dual-wield rule: the same weapon id CAN occupy both hands (one rig = one owned copy), but
        // only when a spare copy still exists in the bag — RemoveItemAmount below consumes it.
        // Without a spare (single owned sword / starter auto-equip), dropping onto the other hand
        // MOVES it there (drop that rig first) so an item is never duplicated onto the body. Already
        // on the drop hand → keep the rig in place.
        bool targetHolds = slot == EquipSlot.LeftHand
            ? RigHolds(combat.LeftHand, weaponId)
            : RigHolds(combat.RightHand, weaponId);
        if (targetHolds)
        {
            _selectedWeaponId = "";
            RefreshInventoryUi();
            return;
        }
        bool otherHolds = slot == EquipSlot.LeftHand
            ? RigHolds(combat.RightHand, weaponId)
            : RigHolds(combat.LeftHand, weaponId);
        if (otherHolds)
        {
            var spareCopies = ToolManager.Instance != null ? ToolManager.Instance.CountItem(weaponId) : 0;
            if (spareCopies < 1)
            {
                if (slot == EquipSlot.LeftHand)
                {
                    Destroy(combat.RightHand);
                    combat.RightHand = null;
                }
                else
                {
                    Destroy(combat.LeftHand);
                    combat.LeftHand = null;
                }
                combat.SetTwoHand(false);
            }
        }

        // Never lose the currently equipped weapon: anything that's about to be cleared by
        // EquipInto goes back into the bag first. If the bag can't hold it, abort the swap so
        // nothing disappears (re-equipping the same id on the other hand skips this).
        string replaced = ReplacedWeaponId(combat, weaponId, slot);
        if (!string.IsNullOrEmpty(replaced))
        {
            var tm = ToolManager.Instance;
            if (tm != null && !tm.CanHoldItem(replaced))
            {
                GameManager.Instance?.UIManager?.ShowMessage(Localization.T("Túi đồ đầy."), 1.5f);
                return;
            }
            tm?.AddItem(replaced, 1);
        }

        var rig = WeaponRigBuilder.EquipInto(player.gameObject, weapon, slot == EquipSlot.LeftHand);
        if (rig == null) return;
        // Match the new weapon's visual pose to the current combat state (drawn if fighting,
        // stowed on the body if not).
        var pc = player.GetComponent<PlayerController>();
        bool fighting = pc != null && pc.FightingMode;
        WeaponRigBuilder.ApplyPose(player.gameObject, draw: fighting, instant: true);

        // Equipping takes the weapon out of the bag: it's now on the character. Removing a copy
        // that isn't in the ToolManager inventory (e.g. owned but never picked up as an item, or
        // the starter auto-equip at boot) is a harmless no-op via RemoveItemAmount's false return.
        ToolManager.Instance?.RemoveItemAmount(weaponId, 1);

        _selectedWeaponId = "";
        RefreshInventoryUi();
    }

    /// <summary>The weapon id held on the target hand that a new equip into that slot would clear, if any.</summary>
    private static string ReplacedWeaponId(CombatController combat, string incomingId, EquipSlot slot)
    {
        var rig = slot == EquipSlot.LeftHand ? combat.LeftHand : combat.RightHand;
        var host = rig != null ? rig.GetComponent<WeaponRigHost>() : null;
        // Bare fists are innate (never a bag item) — EquipInto clears them itself.
        if (host != null && host.Data != null && host.Data.id != incomingId && host.Data.id != WeaponCatalog.FistWeaponId)
            return host.Data.id;
        return null;
    }

    /// <summary>
    /// Remove <paramref name="weaponId"/> from the hands (single / two-hand / dual-wield mirrors)
    /// and return it to the backpack so equipment can be dragged back out of a slot.
    /// <paramref name="destSlot"/> is the specific sheet cell it was dropped onto (weapon lands
    /// there instead of the first free slot). <paramref name="sourceHand"/> is the hand slot the
    /// drag started from — only that hand is unequipped (so a second identical weapon on the other
    /// hand stays equipped); when absent, every hand holding the weapon is cleared.
    /// </summary>
    public void UnequipWeapon(string weaponId, int destSlot = -1, EquipSlot sourceHand = (EquipSlot)(-1))
    {
        if (string.IsNullOrEmpty(weaponId)) return;
        // Bare fists can't be unequipped/dropped — they are the empty-hand combat mode itself.
        if (weaponId == WeaponCatalog.FistWeaponId) return;
        var combat = CombatOf();
        if (combat == null) return;

        bool equipped = RigHolds(combat.RightHand, weaponId) || RigHolds(combat.LeftHand, weaponId);
        if (!equipped) return;

        // Bag full → keep the weapon equipped rather than let it vanish.
        var tm = ToolManager.Instance;
        if (tm != null && !tm.CanHoldItem(weaponId))
        {
            GameManager.Instance?.UIManager?.ShowMessage(Localization.T("Túi đồ đầy."), 1.5f);
            return;
        }

        int removed = 0;
        if ((int)sourceHand >= 0)
        {
            var handRig = sourceHand == EquipSlot.LeftHand ? combat.LeftHand : combat.RightHand;
            if (handRig != null && RigHolds(handRig, weaponId))
            {
                Destroy(handRig);
                if (sourceHand == EquipSlot.LeftHand) combat.LeftHand = null;
                else combat.RightHand = null;
                removed++;
            }
        }
        else
        {
            if (combat.RightHand != null && RigHolds(combat.RightHand, weaponId))
            {
                Destroy(combat.RightHand);
                combat.RightHand = null;
                removed++;
            }
            if (combat.LeftHand != null && RigHolds(combat.LeftHand, weaponId))
            {
                Destroy(combat.LeftHand);
                combat.LeftHand = null;
                removed++;
            }
        }
        if (removed <= 0) return;

        combat.SetTwoHand(false);
        if (destSlot >= 0) tm?.PutItem(weaponId, removed, destSlot);
        else tm?.AddItem(weaponId, removed);
        RefreshInventoryUi();
    }

    private static bool RigHolds(GameObject rig, string weaponId)
    {
        var host = rig != null ? rig.GetComponent<WeaponRigHost>() : null;
        return host != null && host.Data != null && host.Data.id == weaponId;
    }

    // ── Humanoid 21-slot equipment sheet (§5.4) ────────────────────────────
    private void BuildEquipmentSheet(Transform parent)
    {
        GearCatalog.EnsureBuilt();
        _equipSlotLabels.Clear();

        SlotButton(parent, "Ear1",        EquipSlot.Ear1,     new Vector2(-200f, -10f));
        SlotButton(parent, "Head",        EquipSlot.Head,     new Vector2(-105f, -10f));
        SlotButton(parent, "Ear2",        EquipSlot.Ear2,     new Vector2(-10f, -10f));
        SlotButton(parent, "Necklace",    EquipSlot.Necklace, new Vector2(-105f, -54f));
        SlotButton(parent, "LHand",       EquipSlot.LeftHand, new Vector2(-200f, -98f));
        SlotButton(parent, "Body",        EquipSlot.Body,     new Vector2(-105f, -98f));
        SlotButton(parent, "RHand",       EquipSlot.RightHand,new Vector2(-10f, -98f));
        SlotButton(parent, "Glove",       EquipSlot.Glove,    new Vector2(-200f, -142f));
        SlotButton(parent, "Belt",        EquipSlot.Belt,     new Vector2(-105f, -142f));
        SlotButton(parent, "Legging",     EquipSlot.Legging,  new Vector2(-105f, -186f));
        SlotButton(parent, "Feet",        EquipSlot.Feet,     new Vector2(-105f, -230f));
        SlotButton(parent, "Finger1",     EquipSlot.Finger1,  new Vector2(-270f, -10f));
        SlotButton(parent, "Finger2",     EquipSlot.Finger2,  new Vector2(-270f, -54f));
        SlotButton(parent, "Finger3",     EquipSlot.Finger3,  new Vector2(-270f, -98f));
        SlotButton(parent, "Finger4",     EquipSlot.Finger4,  new Vector2(-270f, -142f));
        SlotButton(parent, "Finger5",     EquipSlot.Finger5,  new Vector2(-270f, -186f));
        SlotButton(parent, "Finger6",     EquipSlot.Finger6,  new Vector2(62f, -10f));
        SlotButton(parent, "Finger7",     EquipSlot.Finger7,  new Vector2(62f, -54f));
        SlotButton(parent, "Finger8",     EquipSlot.Finger8,  new Vector2(62f, -98f));
        SlotButton(parent, "Finger9",     EquipSlot.Finger9,  new Vector2(62f, -142f));
        SlotButton(parent, "Finger10",    EquipSlot.Finger10, new Vector2(62f, -186f));
    }

    private void SlotButton(Transform parent, string name, EquipSlot slot, Vector2 pos)
    {
        var go = new GameObject(name + "Slot");
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2((pos.x + EquipShiftX) * S, (pos.y + 150f) * S);
        rt.sizeDelta = Sz(62f, 42f);
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
            var drag = go.AddComponent<WeaponDragHandle>();
            drag.Slot = slot;
        }

        EquipSlot capturedSlot = slot;
        go.AddComponent<TooltipSlot>().Bind(() => EquipSlotItemId(capturedSlot));

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
        else if (_current == Tab.Faith)
            RefreshFaith();
        else
            RefreshInventoryUi();
    }

    private void BuildTreeLegend(RectTransform viewport)
    {
        const float gap = 122f * S;
        const float swatch = 14f * S;
        for (int i = 0; i < 6; i++)
        {
            var go = new GameObject("Legend_" + CategoryNames[i]);
            go.transform.SetParent(viewport, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 0.5f);
            rt.anchoredPosition = new Vector2((14f + i * gap) * S, -14f * S);
            rt.sizeDelta = Sz(114f, 18f);

            var swatchGo = new GameObject("Swatch");
            swatchGo.transform.SetParent(go.transform, false);
            var srt = swatchGo.AddComponent<RectTransform>();
            srt.anchorMin = new Vector2(0f, 0.5f);
            srt.anchorMax = new Vector2(0f, 0.5f);
            srt.pivot = new Vector2(0f, 0.5f);
            srt.anchoredPosition = Vector2.zero;
            srt.sizeDelta = new Vector2(swatch, swatch);
            var simg = swatchGo.AddComponent<Image>();
            simg.raycastTarget = false;
            simg.color = CategoryColors[i];

            var label = new GameObject("Label");
            label.transform.SetParent(go.transform, false);
            var lr = label.AddComponent<RectTransform>();
            lr.anchorMin = new Vector2(0f, 0.5f);
            lr.anchorMax = new Vector2(1f, 0.5f);
            lr.offsetMin = new Vector2(swatch + 5f, 0f);
            lr.offsetMax = Vector2.zero;
            var tmp = label.AddComponent<TextMeshProUGUI>();
            GameManager.Instance?.UIManager?.ApplyDefaultFont(tmp);
            tmp.raycastTarget = false;
            tmp.fontSize = Mathf.Max(11f, Screen.height / 100f);
            tmp.color = new Color(0.85f, 0.87f, 0.9f, 1f);
            tmp.alignment = TextAlignmentOptions.MidlineLeft;
            _legendChips.Add(((SkillType)i, simg, tmp));
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
            case Tab.Skills:
            if (_skillSubTab == SkillSubTab.Class)
            {
                if (_classTreeNodes.Count == 0) RebuildSkillTree();
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
                : WeaponCatalog.DisplayName(slot.Type) + " x" + slot.Count;
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
                : WeaponCatalog.DisplayName(slot.Type) + " x" + slot.Count;
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

    // ── Faith tab ─────────────────────────────────────────────────────────
    // Current belief, devotion bars (0-10 per religion), perk summary for the
    // current faith, and the Switch-Faith dialog.
    private void BuildFaithTab(Transform parent)
    {
        _faithTitle = MakeBodyText(parent, "FaithTitle", P(0f, 238f), Sz(920f, 34f));
        _faithTitle.alignment = TextAlignmentOptions.Center;
        _faithTitle.fontSize = Mathf.Max(22f, Screen.height / 34f);

        _faithStatus = MakeBodyText(parent, "FaithStatus", P(0f, 206f), Sz(920f, 26f));
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
        {
            WeaponRigBuilder.EquipInto(player.gameObject, next);
            var pc = player.GetComponent<PlayerController>();
            bool fighting = pc != null && pc.FightingMode;
            WeaponRigBuilder.ApplyPose(player.gameObject, draw: fighting, instant: true);
        }
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

    /// <summary>Live item id in a ToolManager slot, or null when empty (for hover tooltips).</summary>
    private string SlotItemId(int slot)
    {
        var slotInfo = ToolManager.Instance?.PeekSlot(slot);
        return slotInfo != null && slotInfo.Count > 0 ? slotInfo.Type : null;
    }

    /// <summary>Live item id equipped in a gear slot, or null when empty (for hover tooltips).</summary>
    private string EquipSlotItemId(EquipSlot slot)
    {
        var equip = EquipmentOf();
        return equip != null ? equip.Get(slot) : null;
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
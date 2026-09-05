using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Phase 10/11: Character Info menu — a stacked multi-panel window with a top button bar. Each
/// top button reveals one panel and hides the others (Stats / Skills / Inventory / Map).
///
/// The Inventory panel merges the old Inventory + Equipment tabs: the humanoid 21-slot equipment
/// sheet sits on the LEFT, the backpack storage grid (30 slots) with the mirrored hotbar row
/// ("use bar") on the RIGHT. Stacks can be dragged between the storage grid and the hotbar row
/// via <see cref="ItemDragHandle"/>/<see cref="ItemDropTarget"/> (Minecraft-style) and owned
/// weapons can be dragged onto the L/R Hand slots.
///
/// The Stats panel shows the stats plus the currently-used class and race (name + passive).
/// "Change Class" and "Change Race" open a picker with a confirmation step before applying
/// (class via <see cref="ClassUnlocker"/>, race via <see cref="RaceChangeManager"/> with the
/// Ritual Stone cost for non-Human changes).
///
/// Composes existing PlayerStats / ToolManager / EquipmentSystem / GearCatalog / WeaponRigBuilder /
/// SkillProfile / SkillBindings / SkillCatalog / ClassUnlocker / RaceChangeManager without
/// rewriting them. Built on MenuPanelBase.
/// </summary>
public sealed class CharacterInfoUI : MenuPanelBase
{
    /// <summary>Last shown instance (drag & drop targets resolve it via this).</summary>
    public static CharacterInfoUI Instance;

    public enum Tab { Stats = 0, Skills = 1, Inventory = 2, Map = 3 }

    public Tab ActiveTab = Tab.Stats;

    private readonly Dictionary<Tab, GameObject> _panels = new Dictionary<Tab, GameObject>();
    private Tab _current = Tab.Stats;
    private SkillType _skillView = SkillType.Melee;
    private bool _built;

    private TMP_Text _statsLine;
    private TMP_Text _skillListLine;
    private TMP_Text _skillPointsLine;
    private TMP_Text _equipSummary;
    private TMP_Text _mapLine;
    private TMP_Text _captureLine;
    private TMP_Text _moneyLine;

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

    /// <summary>Horizontal shift applied to the humanoid sheet so the backpack uses the right half.</summary>
    private const float EquipShiftX = -55f;

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
        if (_built) return;
        _built = true;

        SuppressTitle = true;
        Build(Localization.T("CHARACTER INFO"));
        _current = ActiveTab;

        BuildTopButtons();
        BuildPanels();
        ShowTab(_current);
    }

    private void OnDisable()
    {
        if (Instance == this)
            Instance = null;
    }

    private void BuildTopButtons()
    {
        string[] names = { "Stats", "Skills", "Inventory", "Map" };
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
            rt.anchoredPosition = new Vector2(-w * 0.5f + bw * (0.5f + i), 16f);
            rt.sizeDelta = new Vector2(bw - 6f, 46f);
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
            lt.fontSize = Mathf.Max(18f, Screen.height / 52f);
            lt.color = Color.white;
            lt.alignment = TextAlignmentOptions.Center;
        }
    }

    private void BuildPanels()
    {
        // Stats panel: stats + current class/race + change buttons.
        _panels[Tab.Stats] = MakePanel("StatsPanel");
        _statsLine = MakeBodyText(_panels[Tab.Stats].transform, "Stats", new Vector2(-270f, 150f), 500f, 340f);
        _statsLine.fontSize = Mathf.Max(22f, Screen.height / 40f);
        MakeButton(_panels[Tab.Stats].transform, "ChangeClassBtn", "Change Class", new Vector2(-90f, -170f), () => OpenChangeDialog("class"));
        MakeButton(_panels[Tab.Stats].transform, "ChangeRaceBtn", "Change Race", new Vector2(90f, -170f), () => OpenChangeDialog("race"));

        // Skills panel.
        _panels[Tab.Skills] = MakePanel("SkillsPanel");
        BuildSkillTypeBar(_panels[Tab.Skills].transform);
        _skillPointsLine = MakeBodyText(_panels[Tab.Skills].transform, "SkillPoints", new Vector2(-270f, 96f), 240f, 36f);
        _skillListLine = MakeBodyText(_panels[Tab.Skills].transform, "SkillList", new Vector2(-270f, 30f), 360f, 210f);
        _captureLine = MakeBodyText(_panels[Tab.Skills].transform, "Capture", new Vector2(-270f, -160f), 240f, 28f);
        MakeButton(_panels[Tab.Skills].transform, "LearnBtn", "Learn Selected", new Vector2(205f, -120f), LearnSelected);
        MakeButton(_panels[Tab.Skills].transform, "AssignKeyBtn", "Assign Key", new Vector2(205f, -74f), AssignNextSkillKey);

        // Merged Inventory + Equipment panel: equipment sheet LEFT, backpack + use bar RIGHT.
        _panels[Tab.Inventory] = MakePanel("InventoryPanel");
        BuildEquipmentSheet(_panels[Tab.Inventory].transform);
        _equipSummary = MakeBodyText(_panels[Tab.Inventory].transform, "Equipment", new Vector2(-288f, 178f), 560f, 44f);
        BuildWeaponLane(_panels[Tab.Inventory].transform);
        BuildStorageGrid(_panels[Tab.Inventory].transform);
        BuildHotbarMirror(_panels[Tab.Inventory].transform);
        _moneyLine = MakeBodyText(_panels[Tab.Inventory].transform, "Money", new Vector2(8f, -160f), 260f, 24f);

        // Map panel (placeholder summary; the dedicated WorldMapUI is separate).
        _panels[Tab.Map] = MakePanel("MapPanel");
        _mapLine = MakeBodyText(_panels[Tab.Map].transform, "Map", new Vector2(-270f, 160f), 500f, 200f);

        EnsureChangeDialog();
    }

    // ── Backpack storage grid (Inventory tab, right side) ───────────────────
    // 30 Minecraft-style storage slots (5x6). Dragging a stack onto a grid cell stores it;
    // dragging one onto the hotbar mirror row below moves it to the use bar.
    private void BuildStorageGrid(Transform parent)
    {
        MakeBodyText(parent, "StorageHeader", new Vector2(8f, 178f), 280f, 28f)
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
            rt.anchoredPosition = new Vector2(8f + col * 52f, 146f - row * 50f);
            rt.sizeDelta = new Vector2(46f, 46f);
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
    // Mirrors slots 0-9 of ToolManager (same data as the bottom HUD use bar). Click to select
    // (number-key behaviour), drag to swap a stack onto it, drop into it to store from the grid.
    private void BuildHotbarMirror(Transform parent)
    {
        MakeBodyText(parent, "UseBarHeader", new Vector2(8f, -182f), 290f, 24f)
            .text = Localization.T("Use bar (1-0)");

        for (int i = 0; i < ToolManager.HotbarSlotCount; i++)
        {
            var go = new GameObject("HudInvSlot_" + i);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(8f + i * 30f, -206f);
            rt.sizeDelta = new Vector2(28f, 26f);
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
    // Compact 3x3 grid of owned weapons. Clicking selects the weapon (highlighted) so a following
    // click on a hand slot equips it; dragging a cell onto L. Hand / R. Hand also equips it.
    private void BuildWeaponLane(Transform parent)
    {
        MakeBodyText(parent, "WeaponsHeader", new Vector2(-288f, -118f), 300f, 24f)
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
            rt.anchoredPosition = new Vector2(-288f + col * 100f, -146f - row * 32f);
            rt.sizeDelta = new Vector2(94f, 26f);
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
        rt.anchoredPosition = new Vector2(pos.x + EquipShiftX, pos.y + 90f);
        rt.sizeDelta = new Vector2(84f, 30f);
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

    // ── Class / Race change dialog ──────────────────────────────────────────
    // A picker + confirmation built over the same canvas: lists the unlocked classes (with their
    // requirements) or races (with the Ritual Stone cost for non-Human). Picking an option stages
    // a pending change shown in the confirm strip; Confirm applies it, Cancel keeps the old value.
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
        boxRt.sizeDelta = new Vector2(580f, 440f);
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

        _changeTitle = MakeDialogText(box.transform, "Title", new Vector2(0f, 196f), 540f, 32f, TextAlignmentOptions.Center);
        _changeTitle.fontSize = Mathf.Max(18f, Screen.height / 44f);

        _changeOptions = new GameObject("Options").transform;
        _changeOptions.SetParent(box.transform, false);
        var or = _changeOptions.gameObject.AddComponent<RectTransform>();
        or.anchorMin = new Vector2(0.5f, 0.5f);
        or.anchorMax = new Vector2(0.5f, 0.5f);
        or.anchoredPosition = Vector2.zero;
        or.sizeDelta = Vector2.zero;

        _changeConfirmText = MakeDialogText(box.transform, "Confirm", new Vector2(0f, -184f), 540f, 30f, TextAlignmentOptions.Center);

        MakeDialogButton(box.transform, "ConfirmBtn", "Confirm Change", new Vector2(-90f, -212f), ApplyPendingChange);
        MakeDialogButton(box.transform, "CancelBtn", "Cancel", new Vector2(90f, -212f), () => CloseChangeDialog(false));

        var close = MakeDialogButton(box.transform, "DialogClose", "X", new Vector2(256f, 196f), () => CloseChangeDialog(false));
        close.GetComponent<RectTransform>().sizeDelta = new Vector2(40f, 32f);

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

    private TMP_Text MakeDialogText(Transform parent, string name, Vector2 pos, float w, float h, TextAlignmentOptions align)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = new Vector2(w, h);
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
        rt.sizeDelta = new Vector2(160f, 34f);
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
        rt.sizeDelta = new Vector2(w, 26f);
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
            MakeDialogOption(parent, name + req, new Vector2(x, y), 270f, unlocker.IsUnlocked(c.classId), () =>
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
            MakeDialogOption(parent, r.displayName + cost, new Vector2(x, y), 270f, unlocked, () =>
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
        if (_current == Tab.Stats)
            RefreshStats();
        else
            RefreshInventoryUi();
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
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(-230f + bw * (0.5f + i), 132f);
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
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
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
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = new Vector2(160f, 38f);
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
    }

    protected override void Refresh()
    {
        switch (_current)
        {
            case Tab.Stats: RefreshStats(); break;
            case Tab.Skills: SetSkillList(); break;
            case Tab.Inventory: RefreshInventoryUi(); break;
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

        string classLine = CurrentClassSummary();
        string raceLine = CurrentRaceSummary();
        if (!string.IsNullOrEmpty(classLine)) sb.Append('\n').Append(classLine);
        if (!string.IsNullOrEmpty(raceLine)) sb.Append('\n').Append(raceLine);
        _statsLine.text = sb.ToString();
    }

    private string CurrentClassSummary()
    {
        var unlocker = ClassUnlockerOf();
        if (unlocker == null) return "";
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

    private string CurrentRaceSummary()
    {
        var mgr = RaceMgrOf();
        if (mgr == null) return "";
        var active = mgr.ActiveRace;
        if (active == null) return "";
        return Localization.F("Race: {0} — {1}", active.displayName, active.PassiveDescription);
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

        // Cycle only weapons the player owns (starter is always owned).
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
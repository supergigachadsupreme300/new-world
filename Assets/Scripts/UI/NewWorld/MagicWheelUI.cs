using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.InputSystem;

/// <summary>
/// Alt magic-selection wheel (PC). While fighting-mode and holding a magic weapon, holding
/// Alt opens a ring at the screen's centre listing every learned castable skill and unlocks
/// the cursor. Hovering highlights a spell; releasing Alt while a spell is hovered locks it in
/// as the armed magic. With a magic armed and a magic weapon held, the fighting-mode left click
/// casts it via <see cref="SkillProfile.Execute"/> instead of the weapon's basic attack
/// (see <see cref="ConsumeArmedCast"/>).
/// </summary>
public sealed class MagicWheelUI : MonoBehaviour
{
    /// <summary>Hard upper bound on ring entries.</summary>
    public const int MaxEntries = 64;

    private static MagicWheelUI _instance;

    private Canvas _canvas;
    private RectTransform _ring;
    private GameObject _dim;
    private TMP_Text _centerLabel;
    private readonly List<RectTransform> _slots = new List<RectTransform>();
    private readonly List<TMP_Text> _slotLabels = new List<TMP_Text>();
    private readonly List<string> _slotIds = new List<string>();
    private TMP_Text _armedChipLabel;
    private RectTransform _armedChip;
    private bool _fontsApplied;

    private PlayerController _player;
    private bool _isOpen;
    private int _hovered = -1;
    private string _armedSkillId;
    private float _slotSize;
    private float _ringRadius;

    private static readonly Color SlotColor = new Color(0.13f, 0.13f, 0.18f, 0.95f);
    private static readonly Color SlotHover = new Color(0.30f, 0.50f, 0.95f, 1f);
    private static readonly Color SlotArmed = new Color(0.25f, 0.40f, 0.70f, 1f);
    private static readonly Color SlotDimmed = new Color(0.13f, 0.13f, 0.18f, 0.42f);

    /// <summary>Whether the wheel ring is currently open (cursor unlocked).</summary>
    public static bool IsOpen => _instance != null && _instance._isOpen;

    /// <summary>The skill id currently armed by the wheel, or null.</summary>
    public static string ArmedSkillId => _instance != null ? _instance._armedSkillId : null;

    /// <summary>Ensure the singleton exists and returns it (builds on first access).</summary>
    public static MagicWheelUI Ensure()
    {
        if (_instance == null)
        {
            var go = new GameObject("MagicWheelUI");
            Object.DontDestroyOnLoad(go);
            _instance = go.AddComponent<MagicWheelUI>();
        }
        return _instance;
    }

    /// <summary>
    /// Route the fighting-mode left click: when a magic is armed and a magic weapon is held the
    /// armed skill is executed and the click is consumed (true); otherwise the caller falls back
    /// to the weapon's normal attack (false).
    /// </summary>
    public static bool ConsumeArmedCast()
    {
        return ReleaseArmedCast(0f);
    }

    /// <summary>True when a magic is armed AND a magic weapon is held (charge/basic gating).</summary>
    public static bool HasArmedMagic()
    {
        if (_instance == null || string.IsNullOrEmpty(_instance._armedSkillId)) return false;
        if (!_instance.HoldingMagicWeapon()) return false;
        return _instance.PlayerProfile() != null;
    }

    /// <summary>
    /// Ensure a spell is armed so magic aim/charge/fire works without visiting the wheel first.
    /// Keeps the wheel's armed choice when still learned; otherwise auto-arms the most recently
    /// armed spell if still learned, then the first learned castable skill off cooldown, then the
    /// first learned castable. Returns true when a spell is armed afterwards.
    /// </summary>
    public static bool EnsureArmedMagic()
    {
        if (_instance == null) return false;
        var profile = _instance.PlayerProfile();
        if (profile == null || !_instance.HoldingMagicWeapon()) return false;

        if (!string.IsNullOrEmpty(_instance._armedSkillId))
        {
            bool stillLearned = false;
            foreach (var id in profile.Learned)
                if (id == _instance._armedSkillId) { stillLearned = true; break; }
            if (stillLearned) return true;
        }

        var caster = _instance._player != null ? _instance._player.GetComponent<SpellCaster>() : null;

        foreach (var id in profile.Learned)
        {
            var skill = SkillCatalog.Find(id);
            if (skill != null && !skill.IsPassive &&
                (caster == null || caster.CooldownRemaining(skill.CooldownKey) <= 0f))
            {
                _instance._armedSkillId = id;
                _instance.RefreshArmedChip();
                return true;
            }
        }

        foreach (var id in profile.Learned)
        {
            var skill = SkillCatalog.Find(id);
            if (skill != null && !skill.IsPassive)
            {
                _instance._armedSkillId = id;
                _instance.RefreshArmedChip();
                return true;
            }
        }
        return false;
    }

    /// <summary>Fire the armed magic with a charge level (0..1). True if the cast began.</summary>
    public static bool ReleaseArmedCast(float charge)
    {
        if (_instance == null || string.IsNullOrEmpty(_instance._armedSkillId)) return false;
        if (!_instance.HoldingMagicWeapon()) return false;
        var profile = _instance.PlayerProfile();
        if (profile == null) return false;
        profile.ExecuteCharged(_instance._armedSkillId, charge);
        return true;
    }

    private void Awake()
    {
        if (_instance == null) _instance = this;
        Build();
    }

    private void OnDestroy()
    {
        if (_instance == this) _instance = null;
    }

    private void Update()
    {
        EnsureFonts();

        if (GameInput.IsMobile)
        {
            if (_isOpen) Close(false, false);
            return;
        }

        var gm = GameManager.Instance;
        if (gm == null)
            return;
        _player = gm.Player;

        if (gm.InGame && !gm.GamePaused && !MenuPanelBase.AnyShown)
        {
            if (_player == null || _player.IgnoreInput)
            {
                if (_isOpen) Close(false, true);
                return;
            }
            if (!_player.FightingMode || !HoldingMagicWeapon())
            {
                if (_isOpen) Close(false, true);
                return;
            }

            if (AltHeld())
            {
                if (!_isOpen) Open();
            }
            else if (_isOpen)
            {
                Close(true, true);
            }

            if (_isOpen) Paint();
            else RefreshArmedChip();
            return;
        }

        // Paused / modal menu up: shut the wheel without fighting that UI's cursor state.
        if (_isOpen) Close(false, false);
    }

    private static bool AltHeld()
    {
        var kb = Keyboard.current;
        return kb != null && (kb.leftAltKey.isPressed || kb.rightAltKey.isPressed);
    }

    private bool HoldingMagicWeapon()
    {
        var combat = _player != null ? _player.GetComponent<CombatController>() : null;
        if (combat == null) return false;
        return HandIsMagic(combat.RightHand) || HandIsMagic(combat.LeftHand);
    }

    private static bool HandIsMagic(GameObject hand)
    {
        if (hand == null) return false;
        var host = hand.GetComponent<WeaponRigHost>();
        return host != null && host.Data != null && host.Data.Category == WeaponCategory.Magic;
    }

    private SkillProfile PlayerProfile()
    {
        return _player != null ? _player.GetComponent<SkillProfile>() : null;
    }

    private void Open()
    {
        RebuildEntries();
        _hovered = -1;
        _isOpen = true;
        _dim.SetActive(true);
        _ring.gameObject.SetActive(true);
        if (_armedChip != null) _armedChip.gameObject.SetActive(false);
        GameInput.SetCursorLocked(false);
    }

    /// <summary>Close the wheel. <paramref name="applySelection"/> locks in the hovered spell;
    /// <paramref name="relockCursor"/> restores the locked cursor (skipped when a menu owns it).</summary>
    private void Close(bool applySelection, bool relockCursor)
    {
        if (applySelection && _hovered >= 0 && _hovered < _slotIds.Count)
            _armedSkillId = _slotIds[_hovered];
        _isOpen = false;
        if (_dim != null) _dim.SetActive(false);
        if (_ring != null) _ring.gameObject.SetActive(false);
        if (relockCursor) GameInput.SetCursorLocked(true);
        ClearSlots();
        RefreshArmedChip();
    }

    private void RebuildEntries()
    {
        ClearSlots();
        var profile = PlayerProfile();
        if (profile == null) return;

        var ids = new List<string>();
        foreach (var id in profile.Learned)
        {
            var skill = SkillCatalog.Find(id);
            if (skill != null && !skill.IsPassive) ids.Add(id);
            if (ids.Count >= MaxEntries) break;
        }

        // Size the ring for the slot count: grow the ring radius (up to 0.42 of canvas height)
        // and shrink the slots as needed so every slot keeps an arc gap and the ring fits on screen.
        float h = CanvasHeight();
        float maxRadius = h * 0.42f;
        float desiredSlot = h * 0.13f;
        float gapRatio = 1.15f;
        _slotSize = ids.Count > 0 ? Mathf.Min(desiredSlot, (maxRadius * 2f * Mathf.PI) / (ids.Count * gapRatio)) : desiredSlot;
        _ringRadius = ids.Count > 0 ? Mathf.Min(Mathf.Max(h * 0.28f, (ids.Count * _slotSize * gapRatio) / (2f * Mathf.PI)), maxRadius) : 0f;

        for (int i = 0; i < ids.Count; i++)
            CreateSlot(i, ids.Count, SkillCatalog.Find(ids[i]));
    }

    private void CreateSlot(int index, int total, Skill skill)
    {
        float slotSize = _slotSize;
        float radius = _ringRadius;
        float ang = -90f + (360f * index) / total;
        float rad = ang * Mathf.Deg2Rad;
        Vector2 pos = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad)) * radius;

        var slot = new GameObject("Slot_" + index);
        slot.transform.SetParent(_ring, false);
        var rect = slot.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = pos;
        rect.sizeDelta = new Vector2(slotSize, slotSize);
        var img = slot.AddComponent<Image>();
        img.color = SlotColor;
        img.raycastTarget = false;
        _slots.Add(rect);

        var label = MakeLabel(slot.transform, "Label", Vector2.zero, Vector2.one,
            Mathf.Max(7f, slotSize * 0.42f), Color.white);
        label.text = skill != null && !string.IsNullOrEmpty(skill.displayName) ? skill.displayName : skill != null ? skill.id : "?";
        label.enableWordWrapping = false;
        label.overflowMode = TextOverflowModes.Ellipsis;
        _slotLabels.Add(label);
        _slotIds.Add(skill != null ? skill.id : "");
    }

    private void ClearSlots()
    {
        for (int i = 0; i < _slots.Count; i++)
            if (_slots[i] != null)
                Destroy(_slots[i].gameObject);
        _slots.Clear();
        _slotLabels.Clear();
        _slotIds.Clear();
        _hovered = -1;
    }

    private void Paint()
    {
        if (_slotIds.Count == 0)
        {
            if (_centerLabel != null)
                _centerLabel.text = "No spells learned yet";
            return;
        }

        Vector2 mousePx = Mouse.current != null
            ? Mouse.current.position.ReadValue()
            : new Vector2(-100000f, -100000f);
        Vector2 mouseCanvas = (mousePx - new Vector2(Screen.width, Screen.height) * 0.5f) / CanvasScale();

        _hovered = -1;
        float best = float.MaxValue;
        float hit = _slotSize * 0.78f;
        for (int i = 0; i < _slots.Count; i++)
        {
            float d = Vector2.Distance(mouseCanvas, _slots[i].anchoredPosition);
            if (d < hit && d < best)
            {
                best = d;
                _hovered = i;
            }
        }

        for (int i = 0; i < _slots.Count; i++)
        {
            bool hover = i == _hovered;
            _slots[i].localScale = hover ? Vector3.one * 1.18f : Vector3.one;
            var img = _slots[i].GetComponent<Image>();
            Color c;
            if (hover) c = SlotHover;
            else if (_slotIds[i] == _armedSkillId) c = SlotArmed;
            else if (IsOnCooldown(_slotIds[i])) c = SlotDimmed;
            else c = SlotColor;
            img.color = c;
        }

        if (_centerLabel != null)
        {
            _centerLabel.text = _hovered >= 0
                ? "Release to lock in " + SkillName(_slotIds[_hovered])
                : "Release to keep \"" + SkillName(_armedSkillId) + "\"";
        }
    }

    private bool IsOnCooldown(string skillId)
    {
        var caster = _player != null ? _player.GetComponent<SpellCaster>() : null;
        if (caster == null) return false;
        var skill = SkillCatalog.Find(skillId);
        return skill != null && caster.CooldownRemaining(skill.CooldownKey) > 0f;
    }

    private void RefreshArmedChip()
    {
        bool show = !_isOpen
            && !string.IsNullOrEmpty(_armedSkillId)
            && _player != null
            && _player.FightingMode
            && HoldingMagicWeapon();
        if (_armedChip != null) _armedChip.gameObject.SetActive(show);
        if (show && _armedChipLabel != null)
            _armedChipLabel.text = "Armed: " + SkillName(_armedSkillId);
    }

    private static string SkillName(string id)
    {
        var skill = SkillCatalog.Find(id);
        return skill != null && !string.IsNullOrEmpty(skill.displayName) ? skill.displayName : id;
    }

    // ── UI construction ──────────────────────────────────────────────────

    private void Build()
    {
        _canvas = HudCanvas.CreateOverlay("MagicWheelCanvas");
        _canvas.sortingOrder = 45;
        var root = (RectTransform)_canvas.transform;

        // Full-screen dim behind the open ring.
        _dim = new GameObject("Dim");
        _dim.transform.SetParent(root, false);
        var dimRect = _dim.AddComponent<RectTransform>();
        dimRect.anchorMin = Vector2.zero;
        dimRect.anchorMax = Vector2.one;
        dimRect.offsetMin = Vector2.zero;
        dimRect.offsetMax = Vector2.zero;
        var dimImg = _dim.AddComponent<Image>();
        dimImg.color = new Color(0f, 0f, 0f, 0.45f);
        dimImg.raycastTarget = false;
        _dim.SetActive(false);

        // Ring group holding the centre hint + slot buttons.
        var ringGo = new GameObject("Ring");
        ringGo.transform.SetParent(root, false);
        _ring = ringGo.AddComponent<RectTransform>();
        _ring.anchorMin = Vector2.zero;
        _ring.anchorMax = Vector2.one;
        _ring.offsetMin = Vector2.zero;
        _ring.offsetMax = Vector2.zero;
        _centerLabel = MakeLabel(_ring, "CenterHint", Vector2.zero, Vector2.one,
            Mathf.Max(16f, Screen.height / 46f), Color.white);
        _ring.gameObject.SetActive(false);

        // Persistent "Armed: X" chip in the bottom-left corner.
        var chip = new GameObject("ArmedChip");
        chip.transform.SetParent(root, false);
        _armedChip = chip.AddComponent<RectTransform>();
        _armedChip.anchorMin = Vector2.zero;
        _armedChip.anchorMax = Vector2.zero;
        _armedChip.pivot = Vector2.zero;
        _armedChip.anchoredPosition = CanvasUnits(new Vector2(12f, 12f));
        _armedChip.sizeDelta = CanvasUnits(new Vector2(240f, 36f));
        var chipImg = chip.AddComponent<Image>();
        chipImg.color = new Color(0.08f, 0.08f, 0.12f, 0.8f);
        chipImg.raycastTarget = false;
        _armedChipLabel = MakeLabel(chip.transform, "Label", Vector2.zero, Vector2.one,
            Mathf.Max(12f, Screen.height / 72f), Color.white);
        _armedChip.gameObject.SetActive(false);
    }

    private static TMP_Text MakeLabel(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax,
        float fontSizePx, Color color)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rect = go.AddComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.fontSize = fontSizePx;
        tmp.color = color;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.raycastTarget = false;
        ApplyFont(tmp);
        return tmp;
    }

    /// <summary>Apply the default TMP font once the UIManager exists (the wheel can be built before it).</summary>
    private static void ApplyFont(TMP_Text tmp)
    {
        if (tmp == null) return;
        GameManager.Instance?.UIManager?.ApplyDefaultFont(tmp);
    }

    /// <summary>One-shot pass that re-applies the default font to all labels the moment it's available.</summary>
    private void EnsureFonts()
    {
        if (_fontsApplied) return;
        if (GameManager.Instance?.UIManager == null) return;
        ApplyFont(_centerLabel);
        ApplyFont(_armedChipLabel);
        for (int i = 0; i < _slotLabels.Count; i++)
            ApplyFont(_slotLabels[i]);
        _fontsApplied = true;
    }

    private static float CanvasScale() => Screen.width / (1280f / MenuPanelBase.UiScale);

    private static float CanvasHeight() => Screen.height / CanvasScale();

    private static Vector2 CanvasUnits(Vector2 pixels) => new Vector2(pixels.x / CanvasScale(), pixels.y / CanvasScale());
}
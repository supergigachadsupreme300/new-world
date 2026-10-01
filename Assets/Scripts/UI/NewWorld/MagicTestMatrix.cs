using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;

/// <summary>
/// Dev/test magic grid, note 1av/1bu: Alt no longer opens the magic ring (the wheel is retired for
/// selection). The matrix is the Alt destination - a tall, scrollable grid pinned to the right edge
/// that lists every castable magic skill in the game (the full Magic category: base + branch schools,
/// not just what the current profile has learned), grouped by school (<see cref="DamageType"/>).
/// Clicking a row only arms that spell in the wheel's armed chip — it does not cast (the click must not
/// fire a spell; it picks one). Close the grid and use the normal LMB/RMB charge/release flow to fire.
/// While the grid is open the controller suppresses its attack/aim/charge input so a row click cannot
/// leak into a cast.
/// </summary>
public sealed class MagicTestMatrix : MonoBehaviour
{
    private static MagicTestMatrix _instance;
    private static Canvas _canvas;
    private static SpellCaster _casterFor;
    private static bool _matrixOpen;
    private static bool _altWasDown;

    private RectTransform _root;
    private RectTransform _content;
    private ScrollRect _scroll;
    private TMP_Text _statusLabel;
    private readonly List<RowEntry> _rows = new List<RowEntry>();
    private bool _built;

    private sealed class RowEntry
    {
        public string Id;
        public Image Image;
    }

    private static readonly Color RowNormal = new Color(0.13f, 0.14f, 0.18f, 1f);
    private static readonly Color RowArmed = new Color(0.30f, 0.24f, 0.06f, 1f);
    private static readonly Color RowCooldown = new Color(0.08f, 0.08f, 0.11f, 1f);
    private static readonly Color StatusIdle = new Color(0.70f, 0.80f, 1f, 1f);
    private static readonly Color StatusOk = new Color(0.55f, 1f, 0.60f, 1f);
    private static readonly Color StatusFail = new Color(1f, 0.45f, 0.45f, 1f);

    public static MagicTestMatrix Ensure()
    {
        if (_instance == null)
        {
            var go = new GameObject("MagicTestMatrix");
            Object.DontDestroyOnLoad(go);
            _instance = go.AddComponent<MagicTestMatrix>();
        }
        return _instance;
    }

    public static bool IsOpen { get { return _matrixOpen; } }

    private void Awake()
    {
        if (_instance == null) _instance = this;
    }

    private void Update()
    {
        if (!_matrixOpen) return;

        if (GameInput.IsMobile)
        {
            Close(false);
            return;
        }

        var kb = Keyboard.current;
        if (kb != null && kb.escapeKey.wasPressedThisFrame)
        {
            Close(true);
            return;
        }

        RefreshRows();
    }

    /// <summary>Alt edge-trigger: Alt toggles the matrix (called by the wheel's Update).</summary>
    public void HandleAlt(bool altHeld)
    {
        if (GameInput.IsMobile) return;
        bool down = AltHeld();
        if (down && !_altWasDown) Toggle();
        _altWasDown = down;
    }

    private static bool AltHeld()
    {
        var kb = Keyboard.current;
        return kb != null && (kb.leftAltKey.isPressed || kb.rightAltKey.isPressed);
    }

    private void Toggle()
    {
        if (_matrixOpen) Close(true);
        else Open();
    }

    private void Open()
    {
        EnsureBuilt();
        var caster = SpellCaster();
        if (caster == null) return;
        _matrixOpen = true;
        if (_canvas != null) _canvas.gameObject.SetActive(true);
        GameInput.SetCursorLocked(false);
    }

    private void Close(bool relock)
    {
        _matrixOpen = false;
        if (_canvas != null) _canvas.gameObject.SetActive(false);
        if (relock) GameInput.SetCursorLocked(true);
    }

    private static SpellCaster SpellCaster()
    {
        var gm = GameManager.Instance;
        var p = gm != null ? gm.Player : null;
        if (p == null) return null;
        if (_casterFor == null || _casterFor.gameObject != p.gameObject)
            _casterFor = p.GetComponent<SpellCaster>();
        return _casterFor;
    }

    // ── Build ─────────────────────────────────────────────────────────────

    private void EnsureBuilt()
    {
        if (_built) return;
        _built = true;

        _canvas = HudCanvas.CreateOverlay("MagicTestMatrixCanvas");
        _canvas.sortingOrder = 41;
        _root = (RectTransform)_canvas.transform;

        var bg = new GameObject("BgImage");
        bg.transform.SetParent(_root, false);
        var br = bg.AddComponent<RectTransform>();
        br.anchorMin = new Vector2(1f, 0f);
        br.anchorMax = new Vector2(1f, 1f);
        br.pivot = new Vector2(1f, 0.5f);
        br.anchoredPosition = Vector2.zero;
        br.sizeDelta = CanvasUnits(new Vector2(320f, 0f));
        var img = bg.AddComponent<Image>();
        img.color = new Color(0.05f, 0.05f, 0.08f, 0.96f);

        var title = MakeTopLabel(br, "Title", 10f, 40f, 18f, Color.white);
        title.text = "MAGIC TEST GRID";
        _statusLabel = MakeTopLabel(br, "Status", 56f, 22f, 12f, StatusIdle);
        _statusLabel.text = "Click a spell to arm it.";

        var scrollGo = new GameObject("Scroll");
        scrollGo.transform.SetParent(br, false);
        var sr = scrollGo.AddComponent<RectTransform>();
        sr.anchorMin = new Vector2(0f, 0f);
        sr.anchorMax = new Vector2(1f, 1f);
        sr.offsetMin = new Vector2(8f, 8f);
        sr.offsetMax = new Vector2(-8f, -82f);
        var srImg = scrollGo.AddComponent<Image>();
        srImg.color = new Color(0f, 0f, 0f, 0.35f);

        _scroll = scrollGo.AddComponent<ScrollRect>();
        _scroll.horizontal = false;
        _scroll.vertical = true;
        _scroll.movementType = ScrollRect.MovementType.Clamped;
        _scroll.inertia = true;
        _scroll.scrollSensitivity = Mathf.Max(24f, CanvasScale());

        var viewport = new GameObject("Viewport");
        viewport.transform.SetParent(scrollGo.transform, false);
        var vr = viewport.AddComponent<RectTransform>();
        vr.anchorMin = Vector2.zero;
        vr.anchorMax = Vector2.one;
        vr.offsetMin = Vector2.zero;
        vr.offsetMax = Vector2.zero;
        viewport.AddComponent<RectMask2D>();
        _scroll.viewport = vr;

        _content = new GameObject("Content").AddComponent<RectTransform>();
        _content.SetParent(viewport.transform, false);
        _content.anchorMin = new Vector2(0f, 1f);
        _content.anchorMax = new Vector2(1f, 1f);
        _content.pivot = new Vector2(0.5f, 1f);
        _content.sizeDelta = Vector2.zero;
        var fit = _content.gameObject.AddComponent<ContentSizeFitter>();
        fit.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        var group = _content.gameObject.AddComponent<VerticalLayoutGroup>();
        group.padding = new RectOffset(8, 8, 8, 8);
        group.spacing = 3f;
        group.childAlignment = TextAnchor.UpperCenter;
        group.childControlWidth = true;
        group.childControlHeight = false;
        group.childForceExpandWidth = true;
        group.childForceExpandHeight = false;
        _scroll.content = _content;

        PopulateRows();

        // Top of the list on first build only; reopening keeps the scroll position (1ce follow-up).
        if (_scroll != null) _scroll.verticalNormalizedPosition = 1f;

        _canvas.gameObject.SetActive(false);
    }

    private TMP_Text MakeTopLabel(Transform parent, string name, float topInset, float height,
        float fontSize, Color color)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rect = go.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.offsetMin = new Vector2(10f, -(topInset + height));
        rect.offsetMax = new Vector2(-10f, -topInset);

        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.fontSize = fontSize;
        tmp.color = color;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.raycastTarget = false;
        tmp.enableWordWrapping = true;
        ApplyFont(tmp);
        return tmp;
    }

    private void PopulateRows()
    {
        var castables = new List<Skill>();
        foreach (var skill in SkillCatalog.OfType(SkillType.Magic))
            if (!skill.IsPassive)
                castables.Add(skill);

        castables.Sort((a, b) =>
        {
            int bySchool = ((int)a.DamageKind).CompareTo((int)b.DamageKind);
            return bySchool != 0
                ? bySchool
                : string.CompareOrdinal(a.displayName ?? a.id, b.displayName ?? b.id);
        });

        DamageType last = (DamageType)(-1);
        foreach (var skill in castables)
        {
            if (skill.DamageKind != last)
            {
                last = skill.DamageKind;
                MakeSchoolHeader(last);
            }
            MakeRow(skill);
        }
    }

    private void MakeSchoolHeader(DamageType school)
    {
        var go = new GameObject("Header_" + school);
        go.transform.SetParent(_content, false);
        var rect = go.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.sizeDelta = new Vector2(0f, 20f);
        var layout = go.AddComponent<LayoutElement>();
        layout.minHeight = 20f;
        layout.preferredHeight = 20f;

        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.fontSize = 12f;
        tmp.color = SchoolColor(school);
        tmp.alignment = TextAlignmentOptions.Left;
        tmp.raycastTarget = false;
        tmp.text = school.ToString();
        ApplyFont(tmp);
    }

    private void MakeRow(Skill skill)
    {
        var row = new GameObject("Row_" + skill.id);
        row.transform.SetParent(_content, false);
        var rect = row.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.sizeDelta = new Vector2(0f, 30f);
        var layout = row.AddComponent<LayoutElement>();
        layout.minHeight = 30f;
        layout.preferredHeight = 30f;

        var rowImg = row.AddComponent<Image>();
        rowImg.color = RowNormal;

        var label = new GameObject("Label");
        label.transform.SetParent(row.transform, false);
        var lr = label.AddComponent<RectTransform>();
        lr.anchorMin = Vector2.zero;
        lr.anchorMax = Vector2.one;
        lr.offsetMin = new Vector2(10f, 0f);
        lr.offsetMax = new Vector2(-6f, 0f);
        var tmp = label.AddComponent<TextMeshProUGUI>();
        tmp.fontSize = 13f;
        tmp.color = Color.white;
        tmp.alignment = TextAlignmentOptions.Left;
        tmp.raycastTarget = false;
        tmp.enableWordWrapping = false;
        tmp.overflowMode = TextOverflowModes.Ellipsis;
        tmp.text = skill.displayName ?? skill.id;
        ApplyFont(tmp);

        var button = row.AddComponent<Button>();
        button.targetGraphic = rowImg;
        var id = skill.id;
        button.onClick.AddListener(() => SelectId(id));

        _rows.Add(new RowEntry { Id = skill.id, Image = rowImg });
    }

    private void RefreshRows()
    {
        string armed = MagicWheelUI.ArmedSkillId;
        for (int i = 0; i < _rows.Count; i++)
        {
            var row = _rows[i];
            Color c;
            if (row.Id == armed) c = RowArmed;
            else if (IsOnCooldown(row.Id)) c = RowCooldown;
            else c = RowNormal;
            if (row.Image.color != c) row.Image.color = c;
        }
    }

    private static bool IsOnCooldown(string id)
    {
        var caster = SpellCaster();
        if (caster == null) return false;
        var skill = SkillCatalog.Find(id);
        return skill != null && caster.CooldownRemaining(skill.CooldownKey) > 0f;
    }

    /// <summary>Arm a single magic id in the wheel's armed chip (no cast, no learn, no focus top-up).</summary>
    private void SelectId(string id)
    {
        var skill = SkillCatalog.Find(id);
        if (skill == null)
        {
            SetStatus("Cannot select " + id, false);
            return;
        }

        MagicWheelUI.ForceArmMagic(id);
        SetStatus("Armed  " + (skill.displayName ?? id), true);
    }

    private void SetStatus(string text, bool ok)
    {
        if (_statusLabel == null) return;
        _statusLabel.text = text;
        _statusLabel.color = ok ? StatusOk : StatusFail;
    }

    private static void ApplyFont(TMP_Text tmp)
    {
        if (tmp == null) return;
        GameManager.Instance?.UIManager?.ApplyDefaultFont(tmp);
    }

    /// <summary>
    /// The QA swatch for a school HEADER in this matrix, restored in 1ij to the pre-1ib literals.
    ///
    /// <para><b>Deliberately NOT <c>SpellLook.SchoolColor</c>, and this is the one sanctioned
    /// exception to "no consumer re-derives a colour".</b> 1ib folded this into the canonical
    /// palette, which silently recoloured every header on this debug screen. Two reasons that was
    /// wrong rather than merely redundant:</para>
    /// <list type="number">
    /// <item><b>It is a swatch, not a readout.</b> The whole point of a per-school header is to name
    /// the school at a glance. When the swatch and the thing being judged are the same colour, a
    /// mis-coloured spell is invisible here — the panel stops being able to catch the bug it exists
    /// to catch. Four of the nine schools drifted visibly (Dark, Wind, Arcane, Ice).</item>
    /// <item><b>1ib's own claim was "change nothing on screen".</b> This screen is not the game, so
    /// a colour change here is pure unrequested diff — the one place where inheriting the canonical
    /// palette bought nothing at all.</item>
    /// </list>
    /// <para>Gameplay colours all come from <see cref="SpellLook"/>; this is the one place that keeps
    /// its own table, and it is QA-only (the matrix is a debug screen, not a save-scoped surface), so
    /// it needs no parity check. If a school's canonical colour is being tuned, update BOTH tables —
    /// that is the cost of the exception, stated so it is not a surprise.</para>
    /// </summary>
    private static Color SchoolColor(DamageType school)
    {
        switch (school)
        {
            case DamageType.Fire: return new Color(1f, 0.50f, 0.20f, 1f);
            case DamageType.Ice: return new Color(0.55f, 0.85f, 1f, 1f);
            case DamageType.Lightning: return new Color(1f, 0.90f, 0.40f, 1f);
            case DamageType.Holy: return new Color(1f, 1f, 0.70f, 1f);
            case DamageType.Dark: return new Color(0.70f, 0.55f, 1f, 1f);
            case DamageType.Wind: return new Color(0.65f, 1f, 0.85f, 1f);
            case DamageType.Earth: return new Color(0.70f, 0.60f, 0.40f, 1f);
            case DamageType.Water: return new Color(0.40f, 0.70f, 1f, 1f);
            case DamageType.Arcane: return new Color(1f, 0.55f, 0.90f, 1f);
            default: return Color.white;
        }
    }

    private static float CanvasScale() => Screen.width / (1280f / MenuPanelBase.UiScale);
    private static Vector2 CanvasUnits(Vector2 pixels) => new Vector2(pixels.x / CanvasScale(), pixels.y / CanvasScale());
}

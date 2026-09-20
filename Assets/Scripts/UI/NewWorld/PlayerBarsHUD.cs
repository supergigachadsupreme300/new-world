using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Phase 8 (Task 8.1): real HP / FP / Stamina fill bars. Complements the existing text HUD
/// (UIManager) by drawing three anchored fill bars driven from Platform-free data sources:
/// <see cref="PlayerController"/> (HP, Stamina) and an optional <see cref="SpellCaster"/>
/// (FP) discovered on the player, with <see cref="PlayerStats"/> fallback for FP max.
/// A small drain/flash eases value changes and flashes on damage.
/// </summary>
public sealed class PlayerBarsHUD : MonoBehaviour
{
    public bool ShowOnInGame = true;

    private const float BarWidth = 340f;
    private const float BarHeight = 30f;
    private const float BarSpacing = 36f;

    /// <summary>Status chip columns and pooled chip count for the strip under the bars.</summary>
    private const int StatusColumns = 4;
    private const int MaxStatusChips = 10;

    /// <summary>How fast the fill eases down toward a lower target (fraction per second).</summary>
    private const float DrainRate = 1.6f;

    private Canvas _canvas;
    private Image _hpFill;
    private Image _fpFill;
    private Image _stamFill;
    private Image _chargeFill;
    private TMP_Text _hpText;
    private TMP_Text _fpText;
    private TMP_Text _stamText;
    private TMP_Text _chargeText;
    private float _lastHp = -1f, _lastMaxHp = -1f;
    private float _lastFp = -1f, _lastMaxFp = -1f;
    private float _lastStam = -1f, _lastMaxStam = -1f;
    private float _lastHpRaw = -1f;
    private float _hpShown = -1f, _fpShown = -1f, _stamShown = -1f;
    private float _flashTimer;

    // Cached player refs (resolve once per player object instead of GetComponentInChildren
    // every frame) and last-drawn label values so unchanged bars never touch the text mesh.
    private Transform _cachedPlayerRoot;
    private SpellCaster _caster;
    private PlayerStats _stats;
    private int _lastHpText = -1, _lastMaxHpText = -1;
    private int _lastFpText = -1, _lastMaxFpText = -1;
    private int _lastStamText = -1, _lastMaxStamText = -1;
    private int _lastChargePct = -1;

    // Status strip: a pooled row of colored-square chips under the bars that lists every active
    // status on the player root (combat DoT/CC + the food/drink stamina modifier).
    private RectTransform _statusPanel;
    private readonly List<StatusChip> _statusChips = new List<StatusChip>();
    private readonly List<StatusEntry> _statusEntries = new List<StatusEntry>();
    private int _statusFrameCounter;

    private void OnEnable()
    {
        if (_canvas == null)
        {
            _canvas = HudCanvas.CreateOverlay("PlayerBarsCanvas");
            Build();
        }
    }

    private void Build()
    {
        var rect = (RectTransform)_canvas.transform;
        float top = -50f;

        // HP bar (top-left, full)
        _hpFill = HudCanvas.CreateBar(rect, "HPBar",
            new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(16f, top), new Vector2(BarWidth, BarHeight),
            new Color(0f, 0f, 0f, 0.65f), new Color(0.8f, 0.16f, 0.14f));
        _hpText = MakeLabel(_hpFill.transform.parent as RectTransform);

        // FP bar (under HP, same width)
        _fpFill = HudCanvas.CreateBar(rect, "FPBar",
            new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(16f, top - BarSpacing), new Vector2(BarWidth, BarHeight),
            new Color(0f, 0f, 0f, 0.65f), new Color(0.16f, 0.5f, 0.85f));
        _fpText = MakeLabel(_fpFill.transform.parent as RectTransform);

        // Stamina bar (under FP, same width)
        _stamFill = HudCanvas.CreateBar(rect, "StaminaBar",
            new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(16f, top - BarSpacing * 2f), new Vector2(BarWidth, BarHeight),
            new Color(0f, 0f, 0f, 0.65f), new Color(0.2f, 0.8f, 0.3f));
        _stamText = MakeLabel(_stamFill.transform.parent as RectTransform);

        // Cast/draw charge bar (under Stamina) — only shown while an aim is charging.
        _chargeFill = HudCanvas.CreateBar(rect, "MagicChargeBar",
            new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(16f, top - BarSpacing * 3f), new Vector2(BarWidth, BarHeight),
            new Color(0f, 0f, 0f, 0.65f), new Color(0.55f, 0.35f, 0.9f));
        _chargeText = MakeLabel(_chargeFill.transform.parent as RectTransform);
        _chargeFill.transform.parent.gameObject.SetActive(false);

        BuildStatusPanel(rect, top);
    }

    /// <summary>Build the status chip strip under the bars (colored squares + text, pooled).</summary>
    private void BuildStatusPanel(RectTransform canvasRect, float top)
    {
        var panelGo = new GameObject("StatusPanel");
        panelGo.transform.SetParent(canvasRect, false);
        _statusPanel = panelGo.AddComponent<RectTransform>();
        _statusPanel.anchorMin = new Vector2(0f, 1f);
        _statusPanel.anchorMax = new Vector2(0f, 1f);
        _statusPanel.pivot = new Vector2(0f, 1f);
        _statusPanel.anchoredPosition = new Vector2(16f, top - BarSpacing * 4f);
        _statusPanel.sizeDelta = new Vector2(BarWidth, BarHeight * 0.8f);

        var grid = panelGo.AddComponent<GridLayoutGroup>();
        grid.cellSize = new Vector2(82f, 24f);
        grid.spacing = new Vector2(4f, 4f);
        grid.childAlignment = TextAnchor.UpperLeft;
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = StatusColumns;

        for (int i = 0; i < MaxStatusChips; i++)
        {
            var chipGo = new GameObject("StatusChip" + i);
            chipGo.transform.SetParent(_statusPanel, false);
            var chipRect = chipGo.AddComponent<RectTransform>();
            var chipColor = chipGo.AddComponent<Image>();
            chipColor.raycastTarget = false;

            var labelGo = new GameObject("Label");
            labelGo.transform.SetParent(chipRect, false);
            var labelRect = labelGo.AddComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;
            var tmp = labelGo.AddComponent<TextMeshProUGUI>();
            tmp.fontSize = 12f;
            tmp.color = Color.white;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.enableWordWrapping = false;
            tmp.raycastTarget = false;

            _statusChips.Add(new StatusChip { Root = chipRect, Target = chipColor, Text = tmp });
            chipGo.SetActive(false);
        }
    }

    private static TMP_Text MakeLabel(RectTransform barRoot)
    {
        var go = new GameObject("Label");
        go.transform.SetParent(barRoot, false);
        var rect = go.AddComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.fontSize = Mathf.Max(14f, Screen.height / 70f);
        tmp.color = Color.white;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.raycastTarget = false;
        return tmp;
    }

    private void Update()
    {
        var gm = GameManager.Instance;
        var player = gm != null ? gm.Player : null;
        bool inGame = gm != null && gm.InGame;
        if (_canvas != null)
        {
            bool want = ShowOnInGame ? inGame : true;
            if (_canvas.gameObject.activeSelf != want)
                _canvas.gameObject.SetActive(want);
        }
        if (player == null) return;

        EnsureRefs(player.transform);

        float hp = player.HP;
        float maxHp = Mathf.Max(1f, player.MaxHP);
        float stam = player.Stamina;
        float maxStam = Mathf.Max(1f, player.MaxStamina);
        float fp = ReadCurrentFp();
        float maxFp = Mathf.Max(1f, ReadMaxFp());

        // Damage flash must compare to the raw previous value BEFORE Tick syncs it.
        bool hpHit = hp < _lastHpRaw - 0.01f;
        _lastHpRaw = hp;

        Tick(_hpFill, hp, maxHp, ref _lastHp, ref _lastMaxHp, ref _hpShown);
        Tick(_stamFill, stam, maxStam, ref _lastStam, ref _lastMaxStam, ref _stamShown);
        Tick(_fpFill, fp, maxFp, ref _lastFp, ref _lastMaxFp, ref _fpShown);
        UpdateLabel(_hpText, "HP", hp, maxHp, ref _lastHpText, ref _lastMaxHpText);
        UpdateLabel(_fpText, "Mana", fp, maxFp, ref _lastFpText, ref _lastMaxFpText);
        UpdateLabel(_stamText, "Stam", stam, maxStam, ref _lastStamText, ref _lastMaxStamText);

        UpdateChargeBar(player);

        UpdateStatusStrip(player);

        if (hpHit && _hpFill != null)
        {
            _flashTimer = 0.25f;
            _hpFill.color = new Color(1f, 0.9f, 0.4f);
        }
        if (_flashTimer > 0f)
        {
            _flashTimer -= Time.deltaTime;
            if (_flashTimer <= 0f && _hpFill != null)
                _hpFill.color = new Color(0.8f, 0.16f, 0.14f);
        }
    }

    /// <summary>
    /// Ease the fill toward its target so decreases are visibly rendered even with fast regen:
    /// increases and level-ups apply instantly; decreases drain down over a fraction of a second.
    /// </summary>
    private static void Tick(Image fill, float cur, float max, ref float lastCur, ref float lastMax, ref float shown)
    {
        if (fill == null) return;

        bool changed = Mathf.Abs(cur - lastCur) >= 0.01f || Mathf.Abs(max - lastMax) >= 0.01f;
        if (changed)
        {
            lastCur = cur;
            lastMax = max;
        }

        if (shown < 0f) shown = 1f;
        float target = Mathf.Clamp01(cur / max);
        float next;
        if (target < shown)
            next = Mathf.Max(target, shown - DrainRate * Time.deltaTime);
        else
            next = target;

        if (changed || Mathf.Abs(next - shown) > 0.001f)
        {
            shown = next;
            fill.fillAmount = shown;
        }
    }

    private static void UpdateLabel(TMP_Text label, string name, float cur, float max,
        ref int lastCur, ref int lastMax)
    {
        if (label == null) return;
        int c = Mathf.RoundToInt(cur);
        int m = Mathf.RoundToInt(max);
        if (c == lastCur && m == lastMax) return;
        lastCur = c;
        lastMax = m;
        label.text = name + " " + c + "/" + m;
    }

    private void UpdateChargeBar(PlayerController player)
    {
        if (_chargeFill == null) return;
        var root = _chargeFill.transform.parent;
        bool charging = player != null && player.IsCharging;
        if (root.gameObject.activeSelf != charging)
            root.gameObject.SetActive(charging);
        if (!charging) return;
        float level = player.MagicChargeProgress;
        _chargeFill.fillAmount = Mathf.Clamp01(level);
        // Magic overcharges past 100% — overflow the fill past the track's end so the bar keeps
        // expressing the released cap (fill is left-anchored, so scaling X grows rightward).
        _chargeFill.transform.localScale = level > 1f ? new Vector3(level, 1f, 1f) : Vector3.one;
        if (_chargeText != null)
        {
            int pct = Mathf.RoundToInt(level * 100f);
            if (pct != _lastChargePct)
            {
                _lastChargePct = pct;
                _chargeText.text = "Charge " + pct + "%";
            }
        }
    }

    /// <summary>
    /// Poll the player root for every active status (combat DoT/CC components and the food/drink
    /// stamina modifier) and mirror it onto the pooled chip strip under the bars. Chips update
    /// only when their color or text actually changes.
    /// </summary>
    private void UpdateStatusStrip(PlayerController player)
    {
        if (_statusPanel == null) return;
        // Statuses count down in whole seconds and appear/disappear at event pace — a 3-frame
        // (~50 ms) poll is invisible but cuts the GetComponent scan + chip preserves by 1/3 (1dr).
        _statusFrameCounter++;
        if (_statusFrameCounter % 3 != 0)
            return;
        _statusEntries.Clear();
        var root = player.transform.root;

        var dot = root.GetComponent<SpellDoT>();
        if (dot != null)
        {
            bool burn = dot.Type == DamageType.Fire;
            _statusEntries.Add(new StatusEntry(
                burn ? BurnStatusColor : OtherDotStatusColor,
                (burn ? "BURN " : "DOT ") + Mathf.CeilToInt(dot.Remaining) + "s"));
        }

        var wet = root.GetComponent<WetStatus>();
        if (wet != null)
            _statusEntries.Add(new StatusEntry(WetStatusColor, "WET " + Mathf.CeilToInt(wet.Remaining) + "s"));

        var chill = root.GetComponent<ChillStatus>();
        if (chill != null && chill.Cold > 0)
            _statusEntries.Add(new StatusEntry(ChillStatusColor,
                "CHILL " + chill.Cold + "/" + ChillStatus.FrostStackThreshold));

        var blind = root.GetComponent<BlindStatus>();
        if (blind != null)
            _statusEntries.Add(new StatusEntry(BlindStatusColor, "BLIND " + Mathf.CeilToInt(blind.Remaining) + "s"));

        if (player.HasStaminaBuff)
        {
            int pct = Mathf.RoundToInt((player.StaminaRegenModifier - 1f) * 100f);
            int secs = Mathf.CeilToInt(player.StaminaBuffRemaining);
            if (pct >= 0)
                _statusEntries.Add(new StatusEntry(StamBuffColor, "+" + pct + "% STAM " + secs + "s"));
            else
                _statusEntries.Add(new StatusEntry(StamDebuffColor, pct + "% STAM " + secs + "s"));
        }

        int rows = (_statusEntries.Count + StatusColumns - 1) / StatusColumns;
        float h = rows * 24f + Mathf.Max(0, rows - 1) * 4f;
        if (Mathf.Abs(_statusPanel.sizeDelta.y - h) > 0.01f)
            _statusPanel.sizeDelta = new Vector2(BarWidth, h);

        for (int i = 0; i < _statusChips.Count; i++)
        {
            var chip = _statusChips[i];
            bool active = i < _statusEntries.Count;
            if (chip.Root.gameObject.activeSelf != active)
                chip.Root.gameObject.SetActive(active);
            if (!active) continue;
            var entry = _statusEntries[i];
            if (chip.Target.color != entry.Color)
                chip.Target.color = entry.Color;
            if (chip.Text.text != entry.Text)
                chip.Text.text = entry.Text;
        }
    }

    /// <summary>
    /// Resolve the FP data sources once per player object. The player can be swapped between
    /// game sessions, so the cache is keyed to the player root transform.
    /// </summary>
    private void EnsureRefs(Transform playerRoot)
    {
        if (_cachedPlayerRoot == playerRoot) return;
        _cachedPlayerRoot = playerRoot;
        _caster = playerRoot != null ? playerRoot.GetComponentInChildren<SpellCaster>() : null;
        _stats = playerRoot != null ? playerRoot.GetComponentInChildren<PlayerStats>() : null;
    }

    private float ReadCurrentFp()
    {
        return _caster != null ? _caster.CurrentFp : 0f;
    }

    private float ReadMaxFp()
    {
        if (_stats != null) return _stats.MaxFocusPoints;
        return _caster != null ? Mathf.Max(1f, _caster.MaxFp) : 1f;
    }

    // Status chip colors (colored squares per status in the strip under the bars).
    private static readonly Color BurnStatusColor = new Color(0.9f, 0.42f, 0.15f);
    private static readonly Color OtherDotStatusColor = new Color(0.45f, 0.7f, 0.3f);
    private static readonly Color WetStatusColor = new Color(0.2f, 0.55f, 0.9f);
    private static readonly Color ChillStatusColor = new Color(0.4f, 0.85f, 0.95f);
    private static readonly Color BlindStatusColor = new Color(0.3f, 0.3f, 0.32f);
    private static readonly Color StamBuffColor = new Color(0.3f, 0.8f, 0.45f);
    private static readonly Color StamDebuffColor = new Color(0.85f, 0.3f, 0.3f);

    /// <summary>One pooled chip in the status strip: a colored square plus a centered label.</summary>
    private sealed class StatusChip
    {
        public RectTransform Root;
        public Image Target;
        public TMP_Text Text;
    }

    /// <summary>A transient status display line (color square + text) resolved each frame.</summary>
    private struct StatusEntry
    {
        public readonly Color Color;
        public readonly string Text;

        public StatusEntry(Color color, string text)
        {
            Color = color;
            Text = text;
        }
    }
}
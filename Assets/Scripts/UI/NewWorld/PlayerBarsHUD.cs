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

        float hp = player.HP;
        float maxHp = Mathf.Max(1f, player.MaxHP);
        float stam = player.Stamina;
        float maxStam = Mathf.Max(1f, player.MaxStamina);
        float fp = ReadCurrentFp(player.transform);
        float maxFp = Mathf.Max(1f, ReadMaxFp(player.transform));

        // Damage flash must compare to the raw previous value BEFORE Tick syncs it.
        bool hpHit = hp < _lastHpRaw - 0.01f;
        _lastHpRaw = hp;

        Tick(_hpFill, hp, maxHp, ref _lastHp, ref _lastMaxHp, ref _hpShown);
        Tick(_stamFill, stam, maxStam, ref _lastStam, ref _lastMaxStam, ref _stamShown);
        Tick(_fpFill, fp, maxFp, ref _lastFp, ref _lastMaxFp, ref _fpShown);
        UpdateLabel(_hpText, "HP", hp, maxHp);
        UpdateLabel(_fpText, "Mana", fp, maxFp);
        UpdateLabel(_stamText, "Stam", stam, maxStam);

        UpdateChargeBar(player);

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

    private static void UpdateLabel(TMP_Text label, string name, float cur, float max)
    {
        if (label == null) return;
        label.text = name + " " + Mathf.RoundToInt(cur) + "/" + Mathf.RoundToInt(max);
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
        _chargeFill.fillAmount = level;
        if (_chargeText != null)
            _chargeText.text = "Charge " + Mathf.RoundToInt(level * 100f) + "%";
    }

    private static float ReadCurrentFp(Transform player)
    {
        var caster = player != null ? player.GetComponentInChildren<SpellCaster>() : null;
        if (caster != null) return caster.CurrentFp;
        return 0f;
    }

    private static float ReadMaxFp(Transform player)
    {
        var stats = player != null ? player.GetComponentInChildren<PlayerStats>() : null;
        if (stats != null) return stats.MaxFocusPoints;
        var caster = player != null ? player.GetComponentInChildren<SpellCaster>() : null;
        return caster != null ? Mathf.Max(1f, caster.MaxFp) : 1f;
    }
}
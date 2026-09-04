using UnityEngine;
using UnityEngine.UI;

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

    private Canvas _canvas;
    private Image _hpFill;
    private Image _fpFill;
    private Image _stamFill;
    private float _lastHp = -1f, _lastMaxHp = -1f;
    private float _lastFp = -1f, _lastMaxFp = -1f;
    private float _lastStam = -1f, _lastMaxStam = -1f;
    private float _flashTimer;

    private void OnEnable()
    {
        _canvas = HudCanvas.CreateOverlay("PlayerBarsCanvas");
        Build();
    }

    private void Build()
    {
        var rect = (RectTransform)_canvas.transform;
        float top = -40f;

        // HP bar (top-left, full)
        _hpFill = HudCanvas.CreateBar(rect, "HPBar",
            new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(16f, top), new Vector2(260f, 22f),
            new Color(0f, 0f, 0f, 0.65f), new Color(0.8f, 0.16f, 0.14f));

        // FP bar (under HP, shorter)
        _fpFill = HudCanvas.CreateBar(rect, "FPBar",
            new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(16f, top - 26f), new Vector2(200f, 14f),
            new Color(0f, 0f, 0f, 0.65f), new Color(0.16f, 0.5f, 0.85f));

        // Stamina bar (under FP, shortest)
        _stamFill = HudCanvas.CreateBar(rect, "StaminaBar",
            new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(16f, top - 46f), new Vector2(170f, 10f),
            new Color(0f, 0f, 0f, 0.65f), new Color(0.2f, 0.8f, 0.3f));
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

        UpdateBar(_hpFill, hp, maxHp, ref _lastHp, ref _lastMaxHp);
        UpdateBar(_stamFill, stam, maxStam, ref _lastStam, ref _lastMaxStam);
        UpdateBar(_fpFill, fp, maxFp, ref _lastFp, ref _lastMaxFp);

        // Damage flash
        if (hp < _lastHp && _hpFill != null)
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

    private static void UpdateBar(Image fill, float cur, float max, ref float lastCur, ref float lastMax)
    {
        if (fill == null) return;
        if (Mathf.Abs(cur - lastCur) < 0.01f && Mathf.Abs(max - lastMax) < 0.01f) return;
        lastCur = cur;
        lastMax = max;
        fill.fillAmount = Mathf.Clamp01(cur / max);
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
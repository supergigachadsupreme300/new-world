using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Aggregates the ACTIVE class's passive modifiers into a cached multiplier set (game-design
/// §3.2.1). Mirrors <see cref="RacePassiveManager"/> / <c>ReligionManager</c>: reads the active
/// class from <see cref="ClassUnlocker"/>, recomputes caches on class change, and exposes the
/// modifiers combat/utility code queries each frame. Only the active class's skills grant
/// modifiers — switching classes swaps the whole set in/out.
/// </summary>
[DisallowMultipleComponent]
public class ClassPassiveManager : MonoBehaviour
{
    private ClassUnlocker _unlocker;
    private PlayerController _controller;
    private string _lastClassId = string.Empty;
    private readonly Dictionary<ClassModType, float> _mods = new Dictionary<ClassModType, float>();

    private void Awake()
    {
        _unlocker = GetComponent<ClassUnlocker>();
        _controller = GetComponent<PlayerController>();
    }

    private void OnEnable()
    {
        if (_unlocker != null)
        {
            _unlocker.OnActiveClassChanged += OnClassChanged;
            _unlocker.OnClassUnlocked += OnClassChanged;
        }
    }

    private void OnDisable()
    {
        if (_unlocker != null)
        {
            _unlocker.OnActiveClassChanged -= OnClassChanged;
            _unlocker.OnClassUnlocked -= OnClassChanged;
        }
    }

    private void Start()
    {
        Refresh();
    }

    private void OnClassChanged(ClassData _)
    {
        Refresh();
    }

    /// <summary>Rebuild the cached modifier sum from the active class's tree.</summary>
    public void Refresh()
    {
        _mods.Clear();
        if (_unlocker == null) _unlocker = GetComponent<ClassUnlocker>();

        string activeId = _unlocker != null ? _unlocker.ActiveClassId : string.Empty;
        _lastClassId = activeId;

        foreach (var skill in ClassSkillCatalog.ForClass(activeId))
        {
            if (skill == null || skill.Mods == null) continue;
            foreach (var mod in skill.Mods)
            {
                if (mod.amount == 0f) continue;
                if (_mods.TryGetValue(mod.kind, out float cur)) _mods[mod.kind] = cur + mod.amount;
                else _mods[mod.kind] = mod.amount;
            }
        }
    }

    private float Sum(ClassModType kind) => _mods.TryGetValue(kind, out float v) ? v : 0f;

    private float Mul(ClassModType kind) => 1f + Sum(kind);

    /// <summary>Id of the active class this manager is currently mirroring.</summary>
    public string ActiveClassId => _unlocker != null ? _unlocker.ActiveClassId : string.Empty;

    // ── Modifier getters (all read-only; safe to call every frame) ─────────────
    public float MeleePowerMul => Mul(ClassModType.MeleePowerMul);
    public float SpellPowerMul => Mul(ClassModType.SpellPowerMul);
    public float CooldownMul => Mul(ClassModType.CooldownMul);
    public float AttackSpeedMul => Mul(ClassModType.AttackSpeedMul);
    public float BackstabMul => Mul(ClassModType.BackstabMul);
    public float HealPowerMul => Mul(ClassModType.HealPowerMul);
    public float ParryWindowMul => Mul(ClassModType.ParryWindowMul);
    public float ConsumablePotencyMul => Mul(ClassModType.ConsumablePotencyMul);
    public float DefenseMeleeMul => Mul(ClassModType.DefenseMeleeMul);
    public float EquipLoadBonus => Sum(ClassModType.EquipLoadBonus);
    public float RangedHandlingMul => Mul(ClassModType.RangedHandlingMul);
    public float AuraStrengthMul => Mul(ClassModType.AuraStrength);
    public float BlockingMul => Mul(ClassModType.BlockingMul);
    public float StaggerResistMul => Mul(ClassModType.StaggerResistMul);
    public float CraftSuccessMul => Mul(ClassModType.CraftSuccessMul);
    public float RepairMul => Mul(ClassModType.RepairMul);
    public float StaminaRegenMul => Mul(ClassModType.StaminaRegenMul);
    public float HpRegenPerSecond => Sum(ClassModType.HpRegenPerSecond);

    /// <summary>
    /// Live berserker multiplier: 1 + ΣBerserkScale × (1 − HP/MaxHP) — the lower the player's HP,
    /// the stronger their melee output. 1 when no berserk scale is active or HP is full.
    /// </summary>
    public float BerserkMultiplier
    {
        get
        {
            float scale = Sum(ClassModType.BerserkScale);
            if (scale <= 0f) return 1f;
            if (_controller == null) _controller = GetComponent<PlayerController>();
            if (_controller == null || _controller.MaxHP <= 0) return 1f;
            float hpFrac = Mathf.Clamp01(_controller.HP / (float)_controller.MaxHP);
            return 1f + scale * (1f - hpFrac);
        }
    }

    /// <summary>
    /// Effective melee multiplier: MeleePowerMul × BerserkMultiplier (folded live so berserkers
    /// get stronger as HP drops).
    /// </summary>
    public float EffectiveMeleeMul => MeleePowerMul * BerserkMultiplier;
}
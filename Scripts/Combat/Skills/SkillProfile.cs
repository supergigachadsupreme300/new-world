using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Player skill profile (Phase 10). Owns the skill-point bank, the learned-skill set, the
/// prerequisite gating and the execution of castable skills. Composes the OO skill model:
/// every learned/executed skill is handled through its composed <see cref="Skill.Effect"/> —
/// this profile never branches on skill kind.
///
/// Point source: +1 point on every <see cref="SkillXpTracker"/> category level-up, spendable on
/// any skill whose prerequisites are met. Passives apply additively to <see cref="PlayerStats"/>
/// on learn (always-on). Castables route through the shared <see cref="SpellCaster"/> /
/// <see cref="StaminaSystem"/> / weapon art executor.
/// </summary>
public sealed class SkillProfile : MonoBehaviour
{
    [Header("Points")]
    public int Points = 0;

    [Header("State")]
    public List<string> LearnedSkillIds = new List<string>();

    private readonly HashSet<string> _learned = new HashSet<string>();
    private PlayerStats _stats;
    private SpellCaster _caster;
    private StaminaSystem _stamina;
    private SkillXpTracker _xp;

    private void Awake()
    {
        _stats = GetComponent<PlayerStats>();
        _caster = GetComponent<SpellCaster>();
        _stamina = GetComponent<StaminaSystem>();
        _xp = GetComponent<SkillXpTracker>();

        foreach (var id in LearnedSkillIds) _learned.Add(id);

        if (_xp != null)
            _xp.OnSkillLevelUp += OnCategoryLevelUp;
    }

    private void OnDestroy()
    {
        if (_xp != null)
            _xp.OnSkillLevelUp -= OnCategoryLevelUp;
    }

    /// <summary>Grant one skill point per category level-up (any of the 6 categories).</summary>
    private void OnCategoryLevelUp(SkillType skill, int level)
    {
        Points++;
    }

    /// <summary>True if the given skill id has been learned.</summary>
    public bool HasLearned(string id) => _learned.Contains(id);

    /// <summary>A read-only iteration of learned skill ids.</summary>
    public IEnumerable<string> Learned => _learned;

    /// <summary>
    /// Whether <paramref name="skill"/> can be learned now: prerequisites met and points
    /// available and not already learned.
    /// </summary>
    public bool CanLearn(Skill skill)
    {
        if (skill == null || _learned.Contains(skill.id)) return false;
        if (Points <= 0) return false;
        return skill.PrereqsMet(_learned);
    }

    /// <summary>
    /// Learn (spend 1 point, gate on prerequisites) and, if passive, apply its effect immediately.
    /// Passives are always-on once learned.
    /// </summary>
    public bool Learn(Skill skill)
    {
        if (!CanLearn(skill)) return false;
        Points--;
        _learned.Add(skill.id);
        LearnedSkillIds.Add(skill.id);

        if (skill.IsPassive)
        {
            var ctx = BuildContext();
            skill.Effect?.Execute(ctx);
        }
        return true;
    }

    /// <summary>
    /// Execute a castable skill by id. Validates learned, affordability and cooldown, spends
    /// the cost, starts the cooldown, then runs the composed effect. Returns true if it fired.
    /// Passives resolve to false (they are not cast).
    /// </summary>
    public bool Execute(string id)
    {
        var skill = SkillCatalog.Find(id);
        if (skill == null || skill.IsPassive || !_learned.Contains(id)) return false;

        var ctx = BuildContext();
        if (!skill.CanAfford(ctx)) return false;
        if (ctx.Caster != null && !ctx.Caster.CooldownReady(skill.CooldownKey)) return false;
        if (!skill.TrySpend(ctx)) return false;

        if (ctx.Caster != null && skill.SkillCost.Cooldown > 0f)
            ctx.Caster.StartCooldown(skill.CooldownKey, skill.SkillCost.Cooldown);

        skill.Effect?.Execute(ctx);
        return true;
    }

    private SkillContext BuildContext()
    {
        var art = GetComponentInChildren<WeaponArtExecutor>();
        return new SkillContext
        {
            Caster = _caster,
            Stamina = _stamina,
            Stats = _stats,
            ArtExecutor = art,
            Origin = transform,
            User = gameObject
        };
    }
}
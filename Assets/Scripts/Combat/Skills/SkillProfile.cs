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
    /// <summary>Temporary diagnostic logging for the skill-execution report.</summary>
    private const bool SkillDebug = true;

    [Header("Points")]
    public int Points = 0;

    [Header("State")]
    public List<string> LearnedSkillIds = new List<string>();

    private readonly HashSet<string> _learned = new HashSet<string>();
    private readonly HashSet<string> _appliedPassives = new HashSet<string>();
    private PlayerStats _stats;
    private SpellCaster _caster;
    private StaminaSystem _stamina;
    private SkillXpTracker _xp;

    // ── Skill leveling ────────────────────────────────────────────────────
    public const int MaxSkillLevel = 100;

    /// <summary>XP granted per successful cast (raw, before the race all-bonus).</summary>
    private const float XpPerUse = 12f;

    /// <summary>Category-bar XP granted per cast (before the race per-category bonus).</summary>
    private const float CategoryXpPerUse = 10f;

    /// <summary>XP needed for the first level-up.</summary>
    private const float BaseXpToNext = 20f;

    /// <summary>Extra XP added per level to the next-level threshold (linear curve).</summary>
    private const float XpGrowthPerLevel = 15f;

    private readonly List<SkillProgress> _progress = new List<SkillProgress>();

    private void Awake()
    {
        _stats = GetComponent<PlayerStats>();
        _caster = GetComponent<SpellCaster>();
        _stamina = GetComponent<StaminaSystem>();
        _xp = GetComponent<SkillXpTracker>();

        foreach (var id in LearnedSkillIds) _learned.Add(id);

        if (_xp == null)
            _xp = GetComponent<SkillXpTracker>();
        if (_xp != null)
            _xp.OnSkillLevelUp += OnCategoryLevelUp;
    }

    private void OnDestroy()
    {
        if (_xp != null)
            _xp.OnSkillLevelUp -= OnCategoryLevelUp;
    }

    /// <summary>Grant one skill point per category level-up (any of the 7 categories).</summary>
    private void OnCategoryLevelUp(SkillType skill, int level)
    {
        Points++;
    }

    // ── Skill leveling: read / persist / advance ──────────────────────────

    /// <summary>Current learned level of a skill (1 when never used).</summary>
    public int LevelOf(string id)
    {
        foreach (var p in _progress)
            if (p.SkillId == id) return p.Level;
        return 1;
    }

    /// <summary>Progress of a skill, if it has earned any XP yet.</summary>
    public bool TryGetProgress(string id, out int level, out float xp, out float toNext)
    {
        foreach (var p in _progress)
        {
            if (p.SkillId != id) continue;
            level = p.Level;
            xp = p.Xp;
            toNext = XpToNext(p.Level);
            return true;
        }
        level = 1;
        xp = 0f;
        toNext = BaseXpToNext;
        return false;
    }

    /// <summary>Grants per-use XP (skill level + category bar balanced by an existing architecture).</summary>
    private void GainUse(Skill skill, SkillContext ctx)
    {
        if (skill == null) return;
        var entry = _progress.Find(p => p.SkillId == skill.id);
        if (entry == null)
        {
            entry = new SkillProgress { SkillId = skill.id };
            _progress.Add(entry);
        }

        float raceBonus = 1f;
        if (ctx.Stats != null && ctx.Stats.Race != null)
            raceBonus = 1f + ctx.Stats.Race.XpBonusAll / 100f;
        float skillXp = XpPerUse * raceBonus;
        var talents = GetComponent<TalentTracker>();
        if (talents != null)
            skillXp *= 1f + talents.TypeXpBonus(skill.Type) / 100f;
        entry.Xp += skillXp;

        while (entry.Level < MaxSkillLevel && entry.Xp >= XpToNext(entry.Level))
        {
            entry.Xp -= XpToNext(entry.Level);
            entry.Level++;
        }

        if (_xp != null)
            _xp.AddXp(skill.Type, CategoryXpPerUse);
    }

    private static float XpToNext(int level) => BaseXpToNext + XpGrowthPerLevel * (level - 1);

    /// <summary>Serialize per-skill levels to JSON for the save file.</summary>
    public string SaveProgress()
    {
        return JsonUtility.ToJson(new SkillProgressSet { Progress = _progress });
    }

    /// <summary>Load per-skill levels from a saved JSON blob.</summary>
    public void RestoreProgress(string json)
    {
        _progress.Clear();
        if (string.IsNullOrEmpty(json)) return;
        try
        {
            var set = JsonUtility.FromJson<SkillProgressSet>(json);
            if (set?.Progress != null)
                foreach (var p in set.Progress)
                    if (p != null && !string.IsNullOrEmpty(p.SkillId))
                        _progress.Add(p);
        }
        catch
        {
            _progress.Clear();
        }
    }

    [System.Serializable]
    private sealed class SkillProgressSet
    {
        public List<SkillProgress> Progress = new List<SkillProgress>();
    }

    /// <summary>
    /// Re-resolve a combat dependency if it was missing (the combat stack is rigged later than
    /// this profile by <c>WeaponRigBuilder.EnsureCombatStack</c>, so cached fields may be null at
    /// Awake). Called at learn/cast time so spur-of-the-moment component additions still work.
    /// </summary>
    private void ReconcileDependencies()
    {
        if (_stats == null) _stats = GetComponent<PlayerStats>();
        if (_caster == null) _caster = GetComponent<SpellCaster>();
        if (_stamina == null) _stamina = GetComponent<StaminaSystem>();
        if (_xp == null)
        {
            _xp = GetComponent<SkillXpTracker>();
            if (_xp != null) _xp.OnSkillLevelUp += OnCategoryLevelUp;
        }
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
        ReconcileDependencies();
        Points--;
        _learned.Add(skill.id);
        LearnedSkillIds.Add(skill.id);

        if (skill.IsPassive)
        {
            var ctx = BuildContext();
            skill.Effect?.Execute(ctx);
            _appliedPassives.Add(skill.id);
        }
        return true;
    }

    /// <summary>
    /// Dev/test grant (used by the magic test matrix): learn a castable active skill for this
    /// session without spending a point or meeting prerequisites, so any magic spell can be cast
    /// for testing without levelling. Passives are refused (their effects are not applied). Returns
    /// true when the skill is castable and learned afterwards.
    /// </summary>
    public bool TestGrant(string id)
    {
        if (string.IsNullOrEmpty(id)) return false;
        var skill = SkillCatalog.Find(id);
        if (skill == null || skill.IsPassive) return false;
        if (_learned.Add(id))
            LearnedSkillIds.Add(id);
        return true;
    }

    /// <summary>
    /// Rebuild the point bank + learned set from a save (used by <see cref="SaveManager"/> on load).
    /// Passives that haven't been applied this session are re-run so restored skills keep working
    /// without requiring a reload — guarded by <see cref="_appliedPassives"/> so a restore that
    /// lands on an already-applied session never double-buffs.
    /// </summary>
    public void RestoreState(int points, IEnumerable<string> learned)
    {
        ReconcileDependencies();
        Points = Mathf.Max(0, points);
        _learned.Clear();
        LearnedSkillIds.Clear();
        if (learned != null)
            foreach (var id in learned)
                if (!string.IsNullOrEmpty(id) && _learned.Add(id))
                    LearnedSkillIds.Add(id);

        SkillCatalog.EnsureBuilt();
        foreach (var id in _learned)
        {
            if (_appliedPassives.Contains(id)) continue;
            var skill = SkillCatalog.Find(id);
            if (skill != null && skill.IsPassive)
            {
                skill.Effect?.Execute(BuildContext());
                _appliedPassives.Add(id);
            }
        }
    }

    /// <summary>
    /// Execute a castable skill by id (no charge). Validates learned, affordability and cooldown,
    /// spends the cost, starts the cooldown, then runs the composed effect. Returns true if it fired.
    /// Passives resolve to false (they are not cast).
    /// </summary>
    public bool Execute(string id)
    {
        return ExecuteCharged(id, 0f);
    }

    /// <summary>
    /// Execute a castable skill by id with a charge level (0..1+) and any focus already drained
    /// in real time while charging. Identical to <see cref="Execute"/> except the charge is carried
    /// on the context so magic deliveries scale damage/size/cost.
    /// Wheel-cast magic (a <see cref="SpellCastEffect"/> skill) hands everything to SpellCaster: the
    /// fast cast owns FP/instant delivery/cooldown and reports the true begin result so callers never
    /// fake-fire — other skills keep the flat resource spend + cooldown here.
    /// </summary>
    public bool ExecuteCharged(string id, float charge, float prepaidFocus = 0f)
    {
        var skill = SkillCatalog.Find(id);
        if (skill == null)
        {
            // Class-skill fallback (§3.2.1): hotkeyed ids of the form "cls.{class}.{node}" route
            // through the class caster (active-class validation, shared cooldowns/costs).
            ReconcileDependencies();
            var classSkill = ClassSkillCatalog.Find(id);
            if (classSkill != null)
                return ClassSkillCaster.Execute(gameObject, id, charge, BuildContext());
            // Race-skill fallback: hotkeyed ids of the form "rac.{race}.{node}" route
            // through the race caster (active-race validation, shared cooldowns/costs).
            var raceSkill = RaceSkillCatalog.Find(id);
            if (raceSkill != null)
                return RaceSkillCaster.Execute(gameObject, id, charge, BuildContext());
            if (SkillDebug) Debug.Log($"[Skill] \"{id}\" not found in catalog");
            return false;
        }
        if (skill.IsPassive)
        {
            if (SkillDebug) Debug.Log($"[Skill] \"{id}\" is passive — not castable");
            return false;
        }
        if (!_learned.Contains(id))
        {
            if (SkillDebug) Debug.Log($"[Skill] \"{id}\" not learned yet");
            return false;
        }

        ReconcileDependencies();

        // Shield skills (§3.3) bash with the equipped shield — they are meaningless without one
        // in hand, so gate before any cost/cooldown is spent.
        if (skill.Type == SkillType.Shield)
        {
            var combat = GetComponent<CombatController>();
            if (combat == null || !combat.HasShield)
            {
                if (SkillDebug) Debug.Log($"[Skill] \"{id}\" needs a shield equipped");
                return false;
            }
        }

        if (skill.Effect is SpellCastEffect cast && cast.Spell != null)
        {
            // Wheel-cast magic: SpellCaster owns the FP cost (settled against the prepaid drain),
            // casts instantly with no cooldown, and reports whether the delivery actually began.
            // The learned level multiplies power/size and lowers cost the same way a weapon would.
            var ctx = BuildContext();
            ctx.ChargeLevel = Mathf.Max(0f, charge);
            ctx.PrepaidFocus = Mathf.Max(0f, prepaidFocus);
            ctx.SkillLevel = LevelOf(skill.id);
            var mods = new MagicWeaponMods
            {
                DamageMult = ctx.PowerScale,
                CastTimeMult = 1f,
                CooldownMult = ctx.EcoScale,
                FpCostMult = ctx.EcoScale,
                RadiusMult = ctx.SizeScale,
                RangeMult = ctx.SizeScale
            };
            Transform origin = ctx.Origin != null ? ctx.Origin : (ctx.User != null ? ctx.User.transform : null);
            bool began = ctx.Caster != null && ctx.Caster.BeginCast(cast.Spell, origin, mods,
                ctx.ChargeLevel, ctx.PrepaidFocus, fast: true);
            if (began) GainUse(skill, ctx);
            return began;
        }

        var costCtx = BuildContext();
        costCtx.ChargeLevel = Mathf.Max(0f, charge);
        costCtx.PrepaidFocus = Mathf.Max(0f, prepaidFocus);
        costCtx.SkillLevel = LevelOf(skill.id);
        if (!skill.CanAfford(costCtx))
        {
            if (SkillDebug)
                Debug.Log($"[Skill] \"{id}\" cannot afford cost {skill.SkillCost.ToString()}");
            return false;
        }
        if (costCtx.Caster != null && !costCtx.Caster.CooldownReady(skill.CooldownKey))
        {
            if (SkillDebug)
                Debug.Log($"[Skill] \"{id}\" on cooldown ({costCtx.Caster.CooldownRemaining(skill.CooldownKey):F1}s left)");
            return false;
        }
        if (!skill.TrySpend(costCtx))
        {
            if (SkillDebug) Debug.Log($"[Skill] \"{id}\" cost spend failed");
            return false;
        }

        // Economy from leveling shaves the cooldown (floor 50%), tree cooldown-reduction perks §3.3 too.
        if (costCtx.Caster != null && skill.SkillCost.Cooldown > 0f)
            costCtx.Caster.StartCooldown(skill.CooldownKey, skill.SkillCost.Cooldown * costCtx.EcoScale
                * (costCtx.Stats != null ? costCtx.Stats.CooldownReductionMult : 1f));

        skill.Effect?.Execute(costCtx);
        GainUse(skill, costCtx);
        if (SkillDebug) Debug.Log($"[Skill] \"{id}\" executed");
        return true;
    }

    private SkillContext BuildContext()
    {
        // Casts originate from the equipped weapon (the WeaponSkillExecutor lives on the in-hand
        // rig), not the player root — otherwise projectiles/rays fire from the feet.
        var skillExec = GetComponentInChildren<WeaponSkillExecutor>();
        return new SkillContext
        {
            Caster = _caster,
            Stamina = _stamina,
            Stats = _stats,
            SkillExecutor = skillExec,
            Origin = skillExec != null ? skillExec.transform : transform,
            User = gameObject
        };
    }
}

/// <summary>Per-skill leveling record (one entry per skill that has earned XP).</summary>
[System.Serializable]
public sealed class SkillProgress
{
    public string SkillId;
    public int Level = 1;
    public float Xp;
}
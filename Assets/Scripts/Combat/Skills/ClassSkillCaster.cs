using UnityEngine;

/// <summary>
/// Executes class-skill actives (game-design §3.2.1). <see cref="SkillProfile"/> delegates here
/// when a hotkeyed id isn't in the normal <see cref="SkillCatalog"/>: validates the skill is
/// castable and belongs to the ACTIVE class, reuses the shared casting pipeline (SpellCaster
/// cooldowns, Focus/Stamina costs), then runs the composed <see cref="IClassEffect"/> set against
/// a <see cref="ClassSkillContext"/>.
/// </summary>
public static class ClassSkillCaster
{
    private const bool SkillDebug = true;

    /// <summary>The owning class id extracted from a class skill id ("cls.{classId}.{node}").</summary>
    public static string ClassIdOf(string classSkillId)
    {
        if (string.IsNullOrEmpty(classSkillId)) return string.Empty;
        int dot = classSkillId.IndexOf('.');
        string body = dot >= 0 ? classSkillId.Substring(0, dot) : classSkillId;
        return body.StartsWith("cls.") ? body.Substring("cls.".Length) : body;
    }

    /// <summary>
    /// Validate + execute a class skill (active behaviors only). Returns true if it fired.
    /// <paramref name="baseCtx"/> carries the shared wiring built by <see cref="SkillProfile"/>
    /// (caster, stamina, stats, origin) so costs spend through the normal pipeline.
    /// </summary>
    public static bool Execute(GameObject user, string id, float charge, SkillContext baseCtx)
    {
        var skill = ClassSkillCatalog.Find(id);
        if (skill == null)
        {
            if (user != null && SkillDebug)
                Debug.Log($"[Skill] \"{id}\" not found in class catalog");
            return false;
        }
        if (skill.IsPassive)
        {
            if (SkillDebug) Debug.Log($"[Skill] class skill \"{id}\" is passive — not castable");
            return false;
        }
        if (user == null) return false;

        var unlocker = user.GetComponent<ClassUnlocker>();
        if (unlocker == null || !unlocker.IsUnlocked(ClassIdOf(id)))
        {
            if (SkillDebug) Debug.Log($"[Skill] class skill \"{id}\" class not unlocked");
            return false;
        }
        // Skills swap in/out with the active class: only the ACTIVE class's kit casts.
        if (!string.Equals(unlocker.ActiveClassId, ClassIdOf(id), System.StringComparison.OrdinalIgnoreCase))
        {
            if (SkillDebug)
                Debug.Log($"[Skill] class skill \"{id}\" requires active class {ClassIdOf(id)}");
            return false;
        }

        if (baseCtx == null)
            baseCtx = new SkillContext();
        baseCtx.ChargeLevel = Mathf.Clamp01(charge);

        if (!skill.SkillCost.CanAfford(baseCtx))
        {
            if (SkillDebug) Debug.Log($"[Skill] class skill \"{id}\" cannot afford cost");
            return false;
        }
        if (baseCtx.Caster != null && !baseCtx.Caster.CooldownReady(skill.CooldownKey))
        {
            if (SkillDebug)
                Debug.Log($"[Skill] class skill \"{id}\" on cooldown ({baseCtx.Caster.CooldownRemaining(skill.CooldownKey):F1}s left)");
            return false;
        }
        if (!skill.SkillCost.Spend(baseCtx))
        {
            if (SkillDebug) Debug.Log($"[Skill] class skill \"{id}\" cost spend failed");
            return false;
        }
        if (baseCtx.Caster != null && skill.SkillCost.Cooldown > 0f)
            baseCtx.Caster.StartCooldown(skill.CooldownKey, skill.SkillCost.Cooldown);

        var fx = skill.Effects;
        if (fx != null && fx.Length > 0)
        {
            var cctx = new ClassSkillContext
            {
                User = user,
                Origin = baseCtx.Origin,
                Caster = baseCtx.Caster,
                Stamina = baseCtx.Stamina,
                Stats = baseCtx.Stats,
                Controller = user.GetComponent<PlayerController>(),
                Unlocker = unlocker,
                Passives = user.GetComponent<ClassPassiveManager>(),
                ChargeLevel = baseCtx.ChargeLevel
            };
            for (int i = 0; i < fx.Length; i++)
                fx[i]?.Execute(cctx);
        }
        return true;
    }
}
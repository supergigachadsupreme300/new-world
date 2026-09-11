using UnityEngine;

/// <summary>
/// Executes race-skill actives. <see cref="SkillProfile"/> delegates here when a hotkeyed id
/// isn't in the normal <see cref="SkillCatalog"/> or <see cref="ClassSkillCatalog"/>: validates
/// the skill is castable and belongs to the ACTIVE race, reuses the shared casting pipeline
/// (SpellCaster cooldowns, Focus/Stamina costs), then runs the composed <see cref="IRaceEffect"/>
/// set against a <see cref="RaceSkillContext"/>.
/// </summary>
public static class RaceSkillCaster
{
    private const bool SkillDebug = true;

    /// <summary>The owning race id extracted from a race skill id ("rac.{raceId}.{node}").</summary>
    public static string RaceIdOf(string raceSkillId)
    {
        if (string.IsNullOrEmpty(raceSkillId)) return string.Empty;
        int dot = raceSkillId.IndexOf('.');
        string body = dot >= 0 ? raceSkillId.Substring(0, dot) : raceSkillId;
        return body.StartsWith("rac.") ? body.Substring("rac.".Length) : body;
    }

    /// <summary>
    /// Validate + execute a race skill (active behaviors only). Returns true if it fired.
    /// <paramref name="baseCtx"/> carries the shared wiring built by <see cref="SkillProfile"/>
    /// (caster, stamina, stats, origin) so costs spend through the normal pipeline.
    /// </summary>
    public static bool Execute(GameObject user, string id, float charge, SkillContext baseCtx)
    {
        var skill = RaceSkillCatalog.Find(id);
        if (skill == null)
        {
            if (user != null && SkillDebug)
                Debug.Log($"[Skill] \"{id}\" not found in race catalog");
            return false;
        }
        if (skill.IsPassive)
        {
            if (SkillDebug) Debug.Log($"[Skill] race skill \"{id}\" is passive — not castable");
            return false;
        }
        if (user == null) return false;

        var raceChange = user.GetComponent<RaceChangeManager>();
        if (raceChange == null || !string.Equals(raceChange.ActiveRaceId, RaceIdOf(id),
            System.StringComparison.OrdinalIgnoreCase))
        {
            if (SkillDebug)
                Debug.Log($"[Skill] race skill \"{id}\" requires active race {RaceIdOf(id)}");
            return false;
        }

        if (baseCtx == null)
            baseCtx = new SkillContext();
        baseCtx.ChargeLevel = Mathf.Clamp01(charge);

        if (!skill.SkillCost.CanAfford(baseCtx))
        {
            if (SkillDebug) Debug.Log($"[Skill] race skill \"{id}\" cannot afford cost");
            return false;
        }
        if (baseCtx.Caster != null && !baseCtx.Caster.CooldownReady(skill.CooldownKey))
        {
            if (SkillDebug)
                Debug.Log($"[Skill] race skill \"{id}\" on cooldown ({baseCtx.Caster.CooldownRemaining(skill.CooldownKey):F1}s left)");
            return false;
        }
        if (!skill.SkillCost.Spend(baseCtx))
        {
            if (SkillDebug) Debug.Log($"[Skill] race skill \"{id}\" cost spend failed");
            return false;
        }
        if (baseCtx.Caster != null && skill.SkillCost.Cooldown > 0f)
            baseCtx.Caster.StartCooldown(skill.CooldownKey, skill.SkillCost.Cooldown);

        var fx = skill.Effects;
        if (fx != null && fx.Length > 0)
        {
            var rctx = new RaceSkillContext
            {
                User = user,
                Origin = baseCtx.Origin,
                Caster = baseCtx.Caster,
                Stamina = baseCtx.Stamina,
                Stats = baseCtx.Stats,
                Controller = user.GetComponent<PlayerController>(),
                RacePassives = user.GetComponent<RacePassiveManager>(),
                SkillPassives = user.GetComponent<RaceSkillPassiveManager>(),
                ChargeLevel = baseCtx.ChargeLevel
            };
            for (int i = 0; i < fx.Length; i++)
                fx[i]?.Execute(rctx);
        }
        return true;
    }
}

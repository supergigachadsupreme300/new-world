using UnityEngine;

/// <summary>
/// Executes a weapon's unique Weapon Skill (§3.2 / §5.4). Spends FP (via the shared
/// SpellCaster FP pool), gates on cooldown, and delivers the skill's damage through the
/// DamageCalculator as a forward strike.
///
/// Attach to the weapon/root; assign Data (the WeaponData carrying .Skill) and Caster
/// (the owner's SpellCaster for FP/cooldown). Call TryUse() when the skill is triggered.
/// </summary>
public class WeaponSkillExecutor : MonoBehaviour
{
    [Header("Wiring")]
    public WeaponData Data;
    public SpellCaster Caster;

    [Header("Skill Effects")]
    public LayerMask TargetLayers = ~0;

    /// <summary>The skill currently equipped on the weapon, if any.</summary>
    public WeaponSkill CurrentSkill => Data != null ? Data.Skill : null;

    /// <summary>Fires when a skill successfully executes (for animation/sound/feedback).</summary>
    public event System.Action<WeaponSkill> OnExecuted;

    /// <summary>Fires for each target struck by the skill with its resolved hit.</summary>
    public event System.Action<WeaponSkill, DamageCalculator.HitResult, GameObject> OnSkillHit;

    private void Awake()
    {
        if (Caster == null)
            Caster = GetComponentInParent<SpellCaster>();
    }

    /// <summary>Remaining cooldown seconds for the equipped skill (0 = ready).</summary>
    public float CooldownRemaining
    {
        get
        {
            var skill = CurrentSkill;
            if (skill == null || Caster == null) return 0f;
            return Caster.CooldownRemaining(SkillKey(skill));
        }
    }

    /// <summary>
    /// Attempt to use the equipped Weapon Skill. Returns true if it began.
    /// </summary>
    public bool TryUse()
    {
        var skill = CurrentSkill;
        if (skill == null || Caster == null) return false;
        if (!Caster.HasFocusPoints(skill.FpCost)) return false;
        if (!Caster.CooldownReady(SkillKey(skill))) return false;

        Caster.TrySpendFocus(skill.FpCost);
        ExecuteSkill(skill);
        OnExecuted?.Invoke(skill);
        return true;
    }

    private void ExecuteSkill(WeaponSkill skill)
    {
        // Kind routes to a delivery mechanic; "none" means the executor does nothing
        // (the skill is just a stat/data definition). Other values map to a mechanic below.
        switch ((skill.Kind ?? "none").ToLowerInvariant())
        {
            case "thrust":
                ExecuteThrust(skill);
                break;
            case "strike":
                ExecuteStrike(skill);
                break;
            case "none":
            default:
                return; // no-op
        }

        // Register cooldown using the skill's id.
        Caster.StartCooldown(SkillKey(skill), skill.Cooldown);
    }

    private void ExecuteStrike(WeaponSkill skill)
    {
        Vector3 origin = transform.position + transform.forward * (skill.Range * 0.5f);

        Collider[] cols = Physics.OverlapSphere(origin, skill.Radius, TargetLayers);
        foreach (var col in cols)
        {
            if (col.transform.root == transform.root) continue;
            ResolveSkillHit(skill, col.gameObject);
        }
    }

    private void ExecuteThrust(WeaponSkill skill)
    {
        Vector3 origin = transform.position + transform.forward * (skill.Range * 0.25f);
        if (Physics.SphereCast(origin, skill.Radius * 0.5f, transform.forward, out RaycastHit hit, skill.Range, TargetLayers))
        {
            if (hit.collider.transform.root != transform.root)
                ResolveSkillHit(skill, hit.collider.gameObject);
        }
    }

    private void ResolveSkillHit(WeaponSkill skill, GameObject target)
    {
        var ctx = new DamageCalculator.HitContext
        {
            AttackPower = skill.BaseDamage,
            SkillMultiplier = 1f,
            Defense = 5f,
            DefenseMultiplier = 1f,
            Type = skill.Type,
            Resistance = NeutralResistance.Instance,
            WeaknessMultiplier = 1f,
            CriticalMultiplier = 1f,
        };
        var result = DamageCalculator.Calculate(ctx, false);

        // Apply to the target's health if it implements IDamageable.
        if (target.TryGetComponent<IDamageable>(out var damageable))
            damageable.TakeDamage(Mathf.RoundToInt(result.TotalDamage));
        if (result.TotalDamage > 0f)
            DamageNumber.Spawn(target.transform.position, result.TotalDamage, skill.Type);

        OnSkillHit?.Invoke(skill, result, target);

        // Impulse.
        Rigidbody rb = target != null ? target.GetComponent<Rigidbody>() : null;
        if (rb != null && skill.Knockback > 0f)
        {
            Vector3 dir = (target.transform.position - transform.position).normalized;
            dir.y = 0.3f;
            rb.AddForce(dir * skill.Knockback, ForceMode.Impulse);
        }
    }

    private string SkillKey(WeaponSkill skill) => "wskill_" + (skill.id ?? skill.name);
}
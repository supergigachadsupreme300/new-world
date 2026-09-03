using UnityEngine;

/// <summary>
/// Executes a weapon's unique Weapon Art (§3.2 / §5.4). Spends FP (via the shared
/// SpellCaster FP pool), gates on cooldown, and delivers the art's damage through the
/// DamageCalculator as a forward strike.
///
/// Attach to the weapon/root; assign Data (the WeaponData carrying .Art) and Caster
/// (the owner's SpellCaster for FP/cooldown). Call TryUse() when the art is triggered.
/// </summary>
public class WeaponArtExecutor : MonoBehaviour
{
    [Header("Wiring")]
    public WeaponData Data;
    public SpellCaster Caster;

    [Header("Art Effects")]
    public LayerMask TargetLayers = ~0;

    /// <summary>The art currently equipped on the weapon, if any.</summary>
    public WeaponArt CurrentArt => Data != null ? Data.Art : null;

    /// <summary>Fires when an art successfully executes (for animation/sound/feedback).</summary>
    public event System.Action<WeaponArt> OnExecuted;

    /// <summary>Fires for each target struck by the art with its resolved hit.</summary>
    public event System.Action<WeaponArt, DamageCalculator.HitResult, GameObject> OnArtHit;

    private void Awake()
    {
        if (Caster == null)
            Caster = GetComponentInParent<SpellCaster>();
    }

    /// <summary>Remaining cooldown seconds for the equipped art (0 = ready).</summary>
    public float CooldownRemaining
    {
        get
        {
            var art = CurrentArt;
            if (art == null || Caster == null) return 0f;
            return Caster.CooldownRemaining(ArtKey(art));
        }
    }

    /// <summary>
    /// Attempt to use the equipped Weapon Art. Returns true if it began.
    /// </summary>
    public bool TryUse()
    {
        var art = CurrentArt;
        if (art == null || Caster == null) return false;
        if (!Caster.HasFocusPoints(art.FpCost)) return false;
        if (!Caster.CooldownReady(ArtKey(art))) return false;

        Caster.TrySpendFocus(art.FpCost);
        ExecuteArt(art);
        OnExecuted?.Invoke(art);
        return true;
    }

    private void ExecuteArt(WeaponArt art)
    {
        // ArtType routes to a delivery mechanic; "none" means the executor does nothing
        // (the art is just a stat/data definition). Other values map to a mechanic below.
        switch ((art.ArtType ?? "none").ToLowerInvariant())
        {
            case "thrust":
                ExecuteThrust(art);
                break;
            case "strike":
                ExecuteStrike(art);
                break;
            case "none":
            default:
                return; // no-op
        }

        // Register cooldown using the art's id.
        Caster.StartCooldown(ArtKey(art), art.Cooldown);
    }

    private void ExecuteStrike(WeaponArt art)
    {
        Vector3 origin = transform.position + transform.forward * (art.Range * 0.5f);

        Collider[] cols = Physics.OverlapSphere(origin, art.Radius, TargetLayers);
        foreach (var col in cols)
        {
            if (col.transform.root == transform.root) continue;
            ResolveArtHit(art, col.gameObject);
        }
    }

    private void ExecuteThrust(WeaponArt art)
    {
        Vector3 origin = transform.position + transform.forward * (art.Range * 0.25f);
        if (Physics.SphereCast(origin, art.Radius * 0.5f, transform.forward, out RaycastHit hit, art.Range, TargetLayers))
        {
            if (hit.collider.transform.root != transform.root)
                ResolveArtHit(art, hit.collider.gameObject);
        }
    }

    private void ResolveArtHit(WeaponArt art, GameObject target)
    {
        var ctx = new DamageCalculator.HitContext
        {
            AttackPower = art.BaseDamage,
            SkillMultiplier = 1f,
            Defense = 5f,
            DefenseMultiplier = 1f,
            Type = art.Type,
            Resistance = NeutralResistance.Instance,
            WeaknessMultiplier = 1f,
            CriticalMultiplier = 1f,
        };
        var result = DamageCalculator.Calculate(ctx, false);

        // Apply to the target's health if it implements IDamageable.
        if (target.TryGetComponent<IDamageable>(out var damageable))
            damageable.TakeDamage(result.TotalDamage);

        OnArtHit?.Invoke(art, result, target);

        // Impulse.
        Rigidbody rb = target != null ? target.GetComponent<Rigidbody>() : null;
        if (rb != null && art.Knockback > 0f)
        {
            Vector3 dir = (target.transform.position - transform.position).normalized;
            dir.y = 0.3f;
            rb.AddForce(dir * art.Knockback, ForceMode.Impulse);
        }
    }

    private string ArtKey(WeaponArt art) => "art_" + (art.id ?? art.name);
}

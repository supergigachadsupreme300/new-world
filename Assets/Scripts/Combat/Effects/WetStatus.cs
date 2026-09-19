using UnityEngine;

/// <summary>
/// Water-magic signature status (§3.7). Attached to the hit target's root. A wet foe
/// moves a little slower (slippery footing) and conducts elemental power: Ice and
/// Lightning spells deal bonus damage against it (damage amp queried by the spell
/// pipeline via <see cref="IsWet"/>). Re-applying refreshes the duration and re-applies
/// the slow, so neighbouring water spells keep the buttoned-up pressure while combos
/// land a juicy frost/shock follow-up.
/// </summary>
public class WetStatus : MonoBehaviour
{
    /// <summary>Movement slow factor (0..1) while wet. 0.85 = 15% slower.</summary>
    public float SlowFactor = 0.85f;

    /// <summary>Seconds the wet status lingers.</summary>
    public float Duration = 4f;

    /// <summary>Multiplier applied to Ice/Lightning spell damage against a wet target.</summary>
    public const float IceLightningDamageBonus = 1.4f;

    /// <summary>Seconds the status has left (HUD strip reads this under the player bars).</summary>
    public float Remaining => Mathf.Max(0f, _expiresAt - Time.time);

    private float _expiresAt;
    private bool _active;

    /// <summary>True if the given object (or its root) is currently soaked.</summary>
    public static bool IsWet(GameObject target)
    {
        if (target == null) return false;
        return target.transform.root.GetComponent<WetStatus>() != null;
    }

    /// <summary>Apply/refresh the wet status on a hit target.</summary>
    public static WetStatus Apply(GameObject target, float duration)
    {
        if (target == null) return null;
        var root = target.transform.root.gameObject;
        var wet = root.GetComponent<WetStatus>();
        if (wet == null)
            wet = root.AddComponent<WetStatus>();
        wet.Duration = Mathf.Max(duration, 0.5f);
        wet._expiresAt = Time.time + wet.Duration;
        wet.RapplySlow();
        // Water douses fire (§3.7): instantly douse any active Burn DoT as the target soaks.
        SpellDoT.RemoveType(target, DamageType.Fire);
        return wet;
    }

    private void RapplySlow()
    {
        if (gameObject.TryGetComponent<EnemyController>(out var enemy))
            enemy.ApplySlow(SlowFactor, Duration);
    }

    private void Update()
    {
        if (!_active)
        {
            _active = true;
            RapplySlow();
        }
        if (Time.time >= _expiresAt)
            Destroy(this);
    }
}
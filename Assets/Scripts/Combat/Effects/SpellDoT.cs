using UnityEngine;

/// <summary>
/// Damage-over-time ticker for spell statuses (Burn / Poison / Rot / Bleed, §3.7).
/// Attached to the hit target's root; ticks the resolved spell-damage fragments and drops
/// DamageNumbers. Re-applying the same status refreshes the full duration and takes the
/// stronger per-tick damage.
/// </summary>
public class SpellDoT : MonoBehaviour
{
    public float DamagePerTick;
    public float TickInterval = 0.5f;
    public float Remaining = 3f;
    public DamageType Type;

    private float _cooldown;

    public static SpellDoT Apply(GameObject target, float damagePerTick, float duration,
        float tickInterval, DamageType type)
    {
        var root = target.transform.root.gameObject;
        var dot = root.GetComponent<SpellDoT>();
        if (dot == null)
            dot = root.AddComponent<SpellDoT>();
        dot.DamagePerTick = Mathf.Max(dot.DamagePerTick, damagePerTick);
        dot.Remaining = Mathf.Max(dot.Remaining, duration);
        dot.TickInterval = Mathf.Max(tickInterval, 0.1f);
        dot.Type = type;
        dot._cooldown = Mathf.Min(dot._cooldown, dot.TickInterval);
        return dot;
    }

    private void Update()
    {
        Remaining -= Time.deltaTime;
        if (Remaining <= 0f)
        {
            Destroy(this);
            return;
        }

        _cooldown -= Time.deltaTime;
        if (_cooldown <= 0f)
        {
            _cooldown = TickInterval;
            Tick();
        }
    }

    private void Tick()
    {
        if (gameObject.TryGetComponent<IDamageable>(out var damageable))
        {
            int amount = Mathf.Max(1, Mathf.RoundToInt(DamagePerTick));
            damageable.TakeDamage(amount);
            DamageNumber.Spawn(transform.position, amount, Type);
        }
    }
}
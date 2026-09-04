using System;
using UnityEngine;

/// <summary>
/// Melee weapon behavior (§3.6 Layer 3). Delegates to a HitboxSystem arc sweep and
/// pulls damage from WeaponData, scaled by the relevant stat (Str heavy / Dex light, §3.4).
///
/// Attach to the weapon/root. A HitboxSystem must be present (this object or a child);
/// it is configured with the weapon's damage and DamageType on each attack.
/// </summary>
[RequireComponent(typeof(HitboxSystem))]
public class MeleeWeaponBehavior : MonoBehaviour, IWeaponBehavior
{
    [Header("Wiring")]
    public WeaponData Data;
    public HitboxSystem Hitbox;

    [Header("Runtime")]
    public float AttackDamage;
    public DamageType AttackerType = DamageType.Physical;

    /// <summary>Optional stat accessor supplying Str/Dex for scaling (wired in Phase 4).</summary>
    public IStatProvider Stats;

    private bool _attacking;

    public event Action Completed;
    public bool IsAttacking => _attacking;

    private void Awake()
    {
        if (Hitbox == null)
            Hitbox = GetComponent<HitboxSystem>();
    }

    /// <summary>
    /// Melee damage from weapon.base scaled by the weapon's declared scaling stat (§3.4).
    /// Heavy-attack variant applies a flat multiplier; the stat coefficient comes from
    /// WeaponData.ScalingCoefficient. Str drives heavy/melee, Dex drives light/one-handed.
    /// </summary>
    private float ComputeScaledDamage(bool isHeavy)
    {
        if (Data == null)
            return AttackDamage;

        // Heavy weapons lean on Strength, light/one-handed weapons lean on Dexterity.
        WeaponScalingStat stat = Data.ScalingStat;
        if (stat == WeaponScalingStat.None)
            stat = isHeavy ? WeaponScalingStat.Strength : WeaponScalingStat.Dexterity;

        float statValue = 0f;
        if (Stats != null)
            statValue = Stats.GetStat(stat);

        float scale = 1f + statValue * Mathf.Max(Data.ScalingCoefficient, 0f);
        float baseDmg = Data.BaseDamage * (isHeavy ? 1.5f : 1f);
        return baseDmg * scale;
    }

    public void BeginAttack(AttackCommand cmd)
    {
        if (_attacking) return;
        _attacking = true;

        // Damage from weapon.base + stat scaling; light/heavy variant.
        float damage = ComputeScaledDamage(cmd.IsHeavy);
        AttackDamage = damage;

        // Configure hitbox with the weapon's single DamageType + reach.
        if (Hitbox != null)
        {
            Hitbox.AttackPower = damage;
            Hitbox.Type = Data != null ? Data.Type : AttackerType;
            Hitbox.Resistance = Stats as IDamageResistance;
            Hitbox.BeginSwing(cmd.Origin != null ? cmd.Origin : transform, damage);
        }
    }

    public void ActiveFrame()
    {
    }

    public void Cancel()
    {
        Hitbox?.CancelSwing();
        _attacking = false;
    }

    private void Update()
    {
        if (!_attacking) return;
        if (Hitbox != null && !Hitbox.IsActive)
        {
            _attacking = false;
            Completed?.Invoke();
        }
    }
}

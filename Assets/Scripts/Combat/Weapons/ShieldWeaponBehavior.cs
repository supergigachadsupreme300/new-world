using System;
using UnityEngine;

/// <summary>
/// Shield weapon behavior (§3.6 Layer 3). A shield is an off-hand defensive tool: LMB lands a
/// short bash (a hitbox arc scaled by the weapon's base damage and Str), while RMB raises the
/// guard instead of attacking — the actual block resolution lives in <see cref="CombatController"/>
/// and <see cref="PlayerController"/>, which read this rig's <see cref="WeaponData"/> mods
/// (BlockAbsorbPercent / BlockStaminaDrainMult) for the stricter shield guard.
///
/// Attach to the weapon/root. A HitboxSystem must be present (this object or a child); it is
/// configured with the weapon's damage and DamageType on each attack.
/// </summary>
[RequireComponent(typeof(HitboxSystem))]
public class ShieldWeaponBehavior : MonoBehaviour, IWeaponBehavior
{
    [Header("Wiring")]
    public WeaponData Data;
    public HitboxSystem Hitbox;

    [Header("Runtime")]
    public float AttackDamage;
    public DamageType AttackerType = DamageType.Physical;

    /// <summary>Optional stat accessor supplying Str for bash scaling (wired by the rig builder).</summary>
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
    /// Bash damage from weapon.base scaled by the shield's declared scaling stat (§3.4).
    /// The heavy-attack variant applies the same flat multiplier used by melee.
    /// </summary>
    private float ComputeScaledDamage(bool isHeavy)
    {
        if (Data == null)
            return AttackDamage;

        WeaponScalingStat stat = Data.ScalingStat;
        if (stat == WeaponScalingStat.None)
            stat = WeaponScalingStat.Strength;

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

        float damage = ComputeScaledDamage(cmd.IsHeavy);
        AttackDamage = damage;

        // Short bash arc ahead of the shield — reach is the weapon's Reach, kept small for off-hand.
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
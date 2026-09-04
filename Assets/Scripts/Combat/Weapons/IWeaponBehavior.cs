using System;
using UnityEngine;

/// <summary>
/// Payload describing an attack to be executed by an IWeaponBehavior (§3.6 Layer 3).
/// The CombatController builds this and hands it to the equipped weapon's behavior.
/// </summary>
public struct AttackCommand
{
    /// <summary>Attack strength variant (light / heavy).</summary>
    public bool IsHeavy;

    /// <summary>World direction the attack faces (usually the owner's forward).</summary>
    public Vector3 Direction;

    /// <summary>Origin (owner transform) for range/placement.</summary>
    public Transform Origin;
}

/// <summary>
/// The weapon behavior contract (§3.6 Layer 3). One concrete module per category
/// (Melee, Ranged, Magic). The CombatController talks ONLY to this — it never knows
/// whether a weapon is melee, ranged, or magic. Adding a weapon kind = one new class.
///
/// Implementations receive attack commands during the active frames of an attack and
/// report back via the OnAttackComplete event.
/// </summary>
public interface IWeaponBehavior
{
    /// <summary>Fires once per attack when the behavior has finished executing.</summary>
    event Action Completed;

    /// <summary>Begin an attack with the given command. Called at attack start.</summary>
    void BeginAttack(AttackCommand cmd);

    /// <summary>Called every frame while the attack is active (connection for ActiveFrame).</summary>
    void ActiveFrame();

    /// <summary>Interrupt/cancel the current attack immediately.</summary>
    void Cancel();

    /// <summary>Whether the behavior is currently executing an attack.</summary>
    bool IsAttacking { get; }
}

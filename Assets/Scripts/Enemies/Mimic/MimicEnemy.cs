using UnityEngine;

/// <summary>Mimic race (1do): stationary ambush chest — lies still, strikes hard at close range, never chases.</summary>
public class MimicEnemy : EnemyController
{
    protected override void ApplyRaceConfig()
    {
        EnemyId = "mimic";
        SetMaxHealth(90);
        Damage = 16;
        MoveSpeed = 0f;
        PatrolSpeed = 0f;
        AttackRange = 2f;
        AlertRange = 2.5f;
        ChaseRange = 3f;
        LeashRange = 4f;
        CanFlee = false;
    }
}
using UnityEngine;

/// <summary>Bat race (1do): glass-cannon flier — fastest race, huge perception/leash, quick to flee.</summary>
public class BatEnemy : EnemyController
{
    protected override void ApplyRaceConfig()
    {
        EnemyId = "bat";
        SetMaxHealth(30);
        Damage = 7;
        MoveSpeed = 4f;
        PatrolSpeed = 2.4f;
        AlertRange = 8f;
        ChaseRange = 12f;
        LeashRange = 20f;
    }
}
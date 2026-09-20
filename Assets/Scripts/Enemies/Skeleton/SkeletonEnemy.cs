using UnityEngine;

/// <summary>Skeleton race (1do): baseline undead foot-soldier — keeps the original generic profile.</summary>
public class SkeletonEnemy : EnemyController
{
    protected override void ApplyRaceConfig()
    {
        EnemyId = "skeleton";
        SetMaxHealth(50);
        Damage = 10;
        MoveSpeed = 2.5f;
        PatrolSpeed = 1.5f;
    }
}
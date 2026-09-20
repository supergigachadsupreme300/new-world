using UnityEngine;

/// <summary>Ice Wolf race (1do): frost-coated hunter — very fast, wide alert radius.</summary>
public class IceWolfEnemy : EnemyController
{
    protected override void ApplyRaceConfig()
    {
        EnemyId = "ice_wolf";
        SetMaxHealth(60);
        Damage = 12;
        MoveSpeed = 3.3f;
        PatrolSpeed = 2f;
        AlertRange = 7f;
    }
}
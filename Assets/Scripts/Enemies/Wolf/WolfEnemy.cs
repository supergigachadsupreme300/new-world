using UnityEngine;

/// <summary>Wolf race (1do): fast, aggressive pack hunter — high speed, wide alert radius.</summary>
public class WolfEnemy : EnemyController
{
    protected override void ApplyRaceConfig()
    {
        EnemyId = "wolf";
        SetMaxHealth(55);
        Damage = 12;
        MoveSpeed = 3.5f;
        PatrolSpeed = 2f;
        AlertRange = 7f;
    }
}
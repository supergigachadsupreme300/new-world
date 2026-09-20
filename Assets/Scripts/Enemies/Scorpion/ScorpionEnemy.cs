using UnityEngine;

/// <summary>Scorpion race (1do): balanced desert stinger — mid stats all around.</summary>
public class ScorpionEnemy : EnemyController
{
    protected override void ApplyRaceConfig()
    {
        EnemyId = "scorpion";
        SetMaxHealth(50);
        Damage = 12;
        MoveSpeed = 2.2f;
        PatrolSpeed = 1.3f;
    }
}
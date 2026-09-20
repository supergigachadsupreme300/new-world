using UnityEngine;

/// <summary>Sea Creature race (1do): deep-water chaser — balanced, slightly bruiser-weight.</summary>
public class SeaCreatureEnemy : EnemyController
{
    protected override void ApplyRaceConfig()
    {
        EnemyId = "sea_creature";
        SetMaxHealth(85);
        Damage = 13;
        MoveSpeed = 2f;
        PatrolSpeed = 1.2f;
    }
}
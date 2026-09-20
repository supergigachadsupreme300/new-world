using UnityEngine;

/// <summary>Drake race (1do): winged lizard — fast and punchy midpoint threat.</summary>
public class DrakeEnemy : EnemyController
{
    protected override void ApplyRaceConfig()
    {
        EnemyId = "drake";
        SetMaxHealth(70);
        Damage = 13;
        MoveSpeed = 3f;
        PatrolSpeed = 1.8f;
    }
}
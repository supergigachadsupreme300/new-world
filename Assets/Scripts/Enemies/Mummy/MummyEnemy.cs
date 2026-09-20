using UnityEngine;

/// <summary>Mummy race (1do): wrapped tomb guardian — balanced, slightly tough.</summary>
public class MummyEnemy : EnemyController
{
    protected override void ApplyRaceConfig()
    {
        EnemyId = "mummy";
        SetMaxHealth(70);
        Damage = 12;
        MoveSpeed = 2f;
        PatrolSpeed = 1.2f;
    }
}
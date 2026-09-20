using UnityEngine;

/// <summary>Slime race (1do): tanky slow blob — high HP, low DPS, never flees.</summary>
public class SlimeEnemy : EnemyController
{
    protected override void ApplyRaceConfig()
    {
        EnemyId = "slime";
        SetMaxHealth(80);
        Damage = 8;
        MoveSpeed = 1.2f;
        PatrolSpeed = 0.8f;
        CanFlee = false;
    }
}
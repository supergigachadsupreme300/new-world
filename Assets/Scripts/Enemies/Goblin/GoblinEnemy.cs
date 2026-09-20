using UnityEngine;

/// <summary>Goblin race (1do): quick, fragile skirmisher — fast and quick to scatter.</summary>
public class GoblinEnemy : EnemyController
{
    protected override void ApplyRaceConfig()
    {
        EnemyId = "goblin";
        SetMaxHealth(45);
        Damage = 10;
        MoveSpeed = 2.8f;
        PatrolSpeed = 1.6f;
    }
}
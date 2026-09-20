using UnityEngine;

/// <summary>Undead race (1do): shambling zombie — mid HP, slow, relentless.</summary>
public class UndeadEnemy : EnemyController
{
    protected override void ApplyRaceConfig()
    {
        EnemyId = "undead";
        SetMaxHealth(75);
        Damage = 11;
        MoveSpeed = 1.6f;
        PatrolSpeed = 1f;
    }
}
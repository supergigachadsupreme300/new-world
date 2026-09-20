using UnityEngine;

/// <summary>Bandit race (1do): balanced humanoid rogue — jack-of-all-trades baseline fighter.</summary>
public class BanditEnemy : EnemyController
{
    protected override void ApplyRaceConfig()
    {
        EnemyId = "bandit";
        SetMaxHealth(60);
        Damage = 12;
        MoveSpeed = 2.6f;
        PatrolSpeed = 1.5f;
    }
}
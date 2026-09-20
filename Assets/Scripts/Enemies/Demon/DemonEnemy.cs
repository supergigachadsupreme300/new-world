using UnityEngine;

/// <summary>Demon race (1do): deep-realm killer — second-tankiest race, armor + DR, long reach.</summary>
public class DemonEnemy : EnemyController
{
    protected override void ApplyRaceConfig()
    {
        EnemyId = "demon";
        SetMaxHealth(130);
        Damage = 18;
        MoveSpeed = 2.2f;
        PatrolSpeed = 1.3f;
        AttackRange = 2f;
        Armor = 2;
        DamageReduction = 0.1f;
    }
}
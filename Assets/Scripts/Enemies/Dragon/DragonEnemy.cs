using UnityEngine;

/// <summary>Dragon race (1do): apex predator — the tankiest spammable race, heavy armor/DR, long reach.</summary>
public class DragonEnemy : EnemyController
{
    protected override void ApplyRaceConfig()
    {
        EnemyId = "dragon";
        SetMaxHealth(140);
        Damage = 20;
        MoveSpeed = 1.6f;
        PatrolSpeed = 0.8f;
        AttackRange = 2.2f;
        Armor = 3;
        DamageReduction = 0.15f;
    }
}
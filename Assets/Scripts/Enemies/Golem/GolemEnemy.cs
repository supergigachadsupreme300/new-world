using UnityEngine;

/// <summary>Golem race (1do): rock humanoid — highest plain-race survivability, Armor + DR.</summary>
public class GolemEnemy : EnemyController
{
    protected override void ApplyRaceConfig()
    {
        EnemyId = "golem";
        SetMaxHealth(120);
        Damage = 16;
        MoveSpeed = 1.2f;
        PatrolSpeed = 0.7f;
        Armor = 3;
        DamageReduction = 0.1f;
    }
}
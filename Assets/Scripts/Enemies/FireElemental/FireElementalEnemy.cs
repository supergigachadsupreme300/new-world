using UnityEngine;

/// <summary>Fire Elemental race (1do): elemental burst damage — squishy but hits hard, light armor.</summary>
public class FireElementalEnemy : EnemyController
{
    protected override void ApplyRaceConfig()
    {
        EnemyId = "fire_elemental";
        SetMaxHealth(60);
        Damage = 14;
        MoveSpeed = 2.4f;
        PatrolSpeed = 1.4f;
        Armor = 1;
    }
}
using UnityEngine;

/// <summary>Yeti race (1do): large white ape — bulky brawler with light armor.</summary>
public class YetiEnemy : EnemyController
{
    protected override void ApplyRaceConfig()
    {
        EnemyId = "yeti";
        SetMaxHealth(100);
        Damage = 15;
        MoveSpeed = 1.8f;
        PatrolSpeed = 1f;
        Armor = 2;
    }
}
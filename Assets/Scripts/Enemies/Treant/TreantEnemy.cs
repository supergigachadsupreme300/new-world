using UnityEngine;

/// <summary>Treant race (1do): living tree — tough and lumbering, long reach.</summary>
public class TreantEnemy : EnemyController
{
    protected override void ApplyRaceConfig()
    {
        EnemyId = "treant";
        SetMaxHealth(95);
        Damage = 14;
        MoveSpeed = 1.4f;
        PatrolSpeed = 0.8f;
        AttackRange = 2f;
    }
}
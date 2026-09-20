using UnityEngine;

/// <summary>
/// Training dummy race (1do): never dies, cannot move, no damage — the test-ground practice
/// post. The armored dummy variant in <c>NewWorldTestGround.SpawnDummy</c> only overrides the
/// flat <see cref="EnemyController.DamageReduction"/> and its aggro ranges.
/// </summary>
public class DummyEnemy : EnemyController
{
    protected override void ApplyRaceConfig()
    {
        EnemyId = "dummy";
        SetMaxHealth(1000);
        Damage = 0;
        Armor = 0;
        DamageReduction = 0f;
        AttackRange = 0f;
        AttackCooldown = 1.2f;
        ChaseRange = 30f;
        AlertRange = 30f;
        LeashRange = 40f;
        MoveSpeed = 0f;
        PatrolSpeed = 0f;
        CanFlee = false;
        Immortal = true;
        RegenPerSecond = Mathf.RoundToInt(MaxHealth * 0.15f);
    }
}
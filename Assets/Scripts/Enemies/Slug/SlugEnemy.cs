using UnityEngine;

/// <summary>Giant slug race (1do): slow crawl, no escape — soft target that never flees.</summary>
public class SlugEnemy : EnemyController
{
    protected override void ApplyRaceConfig()
    {
        EnemyId = "slug";
        SetMaxHealth(65);
        Damage = 9;
        MoveSpeed = 0.9f;
        PatrolSpeed = 0.6f;
        CanFlee = false;
    }
}
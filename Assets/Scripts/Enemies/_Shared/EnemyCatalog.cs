using UnityEngine;

/// <summary>
/// Maps enemy race ids to their per-race brain component (1do). Every race owns a folder under
/// Assets/Scripts/Enemies and a subclass of EnemyController that stamps its own EnemyId + distinct
/// stat profile. Unknown ids fall back to the generic controller (model still resolves via
/// EnemyModelBuilder).
/// </summary>
public static class EnemyCatalog
{
    public static EnemyController AddEnemyComponent(GameObject host, string enemyId)
    {
        switch (enemyId)
        {
            case "slime":          return host.AddComponent<SlimeEnemy>();
            case "wolf":           return host.AddComponent<WolfEnemy>();
            case "goblin":         return host.AddComponent<GoblinEnemy>();
            case "bandit":         return host.AddComponent<BanditEnemy>();
            case "treant":         return host.AddComponent<TreantEnemy>();
            case "golem":          return host.AddComponent<GolemEnemy>();
            case "drake":          return host.AddComponent<DrakeEnemy>();
            case "undead":         return host.AddComponent<UndeadEnemy>();
            case "slug":           return host.AddComponent<SlugEnemy>();
            case "scorpion":       return host.AddComponent<ScorpionEnemy>();
            case "mummy":          return host.AddComponent<MummyEnemy>();
            case "yeti":           return host.AddComponent<YetiEnemy>();
            case "ice_wolf":       return host.AddComponent<IceWolfEnemy>();
            case "fire_elemental": return host.AddComponent<FireElementalEnemy>();
            case "dragon":         return host.AddComponent<DragonEnemy>();
            case "demon":          return host.AddComponent<DemonEnemy>();
            case "mimic":          return host.AddComponent<MimicEnemy>();
            case "sea_creature":   return host.AddComponent<SeaCreatureEnemy>();
            case "skeleton":       return host.AddComponent<SkeletonEnemy>();
            case "bat":            return host.AddComponent<BatEnemy>();
            case "dummy":          return host.AddComponent<DummyEnemy>();
            default:               return host.AddComponent<EnemyController>();
        }
    }
}
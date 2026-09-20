using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Spawns biome-appropriate enemies across the world (planning Task 5.1, game-design §7.1).
/// Reads the biome registry, rolls per-biome enemy tables, honors density and day/night
/// variants (§7.3), and boots the race's per-race brain (EnemyCatalog → EnemyController subclass).
/// Player/designers drop this on the persistent world root.
/// </summary>
public class EnemySpawner : MonoSingleton<EnemySpawner>
{
    [Header("Prefab")]
    [Tooltip("Optional prefab template cloned for each spawn (extra visuals/components). The brain (stats + identity) always comes from EnemyCatalog per race, never this prefab.")]
    public EnemyController EnemyPrefab;

    [Header("Budget")]
    [Tooltip("Maximum live enemies this spawner may manage before it stops spawning.")]
    public int MaxLiveEnemies = 60;
    [Tooltip("Time between re-checks of active enemy count.")]
    public float RefreshInterval = 2f;

    /// <summary>Optional day/night provider; if null the spawner treats it as day.</summary>
    public IFactionTimeProvider TimeProvider;

    private readonly List<EnemyController> _live = new List<EnemyController>();
    private float _refreshTimer;

    public IReadOnlyList<EnemyController> LiveEnemies => _live;

    private void Update()
    {
        _refreshTimer += Time.deltaTime;
        if (_refreshTimer < RefreshInterval) return;
        _refreshTimer = 0f;
        PruneDead();
    }

    private void PruneDead()
    {
        for (int i = _live.Count - 1; i >= 0; i--)
            if (_live[i] == null)
                _live.RemoveAt(i);
    }

    /// <summary>
    /// Spawn a single enemy of the given biome at a world position. Skips if the live
    /// budget is exhausted. Returns the spawned controller, or null.
    /// </summary>
    public EnemyController SpawnAt(BiomeType biome, Vector3 position, float tierScale = 1f)
    {
        PruneDead();
        if (_live.Count >= MaxLiveEnemies) return null;

        bool night = TimeProvider != null && TimeProvider.IsNight();
        var data = BiomeRegistry.Get(biome);
        if (data == null) return null;

        string enemyId = data.RollEnemyId();
        if (string.IsNullOrEmpty(enemyId)) return null;

        // Clone the prefab (if any) for visuals, then swap in the race's own brain component.
        GameObject go;
        if (EnemyPrefab != null)
        {
            go = Instantiate(EnemyPrefab.gameObject, position + Vector3.up * 0.05f, Quaternion.identity);
            var staleBrain = go.GetComponent<EnemyController>();
            if (staleBrain != null)
                Destroy(staleBrain);
        }
        else
        {
            go = new GameObject("Enemy");
            go.transform.position = position + Vector3.up * 0.05f;
        }
        go.name = enemyId + "_" + _live.Count;

        var enemy = EnemyCatalog.AddEnemyComponent(go, enemyId);
        if (enemy == null)
        {
            Destroy(go);
            return null;
        }

        enemy.Biome = biome;

        // Day/night variant strength scaling (§7.3).
        float variant = 1f;
        if (night && UnityEngine.Random.value < data.NightVariantChance)
            variant = BiomeRegistry.NightFactor;
        enemy.TierScale = tierScale * variant;
        enemy.RefreshHealth();

        _live.Add(enemy);
        return enemy;
    }

    /// <summary>Spawn a cluster of enemies around an anchor point.</summary>
    public void SpawnCluster(BiomeType biome, Vector3 center, float radius, float count, float tierScale = 1f)
    {
        int n = Mathf.Clamp(Mathf.RoundToInt(count), 0, MaxLiveEnemies);
        for (int i = 0; i < n; i++)
        {
            Vector3 offset = UnityEngine.Random.insideUnitSphere * radius;
            offset.y = 0f;
            SpawnAt(biome, center + offset, tierScale);
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.4f, 0.2f);
        foreach (var e in _live)
            if (e != null)
                Gizmos.DrawWireSphere(e.transform.position, 0.35f);
    }
}

/// <summary>Minimal seam so the spawner can scale strength at night (§7.3).</summary>
public interface IFactionTimeProvider
{
    bool IsNight();
}
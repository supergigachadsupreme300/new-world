using UnityEngine;

public class EnemySpawner : MonoBehaviour
{
    public GameObject EnemyPrefab;
    public int MaxEnemies = 8;
    public float SpawnInterval = 5f;

    private float _timer;
    private int _spawnedCount;

    private void Update()
    {
        if (EnemyPrefab == null)
            return;

        _timer += Time.deltaTime;
        if (_timer >= SpawnInterval && _spawnedCount < MaxEnemies)
        {
            _timer = 0f;
            SpawnEnemy();
        }
    }

    private void SpawnEnemy()
    {
        Vector3 pos = transform.position + Random.insideUnitSphere * 8f;
        pos.y = 0f;
        var enemy = Instantiate(EnemyPrefab, pos, Quaternion.identity);
        enemy.name = "Enemy_" + _spawnedCount;
        _spawnedCount++;
        Debug.Log("[EnemySpawner] Spawned enemy");
    }
}

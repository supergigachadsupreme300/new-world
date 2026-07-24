using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LivestockSpawner : MonoBehaviour
{
    private readonly List<Livestock> _activeAnimals = new List<Livestock>();
    private float _trickleTimer;
    private const int InitialBatchSize = 10;
    private const int MaxAnimals = 20;
    private const float TrickleIntervalMin = 120f;
    private const float TrickleIntervalMax = 180f;
    private const float SpawnRadiusMin = 30f;
    private const float SpawnRadiusMax = 50f;

    private void Start()
    {
        StartCoroutine(InitialSpawn());
        _trickleTimer = Random.Range(TrickleIntervalMin, TrickleIntervalMax);
    }

    private void Update()
    {
        _activeAnimals.RemoveAll(a => a == null);

        _trickleTimer -= Time.deltaTime;
        if (_trickleTimer <= 0f)
        {
            _trickleTimer = Random.Range(TrickleIntervalMin, TrickleIntervalMax);
            if (_activeAnimals.Count < MaxAnimals)
                SpawnTrickle();
        }
    }

    private IEnumerator InitialSpawn()
    {
        yield return new WaitForSeconds(3f);

        for (int i = 0; i < InitialBatchSize; i++)
        {
            if (_activeAnimals.Count >= MaxAnimals) break;
            SpawnAnimal(GetRandomType(), false);
            yield return new WaitForSeconds(0.3f);
        }
    }

    private void SpawnTrickle()
    {
        int count = Random.Range(1, 3);
        for (int i = 0; i < count; i++)
        {
            if (_activeAnimals.Count >= MaxAnimals) break;
            SpawnAnimal(GetRandomType(), true);
        }
    }

    private Livestock.AnimalType GetRandomType()
    {
        var types = (Livestock.AnimalType[])System.Enum.GetValues(typeof(Livestock.AnimalType));
        return types[Random.Range(0, types.Length)];
    }

    private void SpawnAnimal(Livestock.AnimalType type, bool withAnimation)
    {
        var player = GameManager.Instance?.Player;
        if (player == null) return;

        Vector3 playerPos = player.transform.position;
        float angle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
        float dist = Random.Range(SpawnRadiusMin, SpawnRadiusMax);
        Vector3 spawnPos = playerPos + new Vector3(Mathf.Cos(angle) * dist, 0f, Mathf.Sin(angle) * dist);
        spawnPos.y = 0.5f;

        var go = new GameObject("Livestock_" + type);
        go.transform.SetParent(transform);
        go.transform.position = spawnPos;

        var livestock = go.AddComponent<Livestock>();
        livestock.Type = type;
        _activeAnimals.Add(livestock);

        if (withAnimation)
            livestock.StartSpawnAnimation();
    }
}

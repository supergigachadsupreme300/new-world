using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LivestockSpawner : MonoBehaviour
{
    private readonly List<Livestock> _activeAnimals = new List<Livestock>();
    private readonly Queue<FlyingCrane> _cranePool = new Queue<FlyingCrane>();
    private float _trickleTimer;
    private const int InitialBatchSize = 10;
    private const int MaxAnimals = 20;
    private const int PoolSize = 3;
    private const float TrickleIntervalMin = 120f;
    private const float TrickleIntervalMax = 180f;
    private const float SpawnRadiusMin = 30f;
    private const float SpawnRadiusMax = 50f;

    private void Start()
    {
        for (int i = 0; i < PoolSize; i++)
        {
            var go = new GameObject("FlyingCrane");
            go.transform.SetParent(transform);
            go.AddComponent<FlyingCrane>();
            go.SetActive(false);
            _cranePool.Enqueue(go.GetComponent<FlyingCrane>());
        }

        _trickleTimer = Random.Range(TrickleIntervalMin, TrickleIntervalMax);
    }

    public void Restart()
    {
        StopAllCoroutines();
        _activeAnimals.RemoveAll(a => a == null);
        _trickleTimer = Random.Range(TrickleIntervalMin, TrickleIntervalMax);
        StartCoroutine(InitialSpawn());
    }

    private void Update()
    {
        if (GameManager.Instance != null && GameManager.Instance.GamePaused) return;

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
            SpawnCrane(GetRandomType());
            yield return new WaitForSeconds(0.8f);
        }
    }

    private void SpawnTrickle()
    {
        int count = Random.Range(1, 3);
        for (int i = 0; i < count; i++)
        {
            if (_activeAnimals.Count >= MaxAnimals) break;
            SpawnCrane(GetRandomType());
        }
    }

    private void SpawnCrane(Livestock.AnimalType type)
    {
        var player = GameManager.Instance?.Player;
        if (player == null) return;

        Vector3 playerPos = player.transform.position;
        float angle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
        float dist = Random.Range(SpawnRadiusMin, SpawnRadiusMax);
        Vector3 dropTarget = playerPos + new Vector3(Mathf.Cos(angle) * dist, 0f, Mathf.Sin(angle) * dist);
        dropTarget.y = 0.5f;

        FlyingCrane crane = GetCrane();
        crane.Setup(type, dropTarget, OnCraneLanded);
    }

    private FlyingCrane GetCrane()
    {
        while (_cranePool.Count > 0)
        {
            var crane = _cranePool.Dequeue();
            if (crane != null) return crane;
        }

        var go = new GameObject("FlyingCrane");
        go.transform.SetParent(transform);
        go.AddComponent<FlyingCrane>();
        return go.GetComponent<FlyingCrane>();
    }

    public void ReturnCrane(FlyingCrane crane)
    {
        if (crane != null)
            _cranePool.Enqueue(crane);
    }

    private void OnCraneLanded(Livestock livestock)
    {
        _activeAnimals.Add(livestock);
    }

    private Livestock.AnimalType GetRandomType()
    {
        var types = (Livestock.AnimalType[])System.Enum.GetValues(typeof(Livestock.AnimalType));
        return types[Random.Range(0, types.Length)];
    }
}

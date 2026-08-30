using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LivestockSpawner : MonoBehaviour
{
    private readonly List<Livestock> _activeAnimals = new List<Livestock>();
    private readonly Queue<FlyingCrane> _cranePool = new Queue<FlyingCrane>();
    private static readonly Collider[] _dropBuffer = new Collider[16];
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
        Vector3 fallback = playerPos - player.transform.forward * 20f;
        fallback.y = 0.5f;
        Vector3 dropTarget = FindValidDropTarget(playerPos, fallback);

        FlyingCrane crane = GetCrane();
        crane.Setup(type, dropTarget, OnCraneLanded);
    }

    private Vector3 FindValidDropTarget(Vector3 playerPos, Vector3 fallback)
    {
        for (int attempt = 0; attempt < 8; attempt++)
        {
            float angle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
            float dist = Random.Range(SpawnRadiusMin, SpawnRadiusMax);
            Vector3 target = playerPos + new Vector3(Mathf.Cos(angle) * dist, 0.5f, Mathf.Sin(angle) * dist);
            if (IsValidDropTarget(target))
                return target;
        }
        return fallback;
    }

    private bool IsValidDropTarget(Vector3 target)
    {
        if (Mathf.Abs(target.x) > 250f || Mathf.Abs(target.z) > 250f)
            return false;

        var wb = WorldBuilder.Instance;
        if (wb != null && wb.IsOnRoad(target))
            return false;

        int hitCount = Physics.OverlapSphereNonAlloc(target + Vector3.up * 0.5f, 1.2f, _dropBuffer, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide);
        for (int i = 0; i < hitCount; i++)
        {
            var col = _dropBuffer[i];
            if (col.GetComponentInParent<WaterVolume>() != null)
                return false;
            if (col.name == "Sea" || col.name == "SeaWater")
                return false;
            if (wb != null && wb.FindBuilding(col.gameObject) != null)
                return false;
        }
        return true;
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

    private static readonly Livestock.AnimalType[] _animalTypes =
        (Livestock.AnimalType[])System.Enum.GetValues(typeof(Livestock.AnimalType));

    private Livestock.AnimalType GetRandomType()
    {
        return _animalTypes[Random.Range(0, _animalTypes.Length)];
    }

    public List<Livestock> GetActiveAnimals()
    {
        _activeAnimals.RemoveAll(a => a == null);
        return _activeAnimals;
    }
}

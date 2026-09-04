using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Phase 9 (Task: Object pooling optimization): generic object pooler for frequently spawned
/// objects (projectiles, loot, particles, enemy health bars). Composes Unity's component lifecycle
/// without reaching into any existing spawning code; callers swap ad-hoc Instantiate/Destroy for
/// <see cref="Get(GameObject)"/> / <see cref="Return(GameObject,float)"/>.
/// </summary>
public sealed class ObjectPooler : MonoBehaviour
{
    public static ObjectPooler Instance { get; private set; }

    private readonly Dictionary<EntityId, Queue<GameObject>> _pools =
        new Dictionary<EntityId, Queue<GameObject>>();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    /// <summary>
    /// Pre-warm a pool for a prefab so first-use does not allocate.
    /// </summary>
    public void Warm(GameObject prefab, int count)
    {
        if (prefab == null || count <= 0) return;
        EntityId key = prefab.GetEntityId();
        if (!_pools.TryGetValue(key, out var queue))
        {
            queue = new Queue<GameObject>(count);
            _pools[key] = queue;
        }
        while (queue.Count < count)
            queue.Enqueue(CreateNew(prefab, true));
    }

    /// <summary>Take an object from the pool, or instantiate a fresh one if empty.</summary>
    public GameObject Get(GameObject prefab)
    {
        if (prefab == null) return null;
        EntityId key = prefab.GetEntityId();
        if (_pools.TryGetValue(key, out var queue) && queue.Count > 0)
        {
            GameObject go = queue.Dequeue();
            if (go != null)
            {
                go.SetActive(true);
                return go;
            }
        }
        return CreateNew(prefab, false);
    }

    /// <summary>
    /// Fetch a pooled object and return the requested component (or add it if absent).
    /// </summary>
    public T GetComponent<T>(GameObject prefab) where T : Component
    {
        GameObject go = Get(prefab);
        if (go == null) return null;
        return go.GetComponent<T>() ?? go.AddComponent<T>();
    }

    /// <summary>
    /// Return an object to its pool. If <paramref name="delay"/> is &gt; 0 the object stays
    /// active that many seconds and is released to the pool afterward (no use of coroutines).
    /// </summary>
    public void Return(GameObject go, float delay = 0f)
    {
        if (go == null) return;
        if (delay > 0f)
        {
            go.AddComponent<ReturnTimer>().Begin(this, go, delay);
            return;
        }
        ReleaseNow(go);
    }

    /// <summary>Releases to the pool immediately (also used by the delay timer).</summary>
    private void ReleaseNow(GameObject go)
    {
        EntityId key = go.GetEntityId();
        if (!_pools.TryGetValue(key, out var queue))
        {
            // Register from the pool the object originally came from, else a throwaway pool.
            queue = new Queue<GameObject>();
            _pools[key] = queue;
        }
        go.SetActive(false);
        queue.Enqueue(go);
    }

    private static GameObject CreateNew(GameObject prefab, bool inactive)
    {
        GameObject go = Instantiate(prefab);
        go.SetActive(!inactive);
        return go;
    }

    /// <summary>Returns all pooled objects to their pools now.</summary>
    public void Clear()
    {
        foreach (var queue in _pools.Values)
        {
            while (queue.Count > 0)
            {
                GameObject go = queue.Dequeue();
                if (go != null) Destroy(go);
            }
        }
        _pools.Clear();
    }

    /// <summary>
    /// Tiny helper that releases an object back to the pool after a delay without using
    /// a MonoBehaviour coroutine (avoids GC-heavy allocations on the hot path).
    /// </summary>
    private sealed class ReturnTimer : MonoBehaviour
    {
        private ObjectPooler _pool;
        private GameObject _target;
        private float _remaining;

        public void Begin(ObjectPooler pool, GameObject target, float delay)
        {
            _pool = pool;
            _target = target;
            _remaining = delay;
        }

        private void Update()
        {
            if (_target == null) { Destroy(gameObject); return; }
            _remaining -= Time.deltaTime;
            if (_remaining <= 0f)
            {
                _pool.ReleaseNow(_target);
                Destroy(gameObject);
            }
        }
    }
}
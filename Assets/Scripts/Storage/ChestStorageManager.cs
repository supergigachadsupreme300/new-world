using System;
using System.Collections.Generic;
using UnityEngine;

// Persistent player-chest storage for Phase 3C.
// Each placed chest keeps its own dictionary keyed by world position;
// each chest holds up to MaxDistinctTypes item types (auto-stacked counts).
public class ChestStorageManager : MonoSingleton<ChestStorageManager>
{
    public const int MaxDistinctTypes = 10;

    private readonly Dictionary<Vector3, Dictionary<string, int>> _storages =
        new Dictionary<Vector3, Dictionary<string, int>>();

    public void Initialize()
    {
    }

    public Dictionary<string, int> GetContents(Vector3 position)
    {
        return StorageFor(position);
    }

    public int CountItem(Vector3 position, string itemType)
    {
        var storage = StorageFor(position);
        return storage.TryGetValue(itemType, out var count) ? count : 0;
    }

    public bool IsFull(Vector3 position, string itemType)
    {
        var storage = StorageFor(position);
        if (storage.ContainsKey(itemType))
            return false;
        return storage.Count >= MaxDistinctTypes;
    }

    // Stores count; returns false when the chest cannot hold a new item type.
    public bool StoreItem(Vector3 position, string itemType, int count)
    {
        if (string.IsNullOrEmpty(itemType) || count <= 0)
            return false;
        var storage = StorageFor(position);
        if (storage.ContainsKey(itemType))
        {
            storage[itemType] += count;
            return true;
        }
        if (storage.Count >= MaxDistinctTypes)
            return false;
        storage[itemType] = count;
        return true;
    }

    // Takes up to count items; returns how many were actually taken.
    public int TakeItem(Vector3 position, string itemType, int count)
    {
        var storage = StorageFor(position);
        if (!storage.TryGetValue(itemType, out var available))
            return 0;
        int take = Mathf.Min(available, count);
        if (take >= available)
            storage.Remove(itemType);
        else
            storage[itemType] = available - take;
        return take;
    }

    private Dictionary<string, int> StorageFor(Vector3 position)
    {
        var key = Key(position);
        if (!_storages.TryGetValue(key, out var storage))
        {
            storage = new Dictionary<string, int>();
            _storages[key] = storage;
        }
        return storage;
    }

    private static Vector3 Key(Vector3 position)
    {
        return new Vector3(
            Mathf.Round(position.x * 100f) / 100f,
            Mathf.Round(position.y * 100f) / 100f,
            Mathf.Round(position.z * 100f) / 100f);
    }

    public string SerializeState()
    {
        var slots = new List<ChestSlotSave>();
        foreach (var kv in _storages)
        {
            foreach (var item in kv.Value)
            {
                slots.Add(new ChestSlotSave
                {
                    x = kv.Key.x,
                    y = kv.Key.y,
                    z = kv.Key.z,
                    type = item.Key,
                    count = item.Value
                });
            }
        }
        return JsonUtility.ToJson(new ChestStorageSaveData { slots = slots.ToArray() });
    }

    public void DeserializeState(string json)
    {
        _storages.Clear();
        if (string.IsNullOrEmpty(json)) return;
        try
        {
            var data = JsonUtility.FromJson<ChestStorageSaveData>(json);
            if (data == null || data.slots == null) return;
            foreach (var slot in data.slots)
            {
                if (slot == null || string.IsNullOrEmpty(slot.type) || slot.count <= 0)
                    continue;
                var key = new Vector3(slot.x, slot.y, slot.z);
                if (!_storages.TryGetValue(key, out var storage))
                {
                    storage = new Dictionary<string, int>();
                    _storages[key] = storage;
                }
                if (storage.ContainsKey(slot.type))
                    storage[slot.type] += slot.count;
                else if (storage.Count < MaxDistinctTypes)
                    storage[slot.type] = slot.count;
            }
        }
        catch
        {
            // ignore malformed save
        }
    }

    [Serializable]
    public class ChestStorageSaveData
    {
        public ChestSlotSave[] slots;
    }

    [Serializable]
    public class ChestSlotSave
    {
        public float x;
        public float y;
        public float z;
        public string type;
        public int count;
    }
}
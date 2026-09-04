using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Phase 9 (Task: LOD system for distant chunks): manages a chunk-detail LOD track. Given a set
/// of chunk roots (each a GameObject that owns a <see cref="MeshRenderer"/> plus optional lower-
/// detail detail meshes as children), it switches which visual is active based on distance and
/// screen-fill from the main camera. Self-contained helper that a scene can drive with
/// <see cref="RegisterChunk"/>; it composes existing chunk mesh objects without touching their
/// generation code.
/// </summary>
public sealed class ChunkLodManager : MonoBehaviour
{
    [System.Serializable]
    public class LodBand
    {
        [Tooltip("Details activated from this start distance up to the next band (or infinity).")]
        public float StartDistance = 0f;
        [Tooltip("Child detail mesh (name match) to enable in this band, or null for the root.")]
        public string DetailName = "";
    }

    [Tooltip("Distance bands, ascending. First match by StartDistance wins.")]
    public List<LodBand> Bands = new List<LodBand>
    {
        new LodBand { StartDistance = 0f, DetailName = "" },
        new LodBand { StartDistance = 30f, DetailName = "Lod1" },
        new LodBand { StartDistance = 60f, DetailName = "Lod2" }
    };

    [Tooltip("Distant chunks beyond the last band are hidden.")]
    public float CullDistance = 120f;
    [Tooltip("Re-evaluate selection after this many frames instead of every frame.")]
    public int RefreshEveryFrames = 2;

    private readonly List<ChunkEntry> _chunks = new List<ChunkEntry>();
    private int _frame;

    private class ChunkEntry
    {
        public Transform Root;
        public readonly Dictionary<string, GameObject> Details = new Dictionary<string, GameObject>();
        public int BandIndex = -1;
    }

    /// <summary>Register a chunk root and index its child detail meshes by name.</summary>
    public void RegisterChunk(GameObject root)
    {
        if (root == null) return;
        var entry = new ChunkEntry { Root = root.transform };
        if (root.transform.childCount > 0)
        {
            foreach (Transform child in root.transform)
                entry.Details[child.name] = child.gameObject;
        }
        entry.BandIndex = -1;
        _chunks.Add(entry);
    }

    /// <summary>Unregister a previously registered chunk root.</summary>
    public void UnregisterChunk(GameObject root)
    {
        if (root == null) return;
        for (int i = _chunks.Count - 1; i >= 0; i--)
        {
            if (_chunks[i].Root == root.transform)
                _chunks.RemoveAt(i);
        }
    }

    private void Update()
    {
        _frame++;
        if (RefreshEveryFrames > 0 && _frame % RefreshEveryFrames != 0)
            return;

        Camera cam = Camera.main;
        if (cam == null) return;
        Vector3 camPos = cam.transform.position;

        for (var i = _chunks.Count - 1; i >= 0; i--)
        {
            var chunk = _chunks[i];
            if (chunk.Root == null)
            {
                _chunks.RemoveAt(i);
                continue;
            }

            float dist = Vector3.Distance(camPos, chunk.Root.position);
            int band = BandFor(dist);
            chunk.Root.gameObject.SetActive(dist <= CullDistance);
            if (band != chunk.BandIndex)
            {
                chunk.BandIndex = band;
                ApplyBand(chunk, band);
            }
        }
    }

    private int BandFor(float dist)
    {
        int index = 0;
        for (int b = 0; b < Bands.Count; b++)
        {
            if (dist >= Bands[b].StartDistance)
                index = b;
            else
                break;
        }
        return index;
    }

    private void ApplyBand(ChunkEntry chunk, int band)
    {
        string name = band < Bands.Count ? Bands[band].DetailName : "";
        bool useDetail = !string.IsNullOrEmpty(name);

        // Enable exactly one visual (root mesh or named detail child).
        // Only touch children that are in the Details dictionary (LOD meshes).
        // Props (trees, rocks) are NOT in Details and must stay untouched.
        if (useDetail)
        {
            if (chunk.Details.TryGetValue(name, out var detail) && detail != null)
                detail.SetActive(true);
            foreach (var kv in chunk.Details)
                if (kv.Value != detail) kv.Value.SetActive(false);
        }
        else
        {
            foreach (var kv in chunk.Details)
                kv.Value.SetActive(false);
            var mr = chunk.Root.GetComponent<MeshRenderer>();
            if (mr != null) mr.enabled = true;
        }
    }
}

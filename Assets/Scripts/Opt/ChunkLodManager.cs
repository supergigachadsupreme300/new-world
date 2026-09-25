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

    [Tooltip("Distant chunks beyond the last band are hidden. The effective cull distance auto-scales with the WorldStreamer render radius so streamed chunks are never hidden early.")]
    public float CullDistance = 120f;
    [Tooltip("Re-evaluate selection after this many frames instead of every frame.")]
    public int RefreshEveryFrames = 2;

    private readonly List<ChunkEntry> _chunks = new List<ChunkEntry>();
    private WorldStreamer _streamer;
    private int _frame;

    // 1ea: the all-chunk sweep is burst over refresh ticks (a rolling cursor) instead of touching
    // every streamed chunk every refresh, and distances are compared squared (no per-chunk sqrt).
    private const int ScanBudget = 1024;
    private int _scanCursor;

    private class ChunkEntry
    {
        public Transform Root;
        public MeshRenderer Mr;
        public readonly Dictionary<string, GameObject> Details = new Dictionary<string, GameObject>();
        public ChunkObject Chunk;
        public int BandIndex = -1;
    }

    /// <summary>
    /// Register a chunk root and index its child detail meshes by name. Only children that look
    /// like LOD detail meshes (name starts with "Lod", matching the band DetailNames) are tracked —
    /// prop children (trees, rocks, etc.) must never be toggled by ApplyBand. The chunk itself is
    /// kept so ApplyBand can rebuild a stale decimated LOD before showing it (1e6).
    /// </summary>
    public void RegisterChunk(GameObject root)
    {
        if (root == null) return;
        var entry = new ChunkEntry
        {
            Root = root.transform,
            Mr = root.GetComponent<MeshRenderer>(),
            Chunk = root.GetComponent<ChunkObject>(),
        };
        if (root.transform.childCount > 0)
        {
            foreach (Transform child in root.transform)
            {
                if (child.name.StartsWith("Lod", System.StringComparison.Ordinal))
                    entry.Details[child.name] = child.gameObject;
            }
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
        float cullSq = EffectiveCullDistance();
        cullSq *= cullSq;

        // Rolling burst (1ea): evaluate at most ScanBudget chunks per refresh, wrapping around.
        // Null-root entries are dropped in place (the compacted entry shifts into the cursor slot).
        int checkedCount = 0;
        while (checkedCount < ScanBudget && _chunks.Count > 0)
        {
            if (_scanCursor >= _chunks.Count)
                _scanCursor = 0;
            var chunk = _chunks[_scanCursor];
            if (chunk.Root == null)
            {
                _chunks.RemoveAt(_scanCursor);
                continue;
            }
            _scanCursor++;
            checkedCount++;

            // 1gc: dormant (hidden-but-retained) chunks must never be re-enabled by the band sweep.
            // WorldStreamer demotes them out of LoadedChunks and NewWorldSystems unregisters them on
            // its timer-gated delta-diff, but in the gap between demote and unregister the sweep here
            // runs first — skipping keeps the visuals exactly as the demote left them (root + LOD off,
            // the coarse far cell covers). Costs one bool read per scan tick.
            if (chunk.Chunk != null && chunk.Chunk.Dormant)
                continue;

            Vector3 off = camPos - chunk.Root.position;
            float distSq = off.x * off.x + off.y * off.y + off.z * off.z;
            int band = BandForSq(distSq);
            bool wantVisible = distSq <= cullSq;
            if (chunk.Root.gameObject.activeSelf != wantVisible)
                chunk.Root.gameObject.SetActive(wantVisible);
            if (band != chunk.BandIndex)
            {
                chunk.BandIndex = band;
                ApplyBand(chunk, band);
            }
            else if (band > 0 && chunk.Chunk != null && chunk.Chunk.LodDirty)
            {
                // 1fz: deformation (PatchRegion -> _lodDirty) can hit a chunk that ALREADY shows a
                // detail band, and the refresh above only ran on band CHANGE — so the active Lod
                // child kept the pre-deform surface until the player crossed a band boundary (a
                // visible hole at the cast site, healed only by getting close). Honor the
                // no-stale-far-surface invariant directly: rebuild the dirty grids in place while
                // the detail stays selected. RefreshLodMeshes early-outs when the flag is clear, so
                // this costs one bool read per scan tick for clean chunks.
                chunk.Chunk.RefreshLodMeshes();
            }
        }
    }

    private int BandForSq(float distSq)
    {
        int index = 0;
        for (int b = 0; b < Bands.Count; b++)
        {
            if (distSq >= Bands[b].StartDistance * Bands[b].StartDistance)
                index = b;
            else
                break;
        }
        return index;
    }

    /// <summary>
    /// Effective cull distance auto-matches the streaming render radius: a chunk extends
    /// (radius + 1) chunk-steps from the focus (hysteresis keep-margin), so chunks are only
    /// hidden once they fall beyond the streamed area, never while still being streamed in.
    /// </summary>
    private float EffectiveCullDistance()
    {
        if (_streamer == null)
            _streamer = Object.FindAnyObjectByType<WorldStreamer>();
        if (_streamer != null && _streamer.RenderDistance != null)
            return Mathf.Max(CullDistance, (_streamer.RenderDistance.Radius + 1) * TerrainChunkCoord.ChunkSize);
        return CullDistance;
    }

    private void ApplyBand(ChunkEntry chunk, int band)
    {
        string name = band < Bands.Count ? Bands[band].DetailName : "";
        bool useDetail = !string.IsNullOrEmpty(name);

        // Cache the root renderer: the FULL mesh stays hidden while a detail band is active and is
        // restored for band 0 (1e6 fix — previously the root renderer stayed ENABLED in the detail
        // bands, so a distant chunk drew its ~1800-tri root AND the detail on top; no real saving).
        // The MeshCollider is untouched — it lives on the root and rides the root mesh, so
        // collider-on-demand (1dq) physics never depends on which renderer is active.
        MeshRenderer rootMr = chunk.Mr != null ? chunk.Mr : (chunk.Mr = chunk.Root.GetComponent<MeshRenderer>());

        // Enable exactly one visual (root mesh or named detail child).
        // Only touch children that are in the Details dictionary (LOD meshes).
        // Props (trees, rocks) are NOT in Details and must stay untouched.
        if (useDetail)
        {
            // Request the decimated data before showing it: deformation (ChunkObject.PatchRegion)
            // marks LOD stale, so a band switch first refreshes the grid from the current terrain —
            // a far band never renders a pre-excavation hole.
            if (chunk.Chunk != null)
                chunk.Chunk.RefreshLodMeshes();

            GameObject detail = null;
            if (chunk.Details.TryGetValue(name, out var candidate) && candidate != null)
                detail = candidate;

            if (detail != null)
            {
                detail.SetActive(true);
                if (rootMr != null)
                    rootMr.enabled = false;
            }
            else if (rootMr != null)
            {
                // Named detail missing — keep the full mesh so the chunk never goes invisible.
                rootMr.enabled = true;
            }

            foreach (var kv in chunk.Details)
                if (kv.Value != null && kv.Value != detail)
                    kv.Value.SetActive(false);
        }
        else
        {
            foreach (var kv in chunk.Details)
            {
                if (kv.Value != null)
                    kv.Value.SetActive(false);
            }
            if (rootMr != null)
                rootMr.enabled = true;
        }
    }
}

using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Phase 9 (Task: chunk distance cull): hides a streamed chunk root once it falls past the
/// distance the WORLD promises to be covered. Given a set of chunk roots it toggles each root
/// GameObject's active state from the main camera's distance; it composes existing chunk mesh
/// objects without touching their generation code, and a scene can drive it with
/// <see cref="RegisterChunk"/>.
///
/// <para><b>1f6 removed this class's other half.</b> It used to also switch a detail-LOD band per
/// chunk (full mesh → a "Lod1" child decimated to every 2nd corner → a "Lod2" child at every 3rd)
/// and built those children itself via <c>ChunkObject.RefreshLodMeshes</c>. Both are gone: the band
/// children, the band table and the whole decimated-LOD build path were deleted, because the
/// coarse lattice was drawing a DIFFERENT surface from the one under the player's feet at 30 m and
/// 60 m. Every chunk now draws its own root mesh at every distance, and the only thing left here is
/// the cull.</para>
///
/// <para>What the cull is FOR is not "save draw calls" — it is the 1gh invariant below: a real chunk
/// is the ONLY owner of some footprints, and hiding one that no far cell covers opens a real hole in
/// the ground. That is why <see cref="EffectiveCullDistance"/> never shrinks below the streamer's
/// own retention extent.</para>
/// </summary>
public sealed class ChunkDistanceCull : MonoBehaviour
{
    [Tooltip("Distant chunks beyond this are hidden, unless the streamed extent below is larger. Auto-scales with the WorldStreamer render radius so streamed chunks are never hidden early.")]
    public float CullDistance = 120f;
    [Tooltip("Re-evaluate chunk visibility after this many frames instead of every frame.")]
    public int RefreshEveryFrames = 2;

    private readonly List<ChunkEntry> _chunks = new List<ChunkEntry>();
    private WorldStreamer _streamer;
    private int _frame;

    // 1ea: the all-chunk sweep is burst over refresh ticks (a rolling cursor) instead of touching
    // every streamed chunk every refresh, and distances are compared squared (no per-chunk sqrt).
    private const int ScanBudget = 1024;
    private int _scanCursor;

    // (1gf) QA readout: main-thread wall time (ms) of the last full visibility sweep + the rolling
    // peak since this manager was created. The sweep itself is a distance compare and a SetActive
    // per chunk that changed state, so this is small in steady state; the peak was traced (1ik) to
    // the INITIAL fill, when every registered chunk flips at once, not to a recurring cost.
    // Surfaced on the bench HUD so a long sprint shows which stage actually eats the gameplay frame.
    private float _lastCullMs;
    private float _peakCullMs;
    public float LastCullMs => _lastCullMs;
    public float PeakCullMs => _peakCullMs;

    private class ChunkEntry
    {
        public Transform Root;
        public ChunkObject Chunk;
    }

    /// <summary>
    /// Register a chunk root so this sweep manages its visibility. Only the root is tracked — the
    /// chunk's props (trees, rocks) are children of the same transform and must never be toggled
    /// individually. The ChunkObject is kept so the sweep can read its <c>Dormant</c> flag.
    /// </summary>
    public void RegisterChunk(GameObject root)
    {
        if (root == null) return;
        var entry = new ChunkEntry
        {
            Root = root.transform,
            Chunk = root.GetComponent<ChunkObject>(),
        };
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

        // (1gf) sweep timing for the bench readout: frames skipped by the refresh gate are no-op
        // (~nothing to time), so the stopwatch starts here and records only real sweep frames.
        float sweepStart = Time.realtimeSinceStartup;

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

            // 1gc: dormant (hidden-but-retained) chunks must never be re-enabled by the sweep.
            // WorldStreamer demotes them out of LoadedChunks and NewWorldSystems unregisters them on
            // its timer-gated delta-diff, but in the gap between demote and unregister the sweep here
            // runs first — skipping keeps the visuals exactly as the demote left them (the coarse far
            // cell covers). Costs one bool read per scan tick.
            if (chunk.Chunk != null && chunk.Chunk.Dormant)
                continue;

            Vector3 off = camPos - chunk.Root.position;
            float distSq = off.x * off.x + off.y * off.y + off.z * off.z;
            bool wantVisible = distSq <= cullSq;
            if (chunk.Root.gameObject.activeSelf != wantVisible)
                chunk.Root.gameObject.SetActive(wantVisible);
        }

        // (1gf) record the sweep spend for the bench readout (rolling peak, never resets).
        _lastCullMs = (Time.realtimeSinceStartup - sweepStart) * 1000f;
        if (_lastCullMs > _peakCullMs)
            _peakCullMs = _lastCullMs;
    }

    /// <summary>
    /// Effective cull distance auto-matches the streaming render radius: a chunk extends
    /// (radius + 1) chunk-steps from the focus (hysteresis keep-margin), so chunks are only
    /// hidden once they fall beyond the streamed area, never while still being streamed in.
    /// (1gh) Invariant: the cull must ALSO cover the real-chunk extent the streamer retains
    /// (NearRingRadius + 1 hysteresis + DormantRingDepth). Far cells only exist BEYOND the near
    /// ring (WorldStreamer.FarShell.FarCellForChunk), so a chunk inside it is the ONLY surface its
    /// cell has — culling it opens a real hole. With a small RenderDistance asset the render term
    /// alone used to dip below the near ring (e.g. radius 7 =&gt; 240 m &lt; ring-8/9 chunks at ~250 m),
    /// hiding loaded ground that no far cell could cover. The max guarantees the sweep never hides
    /// ground the streamer is the sole owner of.
    /// </summary>
    private float EffectiveCullDistance()
    {
        if (_streamer == null)
            _streamer = Object.FindAnyObjectByType<WorldStreamer>();
        float streamSize = TerrainChunkCoord.ChunkSize;
        float extent = CullDistance;
        if (_streamer != null)
        {
            if (_streamer.RenderDistance != null)
                extent = Mathf.Max(extent, (_streamer.RenderDistance.Radius + 1) * streamSize);
            // (1gh) The streamed real-chunk extent (near ring + hysteresis keep + dormant depth)
            // is what the streamer may RETAIN loaded; the cull must never hide those.
            extent = Mathf.Max(extent, (_streamer.NearRingRadius + 1 + _streamer.DormantRingDepth) * streamSize);
        }
        return extent;
    }
}
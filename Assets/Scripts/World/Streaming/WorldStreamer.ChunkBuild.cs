using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

/// <summary>
/// Background-thread chunk generation (noise or disk-saved mods) portion of the WorldStreamer partial class.
/// </summary>

public partial class WorldStreamer
{

    // --- Background thread: generate entire chunk ---

    /// <summary>
    /// Tile mods of a NEIGHBOURING chunk, read from its save file and keyed localZ*cs+localX the
    /// same way this file's own build path keys them. Returns null when the chunk has no save or no
    /// usable mods, which leaves the caller's corner as NaN so the ordinary noise regeneration still
    /// runs for it. Split out of <c>BuildChunkMeshData</c> so the foreign-corner pass can read a
    /// neighbour's mods without duplicating the filtering rules. Safe on the build thread: it
    /// touches nothing but <c>ChunkSaveManager</c> and its own locals.
    /// </summary>
    private Dictionary<int, ChunkTileMod> ForeignTileMods(TerrainChunkCoord tc, long seed)
    {
        if (!ChunkSaveManager.TryLoadChunk(seed, tc, out ChunkSaveData fsave))
            return null;
        if (fsave.Mods.Count == 0)
            return null;
        int fcs = TerrainChunkCoord.ChunkSize;
        var map = new Dictionary<int, ChunkTileMod>();
        for (int i = 0; i < fsave.Mods.Count; i++)
        {
            ChunkTileMod m = fsave.Mods[i];
            if (m.LocalX >= 0 && m.LocalX < fcs && m.LocalZ >= 0 && m.LocalZ < fcs)
                map[m.LocalZ * fcs + m.LocalX] = m;
        }
        return map;
    }

    /// <summary>
    /// A neighbour's saved height differs from pristine noise by more than this, so it is treated
    /// as a real edit rather than as an untouched corner that a mod stored verbatim. A mod holds
    /// all four corners of its tile, including the ones the edit never reached, so "this tile has a
    /// sane value for the node" is NOT the same as "this tile edited the node" - and preferring the
    /// first sane value alone would let an untouched corner outvote the one real edit. The audit
    /// classified 345 of 400 seam nodes as exactly one side edited against pristine, so preferring
    /// the non-pristine value is reading the measured shape of the fault, not guessing at it.
    /// </summary>
    private const float SeamPristineTol = 0.01f;

    // The four tiles sharing world node (wx,wz), and the Heights slot each stores it in.
    // Read left-to-right, top-to-bottom: (wx-1,wz-1) NE, (wx,wz-1) NW, (wx-1,wz) SE, (wx,wz) SW.
    private static readonly int[] SeamTileDX = { -1, 0, -1, 0 };
    private static readonly int[] SeamTileDZ = { -1, -1, 0, 0 };
    private static readonly int[] SeamTileSlot = { 1, 0, 2, 3 };

    /// <summary>
    /// Resolves world node (wx,wz) from the four tiles that share it, across this chunk and up to
    /// three neighbours, and reports the agreed height. Every chunk that borders the node runs this
    /// with the same inputs, so all four arrive at the same value regardless of build order.
    /// Ties are broken by <see cref="SeamTileDX"/> order, which is fixed, so the result is
    /// deterministic even if two tiles were edited to different heights.
    ///
    /// Neighbour mods are read from SAVE FILES only, never from the live <c>_loadedChunks</c>
    /// dictionary: this runs on a ThreadPool thread (BackgroundGenerateChunk) while the main thread
    /// builds, unloads and demotes, and Dictionary&lt;TKey,TValue&gt; is not safe to read during a
    /// write. The whole build path is already disk-driven - this chunk is itself built from
    /// ChunkSaveManager.TryLoadChunk - so reading a neighbour from disk is the same source the
    /// neighbour itself was built from, and it costs no fidelity. The residue is an edit still in
    /// memory and not yet flushed, which keeps the old value until the save lands: the same bounded,
    /// self-correcting staleness 1i3 documents for the far shell.
    /// </summary>
    private bool TryResolveSeamCorner(int wx, int wz, TerrainChunkCoord tc, int cs,
        Dictionary<int, ChunkTileMod> ownMods,
        Dictionary<TerrainChunkCoord, Dictionary<int, ChunkTileMod>> foreignMods,
        long seed, out float height)
    {
        height = 0f;
        // The node's pristine height, so an untouched corner carried by a mod cannot outvote the
        // one tile that actually edited this node. Sampled once per node, and only for the handful
        // of boundary nodes this chunk's own tiles left NaN.
        float pristine = TerrainNoiseGenerator.GetHeight(seed,
            wx * ChunkData.Size, wz * ChunkData.Size);
        bool havePristine = false;
        for (int t = 0; t < 4; t++)
        {
            int tileX = wx + SeamTileDX[t];
            int tileZ = wz + SeamTileDZ[t];
            TerrainChunkCoord owner =
                new TerrainChunkCoord(FloorDiv(tileX, cs), FloorDiv(tileZ, cs));
            int lx = tileX - owner.X * cs;
            int lz = tileZ - owner.Z * cs;
            Dictionary<int, ChunkTileMod> map;
            if (owner.X == tc.X && owner.Z == tc.Z)
            {
                map = ownMods;
            }
            else
            {
                if (!foreignMods.TryGetValue(owner, out map))
                {
                    map = ForeignTileMods(owner, seed);
                    foreignMods[owner] = map;
                }
            }
            if (map == null)
                continue;
            if (!map.TryGetValue(lz * cs + lx, out ChunkTileMod m))
                continue;
            if (m.Heights == null || m.Heights.Length < ChunkData.VertexCount)
                continue;
            float v = m.Heights[SeamTileSlot[t]];
            if (!IsSaneHeight(v))
                continue;
            // A real edit beats an untouched corner outright, and returns here, so a later
            // pristine tile cannot undo it.
            if (Mathf.Abs(v - pristine) > SeamPristineTol)
            {
                height = v;
                return true;
            }
            // Otherwise remember the first sane value as the fallback, and keep looking: a second
            // tile may hold the actual edit.
            if (!havePristine)
            {
                height = v;
                havePristine = true;
            }
        }
        // Nothing was edited at this node; every tile that holds it holds the untouched value.
        // That is still a real answer, and it beats each chunk regenerating the node from noise
        // independently.
        return havePristine;
    }

    /// <summary>
    /// Runs on a ThreadPool thread. Builds the chunk from disk deformation mods when they
    /// exist, otherwise from noise (a cache-miss), then merges the tile meshes into one
    /// thread-safe chunk mesh. The mesh mode (smooth heightfield vs. stepped voxel, 1et) is
    /// captured on the MAIN thread at dispatch time so a chunk never changes shape mid-build.
    /// </summary>
    private void BackgroundGenerateChunk(TerrainChunkCoord tc, long seed, bool voxel, int lowPolyStep)
    {
        try
        {
            _readyChunks.Enqueue(voxel ? BuildVoxelChunk(tc, seed) : BuildOrLoadChunk(tc, seed, lowPolyStep));
        }
        catch (System.Exception ex)
        {
            // Full stack (ex.ToString()), not just ex.Message: a persistent per-chunk failure drops
            // the chunk from in-flight and it re-dispatches every poll, so the repeated warning here
            // must NAME the exact line throwing (1fx diagnostic) or a 'chunks near the player missing'
            // bug is invisible.
            Debug.LogWarning($"[WorldStreamer] Background chunk generation failed for {tc}; it will retry. {ex}");
            byte _;
            _chunksInFlight.TryRemove(tc, out _);
        }
    }

    /// <summary>
    /// Builds a terrain chunk's 900 tile meshes plus the merged chunk mesh. Reads the terrain
    /// chunk's save file first: deformed tiles restore their saved heights (so revisiting an
    /// edited area is fast and exact); every other corner regenerates from noise (the cache
    /// miss path). Shared-edge contract is preserved because every tile whose corner a deformed
    /// tile touches is saved/handled together by DeformAt.
    /// </summary>
    private TerrainChunkMeshData BuildOrLoadChunk(TerrainChunkCoord tc, long seed, int lowPolyStep = 0)
    {
        int cs = TerrainChunkCoord.ChunkSize;
        int gridSize = TerrainChunkCoord.CornerGridSize; // 31

        bool loaded = ChunkSaveManager.TryLoadChunk(seed, tc, out ChunkSaveData save);
        Dictionary<int, ChunkTileMod> mods = null;
        bool hadLoadedMods = false;
        if (loaded && save.Mods.Count > 0)
        {
            mods = new Dictionary<int, ChunkTileMod>();
            for (int i = 0; i < save.Mods.Count; i++)
            {
                ChunkTileMod m = save.Mods[i];
                if (m.LocalX >= 0 && m.LocalX < cs && m.LocalZ >= 0 && m.LocalZ < cs)
                    mods[m.LocalZ * cs + m.LocalX] = m;
            }
            hadLoadedMods = true;
        }

        // Corner grid: NaN marks a corner that must regenerate from noise. Deformed tiles stamp
        // their saved corner heights first so shared edges within the patch stay gapless.
        // CRITICAL: the grid MUST be NaN-seeded, not zero-seeded. `new float[,]` zero-fills every
        // element, and zero is a VALID height — so an unstamped corner would read as "present"
        // (float.IsNaN(0f) is false) and the whole chunk would collapse to height 0 instead of
        // regenerating from noise. Prefill with NaN so only genuinely saved corners count as
        // present and every other corner rolls.
        float[,] corners = new float[gridSize, gridSize];
        for (int gi = 0; gi < gridSize * gridSize; gi++)
            corners[gi / gridSize, gi % gridSize] = float.NaN;
        if (mods != null)
        {
            foreach (KeyValuePair<int, ChunkTileMod> kv in mods)
            {
                ChunkTileMod m = kv.Value;
                if (m.Heights == null || m.Heights.Length < ChunkData.VertexCount)
                    continue;
                // Only stamp finite, in-band heights; any other value leaves the corner as
                // NaN so the regeneration loop below re-rolls it from noise (never garbage).
                corners[m.LocalX, m.LocalZ + 1] = IsSaneHeight(m.Heights[0]) ? m.Heights[0] : float.NaN;     // NW
                corners[m.LocalX + 1, m.LocalZ + 1] = IsSaneHeight(m.Heights[1]) ? m.Heights[1] : float.NaN; // NE
                corners[m.LocalX + 1, m.LocalZ] = IsSaneHeight(m.Heights[2]) ? m.Heights[2] : float.NaN;     // SE
                corners[m.LocalX, m.LocalZ] = IsSaneHeight(m.Heights[3]) ? m.Heights[3] : float.NaN;         // SW
            }
        }
        // 1i4. A corner on a chunk seam is a corner of up to four chunks, and until now each chunk
        // filled its own copy of that height from its OWN tiles. ApplyHeightEdits writes every
        // LOADED tile at a world coordinate, so two chunks that were both loaded when the edit
        // landed agree with each other - which is why most of the world looks right and the fault
        // hides. Where one side was not loaded, that side keeps pristine noise FOREVER, because
        // nothing reconciles it: ReconcileModifiedBorders repairs slab-wall bottoms, not corner
        // heights. The audit found 345 of 400 corner nodes in that state, so it is the NORMAL
        // state of a seam rather than an edge case - and with no side walls in low-poly a 0.29 m
        // step at a corner is a see-through crack, not a terrace.
        //
        // The fix resolves a shared corner from the WORLD instead of from this chunk's own tiles, so
        // every chunk computes the same height for a node no matter which was built first.
        //
        // 1i5. ALL FOUR edges, and ALL FOUR touching tiles. 1i4 scanned only the west and south
        // edges and, for each node, asked only the tile one metre back in BOTH axes - tile
        // (wx-1,wz-1), stored as its NE slot. That is one of the four tiles that share a node, and
        // it is only correct when the edit happens to have landed in that one:
        //  - The node has four touching tiles, in up to four chunks. Two are in this chunk (which
        //    already stamped them, or left them NaN), and TWO are in the neighbour. 1i4 read one
        //    of the neighbour's two, so an edit held by the other went unseen.
        //  - 1i4 never scanned the east and north edges at all. Its west-east case worked only
        //    because the EASTERN chunk scans its own west edge and finds this chunk. The reverse -
        //    edit in the east, western neighbour still pristine - was never asked, and a local dig
        //    produces exactly that.
        // So the seam propagated one way and not the other, which is rule 7's "a scope is a claim
        // about the mechanism, so name which owner(s) the walk admits": 1i4 admitted ONE owner and
        // called the pass a resolution.
        //
        // The four touching tiles of node (wx,wz), and the Heights slot each stores it in
        // (SW=3, NE=1, NW=0, SE=2, per the stamp above):
        //     (wx-1,wz-1) NE | (wx,wz-1) NW
        //     (wx-1,wz  ) SE | (wx,wz  ) SW
        //
        // Preference order is that table read left-to-right, top-to-bottom, so the canonical
        // (wx-1,wz-1) tile still wins a node where several carry a value - a strict generalisation
        // of 1i4 rather than a redefinition, and a deterministic tie-break.
        var foreignMods = new Dictionary<TerrainChunkCoord, Dictionary<int, ChunkTileMod>>(4);
        for (int fz = 0; fz < gridSize; fz++)
        {
            for (int fx = 0; fx < gridSize; fx++)
            {
                // A non-NaN corner was written by this chunk's own tile, which is the local source
                // of truth and wins; never overwrite it with a neighbour's copy of the same edit.
                if (!float.IsNaN(corners[fx, fz]))
                    continue;
                // Interior node: it has exactly one owner and it is this chunk, so there is nothing
                // to ask. Only a boundary node can be shared, and that is all four edges.
                if (fx != 0 && fx != gridSize - 1 && fz != 0 && fz != gridSize - 1)
                    continue;
                int wx = tc.X * cs + fx;
                int wz = tc.Z * cs + fz;
                if (TryResolveSeamCorner(wx, wz, tc, cs, mods, foreignMods, seed,
                        out float seamH))
                    corners[fx, fz] = seamH;
                // Still NaN: no chunk holds a sane value, so the regeneration loop below rolls it
                // from noise exactly as before.
            }
        }

        for (int gz = 0; gz < gridSize; gz++)
        {
            for (int gx = 0; gx < gridSize; gx++)
            {
                bool missing = mods == null || float.IsNaN(corners[gx, gz]);
                if (!missing)
                    continue;
                float worldX = (tc.X * cs + gx) * ChunkData.Size;
                float worldZ = (tc.Z * cs + gz) * ChunkData.Size;
                corners[gx, gz] = TerrainNoiseGenerator.GetHeight(seed, worldX, worldZ);
            }
        }

        // Build tile mesh data (saved heights where a mod exists, corner grid otherwise).
        ChunkMeshData[] tiles = new ChunkMeshData[cs * cs];
        for (int tz = 0; tz < cs; tz++)
        {
            for (int tx = 0; tx < cs; tx++)
            {
                ChunkCoord tileCoord = new ChunkCoord(tc.X * cs + tx, tc.Z * cs + tz);
                ChunkData data = new ChunkData(tileCoord.X, tileCoord.Z, seed);
                if (mods != null && mods.TryGetValue(tz * cs + tx, out ChunkTileMod m))
                {
                    if (m.Heights != null && m.Heights.Length >= ChunkData.VertexCount)
                    {
                        // Reject any garbage slot — fall back to the (already-sanitized/
                        // regenerated) corner grid so the high value never enters the mesh.
                        data.Heights[0] = IsSaneHeight(m.Heights[0]) ? m.Heights[0] : corners[tx, tz + 1];     // NW
                        data.Heights[1] = IsSaneHeight(m.Heights[1]) ? m.Heights[1] : corners[tx + 1, tz + 1]; // NE
                        data.Heights[2] = IsSaneHeight(m.Heights[2]) ? m.Heights[2] : corners[tx + 1, tz];     // SE
                        data.Heights[3] = IsSaneHeight(m.Heights[3]) ? m.Heights[3] : corners[tx, tz];          // SW
                    }
                    data.Version = m.Version;
                    data.HasModifications = true;
                }
                else
                {
                    data.Heights[0] = corners[tx, tz + 1];     // NW
                    data.Heights[1] = corners[tx + 1, tz + 1]; // NE
                    data.Heights[2] = corners[tx + 1, tz];     // SE
                    data.Heights[3] = corners[tx, tz];          // SW
                    data.Version = 1;
                }
                // Re-smooth legacy flat-slab tiles toward their noise on load (1cj) BEFORE the
                // mesh is built, so the rendered terrain matches the heights.
                RelaxLegacySlabTile(ref data);
                tiles[tz * cs + tx] = ChunkMeshGenerator.BuildMeshData(data, TerrainNoiseGenerator.DefaultLayers, EffectiveRefineThreshold);
            }
        }

        return new TerrainChunkMeshData
        {
            Coord = tc,
            Tiles = tiles,
            Merged = ChunkMeshGenerator.BuildMergedMeshData(tiles, null, seed, lowPolyStep),
            HadLoadedMods = hadLoadedMods,
        };
    }
}
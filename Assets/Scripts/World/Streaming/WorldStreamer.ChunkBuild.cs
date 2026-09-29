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
        // Addressing (rule 8: the copy's arithmetic IS the seam contract, so it is checked with a
        // worked example at world (30,30), where four chunks meet). The tile that OWNS a world node
        // is always the tile one metre back in each axis, and always writes it as its NE slot - the
        // stamp above is corners[LocalX+1, LocalZ+1] = Heights[1]. So the owning chunk is the chunk
        // of tile (wx-1, wz-1) and the value is that chunk's tile (wx-1, wz-1)'s NE height. Worked
        // example at world (30,30), where four chunks meet: the owner is chunk (0,0), the owning
        // tile is its local (29,29) and the value is Heights[1]. The other three chunks at that node
        // adopt it from there, and chunk (0,0) already stamped it locally. On a west edge the same
        // rule gives owner (tx-1, tz) and local tile (29, gz-1) - NOT (29,29) - which is why the
        // local tile coords below are derived from the node and never assumed.
        //
        // Only the WEST and SOUTH edges need this. The east and north edges are owned by this
        // chunk's own tile (29,_) or (_ ,29), and a node with fx>0 and fz>0 is interior and can
        // never be foreign - so the foreign lookup runs on at most ~61 boundary nodes, touching
        // at most four neighbouring chunks.
        var foreignMods = new Dictionary<TerrainChunkCoord, Dictionary<int, ChunkTileMod>>(4);
        for (int fz = 0; fz < gridSize; fz++)
        {
            for (int fx = 0; fx < gridSize; fx++)
            {
                // A non-NaN corner was written by this chunk's own tile, which is the local source
                // of truth and wins; never overwrite it with a neighbour's copy of the same edit.
                if (!float.IsNaN(corners[fx, fz]))
                    continue;
                if (fx != 0 && fz != 0)
                    continue;
                int wx = tc.X * cs + fx;
                int wz = tc.Z * cs + fz;
                TerrainChunkCoord owner =
                    new TerrainChunkCoord(FloorDiv(wx - 1, cs), FloorDiv(wz - 1, cs));
                if (owner.X == tc.X && owner.Z == tc.Z)
                    continue;

                // The owner is read from its SAVE FILE only, never from the live _loadedChunks
                // dictionary. This method runs on a ThreadPool thread (BackgroundGenerateChunk)
                // while the main thread builds, unloads and demotes chunks, and
                // Dictionary<TKey,TValue> is not safe to read during a write - a concurrent read can
                // throw or hand back a torn entry. The whole build path is already disk-driven
                // (this chunk is built from ChunkSaveManager.TryLoadChunk above), so reading a
                // neighbour from disk is not a downgrade: it is the same source the owner chunk
                // itself was built from. The residue is an edit that is in memory and not yet
                // flushed - the seam keeps its old value until the owner's save lands, which is the
                // same bounded staleness 1i3 documents for the far shell, and it self-corrects on
                // the next load.
                int ltx = wx - 1 - owner.X * cs;
                int ltz = wz - 1 - owner.Z * cs;
                if (!foreignMods.TryGetValue(owner, out Dictionary<int, ChunkTileMod> fm))
                {
                    fm = ForeignTileMods(owner, seed);
                    foreignMods[owner] = fm;
                }
                if (fm != null
                    && fm.TryGetValue(ltz * cs + ltx, out ChunkTileMod om)
                    && om.Heights != null && om.Heights.Length >= ChunkData.VertexCount
                    && IsSaneHeight(om.Heights[1]))
                    corners[fx, fz] = om.Heights[1];
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
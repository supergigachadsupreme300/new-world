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
    /// Runs on a ThreadPool thread. Builds the chunk from disk deformation mods when they
    /// exist, otherwise from noise (a cache-miss), then merges the tile meshes into one
    /// thread-safe chunk mesh.
    /// </summary>
    private void BackgroundGenerateChunk(TerrainChunkCoord tc, long seed)
    {
        try
        {
            _readyChunks.Enqueue(BuildOrLoadChunk(tc, seed));
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"[WorldStreamer] Background chunk generation failed for {tc}: {ex.Message}");
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
    private TerrainChunkMeshData BuildOrLoadChunk(TerrainChunkCoord tc, long seed)
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
                tiles[tz * cs + tx] = ChunkMeshGenerator.BuildMeshData(data, TerrainNoiseGenerator.DefaultLayers);
            }
        }

        return new TerrainChunkMeshData
        {
            Coord = tc,
            Tiles = tiles,
            Merged = ChunkMeshGenerator.BuildMergedMeshData(tiles, null, seed),
            HadLoadedMods = hadLoadedMods,
        };
    }
}
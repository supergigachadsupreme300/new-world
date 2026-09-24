using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Voxel-terrain experiment (1et) portion of the WorldStreamer partial class: the background chunk
/// builder, the height-field adapter, the full-rebuild path with real neighbour borders, and the
/// v2 column-run persistence flush. See <see cref="VoxelChunkData"/> (column store) and
/// <see cref="VoxelMesher"/> (renderer).
///
/// Phase 1 keeps EVERY public entry point intact — <see cref="VoxelTerrainEnabled"/> only switches
/// how the chunk mesh + save files are produced. Deformation (DeformAt/FlattenAt/ApplyHeightEdits),
/// the 4-corner ChunkData API, GetDigDepth, collider-on-demand, pooled meshes, budgets, the prop
/// ring and the far shell all keep their existing contracts; voxel chunks just route rebuilds and
/// flushes through this partial instead.
/// </summary>
public partial class WorldStreamer
{
    /// <summary>
    /// Background-thread chunk builder for voxel mode (dispatch twin of BuildOrLoadChunk). Loads
    /// the v2 column-run save (migrating a legacy v1 height-field save on read), builds the 900
    /// flat-tile adapter entries for _loadedData, and renders the stepped mesh from the columns.
    /// </summary>
    private TerrainChunkMeshData BuildVoxelChunk(TerrainChunkCoord tc, long seed)
    {
        int cs = TerrainChunkCoord.ChunkSize;
        int cminX = tc.X * cs;
        int cminZ = tc.Z * cs;

        VoxelChunkData vc = ChunkSaveManager.TryLoadVoxelChunk(seed, tc, out VoxelChunkData loaded)
            ? loaded
            : VoxelChunkData.Create(tc, seed);

        // Height-field adapter (P1): each 1 m tile carries all four corner heights equal to its
        // integer column top, so tools/spells/shovel gates read the exact same 4-corner ChunkData
        // API as smooth chunks — DeformAt / ApplyHeightEdits / CurrentHeightOf / GetDigDepth work
        // unchanged. The stepped mesh (VoxelMesher) is what actually renders; these entries only
        // populate _loadedData for edits, validation and persistence.
        var tiles = new ChunkMeshData[cs * cs];
        for (int tz = 0; tz < cs; tz++)
        {
            for (int tx = 0; tx < cs; tx++)
            {
                int top = vc.ColumnTop(tx, tz);
                var coord = new ChunkCoord(cminX + tx, cminZ + tz);
                var data = new ChunkData(coord.X, coord.Z, seed);
                data.Heights[0] = top;
                data.Heights[1] = top;
                data.Heights[2] = top;
                data.Heights[3] = top;
                data.Version = 1;
                tiles[tz * cs + tx] = new ChunkMeshData { Coord = coord, Data = data };
            }
        }

        return new TerrainChunkMeshData
        {
            Coord = tc,
            Tiles = tiles,
            // No border on the initial load build: neighbour columns are either pristine (same
            // noise rounding) or get repaired the moment both chunks are in memory (the
            // HadLoadedMods reconcile full-rebuilds the loaded modified neighbour with real border).
            Merged = VoxelMesher.Build(vc),
            HadLoadedMods = vc.HasModifications,
        };
    }

    /// <summary>Full rebuild of one voxel chunk's stepped mesh from the in-memory tiles (authoritative
    /// edits) and the real neighbour border (cross-chunk seam walls). Main thread only — the voxel
    /// twin of FullRebuildChunk.</summary>
    private void FullRebuildVoxelChunk(TerrainChunkCoord tc)
    {
        if (!_loadedChunks.TryGetValue(tc, out ChunkObject obj))
            return;

        VoxelChunkData vc = VoxelChunkData.Create(tc, Seed);
        tc.GetTileRange(out int cminX, out int cminZ, out int cmaxX, out int cmaxZ);
        int cs = TerrainChunkCoord.ChunkSize;
        for (int tz = 0; tz < cs; tz++)
        {
            for (int tx = 0; tx < cs; tx++)
            {
                int top = VoxelTopFromTile(cminX + tx, cminZ + tz);
                if (top != int.MinValue)
                    vc.SetColumnTop(tx, tz, top);
            }
        }

        obj.VoxelMesh = true;
        obj.ApplyMerged(VoxelMesher.Build(vc, BuildVoxelBorderTops(tc)),
            GroundMaterial, buildCollider: obj.HasCollider);
    }

    /// <summary>Column top for a world tile from the in-memory 4-corner data: the rounded average of
    /// its sane corners (a flat column averages to its own integer top; a cratered column steps to
    /// the next band). int.MinValue when the tile isn't loaded — the caller leaves that column
    /// pristine (noise).</summary>
    private int VoxelTopFromTile(int wx, int wz)
    {
        if (!_loadedData.TryGetValue(new ChunkCoord(wx, wz), out ChunkData data))
            return int.MinValue;
        float sum = 0f;
        int count = 0;
        for (int i = 0; i < ChunkData.VertexCount; i++)
        {
            if (IsSaneHeight(data.Heights[i]))
            {
                sum += data.Heights[i];
                count++;
            }
        }
        if (count == 0)
            return int.MinValue;
        return Mathf.RoundToInt(Mathf.Clamp(sum / count,
            -ChunkMeshGenerator.MaxTerrainHeight, ChunkMeshGenerator.MaxTerrainHeight));
    }

    /// <summary>Column tops for the one-column ring OUTSIDE a chunk, from loaded tiles only.
    /// Missing neighbours are left out so the mesher falls back to the same deterministic noise
    /// rounding a pristine neighbour derives (no phantom walls on untouched seams — same policy as
    /// BuildBorderCorners).</summary>
    private Dictionary<long, int> BuildVoxelBorderTops(TerrainChunkCoord tc)
    {
        int cs = TerrainChunkCoord.ChunkSize;
        tc.GetTileRange(out int cminX, out int cminZ, out int cmaxX, out int cmaxZ);
        var border = new Dictionary<long, int>(cs * 4);
        for (int i = 0; i < cs; i++)
        {
            VoxelBorderIfLoaded(cminX + i, cminZ - 1, border);
            VoxelBorderIfLoaded(cminX + i, cmaxZ + 1, border);
            VoxelBorderIfLoaded(cminX - 1, cminZ + i, border);
            VoxelBorderIfLoaded(cmaxX + 1, cminZ + i, border);
        }
        return border;
    }

    private void VoxelBorderIfLoaded(int wx, int wz, Dictionary<long, int> border)
    {
        int top = VoxelTopFromTile(wx, wz);
        if (top != int.MinValue)
            border[((long)wx << 32) | (uint)wz] = top;
    }

    /// <summary>
    /// Persist a voxel chunk's edited columns as one v2 file (atomic tmp+swap on the background
    /// writer). The whole chunk's dirty tile tops are resolved onto the column store (sparse —
    /// pristine columns stored as nothing), so a flush always writes a complete restorable snapshot
    /// over the existing file. When every resolved column returns to its pristine top the stale file
    /// is DELETED, so restoring a carve exactly to noise leaves no ghost edits on reload.
    /// </summary>
    private void FlushVoxelChunk(TerrainChunkCoord tc)
    {
        if (!ChunkSaveManager.SynchronousWrites)
            return;
        tc.GetTileRange(out int cminX, out int cminZ, out int cmaxX, out int cmaxZ);
        int cs = TerrainChunkCoord.ChunkSize;
        VoxelChunkData vc = VoxelChunkData.Create(tc, Seed);
        for (int tz = 0; tz < cs; tz++)
        {
            for (int tx = 0; tx < cs; tx++)
            {
                var tile = new ChunkCoord(cminX + tx, cminZ + tz);
                if (!_dirtyTiles.Contains(tile))
                    continue;
                _dirtyTiles.Remove(tile);
                int top = VoxelTopFromTile(cminX + tx, cminZ + tz);
                if (top != int.MinValue)
                    vc.SetColumnTop(tx, tz, top);
            }
        }
        if (vc.HasModifications)
            ChunkSaveManager.SaveVoxelChunk(Seed, tc, vc);
        else if (System.IO.File.Exists(ChunkSaveManager.ChunkFilePath(Seed, tc)))
            ChunkSaveManager.DeleteChunk(Seed, tc);
    }
}
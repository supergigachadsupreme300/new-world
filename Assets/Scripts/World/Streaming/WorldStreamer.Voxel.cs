using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Voxel-terrain (1et; multi-run + sculpt 1eu) portion of the WorldStreamer partial class: the
/// background chunk builder, the height-field adapter, the store-backed full-rebuild path with real
/// neighbour borders, the toolbar carving API and the v3 column-run persistence flush. See
/// <see cref="VoxelChunkData"/> (column store) and <see cref="VoxelMesher"/> (renderer).
///
/// The voxel path keeps EVERY public entry point intact — <see cref="VoxelTerrainEnabled"/> only
/// switches how the chunk mesh + save files are produced. Deformation (DeformAt/FlattenAt/
/// ApplyHeightEdits), the 4-corner ChunkData API, GetDigDepth, collider-on-demand, pooled meshes,
/// budgets, the prop ring and the far shell all keep their existing contracts; voxel chunks just
/// route rebuilds and flushes through this partial instead.
///
/// Since 1eu the column store (<see cref="ChunkObject.VoxelStore"/>) is the AUTHORITATIVE edit
/// record: everything pipes through it. Surface tools move a column's top via volume ops on the
/// overburden only (<see cref="VoxelChunkData.SetSurfaceTop"/>), the new toolbar SculptVoxel*
/// volume ops carve multi-run caves/raises directly into it, rebuilds keep it as the baseline and
/// flushes serialize it — a roofed chamber survives rebuilds and reloads.
/// </summary>
public partial class WorldStreamer
{
    /// <summary>
    /// Background-thread chunk builder for voxel mode (dispatch twin of BuildOrLoadChunk). Loads
    /// the v3 multi-run column save (migrating v2 single-run / legacy v1 height-field saves on
    /// read), builds the 900 flat-tile adapter entries for _loadedData, renders the stepped mesh
    /// from the columns, and ships the store to the main thread for the ChunkObject (1eu).
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
            // The store ships to the main thread with the mesh (1eu): rebuilds/flushes reuse it.
            Voxel = vc,
        };
    }

    /// <summary>Full rebuild of one voxel chunk's stepped mesh from its LIVE column store (clear-only
    /// rebuilds keep the store as baseline, 1eu) + the real neighbour border (cross-chunk seam walls).
    /// Only the tiles that were actually deformed since load are overlaid onto the store (via
    /// SetSurfaceTop — a surface dig moves the top WITHOUT collapsing any buried cave runs), so a
    /// sculpted chamber or overhang survives any rebuild the surface tools trigger. Main thread only.</summary>
    private void FullRebuildVoxelChunk(TerrainChunkCoord tc)
    {
        if (!_loadedChunks.TryGetValue(tc, out ChunkObject obj))
            return;

        VoxelChunkData vc = obj.VoxelStore ?? VoxelChunkData.Create(tc, Seed);
        obj.VoxelStore = vc;
        tc.GetTileRange(out int cminX, out int cminZ, out int cmaxX, out int cmaxZ);
        int cs = TerrainChunkCoord.ChunkSize;
        for (int tz = 0; tz < cs; tz++)
        {
            for (int tx = 0; tx < cs; tx++)
            {
                var tile = new ChunkCoord(cminX + tx, cminZ + tz);
                if (!_dirtyTiles.Contains(tile))
                    continue;
                int top = VoxelTopFromTile(cminX + tx, cminZ + tz);
                if (top != int.MinValue)
                    vc.SetSurfaceTop(tx, tz, top);
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
    /// Persist a voxel chunk's edited column RUNS as one v3 file (atomic tmp+swap on the background
    /// writer). The base is the chunk's live store (<see cref="ChunkObject.VoxelStore"/>) — the
    /// authoritative record of surface tops AND sculpted caves — with any still-dirty surface tiles
    /// overlaid on top before the snapshot, so a flush always writes a complete restorable snapshot
    /// over the existing file. When the store has returned to fully pristine the stale file is
    /// DELETED, so restoring a carve exactly to noise leaves no ghost edits on reload.
    /// </summary>
    private void FlushVoxelChunk(TerrainChunkCoord tc)
    {
        if (!ChunkSaveManager.SynchronousWrites)
            return;
        if (!_loadedChunks.TryGetValue(tc, out ChunkObject obj))
            return;

        VoxelChunkData vc = obj.VoxelStore ?? VoxelChunkData.Create(tc, Seed);
        obj.VoxelStore = vc;
        tc.GetTileRange(out int cminX, out int cminZ, out int cmaxX, out int cmaxZ);
        int cs = TerrainChunkCoord.ChunkSize;
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
                    vc.SetSurfaceTop(tx, tz, top);
            }
        }
        if (vc.HasModifications)
            ChunkSaveManager.SaveVoxelChunk(Seed, tc, vc);
        else if (System.IO.File.Exists(ChunkSaveManager.ChunkFilePath(Seed, tc)))
            ChunkSaveManager.DeleteChunk(Seed, tc);
    }

    /// <summary>
    /// Dig a roofed chamber under the terrain (toolbar cave carving, 1eu): within the horizontal
    /// disc around <paramref name="center"/> each column loses the solid run
    /// [surfaceTop - roofThickness - chamberHeight, surfaceTop - roofThickness], leaving a ceiling
    /// of <paramref name="roofThickness"/> metres of earth over a void of <paramref name="chamberHeight"/>
    /// (its floor is the column's next solid run below — the column splits, creating real overhang).
    /// Affects the loaded chunks in range only (unloaded terrain has no columns to edit); the chunk
    /// meshes are rebuilt and flushed immediately so the cave is persistent. Main thread only.
    /// </summary>
    public void SculptVoxelCave(Vector3 center, float radius, float roofThickness, float chamberHeight)
    {
        SculptVoxelVolume(center, radius, false, roofThickness, chamberHeight);
    }

    /// <summary>
    /// Fill a solid step of <paramref name="height"/> metres on top of the terrain (toolbar raise,
    /// 1eu): within the horizontal disc around <paramref name="center"/> every column grows solid
    /// volume [surfaceTop, surfaceTop + height] (merging upward runs). Loaded chunks in range only;
    /// meshes rebuilt + flushed immediately. Main thread only.
    /// </summary>
    public void SculptVoxelRaise(Vector3 center, float radius, float height)
    {
        SculptVoxelVolume(center, radius, true, 0f, height);
    }

    /// <summary>Shared toolbar volume op (1eu). <paramref name="add"/>=false carves the top-down
    /// run (cave), true fills the step (raise). Mutates the loaded chunks' column stores directly,
    /// then full-rebuilds each affected chunk (borders from its live neighbours) and flushes it.</summary>
    private void SculptVoxelVolume(Vector3 center, float radius, bool add, float a, float b)
    {
        if (radius <= 0f)
            return;
        int cs = TerrainChunkCoord.ChunkSize;
        int minWX = Mathf.FloorToInt(center.x - radius);
        int maxWX = Mathf.FloorToInt(center.x + radius);
        int minWZ = Mathf.FloorToInt(center.z - radius);
        int maxWZ = Mathf.FloorToInt(center.z + radius);
        float r2 = radius * radius;
        var touched = new HashSet<TerrainChunkCoord>();
        for (int wz = minWZ; wz <= maxWZ; wz++)
        {
            for (int wx = minWX; wx <= maxWX; wx++)
            {
                float dx = wx + 0.5f - center.x;
                float dz = wz + 0.5f - center.z;
                if (dx * dx + dz * dz > r2)
                    continue;
                var tile = new ChunkCoord(wx, wz);
                TerrainChunkCoord tc = TerrainChunkCoord.FromTile(tile);
                if (!_loadedChunks.TryGetValue(tc, out ChunkObject obj))
                    continue; // unloaded chunk — no column store to edit (regenerates pristine later)
                VoxelChunkData vc = obj.VoxelStore ?? VoxelChunkData.Create(tc, Seed);
                obj.VoxelStore = vc;
                int lx = wx - tc.X * cs;
                int lz = wz - tc.Z * cs;
                int top = vc.ColumnTop(lx, lz);
                if (add)
                    vc.AddSolid(lx, lz, top, top + Mathf.RoundToInt(b));
                else
                {
                    int roof = Mathf.RoundToInt(a);
                    int hgt = Mathf.RoundToInt(b);
                    vc.RemoveSolid(lx, lz, top - roof - hgt, top - roof);
                }
                touched.Add(tc);
            }
        }
        foreach (TerrainChunkCoord tc in touched)
        {
            if (VoxelTerrainEnabled && _loadedChunks.TryGetValue(tc, out ChunkObject obj))
            {
                obj.VoxelMesh = true;
                FullRebuildVoxelChunk(tc);
                FlushDirtyChunk(tc);
            }
        }
    }
}
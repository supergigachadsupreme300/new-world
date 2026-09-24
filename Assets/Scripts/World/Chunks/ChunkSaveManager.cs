using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using UnityEngine;

/// <summary>
/// A single locally-modified tile inside a terrain chunk (local coords 0..29).
/// Only deformed tiles are stored — pristine tiles regenerate from noise, which is how
/// revisiting a chunk stays cheap AND full (one small file per terrain chunk, not 900).
/// </summary>
public struct ChunkTileMod
{
    public int LocalX;
    public int LocalZ;
    /// <summary>4 corner heights, slot layout matches <see cref="ChunkData"/>.</summary>
    public float[] Heights;
    /// <summary>Modification version stamp (incremented per deform).</summary>
    public int Version;
}

/// <summary>Mod set for one terrain chunk, ready for serialization.</summary>
public class ChunkSaveData
{
    public readonly List<ChunkTileMod> Mods = new List<ChunkTileMod>();
}

/// <summary>
/// Persists terrain-chunk deformation as binary files under a per-seed world folder:
///     worlds/{seed}/tc_{chunkX}_{chunkZ}.dat
///
/// One file per terrain chunk (30x30 tiles) holding only the tiles the player actually
/// deformed. First load of a pristine chunk hits the noise generator and writes nothing;
/// revisiting a deformed area reads a single small file instead of regenerating those
/// tiles from noise (and instead of the old per-tile read/write burst of 250+ tiny files).
///
/// File layout (little-endian):
///   Header:  "NWTC"  (4 bytes)
///   Version: int
///   Seed:    long
///   ChunkX:  int
///   ChunkZ:  int
///   TileCount: int
///   Per tile:  LocalX:int, LocalZ:int, Version:int, Heights:4 x float
/// </summary>
public static class ChunkSaveManager
{
    private const int CurrentVersion = 1;
    /// <summary>Version of the column-run (voxel) chunk format (1et). A v2 file is never written
    /// by the smooth path and a v1 file is never written by the voxel path; the voxel reader
    /// accepts both (migrating v1 height-field mods to columns on read), the smooth reader still
    /// rejects v2 (see TryLoadChunk's version guard).</summary>
    private const int VoxelSaveVersion = 2;
    private static readonly byte[] Magic = { (byte)'N', (byte)'W', (byte)'T', (byte)'C' };

    /// <summary>Chunk save-path cache. <see cref="Application.persistentDataPath"/> is
    /// main-thread-only in modern Unity, but chunk generation resolves the file path on the
    /// background threads — so the path is captured once on the main thread (see <see cref="Warmup"/>)
    /// and background threads only read this cached string.</summary>
    private static string _cachedBaseDir;

    /// <summary>Root base directory for all saved worlds (persistent storage).</summary>
    public static string BaseDir
    {
        get
        {
            if (_cachedBaseDir == null)
                _cachedBaseDir = Path.Combine(Application.persistentDataPath, ChunkKey.WorldsRoot);
            return _cachedBaseDir;
        }
    }

    /// <summary>
    /// Populate the <see cref="BaseDir"/> cache on the main thread. MUST be called before any
    /// background-thread chunk generation reads a file (WorldStreamer does this in Awake); worker
    /// threads never touch <see cref="Application"/> APIs.
    /// </summary>
    public static void Warmup()
    {
        _ = BaseDir;
    }

    /// <summary>
    /// If true, deformation flushes dirty tiles by queueing a save at unload time (batched per
    /// terrain chunk, so a cast schedules 1-2 files, not 250+), which is the safe default. When
    /// false the snapshot is skipped entirely and the dirty marks ride until the world closes
    /// (WorldStreamer.OnDestroy) — either way, since 1es, the actual file write happens on a
    /// background worker and never blocks the main thread.
    /// </summary>
    public static bool SynchronousWrites = true;

    /// <summary>Full path of a terrain chunk's save file.</summary>
    public static string ChunkFilePath(long seed, TerrainChunkCoord tc)
    {
        return Path.Combine(BaseDir, seed.ToString(), $"tc_{tc.X}_{tc.Z}.dat");
    }

    /// <summary>Try to load a terrain chunk's deformation mods from disk. True on success (file exists + valid).</summary>
    public static bool TryLoadChunk(long seed, TerrainChunkCoord tc, out ChunkSaveData data)
    {
        data = new ChunkSaveData();
        string path = ChunkFilePath(seed, tc);
        if (!File.Exists(path))
            return false;

        try
        {
            using (FileStream fs = new FileStream(path, FileMode.Open, FileAccess.Read))
            using (BinaryReader reader = new BinaryReader(fs))
            {
                byte[] magic = reader.ReadBytes(4);
                if (magic[0] != Magic[0] || magic[1] != Magic[1] || magic[2] != Magic[2] || magic[3] != Magic[3])
                    return false;

                int version = reader.ReadInt32();
                if (version > CurrentVersion)
                    return false;

                long fileSeed = reader.ReadInt64();
                int fileX = reader.ReadInt32();
                int fileZ = reader.ReadInt32();
                if (fileSeed != seed || fileX != tc.X || fileZ != tc.Z)
                    return false;

                int count = reader.ReadInt32();
                count = Mathf.Clamp(count, 0, TerrainChunkCoord.ChunkArea);
                data.Mods.Capacity = count;
                for (int i = 0; i < count; i++)
                {
                    var mod = new ChunkTileMod
                    {
                        LocalX = reader.ReadInt32(),
                        LocalZ = reader.ReadInt32(),
                        Version = reader.ReadInt32(),
                        Heights = new float[ChunkData.VertexCount]
                    };
                    for (int h = 0; h < ChunkData.VertexCount; h++)
                        mod.Heights[h] = reader.ReadSingle();
                    data.Mods.Add(mod);
                }
                return true;
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[ChunkSaveManager] Failed to load chunk {tc.X},{tc.Z}: {e.Message}");
            data = new ChunkSaveData();
            return false;
        }
    }

    /// <summary>
    /// Try to load a voxel chunk's column-run data (1et). Accepts the v2 column format directly and
    /// a legacy v1 height-field file (converted to column tops on read — the v1 file is left in
    /// place until a voxel edit rewrites it as v2, so the smooth path keeps its own format).
    /// Returns false on missing/corrupt/foreign files so the caller regenerates pristine from noise.
    /// </summary>
    public static bool TryLoadVoxelChunk(long seed, TerrainChunkCoord tc, out VoxelChunkData vc)
    {
        vc = null;
        string path = ChunkFilePath(seed, tc);
        if (!File.Exists(path))
            return false;

        int version;
        try
        {
            using (FileStream fs = new FileStream(path, FileMode.Open, FileAccess.Read))
            using (BinaryReader reader = new BinaryReader(fs))
            {
                byte[] magic = reader.ReadBytes(4);
                if (magic[0] != Magic[0] || magic[1] != Magic[1] || magic[2] != Magic[2] || magic[3] != Magic[3])
                    return false;

                version = reader.ReadInt32();
                if (version != VoxelSaveVersion && version != CurrentVersion)
                    return false;

                long fileSeed = reader.ReadInt64();
                int fileX = reader.ReadInt32();
                int fileZ = reader.ReadInt32();
                if (fileSeed != seed || fileX != tc.X || fileZ != tc.Z)
                    return false;

                if (version == VoxelSaveVersion)
                {
                    vc = VoxelChunkData.Create(tc, seed);
                    int count = reader.ReadInt32();
                    count = Mathf.Clamp(count, 0, TerrainChunkCoord.ChunkArea);
                    int cs = TerrainChunkCoord.ChunkSize;
                    for (int i = 0; i < count; i++)
                    {
                        int idx = reader.ReadInt32();
                        int top = reader.ReadInt32();
                        int lx = idx % cs;
                        int lz = idx / cs;
                        if (lx >= 0 && lx < cs && lz >= 0 && lz < cs)
                            vc.SetColumnTop(lx, lz, top);
                    }
                    return true;
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[ChunkSaveManager] Failed to load voxel chunk {tc.X},{tc.Z}: {e.Message}");
            vc = null;
            return false;
        }

        // Legacy v1 height-field file: convert each tile's 4-corner mod to a column top (the rounded
        // average of its sane corners) — the same reduction FullRebuildVoxelChunk applies to live
        // tiles, so an existing smooth save restores the same stepped chunk.
        if (!TryLoadChunk(seed, tc, out ChunkSaveData legacy) || legacy.Mods.Count == 0)
            return false;
        vc = VoxelChunkData.Create(tc, seed);
        for (int i = 0; i < legacy.Mods.Count; i++)
        {
            ChunkTileMod m = legacy.Mods[i];
            if (m.Heights == null || m.Heights.Length < ChunkData.VertexCount)
                continue;
            float sum = 0f;
            int count = 0;
            for (int h = 0; h < ChunkData.VertexCount; h++)
            {
                if (float.IsFinite(m.Heights[h]))
                {
                    sum += m.Heights[h];
                    count++;
                }
            }
            if (count == 0)
                continue;
            vc.SetColumnTop(m.LocalX, m.LocalZ,
                Mathf.RoundToInt(Mathf.Clamp(sum / count, VoxelChunkData.MinTop, VoxelChunkData.MaxTop)));
        }
        return true;
    }

    /// <summary>A chunk save queued for the background writer (1es; voxel payload 1et). Exactly one
    /// of <see cref="Data"/> (smooth height-field mods, v1) / <see cref="Voxel"/> (column runs, v2)
    /// is set; the voxel ownership is transferred to the worker the same way a ChunkSaveData is —
    /// never touched on the main thread after enqueue.</summary>
    private readonly struct SaveWork
    {
        public readonly long Seed;
        public readonly TerrainChunkCoord Tc;
        public readonly ChunkSaveData Data;
        public readonly VoxelChunkData Voxel;
        public SaveWork(long seed, TerrainChunkCoord tc, ChunkSaveData data, VoxelChunkData voxel = null)
        {
            Seed = seed;
            Tc = tc;
            Data = data;
            Voxel = voxel;
        }
    }

    /// <summary>Saves waiting for the background writer (1es): enqueueing never blocks the main
    /// thread on disk. <see cref="FlushPendingSaves"/> drains the queue synchronously at shutdown /
    /// before a world-reset wipe so nothing queued is lost.</summary>
    private static readonly ConcurrentQueue<SaveWork> _saveQueue = new ConcurrentQueue<SaveWork>();
    private static int _saveWorkerRunning;

    /// <summary>Serializes file writes across the background writer and a synchronous
    /// <see cref="FlushPendingSaves"/> (both may target the same chunk's tmp path).</summary>
    private static readonly object _saveWriteLock = new object();

    /// <summary>
    /// Queue a terrain chunk's deformation for a background save (1es). The snapshot (
    /// <see cref="ChunkSaveData"/>) was fully built by the caller and is no longer touched on the
    /// main thread, so the worker may serialize it freely. The actual write drains on a single
    /// ThreadPool worker; the interlocked flag + re-check pattern handles the near-empty race (an
    /// item enqueued just as the worker gives up always spawns a fresh drain).
    /// </summary>
    public static void SaveChunk(long seed, TerrainChunkCoord tc, ChunkSaveData data)
    {
        if (data == null || data.Mods.Count == 0)
            return;
        _saveQueue.Enqueue(new SaveWork(seed, tc, data));
        if (Interlocked.CompareExchange(ref _saveWorkerRunning, 1, 0) == 0)
            ThreadPool.QueueUserWorkItem(_ => DrainSaveQueue());
    }

    /// <summary>
    /// Queue a voxel chunk's edited columns for a background v2 save (1et). The column store was
    /// fully resolved by the caller and is no longer touched on the main thread, so the worker may
    /// serialize it freely. Queued exactly like a smooth save (same worker, same atomic tmp+swap).
    /// </summary>
    public static void SaveVoxelChunk(long seed, TerrainChunkCoord tc, VoxelChunkData voxel)
    {
        if (voxel == null || !voxel.HasModifications)
            return;
        _saveQueue.Enqueue(new SaveWork(seed, tc, null, voxel));
        if (Interlocked.CompareExchange(ref _saveWorkerRunning, 1, 0) == 0)
            ThreadPool.QueueUserWorkItem(_ => DrainSaveQueue());
    }

    private static void DrainSaveQueue()
    {
        try
        {
            while (true)
            {
                while (_saveQueue.TryDequeue(out SaveWork w))
                    SaveChunkNow(w.Seed, w.Tc, w.Data, w.Voxel);
                _saveWorkerRunning = 0;
                if (_saveQueue.IsEmpty)
                    return;
                if (Interlocked.CompareExchange(ref _saveWorkerRunning, 1, 0) != 0)
                    return;
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[ChunkSaveManager] Background chunk save failed: {e.Message}");
        }
        finally
        {
            _saveWorkerRunning = 0;
        }
    }

    /// <summary>
    /// Flush every queued save synchronously on the calling thread (1es). Used by
    /// WorldStreamer.OnDestroy so quitting never loses a chunk, and by ResetTerrainSaves BEFORE the
    /// save-file wipe so a queued write can't resurrect the edits being discarded.
    /// </summary>
    public static void FlushPendingSaves()
    {
        while (_saveQueue.TryDequeue(out SaveWork w))
            SaveChunkNow(w.Seed, w.Tc, w.Data, w.Voxel);
    }

    /// <summary>Serialized atomic write (tmp+swap) for one chunk. Runs on the background writer for
    /// queued saves, or on the main thread during <see cref="FlushPendingSaves"/>. Routes to the
    /// v2 column-run writer when a voxel payload is present (1et).</summary>
    private static void SaveChunkNow(long seed, TerrainChunkCoord tc, ChunkSaveData data,
        VoxelChunkData voxel = null)
    {
        if (voxel != null)
        {
            WriteVoxelChunk(seed, tc, voxel);
            return;
        }
        if (data == null || data.Mods.Count == 0)
            return;
        lock (_saveWriteLock)
        {
            string dir = Path.Combine(BaseDir, seed.ToString());
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            string path = ChunkFilePath(seed, tc);
            string tmp = path + ".tmp";

            try
            {
                using (FileStream fs = new FileStream(tmp, FileMode.Create, FileAccess.Write))
                using (BinaryWriter writer = new BinaryWriter(fs))
                {
                    writer.Write(Magic);
                    writer.Write(CurrentVersion);
                    writer.Write(seed);
                    writer.Write(tc.X);
                    writer.Write(tc.Z);
                    writer.Write(data.Mods.Count);
                    for (int i = 0; i < data.Mods.Count; i++)
                    {
                        ChunkTileMod mod = data.Mods[i];
                        writer.Write(mod.LocalX);
                        writer.Write(mod.LocalZ);
                        writer.Write(mod.Version);
                        for (int h = 0; h < ChunkData.VertexCount; h++)
                            writer.Write(mod.Heights[h]);
                    }
                }

                if (File.Exists(path))
                    File.Delete(path);
                File.Move(tmp, path);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[ChunkSaveManager] Failed to save chunk {tc.X},{tc.Z}: {e.Message}");
            }
        }
    }

    /// <summary>
    /// Serialized atomic v2 column-run write (tmp+swap), same worker + lock as v1 writes (1et).
    /// File layout (little-endian, extends the v1 header):
    ///   Header:  "NWTC"            (4 bytes)
    ///   Version: int               (2 = column runs)
    ///   Seed:    long
    ///   ChunkX:  int
    ///   ChunkZ:  int
    ///   ColCount:int
    ///   Per column: Index:int (localZ*30+localX), Top:int   (single run [ColumnBaseY..Top])
    /// </summary>
    private static void WriteVoxelChunk(long seed, TerrainChunkCoord tc, VoxelChunkData voxel)
    {
        lock (_saveWriteLock)
        {
            string dir = Path.Combine(BaseDir, seed.ToString());
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            string path = ChunkFilePath(seed, tc);
            string tmp = path + ".tmp";

            try
            {
                var columns = new List<KeyValuePair<int, int>>();
                voxel.ForEachColumnTop((int lx, int lz, int top) =>
                    columns.Add(new KeyValuePair<int, int>(
                        lz * TerrainChunkCoord.ChunkSize + lx, top)));

                using (FileStream fs = new FileStream(tmp, FileMode.Create, FileAccess.Write))
                using (BinaryWriter writer = new BinaryWriter(fs))
                {
                    writer.Write(Magic);
                    writer.Write(VoxelSaveVersion);
                    writer.Write(seed);
                    writer.Write(tc.X);
                    writer.Write(tc.Z);
                    writer.Write(columns.Count);
                    for (int i = 0; i < columns.Count; i++)
                    {
                        writer.Write(columns[i].Key);
                        writer.Write(columns[i].Value);
                    }
                }

                if (File.Exists(path))
                    File.Delete(path);
                File.Move(tmp, path);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[ChunkSaveManager] Failed to save voxel chunk {tc.X},{tc.Z}: {e.Message}");
            }
        }
    }

    /// <summary>Remove a terrain chunk's save file (e.g. on world reset).</summary>
    public static void DeleteChunk(long seed, TerrainChunkCoord tc)
    {
        string path = ChunkFilePath(seed, tc);
        if (File.Exists(path))
            File.Delete(path);
    }

    /// <summary>
    /// Delete every terrain-chunk save file for a world: a permanent, deliberate map reset. The
    /// next load of any chunk regenerates pristine from noise. Idempotent — missing files/slots
    /// are skipped.
    /// </summary>
    public static void ResetWorldSaves(long seed)
    {
        try
        {
            string dir = Path.Combine(BaseDir, seed.ToString());
            if (!Directory.Exists(dir))
                return;
            foreach (string path in Directory.GetFiles(dir, "tc_*.dat"))
                File.Delete(path);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[ChunkSaveManager] Failed to reset world saves for seed {seed}: {e.Message}");
        }
    }
}
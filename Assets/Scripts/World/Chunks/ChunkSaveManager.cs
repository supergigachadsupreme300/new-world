using System;
using System.Collections.Generic;
using System.IO;
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
    private static readonly byte[] Magic = { (byte)'N', (byte)'W', (byte)'T', (byte)'C' };

    /// <summary>
    /// If true, deformation flushes dirty tiles to disk immediately (batched per terrain chunk,
    /// so a cast writes 1-2 files, not 250+), which is the safe default. When false the write is
    /// deferred until the chunk unloads or the world closes.
    /// </summary>
    public static bool SynchronousWrites = true;

    /// <summary>Root base directory for all saved worlds (persistent storage).</summary>
    public static string BaseDir
    {
        get { return Path.Combine(Application.persistentDataPath, ChunkKey.WorldsRoot); }
    }

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

    /// <summary>Write a terrain chunk's deformation mods to disk (atomic tmp+swap).</summary>
    public static void SaveChunk(long seed, TerrainChunkCoord tc, ChunkSaveData data)
    {
        if (data == null || data.Mods.Count == 0)
            return;

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

    /// <summary>Remove a terrain chunk's save file (e.g. on world reset).</summary>
    public static void DeleteChunk(long seed, TerrainChunkCoord tc)
    {
        string path = ChunkFilePath(seed, tc);
        if (File.Exists(path))
            File.Delete(path);
    }
}
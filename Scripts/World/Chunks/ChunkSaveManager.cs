using System;
using System.IO;
using UnityEngine;

/// <summary>
/// Persists individual chunks as binary files under a per-seed world folder:
///     worlds/{seed}/chunk_{x}_{z}.dat
///
/// First load of a chunk hits the noise generator and is then written to disk.
/// Subsequent loads read a single small binary file — this is what makes
/// revisiting a loaded area fast.
///
/// File layout (little-endian):
///   Header:  "NWCH"  (4 bytes)
///   Version: int
///   Seed:    long
///   ChunkX:  int
///   ChunkZ:  int
///   Version stamp: int
///   HasMods: byte
///   Heights: 5 x float
/// </summary>
public static class ChunkSaveManager
{
    private const int CurrentVersion = 1;
    private static readonly byte[] Magic = { (byte)'N', (byte)'W', (byte)'C', (byte)'H' };

    /// <summary>If true, writes are done synchronously (safer for tests/debug).</summary>
    public static bool SynchronousWrites = true;

    /// <summary>Root base directory for all saved worlds (persistent storage).</summary>
    public static string BaseDir
    {
        get { return System.IO.Path.Combine(Application.persistentDataPath, ChunkKey.WorldsRoot); }
    }

    /// <summary>Try to load a chunk from disk. Returns true on success.</summary>
    public static bool TryLoad(long seed, int x, int z, out ChunkData data)
    {
        data = default;
        string path = System.IO.Path.Combine(BaseDir, seed.ToString(), ChunkKey.FileName(x, z) + "." + ChunkKey.FileExtension);

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
                int stamp = reader.ReadInt32();
                bool hasMods = reader.ReadBoolean();

                var cd = new ChunkData(fileX, fileZ, fileSeed);
                cd.Version = stamp;
                cd.HasModifications = hasMods;
                for (int i = 0; i < ChunkData.VertexCount; i++)
                    cd.Heights[i] = reader.ReadSingle();

                data = cd;
                return true;
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[ChunkSaveManager] Failed to load chunk {x},{z}: {e.Message}");
            return false;
        }
    }

    /// <summary>Write a chunk to disk.</summary>
    public static void Save(long seed, int x, int z, ChunkData data)
    {
        string dir = Path.Combine(BaseDir, seed.ToString());
        if (!Directory.Exists(dir))
            Directory.CreateDirectory(dir);

        string path = Path.Combine(dir, ChunkKey.FileName(x, z) + "." + ChunkKey.FileExtension);
        string tmp = path + ".tmp";

        try
        {
            using (FileStream fs = new FileStream(tmp, FileMode.Create, FileAccess.Write))
            using (BinaryWriter writer = new BinaryWriter(fs))
            {
                writer.Write(Magic);
                writer.Write(CurrentVersion);
                writer.Write(seed);
                writer.Write(x);
                writer.Write(z);
                writer.Write(data.Version);
                writer.Write(data.HasModifications);
                for (int i = 0; i < ChunkData.VertexCount; i++)
                    writer.Write(data.Heights[i]);
            }

            // Atomic-ish replace: write to .tmp then swap.
            if (File.Exists(path))
                File.Delete(path);
            File.Move(tmp, path);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[ChunkSaveManager] Failed to save chunk {x},{z}: {e.Message}");
        }
    }

    /// <summary>Remove a chunk file (e.g. on world reset).</summary>
    public static void Delete(long seed, int x, int z)
    {
        string path = Path.Combine(Path.Combine(BaseDir, seed.ToString()), ChunkKey.FileName(x, z) + "." + ChunkKey.FileExtension);
        if (File.Exists(path))
            File.Delete(path);
    }
}

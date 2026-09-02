using System;

/// <summary>
/// File-naming / world-folder utilities for the chunk persistence layer.
/// Each chunk is stored individually so streaming can read a single file
/// without scanning the whole world.
/// </summary>
public static class ChunkKey
{
    public const string FileExtension = "dat";
    private const string WorldFolderPrefix = "worlds";

    /// <summary>Base folder holding all worlds for this game install.</summary>
    public static string WorldsRoot => WorldFolderPrefix;

    /// <summary>Folder for a specific world seed.</summary>
    public static string WorldFolder(long seed)
    {
        return $"{WorldsRoot}/{seed}";
    }

    /// <summary>Full relative path of a chunk file (without extension).</summary>
    public static string ChunkPath(long seed, int x, int z)
    {
        return $"{WorldFolder(seed)}/chunk_{x}_{z}";
    }

    /// <summary>File name only (no folder), e.g. chunk_-3_7.</summary>
    public static string FileName(int x, int z)
    {
        return $"chunk_{x}_{z}";
    }
}

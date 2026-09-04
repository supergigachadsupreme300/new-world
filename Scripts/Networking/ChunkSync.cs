using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Chunk synchronization (planning Task 7.1 "server generates → clients receive heights").
/// The server owns a chunk source (a deterministic generator or a height provider) and
/// broadcasts <see cref="ChunkData"/> batches to clients on the <c>"chunk"</c> channel.
/// Clients unpack the same packing to reconstruct the height patches. This is data-only and
/// transport/index-agnostic — it rides the <see cref="GameServer"/> broadcast path.
/// </summary>
public static class ChunkSync
{
    public const string Channel = "chunk";
    public const int MaxChunksPerMessage = 32;

    /// <summary>
    /// Provide deterministic heights for one chunk (server side). Returned array must have
    /// <see cref="ChunkData.VertexCount"/> entries.
    /// </summary>
    public static float[] GenerateHeights(int chunkX, int chunkZ, long seed)
    {
        var heights = new float[ChunkData.VertexCount];
        var prng = new System.Random(MixedSeed(chunkX, chunkZ, seed));
        for (int i = 0; i < heights.Length; i++)
            heights[i] = 0.2f + (float)(prng.NextDouble() * 0.3f);
        return heights;
    }

    private static int MixedSeed(int x, int z, long seed)
    {
        unchecked
        {
            long h = seed;
            h = h * 31 + x;
            h = h * 31 + z;
            return (int)(h ^ (h >> 16));
        }
    }

    /// <summary>Serialize an ordered list of chunks into one message payload on "chunk".</summary>
    public static NetMessage Pack(List<ChunkData> chunks)
    {
        var w = new NetWriter();
        w.W(chunks.Count);
        foreach (var c in chunks)
        {
            w.W(c.ChunkX);
            w.W(c.ChunkZ);
            w.W((int)c.Seed);
            for (int i = 0; i < ChunkData.VertexCount; i++)
                w.W(c.Heights != null && i < c.Heights.Length ? c.Heights[i] : 0f);
        }
        return new NetMessage(NetOp.ChunkData, 0, Channel, w.ToArray());
    }

    /// <summary>Unpack a received "chunk" message into reconstructed chunk data.</summary>
    public static List<ChunkData> Unpack(NetMessage msg)
    {
        var list = new List<ChunkData>();
        if (msg == null || msg.Data == null || msg.Data.Length == 0) return list;
        try
        {
            var r = new NetReader(msg.Data);
            int count = r.ReadInt();
            for (int i = 0; i < count; i++)
            {
                int x = r.ReadInt();
                int z = r.ReadInt();
                int seed = r.ReadInt();
                var c = new ChunkData(x, z, seed);
                for (int v = 0; v < ChunkData.VertexCount; v++)
                    c.Heights[v] = r.ReadFloat();
                list.Add(c);
            }
        }
        catch (Exception)
        {
            // malformed batch: return what parsed so far
        }
        return list;
    }

    /// <summary>
    /// Server helper: build a region of chunks around a center and broadcast them to all
    /// clients. Returns the number of chunks sent.
    /// </summary>
    public static int SendRegion(GameServer server, int centerX, int centerZ, long worldSeed, int radius)
    {
        if (server == null || !server.IsRunning) return 0;
        var batch = new List<ChunkData>();
        for (int z = -radius; z <= radius && batch.Count < MaxChunksPerMessage; z++)
            for (int x = -radius; x <= radius && batch.Count < MaxChunksPerMessage; x++)
            {
                var c = new ChunkData(centerX + x, centerZ + z, worldSeed);
                c.Heights = GenerateHeights(centerX + x, centerZ + z, worldSeed);
                batch.Add(c);
            }
        if (batch.Count == 0) return 0;
        server.Broadcast(Pack(batch));
        return batch.Count;
    }
}
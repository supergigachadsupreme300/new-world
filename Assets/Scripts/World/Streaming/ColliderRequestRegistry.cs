using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Chunks that must keep a MeshCollider while some live magic is near them (1dq): spell
/// projectiles register the chunk they currently fly over, so a long-range bolt still stops on
/// the far terrain it reaches, while every other far chunk renders collider-free. The streamer's
/// per-poll ReconcileColliders expands each request by one chunk and keeps those colliders alive
/// for the request's lifetime. Main thread only.
/// </summary>
public static class ColliderRequestRegistry
{
    private static readonly HashSet<TerrainChunkCoord> Requests = new HashSet<TerrainChunkCoord>();
    private static int _version;

    public static int Count => Requests.Count;

    /// <summary>Monotonic edit counter — the streamer polls it to skip the collider walk when idle (1ea).</summary>
    public static int Version => _version;

    /// <summary>Mark a chunk as collider-required (idempotent).</summary>
    public static void Request(TerrainChunkCoord tc)
    {
        if (Requests.Add(tc))
            _version++;
    }

    /// <summary>Drop a previously requested chunk.</summary>
    public static void Release(TerrainChunkCoord tc)
    {
        if (Requests.Remove(tc))
            _version++;
    }

    /// <summary>
    /// True when any requested chunk lies within the Chebyshev <paramref name="radius"/> of
    /// <paramref name="tc"/> (radius 0 = exact chunk only).
    /// </summary>
    public static bool HasNear(TerrainChunkCoord tc, int radius)
    {
        if (Requests.Count == 0)
            return false;
        foreach (TerrainChunkCoord r in Requests)
        {
            if (Mathf.Abs(r.X - tc.X) <= radius && Mathf.Abs(r.Z - tc.Z) <= radius)
                return true;
        }
        return false;
    }
}
using UnityEngine;

/// <summary>
/// Entry point for the Earth school's terrain reshaping (§3.8). Resolves the active
/// <see cref="WorldStreamer"/> and hands shape requests to it; the streamer owns the
/// tile-height edits + chunk mesh/collider rebuild (it has private state access).
/// </summary>
public static class TerrainDeformer
{
    /// <summary>Raise terrain according to the spell's shape at a world-space ground point.</summary>
    public static void Apply(Vector3 center, float radius, TerrainShape shape)
    {
        if (shape == TerrainShape.None || radius <= 0f) return;
        var streamer = Object.FindAnyObjectByType<WorldStreamer>();
        if (streamer == null) return;
        streamer.DeformAt(center, radius, shape);
    }
}
using UnityEngine;

/// <summary>
/// Entry point for the Earth school's terrain reshaping (§3.8). Resolves the active
/// <see cref="WorldStreamer"/> and hands shape requests to it; the streamer owns the
/// tile-height edits + chunk mesh/collider rebuild (it has private state access).
/// </summary>
public static class TerrainDeformer
{
    /// <summary>Raise/lower the terrain according to the spell's shape at a world-space ground point.
    /// <paramref name="dir"/> orients directional shapes (the Wall ridge follows the cast axis).</summary>
    public static void Apply(Vector3 center, float radius, TerrainShape shape, Vector3 dir = default)
    {
        if (shape == TerrainShape.None || radius <= 0f) return;
        var streamer = Object.FindAnyObjectByType<WorldStreamer>();
        if (streamer == null) return;
        streamer.DeformAt(center, radius, shape, dir);
    }

    /// <summary>
    /// Excavate a small crater pit at a ground point — the tools' digging path (shovel/pickaxe).
    /// Shares the Earth-magic crater shape, so tools and spells carve the same smooth bowls and
    /// each call ratchets the floor a CraterStep deeper (dirt, then stone).
    /// </summary>
    public static void Dig(Vector3 center, float radius)
    {
        Apply(center, radius, TerrainShape.Crater, default);
    }

    /// <summary>
    /// Current dig depth below the pristine surface at a ground point (positive = dug down,
    /// ~0 = untouched grass, negative = raised). Tools gate the dirt/stone boundary on this.
    /// </summary>
    public static float DigDepthAt(Vector3 point)
    {
        var streamer = Object.FindAnyObjectByType<WorldStreamer>();
        if (streamer == null) return 0f;
        return streamer.GetDigDepth(point.x, point.z);
    }

    /// <summary>
    /// Resolves the ground point a caster is aiming at, skipping already-RAISED terrain (a Wall /
    /// Ring / Pillar the Earth spells themselves reared) so a repeat cast targets the ground the
    /// player is looking at instead of the wall face now in front of them. Normal ground and
    /// craters are never skipped; non-terrain colliders (creatures, buildings, props) stop the probe
    /// as usual. Falls back to the ground under the aim point at max range.
    /// </summary>
    public static Vector3 ResolveGroundTarget(Vector3 origin, Vector3 fwd, float range)
    {
        var streamer = Object.FindAnyObjectByType<WorldStreamer>();
        long seed = streamer != null ? streamer.Seed : 0L;
        float remaining = Mathf.Max(range, 0.1f);
        Vector3 probe = origin;

        for (int skip = 0; skip < 4; skip++)
        {
            if (!Physics.Raycast(probe, fwd, out RaycastHit hit, remaining, ~0, QueryTriggerInteraction.Ignore))
                break;

            // Only the chunk's OWN terrain collider counts — props (trees/rocks) are children of the
            // chunk GameObject — and only when it sits above the pristine noise: that is a raised
            // deform, not ordinary ground.
            bool raisedTerrain = hit.collider != null
                && hit.collider.GetComponent<ChunkObject>() != null
                && hit.point.y > TerrainNoiseGenerator.GetHeight(seed, hit.point.x, hit.point.z) + 0.25f;
            if (!raisedTerrain)
            {
                Vector3 center = hit.point;
                if (Physics.Raycast(center + Vector3.up * 0.1f, Vector3.down, out RaycastHit groundHit,
                        30f, ~0, QueryTriggerInteraction.Ignore))
                    center = groundHit.point;
                return center;
            }

            // Step just past this wall face and keep looking; consume the travelled distance.
            remaining -= hit.distance + 0.25f;
            if (remaining <= 0f)
                break;
            probe = hit.point + fwd * 0.25f;
        }

        // Nothing but raised terrain (or nothing at all): snap to the ground under the aim point.
        Vector3 fallback = origin + fwd * Mathf.Max(range, 0.1f);
        if (Physics.Raycast(fallback + Vector3.up * 0.1f, Vector3.down, out RaycastHit lastGround,
                30f, ~0, QueryTriggerInteraction.Ignore))
            return lastGround.point;
        return fallback;
    }
}
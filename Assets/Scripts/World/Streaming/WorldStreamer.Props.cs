using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

/// <summary>
/// Prop ring + time-budgeted prop streaming portion of the WorldStreamer partial class.
/// </summary>

public partial class WorldStreamer
{

    /// <summary>
    /// Time-budgeted prop spawning: each tick, a global budget of prop tiles is consumed
    /// across the newest pending chunks. The budget is shared per tick (120 tiles) so props
    /// trail the terrain fill by only a few seconds at the larger render radius (1dg) while
    /// still never hitching a single frame (a density of ~1/200 trees + 1/200 rocks = ~150
    /// cube-heavy GameObjects per chunk at worst).
    /// </summary>
    private const int PropTilesPerTick = 120;

    /// <summary>Wall-clock ceiling for one prop-spawn tick (~a few dozen prop GameObjects), so a
    /// heavily-populated ring can never extend a frame past the budget.</summary>
    private const float PropBudgetMs = 3f;

    /// <summary>
    /// Keeps trees/rocks streamed only within <see cref="PropRingRadius"/> chunks of the focus.
    /// Chunks inside the ring get their deterministic prop stream queued (if not already); chunks
    /// outside it drop their spawned props so the distant radius-N ring never holds ~33k prop
    /// GameObjects (1di). The ring is a hard square (Chebyshev) like the chunk stream itself, so
    /// props appear/disappear on chunk boundaries — close enough to the player that pop-in reads
    /// as normal streaming, and the terrain mesh + collider are never touched. Player/target
    /// hit-ability is preserved because everything within the ring keeps its colliders, and the
    /// spawn chunk (boot ground) is always inside the ring.
    /// </summary>
    private void SyncPropRing(TerrainChunkCoord centre)
    {
        int ring = Mathf.Max(0, PropRingRadius);
        foreach (KeyValuePair<TerrainChunkCoord, ChunkObject> kv in _loadedChunks)
        {
            ChunkObject obj = kv.Value;
            if (obj == null)
                continue;
            bool inside = Mathf.Abs(kv.Key.X - centre.X) <= ring
                       && Mathf.Abs(kv.Key.Z - centre.Z) <= ring;
            if (inside)
            {
                if (!obj.PropsOn)
                    obj.BeginProps(Seed);
            }
            else if (obj.PropsOn)
            {
                obj.ReleaseProps();
            }
        }
    }

    private void StepChunkProps()
    {
        int remaining = PropTilesPerTick;
        float start = Time.realtimeSinceStartup;
        foreach (KeyValuePair<TerrainChunkCoord, ChunkObject> kv in _loadedChunks)
        {
            ChunkObject obj = kv.Value;
            if (obj == null || !obj.PropsOn || !obj.PropsPending)
                continue;
            remaining -= obj.StepProps(Mathf.Max(1, remaining));
            if (remaining <= 0)
                break;
            if ((Time.realtimeSinceStartup - start) * 1000f >= PropBudgetMs)
                break;
        }
    }
}
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

/// <summary>
/// Time-budgeted prop streaming portion of the WorldStreamer partial class.
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

    private void StepChunkProps()
    {
        int remaining = PropTilesPerTick;
        foreach (KeyValuePair<TerrainChunkCoord, ChunkObject> kv in _loadedChunks)
        {
            ChunkObject obj = kv.Value;
            if (obj == null || !obj.PropsPending)
                continue;
            remaining -= obj.StepProps(Mathf.Max(1, remaining));
            if (remaining <= 0)
                break;
        }
    }
}
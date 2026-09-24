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
    /// across the newest pending chunks. The budget is shared per tick so props trail the
    /// terrain fill by only a few seconds at the larger render radius (1dg) while still never
    /// hitching a single frame (a density of ~1/1000 trees + ~1/1000 rocks per tile since
    /// `1dm` — a fifth of the original 1/200 — = ~2 cube-heavy GameObjects per chunk on
    /// average, ~9 before). 1en: the budget was raised 120 -> 1800 tiles/tick (15x) because it
    /// was sized for the pre-1dm 1/200 odds; at 1/1000 a 900-tile chunk averaging ~1.8 props
    /// makes the tile scan itself nano-cheap (two Random.Next per tile), and 1,800 tiles only
    /// roll ~3.6 expected spawns (~1 ms of tree/rock building — safely under PropBudgetMs). The
    /// whole 441-chunk ring (0..near+1) now fills in ~11 s, matching the ~6 s terrain pace
    /// instead of taking ~2 minutes.
    /// </summary>
    private const int PropTilesPerTick = 1800;

    /// <summary>Wall-clock backstop for one prop-spawn tick (~a few dozen prop GameObjects), so a
    /// heavily-populated ring can never extend a frame past the budget. Lowered 3 -> 2 ms by 1es:
    /// the shared stream budget (SpendStreamBudget) is now the primary ceiling and props keep this
    /// per-stage backstop so a single dense poll cannot be more than the largest allowed stage.</summary>
    private const float PropBudgetMs = 2f;

    /// <summary>
    /// Keeps trees/rocks streamed for every chunk the real chunk stream holds. Since 1en the prop
    /// ring has a FLOOR of `near + 1` (chunks 0..near+1 all hold props): props exist wherever the
    /// player sees full-fidelity chunk geometry (0-300 m + the keep ring, incl. the ring-10
    /// hysteresis chunks that render real meshes) — they can never trail the chunk range again,
    /// no matter what a scene has serialized into PropRingRadius. The serialized value may still
    /// push the ring WIDER (a prop-only fringe on real chunks beyond the near stream), but never
    /// narrower; chunks outside the effective ring keep their terrain mesh + collider but drop
    /// their spawned props (ReleaseProps), so the distant radius-N ring never holds ~33k prop
    /// GameObjects. The ring is a hard square (Chebyshev) like the chunk stream itself, so props
    /// appear/disappear on chunk boundaries — pop-in reads as normal streaming. Player/target
    /// hit-ability is preserved because everything within the ring keeps its colliders (1en: the
    /// ring now reaches the mining-relevant keep ring, so a tree chopped at ring 8-10 is hit-able
    /// the same as one at ring 4), and the spawn chunk (boot ground) is always inside the ring.
    /// </summary>
    private void SyncPropRing(TerrainChunkCoord centre, int near)
    {
        int ring = Mathf.Max(PropRingRadius, near + 1);
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
        if (_streamCapped)
            return;                              // 1es: props are deferrable — skip when the shared budget is dry
        int remaining = PropTilesPerTick;
        float chunkStart = Time.realtimeSinceStartup;
        foreach (KeyValuePair<TerrainChunkCoord, ChunkObject> kv in _loadedChunks)
        {
            ChunkObject obj = kv.Value;
            if (obj == null || !obj.PropsOn || !obj.PropsPending)
                continue;
            remaining -= obj.StepProps(Mathf.Max(1, remaining));
            SpendStreamBudget((Time.realtimeSinceStartup - chunkStart) * 1000f);
            chunkStart = Time.realtimeSinceStartup;
            if (remaining <= 0 || _streamCapped)
                break;
            if ((Time.realtimeSinceStartup - chunkStart) * 1000f >= PropBudgetMs)
                break;
        }
    }
}
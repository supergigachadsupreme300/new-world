using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

/// <summary>
/// Runtime terrain deformation (Earth shapes, height helpers, debris) portion of the WorldStreamer partial class.
/// </summary>

public partial class WorldStreamer
{

    // --- Runtime terrain deformation (Earth school, §3.8) ---

    /// <summary>
    /// Reshape the loaded heightmap around a world-space center (main thread only).
    /// <para>
    /// Earth spells carry no status effect — instead they deform the ground as smooth feathered
    /// terrain edits (Ring: a raised annular wall; Spikes: scattered stone spikes; Wall: an
    /// elongated ridge rearing along <paramref name="dir"/>; Pillar: a tall column at the
    /// center; Crater: a wide shallow dish excavated downward). Heights are written as
    /// continuous per-corner elevations — never quantized blocks — so a deform blends into the
    /// untouched turf with a smoothstep rim. Raised shapes (Wall/Ring/Pillar/Spikes) are bounded
    /// AND idempotent: they raise toward a per-corner target of (original noise height + blended
    /// lift) applied with Max against the current height, so a repeat cast reproduces the same
    /// profile and can never stack higher. A Crater is deliberately the inverse — each cast/swing
    /// excavates another CraterStep below the current floor, so pits dig progressively deeper
    /// (revealing the dirt/stone strata bands) with no cap of their own: only the ±MaxTerrainHeight
    /// sanity band bounds them. Each touched tile is marked modified/dirty so it persists and syncs
    /// (deformations last forever — chunk save files, §2.6), and the affected region of each chunk is
    /// rebuilt (merged mesh + collider) in place. Unloaded tiles are ignored — spells only deform
    /// terrain the streamer has in memory.
    /// </para>
    /// </summary>
    public void DeformAt(Vector3 center, float radius, TerrainShape shape, Vector3 dir = default)
    {
        if (shape == TerrainShape.None || radius <= 0f) return;

        // Never raise the ground directly beneath the player's feet: a Wall/ring/pillar rearing
        // up under the capsule embeds it in the rebuilt chunk collider, and the next physics step
        // depenetrates it violently — reads as a teleport. Raised shapes skip tiles inside a small
        // keep-out ring around the player's feet; Crater (excavation) is unaffected.
        float keepOutR = 0.9f; // player capsule radius + margin
        bool protectCaster = shape != TerrainShape.Crater;
        Vector3? casterFeet = null;
        if (protectCaster)
        {
            var player = Object.FindAnyObjectByType<PlayerController>();
            if (player != null)
                casterFeet = player.transform.position;
        }

        float feather = 0.5f;
        float reach = radius + feather;
        int minCX = Mathf.FloorToInt(center.x - reach);
        int maxCX = Mathf.FloorToInt(center.x + reach);
        int minCZ = Mathf.FloorToInt(center.z - reach);
        int maxCZ = Mathf.FloorToInt(center.z + reach);

        // Wall orientation: the cast direction projected onto the XZ plane.
        Vector3 wallDir = new Vector3(dir.x, 0f, dir.z);
        if (wallDir.sqrMagnitude < 0.0001f)
            wallDir = Vector3.right;
        wallDir.Normalize();

        // Ring: a raised annulus with its center left level. Spikes: a smooth mound + sparse
        // deterministic peaks so the ground reads jagged but never chessboard-y. Wall: a ridge
        // band along the cast direction (tall enough to fully block the player). Pillar: a tall
        // column. Crater: a wide dish, dug down.
        float lift = shape == TerrainShape.Ring ? 0.9f
            : shape == TerrainShape.Pillar ? 1.8f
            : shape == TerrainShape.Wall ? 2.6f
            : 0.7f; // Spikes
        // Excavation step per Crater cast/swing (~1.1 m at full influence, feathered at the rim).
        // Unlike the raised shapes (which are IDEMPOTENT and capped), a crater ratchets the floor
        // DOWN by the step every cast: pits dig progressively deeper — through dirt, then stone —
        // with no floor cap of their own. The only bound is WorldStreamer's ±MaxTerrainHeight
        // sanity band (SanitizeHeight), which exists to protect the mesh/collider, not to limit
        // how deep an excavator may go.
        const float CraterStep = 1.1f;
        float ringMid = radius * 0.72f;
        float ringHalfWidth = Mathf.Max(0.6f, radius * 0.28f);
        float pillarCore = radius * 0.45f;
        float wallHalfThick = Mathf.Max(0.6f, radius * 0.25f);
        float wallHalfLen = radius;

        // New height for every world corner (integer x/z) inside the reach. Continuous values,
        // smoothstep-blended at the rim, so the deform reads as genuine terrain (not blocks).
        var newHeights = new Dictionary<long, float>();

        for (int cz = minCZ; cz <= maxCZ; cz++)
        {
            for (int cx = minCX; cx <= maxCX; cx++)
            {
                float wx = cx + 0.5f;
                float wz = cz + 0.5f;
                float dx = wx - center.x;
                float dz = wz - center.z;
                float dist = Mathf.Sqrt(dx * dx + dz * dz);

                float influence;
                if (shape == TerrainShape.Ring)
                {
                    float off = Mathf.Abs(dist - ringMid);
                    influence = off >= ringHalfWidth ? 0f : 1f - off / ringHalfWidth;
                }
                else if (shape == TerrainShape.Pillar)
                {
                    influence = dist <= pillarCore ? 1f
                        : Mathf.Clamp01(1f - (dist - pillarCore) / Mathf.Max(0.01f, radius - pillarCore));
                }
                else if (shape == TerrainShape.Wall)
                {
                    // Distance perpendicular to the cast axis (the ridge spine) + rounded length caps.
                    float along = dx * wallDir.x + dz * wallDir.z;
                    float perp = Mathf.Sqrt(Mathf.Max(0f, dx * dx + dz * dz - along * along));
                    float band = 1f - Mathf.Clamp01((perp - wallHalfThick) / Mathf.Max(0.01f, wallHalfThick));
                    float ends = 1f - Mathf.Clamp01((Mathf.Abs(along) - (wallHalfLen - wallHalfThick)) / Mathf.Max(0.01f, wallHalfThick));
                    influence = Mathf.Min(band, ends);
                }
                else if (shape == TerrainShape.Crater)
                {
                    influence = 1f - Mathf.Clamp01(dist / reach);
                }
                else // Spikes
                {
                    float fall = 1f - Mathf.Clamp01(dist / reach);
                    influence = fall * fall;
                }

                if (influence <= 0f)
                    continue;

                // Skip raising the ground inside the player's keep-out ring: this prevents a
                // Wall / Ring / Pillar from growing directly under the capsule and triggering
                // a violent depenetration "teleport" on the next physics step.
                if (protectCaster && casterFeet.HasValue)
                {
                    float pdx = wx - casterFeet.Value.x;
                    float pdz = wz - casterFeet.Value.z;
                    if (pdx * pdx + pdz * pdz <= keepOutR * keepOutR)
                        continue;
                }

                // Smooth the influence curve (smootherstep) so the deform blends out at the rim.
                float s = influence * influence * (3f - 2f * influence);
                float current = CurrentHeightOf(cx, cz);

                if (shape == TerrainShape.Crater)
                {
                    // Deliberate per-cast excavation: lower each corner by s*CraterStep below its
                    // CURRENT floor. Repeating the cast (or swinging a digging tool) deepens the pit
                    // each time — the inverse of the raised shapes' idempotency — so the player can
                    // dig indefinitely deep (revealing the dirt/stone strata bands). The rim stays
                    // feathered (s ~ 0 at influence edge) so the pit is a smooth bowl, never a cliff;
                    // corners keep their own slope, so the dish is never a flat slab floor.
                    float target = current - s * CraterStep;
                    newHeights[EncodeCorner(cx, cz)] = target;
                }
                else
                {
                    // Raise toward the per-corner ridge target (pristine noise + blended lift), then
                    // Max against the CURRENT height so the edit is IDEMPOTENT: a repeat cast at the
                    // same spot recomputes the same target and changes nothing. The old additive form
                    // (`current + s*lift` capped) kept lifting the whole influence footprint every
                    // cast — steepest near the crest, but a wide low-influence swath too — so the
                    // ground visibly rose across the chunk on the second+ cast. The per-corner target
                    // also keeps the crest a smooth rounded ridge (never a flat slab), and Max can
                    // never LOWER terrain that already sits above the target.
                    float baseY = TerrainNoiseGenerator.GetHeight(Seed, cx, cz);
                    float target = baseY + s * lift;

                    // Spikes: a deterministic few corners jump higher so the field reads jagged.
                    if (shape == TerrainShape.Spikes)
                    {
                        int raw = (cx * 73856093) ^ (cz * 19349663) ^ Seed.GetHashCode();
                        float r = (raw & 0x7fffffff) / (float)0x7fffffff;
                        if (r > 0.78f)
                            target += lift * (0.4f + r * 0.6f) * influence * influence;
                    }

                    // Preserve the absolute raise cap (pristine + lift): the target never exceeds it,
                    // so no shape — zone, storm, summon, or projectile — can stack unbounded.
                    target = Mathf.Min(target, baseY + lift);

                    newHeights[EncodeCorner(cx, cz)] = Mathf.Max(current, target);
                }
            }
        }

        if (newHeights.Count == 0)
            return;

        ApplyHeightEdits(minCX, minCZ, maxCX, maxCZ, newHeights);

        // Excavation kicks up debris that matches the stratum being dug (grass-blend/dirt-brown
        // near the surface, stone-grey once the pit reaches the stone band) — the same physical
        // cube-burst look as pickaxe rock destruction (WorldBuilder.SpawnRockDebris), short-lived
        // so repeated digs and spells don't litter. Only a Crater dent throws debris; the raised
        // shapes (Wall/Ring/Pillar/Spikes) never do.
        if (shape == TerrainShape.Crater)
            SpawnCraterDebris(center);
    }

    /// <summary>
    /// Full rebuild of one chunk's merged mesh + collider from the in-memory tile data, feeding
    /// the boundary ring (adjacent loaded chunks' corner heights) so cross-chunk seams are
    /// seamless (used when border-corner reconciles need full re-emission). Main thread only.
    /// </summary>
    private void FullRebuildChunk(TerrainChunkCoord tc)
    {
        if (!_loadedChunks.TryGetValue(tc, out ChunkObject obj))
            return;

        tc.GetTileRange(out int cminX, out int cminZ, out int cmaxX, out int cmaxZ);
        int cs = TerrainChunkCoord.ChunkSize;
        var tiles = new ChunkMeshData[cs * cs];
        bool anyMissing = false;
        for (int localZ = 0; localZ < cs; localZ++)
        {
            for (int localX = 0; localX < cs; localX++)
            {
                var tileCoord = new ChunkCoord(cminX + localX, cminZ + localZ);
                if (_loadedData.TryGetValue(tileCoord, out ChunkData tileData))
                    tiles[localZ * cs + localX] =
                        ChunkMeshGenerator.BuildMeshData(tileData, TerrainNoiseGenerator.DefaultLayers);
                else
                    anyMissing = true;
            }
        }

        MergedChunkMeshData merged;
        if (anyMissing)
        {
            // A loaded chunk with missing tile bookkeeping (mid unload/reload at the streaming
            // edge or the arena-lane rebuild race) must NOT rebuild sparse: null entries are
            // filled with noise by the merged-mesh builder, which is better than a gap, but the
            // cleanest result is a whole-chunk rebuild from saves/noise — every quad emitted.
            merged = BuildOrLoadChunk(tc, Seed).Merged;
        }
        else
        {
            merged = ChunkMeshGenerator.BuildMergedMeshData(tiles, BuildBorderCorners(tc), Seed);
        }
        // Preserve the chunk's collider-on-demand state (1dq): a far collider-less chunk that gets
        // reconciled/rebuild for a border corner stays collider-less; a live ring chunk re-cooks.
        obj.ApplyMerged(merged, GroundMaterial, buildCollider: obj.HasCollider);
    }

    /// <summary>
    /// Corner heights for the one-tile ring OUTSIDE a chunk, from loaded tiles only. Unloaded
    /// neighbour terrain is left out so the mesh builder falls back to the same deterministic
    /// world noise the pristine corner grid uses (no phantom walls on untouched seams).
    /// </summary>
    private Dictionary<long, float> BuildBorderCorners(TerrainChunkCoord tc)
    {
        int cs = TerrainChunkCoord.ChunkSize;
        tc.GetTileRange(out int cminX, out int cminZ, out int cmaxX, out int cmaxZ);

        var border = new Dictionary<long, float>(cs * 4);
        for (int gx = 0; gx <= cs; gx++)
        {
            CornerIfLoaded(cminX + gx, cminZ - 1, border);
            CornerIfLoaded(cminX + gx, cmaxZ + 1, border);
        }
        for (int gz = 0; gz <= cs; gz++)
        {
            CornerIfLoaded(cminX - 1, cminZ + gz, border);
            CornerIfLoaded(cmaxX + 1, cminZ + gz, border);
        }
        return border;
    }

    private void CornerIfLoaded(int wx, int wz, Dictionary<long, float> border)
    {
        ChunkCoord[] owners =
        {
            new ChunkCoord(wx, wz),         // SW slot of tile (wx, wz)
            new ChunkCoord(wx - 1, wz),     // SE slot of tile (wx-1, wz)
            new ChunkCoord(wx, wz - 1),     // NW slot of tile (wx, wz-1)
            new ChunkCoord(wx - 1, wz - 1), // NE slot of tile (wx-1, wz-1)
        };
        if (_loadedData.TryGetValue(owners[0], out ChunkData d0)) border[EncodeCorner(wx, wz)] = d0.Heights[3];
        else if (_loadedData.TryGetValue(owners[1], out ChunkData d1)) border[EncodeCorner(wx, wz)] = d1.Heights[2];
        else if (_loadedData.TryGetValue(owners[2], out ChunkData d2)) border[EncodeCorner(wx, wz)] = d2.Heights[0];
        else if (_loadedData.TryGetValue(owners[3], out ChunkData d3)) border[EncodeCorner(wx, wz)] = d3.Heights[1];
    }

    /// <summary>True when any tile of the chunk carries a localised player modification.</summary>
    private bool ChunkHasModifiedTiles(TerrainChunkCoord tc)
    {
        tc.GetTileRange(out int minX, out int minZ, out int maxX, out int maxZ);
        for (int x = minX; x <= maxX; x++)
        {
            for (int z = minZ; z <= maxZ; z++)
            {
                if (_loadedData.TryGetValue(new ChunkCoord(x, z), out ChunkData d) && d.HasModifications)
                    return true;
            }
        }
        return false;
    }

    /// <summary>True when any tile of the chunk is a flat-top block (held any 1cg slab).</summary>
    private bool ChunkContainsFlatTile(TerrainChunkCoord tc)
    {
        tc.GetTileRange(out int minX, out int minZ, out int maxX, out int maxZ);
        for (int x = minX; x <= maxX; x++)
        {
            for (int z = minZ; z <= maxZ; z++)
            {
                if (_loadedData.TryGetValue(new ChunkCoord(x, z), out ChunkData d)
                    && ChunkMeshGenerator.IsFlatTile(d))
                    return true;
            }
        }
        return false;
    }

    /// <summary>
    /// After a chunk loads/changes, rebuild any loaded modified orthogonal neighbour so a slab
    /// wall that straddles a chunk seam gets the true neighbour edge as its wall bottom (the
    /// neighbour owns the wall when it is the higher side). Bounded — only modified chunks.
    /// </summary>
    private void ReconcileModifiedBorders(TerrainChunkCoord tc)
    {
        TerrainChunkCoord[] neighbours =
        {
            new TerrainChunkCoord(tc.X + 1, tc.Z),
            new TerrainChunkCoord(tc.X - 1, tc.Z),
            new TerrainChunkCoord(tc.X, tc.Z + 1),
            new TerrainChunkCoord(tc.X, tc.Z - 1),
        };
        foreach (var n in neighbours)
        {
            if (!_loadedChunks.ContainsKey(n) || !ChunkHasModifiedTiles(n))
                continue;
            FullRebuildChunk(n);
        }
    }

    /// <summary>
    /// Levels a rectangular patch of the loaded heightmap to a target height, blending out over a
    /// feathered rim. Routes through the exact same tile-edit + chunk-rebuild + persistence pipeline
    /// as <see cref="DeformAt"/>, so the result is genuine generated terrain (mesh, collider, save
    /// files), not a floating overlay. Unloaded tiles are ignored, so callers must wait for the
    /// patch's chunks (see <see cref="LoadedChunks"/>) before flattening.
    /// </summary>
    public void FlattenAt(Vector3 center, float halfSize, float targetHeight, float feather = 3f)
    {
        if (halfSize <= 0f) return;

        int minCX = Mathf.FloorToInt(center.x - halfSize - feather);
        int maxCX = Mathf.FloorToInt(center.x + halfSize + feather);
        int minCZ = Mathf.FloorToInt(center.z - halfSize - feather);
        int maxCZ = Mathf.FloorToInt(center.z + halfSize + feather);

        // Pad interiors go fully level to the target; the rim blends influence 1 → 0 over `feather`
        // units (smootherstep) so the flat arena melts into the untouched surrounding terrain.
        var newHeights = new Dictionary<long, float>();
        for (int cz = minCZ; cz <= maxCZ; cz++)
        {
            for (int cx = minCX; cx <= maxCX; cx++)
            {
                float wx = cx + 0.5f;
                float wz = cz + 0.5f;
                float ix = 1f - Mathf.Clamp01((Mathf.Abs(wx - center.x) - halfSize) / Mathf.Max(0.01f, feather));
                float iz = 1f - Mathf.Clamp01((Mathf.Abs(wz - center.z) - halfSize) / Mathf.Max(0.01f, feather));
                float influence = Mathf.Min(ix, iz);
                if (influence <= 0f) continue;
                float s = influence * influence * (3f - 2f * influence);
                newHeights[EncodeCorner(cx, cz)] = Mathf.Lerp(CurrentHeightOf(cx, cz), targetHeight, s);
            }
        }

        if (newHeights.Count == 0)
            return;

        ApplyHeightEdits(minCX, minCZ, maxCX, maxCZ, newHeights);
    }

    /// <summary>
    /// Writes an edited corner set into every loaded tile it touches, marks them dirty, and rebuilds
    /// the affected chunks' meshes + colliders (and flushes their save files). Shared by shape
    /// deformation (<see cref="DeformAt"/>) and <see cref="FlattenAt"/>.
    /// Corners not in the set simply keep their current (unchanged) height, so shared edges with
    /// untouched neighbours line up perfectly.
    /// </summary>
    private void ApplyHeightEdits(int minCX, int minCZ, int maxCX, int maxCZ, Dictionary<long, float> newHeights)
    {
        var rebuiltChunks = new HashSet<TerrainChunkCoord>();
        bool changedAny = false;
        for (int cz = minCZ; cz <= maxCZ; cz++)
        {
            for (int cx = minCX; cx <= maxCX; cx++)
            {
                var tile = new ChunkCoord(cx, cz);
                if (!_loadedData.TryGetValue(tile, out ChunkData data))
                    continue;

                data.Heights[0] = CornerOrBase(cx, cz + 1, newHeights);     // NW
                data.Heights[1] = CornerOrBase(cx + 1, cz + 1, newHeights); // NE
                data.Heights[2] = CornerOrBase(cx + 1, cz, newHeights);     // SE
                data.Heights[3] = CornerOrBase(cx, cz, newHeights);         // SW
                data.HasModifications = true;
                data.Version++;
                _loadedData[tile] = data;
                MarkDirty(tile);
                rebuiltChunks.Add(TerrainChunkCoord.FromTile(tile));
                changedAny = true;
            }
        }

        if (!changedAny)
            return;

        // Rebuild only the touched sub-region of each affected chunk in place (mesh + collider)
        // and batch-persist the modified tiles (one file per chunk, not one per tile).
        foreach (var tc in rebuiltChunks)
        {
            ChunkObject obj;
            if (!_loadedChunks.TryGetValue(tc, out obj))
                continue;
            RebuildChunkRegion(tc, obj, minCX, minCZ, maxCX, maxCZ);
            FlushDirtyChunk(tc);
        }
    }

    /// <summary>
    /// Rebuilds the mesh quads spanned by the deformation over one chunk. Slab side walls change
    /// the merged mesh's vertex counts, so once a chunk holds any flat-top block tile it is FULL
    /// rebuilt (with real neighbour border heights); otherwise the fast in-place region patch
    /// (smooth blending — e.g. farm-plot flattening) is used, falling back to a full rebuild when
    /// the patch would cover nearly the whole chunk.
    /// </summary>
    private void RebuildChunkRegion(TerrainChunkCoord tc, ChunkObject obj,
        int minCX, int minCZ, int maxCX, int maxCZ)
    {
        int cs = TerrainChunkCoord.ChunkSize;
        tc.GetTileRange(out int cminX, out int cminZ, out int cmaxX, out int cmaxZ);

        // Any flat slab tile in the chunk forces a full rebuild (side walls change vertex counts).
        if (ChunkContainsFlatTile(tc))
        {
            FullRebuildChunk(tc);
            return;
        }

        int lMinX = Mathf.Clamp(minCX - cminX, 0, cs - 1);
        int lMinZ = Mathf.Clamp(minCZ - cminZ, 0, cs - 1);
        int lMaxX = Mathf.Clamp(maxCX - cminX, 0, cs - 1);
        int lMaxZ = Mathf.Clamp(maxCZ - cminZ, 0, cs - 1);
        int w = lMaxX - lMinX + 1;
        int h = lMaxZ - lMinZ + 1;

        if (w * h >= TerrainChunkCoord.ChunkArea * 0.75f)
        {
            // Region is (almost) the whole chunk — full rebuild is the same cost and safest.
            FullRebuildChunk(tc);
            return;
        }

        var region = new ChunkMeshData[w * h];
        for (int localZ = lMinZ; localZ <= lMaxZ; localZ++)
        {
            for (int localX = lMinX; localX <= lMaxX; localX++)
            {
                var tileCoord = new ChunkCoord(cminX + localX, cminZ + localZ);
                if (!_loadedData.TryGetValue(tileCoord, out ChunkData tileData))
                    tileData = ChunkMeshGenerator.BuildFallbackTileData(cminX + localX, cminZ + localZ, Seed);
                region[(localZ - lMinZ) * w + (localX - lMinX)] =
                    ChunkMeshGenerator.BuildMeshData(tileData, TerrainNoiseGenerator.DefaultLayers);
            }
        }
        obj.PatchRegion(lMinX, lMinZ, lMaxX, lMaxZ, region, Seed);
    }

    private static long EncodeCorner(int cx, int cz) => ((long)cx << 32) | (uint)cz;

    private float CornerOrBase(int cx, int cz, Dictionary<long, float> newHeights)
    {
        return newHeights.TryGetValue(EncodeCorner(cx, cz), out float h) ? h : CurrentHeightOf(cx, cz);
    }

    /// <summary>Current height of a world corner from whichever loaded tile owns it
    /// (shared corners agree, so the first loaded tile wins).</summary>
    private float CurrentHeightOf(int cx, int cz)
    {
        ChunkCoord[] owners =
        {
            new ChunkCoord(cx, cz),         // SW slot of tile (cx, cz)
            new ChunkCoord(cx - 1, cz),     // SE slot of tile (cx-1, cz)
            new ChunkCoord(cx, cz - 1),     // NW slot of tile (cx, cz-1)
            new ChunkCoord(cx - 1, cz - 1), // NE slot of tile (cx-1, cz-1)
        };
        if (_loadedData.TryGetValue(owners[0], out ChunkData d0)) return d0.Heights[3];
        if (_loadedData.TryGetValue(owners[1], out ChunkData d1)) return d1.Heights[2];
        if (_loadedData.TryGetValue(owners[2], out ChunkData d2)) return d2.Heights[0];
        if (_loadedData.TryGetValue(owners[3], out ChunkData d3)) return d3.Heights[1];
        // Corner has no loaded owner tile — neutral base (only ever read by loaded tiles).
        return TerrainNoiseGenerator.GetHeight(Seed, cx + 0.5f, cz + 0.5f);
    }

    /// <summary>
    /// Current dig depth at a world-space ground point: how far the current floor sits BELOW the
    /// pristine noise surface (positive = dug down, ~0 = untouched, negative = raised terrain).
    /// Tools use this to gate the dirt/stone boundary — e.g. the shovel stops once a pit reaches
    /// the stone band and the pickaxe takes over.
    /// </summary>
    public float GetDigDepth(float worldX, float worldZ)
    {
        int cx = Mathf.FloorToInt(worldX);
        int cz = Mathf.FloorToInt(worldZ);
        return TerrainNoiseGenerator.GetHeight(Seed, cx, cz) - CurrentHeightOf(cx, cz);
    }

    /// <summary>
    /// A small burst of excavation debris out of a fresh crater dent, tinted by the stratum the
    /// dig just reached — grass-blend/dirt-brown near the surface, stone-grey once the pit hits
    /// the stone band (the same <see cref="ChunkMeshGenerator.TerrainBandColor"/> the pit walls
    /// render). The look mirrors pickaxe rock destruction (<c>WorldBuilder.SpawnRockDebris</c>):
    /// volume-weighted cubes with an up-biased rigidbody scatter. Short-lived (2.5 s) so repeated
    /// digs and spells never accumulate litter.
    /// </summary>
    private void SpawnCraterDebris(Vector3 center)
    {
        int cx = Mathf.FloorToInt(center.x);
        int cz = Mathf.FloorToInt(center.z);
        float floorY = CurrentHeightOf(cx, cz);
        Color band = ChunkMeshGenerator.TerrainBandColor(Seed, cx, cz, floorY);
        Vector3 spawn = new Vector3(center.x, floorY + 0.08f, center.z);

        int count = Random.Range(3, 6);
        for (int i = 0; i < count; i++)
        {
            float s = Random.Range(0.08f, 0.16f);
            // Cloned from one shared cube template (1du): CreatePrimitive allocated a fresh cube
            // Mesh per piece; Instantiate(reference) reuses the template's mesh and adds only the
            // GameObject/transform weights the debris visually needs. The cube collider is dropped
            // — debris is cosmetic rigidbody scatter, no functional path reads it.
            var chunk = Instantiate(SharedDebrisCube);
            chunk.SetActive(true);
            chunk.name = "DentDebris";
            chunk.transform.position = spawn + Random.insideUnitSphere * 0.15f;
            chunk.transform.rotation = Random.rotation;
            chunk.transform.localScale = Vector3.one * s;
            var r = chunk.GetComponent<Renderer>();
            if (r != null) r.material.color = Color.Lerp(band, Color.black, Random.value * 0.5f);
            var rb = chunk.AddComponent<Rigidbody>();
            rb.mass = s * s * s * 1000f;
            rb.linearVelocity = new Vector3(
                Random.Range(-2.5f, 2.5f), Random.Range(2.5f, 5f), Random.Range(-2.5f, 2.5f));
            rb.angularVelocity = Random.insideUnitSphere * 6f;
            Destroy(chunk, 2.5f);
        }
    }

    /// <summary>One static cube GO shared by every crater-debris clone (1du). Built once, kept
    /// inactive so its own transform/renderer cost is zero, collider removed up front because the
    /// debris clones never need physics interaction beyond their explicit Rigidbody.</summary>
    private static GameObject _sharedDebrisCube;

    private static GameObject SharedDebrisCube
    {
        get
        {
            if (_sharedDebrisCube != null)
                return _sharedDebrisCube;
            _sharedDebrisCube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _sharedDebrisCube.name = "DentDebrisTemplate";
            Collider col = _sharedDebrisCube.GetComponent<Collider>();
            if (col != null)
                Destroy(col);
            _sharedDebrisCube.SetActive(false);
            return _sharedDebrisCube;
        }
    }
}
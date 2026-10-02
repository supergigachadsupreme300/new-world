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
    /// Earth spells carry no status effect — instead they deform the ground as terrain edits (Ring: a
    /// raised annular wall; Spikes: scattered stone spikes; Wall: an elongated ridge rearing along
    /// <paramref name="dir"/>; Pillar: a tall column at the center; Crater: a terraced spherical cap
    /// excavated downward). Heights are written as continuous per-corner elevations, so a deform
    /// blends into the untouched turf with a smoothstep rim; a Crater's own offset is additionally
    /// snapped to a terrace ladder (1f3) so its interior breaks into level treads instead of
    /// interpolating into one smooth funnel. Raised shapes (Wall/Ring/Pillar/Spikes) are bounded
    /// AND idempotent: they raise toward a per-corner target of (original noise height + blended
    /// lift) applied with Max against the current height, so a repeat cast reproduces the same
    /// profile and can never stack higher. A Crater is deliberately the inverse — each cast/swing
    /// excavates another CraterStep below the current floor, so pits dig progressively deeper
    /// (revealing the dirt/stone strata bands) with no cap of their own: only the ±MaxTerrainHeight
    /// sanity band bounds them. Since 1ez a Crater also raises a bounded, idempotent LIP around the
    /// dish (the positive term the monotone cone lacked — see <see cref="DeformAt"/>'s rim constants),
    /// so it reads as an impact, not a smooth funnel. Each touched tile is marked modified/dirty so it persists and syncs
    /// (deformations last forever — chunk save files, §2.6), and the affected region of each chunk is
    /// rebuilt (merged mesh + collider) in place. Unloaded tiles are ignored — spells only deform
    /// terrain the streamer has in memory.
    /// </para>
    /// </summary>
    public void DeformAt(Vector3 center, float radius, TerrainShape shape, Vector3 dir = default,
        bool emitDebris = true)
    {
        if (shape == TerrainShape.None || radius <= 0f) return;

        // Never raise the ground directly beneath the player's feet: a Wall/ring/pillar rearing
        // up under the capsule embeds it in the rebuilt chunk collider, and the next physics step
        // depenetrates it violently — reads as a teleport. Raised shapes skip tiles inside a small
        // keep-out ring around the player's feet. A Crater's DISH is unaffected (falling into a pit
        // does not depenetrate), but its 1ez rim is a raise and is guarded separately below.
        float keepOutR = 0.9f; // player capsule radius + margin
        bool protectCaster = shape != TerrainShape.Crater;
        Vector3? casterFeet = null;
        {
            // Looked up for every shape (a crater's rim now raises too), but only the raised shapes
            // skip whole tiles here; the crater raise is ringed inside its branch.
            var player = Object.FindAnyObjectByType<PlayerController>();
            if (player != null)
                casterFeet = player.transform.position;
        }

        float feather = 0.5f;
        float reach = radius + feather;

        // 1i9. The low-poly surface is emitted from every EffectiveLowPolyStep-th node of the 1 m
        // corner lattice (ChunkMeshGenerator.EmitLowPolySurface) — world nodes whose x AND z are
        // both multiples of the step. A carve whose reach happens to contain none of those nodes
        // therefore writes its entire shape into lattice that no triangle is built from, and the
        // dent is invisible no matter how deep it is. 1hx moved the step 3 -> 6, which put the
        // projectile dent's 1.9 m reach inside the 4.24 m worst-case distance to a sampled node,
        // so the universal impact dent stopped existing. Widen the WRITE bounds for craters to
        // cover the skirt that guarantees a visible mark; the authored influence curve below still
        // uses `reach` unchanged, so the authored shape and depth are untouched.
        int facetStep = shape == TerrainShape.Crater ? EffectiveLowPolyStep : 0;
        float toSampledNode = 0f;
        if (facetStep > 0)
        {
            float sx = center.x - Mathf.Round(center.x / facetStep) * facetStep;
            float sz = center.z - Mathf.Round(center.z / facetStep) * facetStep;
            toSampledNode = Mathf.Sqrt(sx * sx + sz * sz);
        }
        float loopReach = toSampledNode > reach ? toSampledNode + facetStep : reach;
        int minCX = Mathf.FloorToInt(center.x - loopReach);
        int maxCX = Mathf.FloorToInt(center.x + loopReach);
        int minCZ = Mathf.FloorToInt(center.z - loopReach);
        int maxCZ = Mathf.FloorToInt(center.z + loopReach);

        // Wall orientation: the cast direction projected onto the XZ plane. The voxel crater
        // directed-dig clip uses this directly; the Wall ridge rotates it 90° (1ga, below).
        Vector3 wallDir = new Vector3(dir.x, 0f, dir.z);
        if (wallDir.sqrMagnitude < 0.0001f)
            wallDir = Vector3.right;
        wallDir.Normalize();

        // Ring: a raised annulus with its center left level. Spikes: a smooth mound + sparse
        // deterministic peaks so the ground reads jagged but never chessboard-y. Wall: a ridge
        // band across the cast direction (1ga — perpendicular, a left-right barricade; tall
        // enough to fully block the player). Pillar: a tall column. Crater: a wide dish, dug down.
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

        // 1f3: the crater profile is a SPHERICAL CAP, not a cone, and its carve is quantised into
        // terraces. Both halves exist because of the same observation: the smooth cone read as a
        // "smoothed out blanket" (1f2's readout) — its profile is straight from the impact point to
        // the footprint edge, so every sample along it is a different height and the 1 m lattice
        // bilinear-blends the whole thing into one continuous funnel with no level breaks in it.
        //
        // CAP. `craterCapR` (below) solves for the sphere whose lower cap is
        // `craterCapDepth` deep at the centre and exactly meets grade at `reach`:
        //     capR = (reach^2 + capDepth^2) / (2 * capDepth),   capDepth = min(CraterStep, reach)
        // and the dig depth at radius d is `sqrt(capR^2 - d^2) - (capR - capDepth)`, which is
        // `capDepth` at d = 0 and 0 at d = `reach` by construction. The profile is a CURVE now, not
        // a line: steepest at the rim, flattest at the floor, which is what a sphere pressed into
        // ground does. `craterCapDepth` is clamped to `reach` because a sphere deeper than its own
        // rim radius cannot be a cap at all — its centre drops below grade, the lower hemisphere
        // stops intersecting grade anywhere, and the carve keeps a residual depth at the footprint
        // edge instead of feathering to 0. It is defensive, not a live path: `reach` is radius +
        // 0.5 m of feather, so the smallest real dig (pickaxe, radius 1.0) reaches 1.5 m against a
        // 1.1 m CraterStep and takes the unmodified cap.
        // The rim wall is steep by construction: `d(depth)/dd` at the rim is `reach / sqrt(capR^2 -
        // reach^2)` = 60 deg for a 1.9 m projectile reach, 73 deg for a 1.5 m tool dig. That is the
        // requested shape (the user does not need a walkable bowl), and it is a deliberate trade:
        // the previous cone was 34 deg, walkable, and read as smooth. A walkable crater is a
        // DIFFERENT shape, not a tuning of this one.
        //
        // TERRACES. `offset` is snapped to a multiple of `craterTerrace` before it is written, so the
        // surface breaks into level treads instead of interpolating. The snap is applied to the signed
        // OFFSET (the write itself is unchanged), so the 1ez lip's `Max` idempotency and the
        // excavation ratchet both survive untouched; the only cost is that a cast moves the floor by
        // its offset rounded to the nearest terrace, i.e. within half a step of CraterStep.
        // `craterTerrace` is a FRACTION of reach, clamped by `CraterTerraceMin`/`Max`, so a big cast
        // gets a coarse ladder and a small one a fine ladder: one ratio reads as terracing at every
        // size, and the clamp stops a tiny dig from quantising to nothing at all.
        const float CraterTerraceFraction = 0.25f;
        const float CraterTerraceMin = 0.30f;
        const float CraterTerraceMax = 0.80f;

        // 1ez: the raised crater rim. A downward-only dish is a monotone cone (the 1in report): no
        // term anywhere raises, so no resolution can produce a lip. The rim is that positive term.
        // It is a smooth bump across the OUTER band of the dish footprint (fractions of `reach`),
        // zero at BOTH ends, so it adds no step at the footprint boundary and meets the dish
        // continuously at the crossover (~0.68 x reach). A hard Max of two separate curves would
        // jump at the crossover; a signed profile (lip - depression) does not.
        //   inner 0.55 x reach  — bowl is ~0.94 m down here (1f3: was ~0.47 m under the cone)
        //   peak  0.80 x reach  — bowl is only ~0.46 m down (1f3: was ~0.11 m)
        //   outer reach         — meets untouched ground at 0
        // CraterRimLift is an ABSOLUTE lift in metres, NOT a fraction of CraterStep. The lip height
        // is `lift` minus a fraction-only depression, so it is scale-independent: a bigger crater
        // gets a WIDER rim, not a taller one. It is kept absolute and small so the net rim stays
        // under the player's stepOffset (0.5 m) at every size — the lip is a bump you walk over, not
        // a wall. 1f3 re-derived it: the cap digs DEEPER than the cone did at every radius, so the
        // old 0.55 m lift would have left the lip barely proud of grade (~0.10 m) and 1ez's rim would
        // have regressed to invisible as a side effect of the new profile. 0.90 m restores the net
        // ~0.44 m the cone produced. The inner band is deliberately outside the 0.9 m caster keep-out
        // ring (0.55 * reach >= 0.935 m for radius >= 1.2), and the raise branch re-checks the ring
        // anyway (see below).
        const float CraterRimInner = 0.55f;
        const float CraterRimPeak = 0.80f;
        const float CraterRimLift = 0.90f;

        float ringMid = radius * 0.72f;
        float ringHalfWidth = Mathf.Max(0.6f, radius * 0.28f);
        float pillarCore = radius * 0.45f;
        float wallHalfThick = Mathf.Max(0.6f, radius * 0.25f);
        float wallHalfLen = radius;

        // 1f3 cap + terrace terms. Both depend only on `reach`, so they are solved once per cast
        // rather than per node; the loop body costs one sqrt and one round. `craterCapR` is the
        // sphere radius, `craterCapCentre` the height of its centre ABOVE grade (grade - capDepth,
        // which is positive for every cap shallower than a hemisphere), and `craterTerrace` the snap
        // the signed offset is rounded to. Non-crater shapes never read them.
        float craterCapDepth = Mathf.Min(CraterStep, reach);
        float craterCapR = (reach * reach + craterCapDepth * craterCapDepth) / (2f * craterCapDepth);
        float craterCapCentre = craterCapR - craterCapDepth;
        float craterTerrace = Mathf.Clamp(reach * CraterTerraceFraction, CraterTerraceMin, CraterTerraceMax);

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
                    // Distance perpendicular to the ridge spine + rounded length caps. 1ga: the
                    // ridge runs ACROSS the cast axis (perpendicular to the projected cast dir),
                    // so the wall lies left-right in the player's view and reads as a barricade
                    // facing them instead of a thin edge-on slab receding along their sight line.
                    // `wallDir` itself is left untouched for the voxel crater clip below.
                    Vector3 ridge = new Vector3(-wallDir.z, 0f, wallDir.x);
                    float along = dx * ridge.x + dz * ridge.z;
                    float perp = Mathf.Sqrt(Mathf.Max(0f, dx * dx + dz * dz - along * along));
                    float band = 1f - Mathf.Clamp01((perp - wallHalfThick) / Mathf.Max(0.01f, wallHalfThick));
                    float ends = 1f - Mathf.Clamp01((Mathf.Abs(along) - (wallHalfLen - wallHalfThick)) / Mathf.Max(0.01f, wallHalfThick));
                    influence = Mathf.Min(band, ends);
                }
                else if (shape == TerrainShape.Crater)
                {
                    // 1f3 spherical cap. The cone this replaced was `1 - dist/reach`: a straight
                    // line, so every node in the footprint sat at its own distinct height and the
                    // lattice had no level breaks to draw. This is the lower cap of a circle of
                    // radius `craterCapR` whose centre sits `craterCapCentre` above grade: it is
                    // `craterCapDepth` below grade at dist = 0 and exactly 0 at dist = `reach`, by
                    // the identity 2*R*d = reach^2 + d^2 that `craterCapR` is built from — so the
                    // feather to untouched ground is unchanged and only the shape between moves.
                    float depthBelowGrade = Mathf.Sqrt(Mathf.Max(0f,
                        craterCapR * craterCapR - dist * dist)) - craterCapCentre;
                    influence = Mathf.Clamp01(depthBelowGrade / craterCapDepth);
                }
                else // Spikes
                {
                    float fall = 1f - Mathf.Clamp01(dist / reach);
                    influence = fall * fall;
                }

                // P2 (1eu): directed wall-carve. In voxel mode a Crater handed a real direction
                // (the tools aim the dig at the ground they hit, not straight down) is clipped to
                // the half-space AHEAD of the plane through the center perpendicular to the cast —
                // so one swing into a slope carves a niche with a near-vertical face instead of a
                // symmetric bowl that just tilts with the ground. Stepped rendering terraces the
                // one-column influence drop at the plane into a clean retaining wall.
                if (VoxelTerrainEnabled && shape == TerrainShape.Crater && (dir.x != 0f || dir.z != 0f))
                {
                    float along = dx * wallDir.x + dz * wallDir.z;
                    influence *= Mathf.Clamp01(along / Mathf.Max(0.25f, radius * 0.5f) + 0.15f);
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
                    // 1i9: guarantee the carve reaches a RENDERED node. Max'd with the authored
                    // influence, never replacing it, so this can only ADD visible geometry in the one
                    // case where the carve had none — and the deep 1.1 m core at the impact point,
                    // its radius and its per-cast ratchet are all unchanged.
                    s = Mathf.Max(s, CraterFacetSkirt(cx, cz, center, reach, facetStep, toSampledNode));
                    // Old behaviour (1ez keeps it as the bowl's negative term): lower each corner by
                    // s*CraterStep below its CURRENT floor, deepening the pit each cast (the inverse
                    // of the raised shapes' idempotency). The rim is now the missing positive term.
                    float depression = s * CraterStep;

                    // 1ez raised rim: a smooth bump across the outer band of the footprint.
                    float t = dist / reach;
                    float lipBump = 0f;
                    if (t > CraterRimInner && t < 1f)
                    {
                        float u = t < CraterRimPeak
                            ? (t - CraterRimInner) / (CraterRimPeak - CraterRimInner)
                            : (1f - t) / (1f - CraterRimPeak);
                        lipBump = u * u * (3f - 2f * u); // smoothstep, u in [0,1] by construction
                    }
                    float offset = lipBump * CraterRimLift - depression;

                    // 1f3: snap the SIGNED offset to the terrace ladder. This is the only thing
                    // between the profile above and a continuous funnel: the cap decides WHERE the
                    // break happens and the snap decides that it is a level. Applied to `offset`,
                    // never to the write, so both invariants below are untouched — the lip still
                    // Max's against `current` (repeat cast -> same offset -> same target, still
                    // idempotent) and the excavation still subtracts a non-negative amount from
                    // `current` (still an unbounded downward ratchet). The cost is stated rather
                    // than hidden: a cast now moves the floor by its offset rounded to the nearest
                    // terrace, so the per-cast depth is CraterStep within +/- half a terrace
                    // (0.30-0.80 m over the clamp range) instead of exactly CraterStep. The deep
                    // core cannot round away (CraterStep / terrace >= 1.375, so the centre always
                    // moves at least one whole terrace), which is what keeps the ratchet unbounded
                    // in practice and not merely in intent.
                    // KNOWN INTERACTION, dormant today: 1i9's `CraterFacetSkirt` returns 0 unless
                    // `EffectiveLowPolyStep > 0`, and 1ia holds that flag false, so the skirt adds
                    // nothing to `offset` now. If the facet path is ever re-enabled, a skirt whose
                    // whole influence is under half a terrace would snap back to 0 and lose its
                    // guarantee — that is the interaction to re-measure, not to pre-empt here.
                    offset = Mathf.Round(offset / craterTerrace) * craterTerrace;

                    if (offset > 0f)
                    {
                        // Raised lip: IDEMPOTENT and bounded. Target is pristine noise + offset,
                        // Max'd with the current floor so a repeat cast reproduces the same profile
                        // and a lip can never lower terrain that already stands above it.
                        // Never rear it up inside the caster keep-out ring (same teleport guard as
                        // the other raised shapes); the ring is outside the lip band regardless.
                        if (casterFeet.HasValue)
                        {
                            float pdx = wx - casterFeet.Value.x;
                            float pdz = wz - casterFeet.Value.z;
                            if (pdx * pdx + pdz * pdz <= keepOutR * keepOutR)
                                continue;
                        }
                        float baseY = TerrainNoiseGenerator.GetHeight(Seed, cx, cz);
                        newHeights[EncodeCorner(cx, cz)] = Mathf.Max(current, baseY + offset);
                    }
                    else
                    {
                        // Excavation: ratchet the current floor DOWN by |offset| (unchanged).
                        newHeights[EncodeCorner(cx, cz)] = current + offset;
                    }
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
        // shapes (Wall/Ring/Pillar/Spikes) never do. Magic projectile impacts pass emitDebris:false
        // (1gb): they carve the same dent but skip the cubes, playing their own exploding,
        // fading sphere blast instead — only tool digs / zone-strikes keep the cube burst.
        if (shape == TerrainShape.Crater && emitDebris)
            SpawnCraterDebris(center);
    }

    /// <summary>
    /// 1i9: influence a crater places on the nearest RENDERED node when its authored reach contains
    /// none, so that a carve can never be provably invisible. Sized as a fraction of the same
    /// <c>CraterStep</c> the authored dish uses, which is what it renders as: the surface
    /// interpolates linearly between sampled nodes, so dipping one of them by s*CraterStep tilts
    /// the surrounding facet into a visible dish of that depth.
    /// </summary>
    private const float CraterFacetSkirtDepth = 0.5f;

    /// <summary>
    /// 1i9: extra crater influence to write onto the nodes around the nearest sampled (rendered)
    /// lattice node, or 0 when the authored reach already contains one.
    ///
    /// The invariant that makes this safe: it returns 0 unless the carve would otherwise move no
    /// rendered geometry whatsoever, so it can only ever turn an invisible carve into a visible one.
    /// A crater that already reaches a sampled node keeps exactly its authored shape.
    ///
    /// The falloff dies one step out, squared, so the mark reads as THIS crater rather than a
    /// regional tilt, and it is combined with the authored influence by Max at the call site.
    /// </summary>
    private static float CraterFacetSkirt(int cx, int cz, Vector3 center, float reach,
        int facetStep, float toSampledNode)
    {
        if (facetStep <= 0 || toSampledNode < reach)
            return 0f;
        // The corner this writes is world node (cx, cz) — a node is a lattice point, not a tile
        // centre — and the rendered facets sit at exactly those integer multiples of facetStep
        // (EmitLowPolySurface emits gx*step). Measuring the skirt in the target's own space is what
        // makes "t == 0 at the nearest sampled node" exact rather than half a metre out.
        float dx = cx - center.x;
        float dz = cz - center.z;
        float t = Mathf.Clamp01((Mathf.Sqrt(dx * dx + dz * dz) - toSampledNode) / facetStep);
        float f = 1f - t;
        return CraterFacetSkirtDepth * f * f;
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

        // Voxel mode (1et): the stepped mesh has no TOPS-FIRST quad layout and no PatchRegion path,
        // so every rebuild is a full voxel rebuild of the column store with real neighbour border.
        if (VoxelTerrainEnabled)
        {
            FullRebuildVoxelChunk(tc);
            return;
        }

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
                        ChunkMeshGenerator.BuildMeshData(tileData, TerrainNoiseGenerator.DefaultLayers, EffectiveRefineThreshold);
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
            merged = BuildOrLoadChunk(tc, Seed, EffectiveLowPolyStep).Merged;
        }
        else
        {
            merged = ChunkMeshGenerator.BuildMergedMeshData(tiles, BuildBorderCorners(tc), Seed, EffectiveLowPolyStep);
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

    /// <summary>True when any tile of the chunk carries a localised player modification. O(1) set
    /// lookup since 1ea — the set is fed by live edits (ApplyHeightEdits) and by the background
    /// loader's HadLoadedMods flag (ReconcileNewlyLoadedChunk).</summary>
    private bool ChunkHasModifiedTiles(TerrainChunkCoord tc)
    {
        return _modifiedChunks.Contains(tc);
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
    /// Since 1gd each rebuild is ASYNC (<see cref="RequestChunkRebuild"/>) — this used to chain
    /// synchronous 900-tile FullRebuildChunk calls, up to four per reconcile, stacking exactly when
    /// the stream passed a cluster of edited chunks at speed (the "immense lag" on fast movement).
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
            RequestChunkRebuild(n);
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

        // Track the touched chunks in the O(1) modified set (1ea) so the load-reconcile paths
        // (ReconcileNewlyLoadedChunk / ReconcileModifiedBorders) never scan 900 tiles again.
        _modifiedChunks.UnionWith(rebuiltChunks);

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
    /// the patch would cover nearly the whole chunk OR (1ew) when any region tile's adaptive
    /// stretch-split state would change the merged block sizes (PatchRegion re-skims through the
    /// block table but cannot resize it).
    /// </summary>
    private void RebuildChunkRegion(TerrainChunkCoord tc, ChunkObject obj,
        int minCX, int minCZ, int maxCX, int maxCZ)
    {
        int cs = TerrainChunkCoord.ChunkSize;
        tc.GetTileRange(out int cminX, out int cminZ, out int cmaxX, out int cmaxZ);

        // Voxel mode (1et): the stepped mesh has no per-tile TOP quad layout at all (VoxelMesher
        // emits its own whole-chunk buffer), so there is nothing for the in-place PatchRegion path
        // to re-skin — any edit is a full voxel rebuild.
        if (VoxelTerrainEnabled)
        {
            FullRebuildChunk(tc);
            return;
        }

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
        bool refinednessMismatch = false;
        for (int localZ = lMinZ; localZ <= lMaxZ; localZ++)
        {
            for (int localX = lMinX; localX <= lMaxX; localX++)
            {
                var tileCoord = new ChunkCoord(cminX + localX, cminZ + localZ);
                if (!_loadedData.TryGetValue(tileCoord, out ChunkData tileData))
                    tileData = ChunkMeshGenerator.BuildFallbackTileData(cminX + localX, cminZ + localZ, Seed);
                region[(localZ - lMinZ) * w + (localX - lMinX)] =
                    ChunkMeshGenerator.BuildMeshData(tileData, TerrainNoiseGenerator.DefaultLayers, EffectiveRefineThreshold);
            }
        }

        // 1ew: adaptive stretch-split means an edit can flip a tile between coarse (4 verts) and
        // refined (16 verts) in the middle of a chunk, changing the merged buffer's block sizes.
        // PatchRegion re-skims through the block table but cannot resize it, so any tile whose
        // refinedness changed falls back to a full chunk rebuild (feeds the same block table anew).
        for (int localZ = lMinZ; localZ <= lMaxZ && !refinednessMismatch; localZ++)
        {
            for (int localX = lMinX; localX <= lMaxX; localX++)
            {
                bool wasRefined = obj.IsTileRefined(localX, localZ);
                bool nowRefined = ChunkMeshGenerator.IsRefined(region[(localZ - lMinZ) * w + (localX - lMinX)]);
                if (wasRefined != nowRefined)
                {
                    refinednessMismatch = true;
                    break;
                }
            }
        }
        if (refinednessMismatch)
        {
            FullRebuildChunk(tc);
            return;
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
        ObjectPooler pool = ObjectPooler.Instance;
        for (int i = 0; i < count; i++)
        {
            float s = Random.Range(0.08f, 0.16f);
            // Cloned from one shared cube template (1du): CreatePrimitive allocated a fresh cube
            // Mesh per piece; Instantiate(reference) reuses the template's mesh and adds only the
            // GameObject/transform weights the debris visually needs. The cube collider is dropped
            // — debris is cosmetic rigidbody scatter, no functional path reads it.
            // 1e6: pooled when available — debris is transient, spawned in bursts, and fully
            // container-agnostic (position/scale/velocity all rewritten on use), so a pool hit
            // costs a fraction of an Instantiate.
            GameObject chunk = pool != null ? pool.Get(SharedDebrisCube) : Instantiate(SharedDebrisCube);
            chunk.SetActive(true);
            chunk.name = "DentDebris";
            chunk.transform.position = spawn + Random.insideUnitSphere * 0.15f;
            chunk.transform.rotation = Random.rotation;
            chunk.transform.localScale = Vector3.one * s;
            var r = chunk.GetComponent<Renderer>();
            if (r != null) r.material.color = Color.Lerp(band, Color.black, Random.value * 0.5f);
            var rb = chunk.GetComponent<Rigidbody>();
            if (rb == null) rb = chunk.AddComponent<Rigidbody>();
            rb.mass = s * s * s * 1000f;
            rb.linearVelocity = new Vector3(
                Random.Range(-2.5f, 2.5f), Random.Range(2.5f, 5f), Random.Range(-2.5f, 2.5f));
            rb.angularVelocity = Random.insideUnitSphere * 6f;
            if (pool != null)
                pool.Return(chunk, 2.5f);
            else
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
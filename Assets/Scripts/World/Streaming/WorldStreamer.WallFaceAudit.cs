using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>
/// Earth-Wall face audit (1l94) — the measurement pass behind "at a certain angle the earth wall
/// mesh is bug, the player can see below". Read-only (rule 7): it mutates nothing, re-stamps
/// nothing, forces no poll and rebuilds no chunk, so the numbers describe the frame the key was
/// pressed on.
///
/// <para><b>What the report is, and the one candidate shape the existing F3 lane cannot see.</b>
/// The user describes a SLIVER / hole in the drawn wall face — skybox/void behind it — that appears
/// ONLY after a Wall-shape cast (Earth Wall, Landslide), at ANY charge, in BOTH first and third
/// person. That rules out the camera-boom-below-surface reading (first person has no boom) and the
/// 1l93 charge change. A Wall is a pure terrain deform with no secondary mesh: it only rewrites the
/// per-tile corner heights the chunk mesher renders from, so the question is whether the SURFACE
/// the mesher draws parts somewhere a cast touched.
/// </para>
///
/// <para><b>The render path, and why a hole is normally impossible.</b> With the low-poly flag off
/// (its default since 1ia) every 1 m tile draws a top quad from its OWN <c>Heights[0..3]</c>, plus a
/// vertical side wall wherever <c>EdgeIsRaised</c> holds. That predicate compares edge MAXES:
/// <c>max(this edge) &gt; max(neighbour edge) + 0.001f</c> (<c>ChunkMeshGenerator.cs:1133</c>). A
/// normal step therefore gets exactly one wall — the higher side emits it. The wall connects this
/// tile's top edge down to the neighbour's top edge, in <c>bands</c> of interpolated quads
/// (<c>ChunkMeshGenerator.cs:614-622</c>).
/// </para>
///
/// <para><b>The two shapes that escape that guarantee.</b> Both come from the two owning tiles
/// disagreeing along ONE shared edge:
///   <b>D-noWall</b> — the two tops differ but <c>max(this) == max(neighbour)</c>, so NEITHER side
///     passes <c>EdgeIsRaised</c> and no wall is emitted at all; there is a bare vertical difference
///     with nothing covering it.
///   <b>D-crossing</b> — the two tops differ in OPPOSITE directions at the two ends of the edge
///     (higher here, lower there). One side does pass <c>EdgeIsRaised</c> (max decides, not the
///     ends), so a wall IS emitted, but it interpolates top&rarr;bot along an edge whose two ends
///     swap: the band quad is a self-intersecting bowtie, one of its two triangles is wound
///     back-facing, and back-face culling drops it — a triangular hole.
/// Either way the opened feature is a sliver exactly at a WALL-shaped cast, which is the report.
/// The F3 <c>RenderedCornerAudit</c> cannot see it: its corner pass samples CHUNK corners only
/// (nodes on 30 m multiples) and its R2 test compares one node, so an INTRA-CHUNK tile edge — where
/// a bbox-sized Wall actually lands — is outside its scope. That gap is why this lane exists.
/// </para>
///
/// <para><b>Sections.</b>
///   A. fingerprint — vertex-count/step buckets across the loaded set; more than one means two
///         generator versions built this world (rule 11), which voids B–E as evidence.
///   B. target      — is a Wall-shaped deform even in the band (the known-non-zero control), so an
///         empty readout is provably "measured and clean", not "looked nowhere".
///   C. corners     — every interior world corner: read all four owning tiles' stored heights and
///         report the worst disagreement (the raw ownership defect).
///   D. edges       — every interior tile edge: flush / covered / CROSSING / NO-WALL, using
///         <c>EdgeIsRaised</c>'s own max predicate and <c>EdgeHeights</c>'s own corner pairing.
///   E. drawn       — at the worst problem edge, count the wall triangles actually present in the
///         drawn <c>RootMesh</c> (triangles whose three vertices all lie in the edge's vertical
///         plane). Zero means D-noWall confirmed as nothing-drawn; non-zero means D-crossing with a
///         bowtie wall.
/// A VERDICT names the first failure with the world XZ to walk to.
/// </para>
///
    /// <para><b>Scope honesty (rule 8).</b> C and D read <c>_loadedData</c> — the corner heights each
    /// tile renders from — and copy <c>EdgeHeights</c>'s corner pairing
    /// (<c>ChunkMeshGenerator.cs:1077-1092</c>) rather than inventing one; D also copies the
    /// <c>0.001f</c> epsilon of <c>EdgeIsRaised</c>. Neither re-derives a private formula. E is the
    /// mesh-level confirmation. Every section prints a count beside its zeros so a zero cannot be a dead
    /// instrument, and section B names the case where the band simply has no cast in it.
    /// </para>
    ///
    /// <para><b>Reachability note.</b> The three edge helpers below carry the full four-direction
    /// correspondence of <c>EdgeHeights</c>, but <c>AppendEdgePass</c> only ever calls them for the
    /// NORTH and EAST edges (edge 0/1): those are the canonical (south/west) owner of a shared edge,
    /// so each edge is classified once and the South/West cases are never reached as arguments. They
    /// are kept verbatim so the mirror stays faithful if the canonical side is ever changed; do not
    /// read the South/West arms as load-bearing today.
    /// </para>
/// </summary>
public partial class WorldStreamer
{
    /// <summary>Half-width (metres) of the scan window around the player. A Wall is cast within
    /// ~14 m (delivery range + radius), so 20 covers it with margin while keeping the corner walk
    /// to a few thousand nodes.</summary>
    private const float WallFaceAuditRadius = 20f;

    /// <summary>Height agreement tolerance (metres) for calling an edge FLUSH (nothing to cover).
    /// Well above float noise on world-metre heights, well below a visible gap.</summary>
    private const float WallFaceAuditEdgeTol = 0.01f;

    /// <summary>The mesher's own "is this side raised" epsilon, copied from
    /// <c>ChunkMeshGenerator.EdgeIsRaised</c> (line 1133). NOT the tolerance above: the predicate
    /// that decides whether a wall is emitted uses this value, so the audit must use it too.</summary>
    private const float WallFaceAuditRaiseEps = 0.001f;

    /// <summary>How far off the edge line a triangle's vertex must be to still count as lying IN the
    /// edge's vertical plane (mesh verts are written at integer local coords, so this is generous).</summary>
    private const float WallFaceAuditLineEps = 0.02f;

    /// <summary>Owners per world corner, and the tile corner slot each stores it in. Derived from
    /// <c>ChunkData.VertexWorldX/Z</c>: a tile at (tx,tz) puts SW at (tx,tz) slot 3, SE at
    /// (tx+1,tz) slot 2, NE at (tx+1,tz+1) slot 1, NW at (tx,tz+1) slot 0 — so world corner (cx,cz)
    /// is stored by SW tile (cx,cz) slot 3, SE tile (cx-1,cz) slot 2, NE tile (cx-1,cz-1) slot 1,
    /// NW tile (cx,cz-1) slot 0.</summary>
    private static readonly int[] WallOwnerSlot = { 3, 2, 0, 1 };

    /// <summary>Produces the Earth-Wall face audit string. Main thread only.</summary>
    public string WallFaceAudit()
    {
        var sb = new StringBuilder(3072);
        Vector3 focus = _focus != null ? _focus.position : Vector3.zero;
        TerrainChunkCoord focusTc = TerrainChunkCoord.FromWorld(focus);
        int minX = Mathf.FloorToInt(focus.x - WallFaceAuditRadius);
        int maxX = Mathf.CeilToInt(focus.x + WallFaceAuditRadius);
        int minZ = Mathf.FloorToInt(focus.z - WallFaceAuditRadius);
        int maxZ = Mathf.CeilToInt(focus.z + WallFaceAuditRadius);

        sb.Append("EARTH WALL FACE AUDIT  loaded ").Append(_loadedChunks.Count)
          .Append("  dormant ").Append(_dormantChunks.Count)
          .Append("  far cells ").Append(_farSectors.Count)
          .Append("  step ").Append(EffectiveLowPolyStep)
          .Append("  tiles ").Append(_loadedData.Count)
          .Append("  focus ").Append(focus.x.ToString("0.0")).Append(',')
          .Append(focus.z.ToString("0.0")).Append("  chunk ").Append(focusTc.X)
          .Append(',').Append(focusTc.Z)
          .Append("  band x[").Append(minX).Append(',').Append(maxX)
          .Append("] z[").Append(minZ).Append(',').Append(maxZ).Append(']');

        Dictionary<string, int> buckets;
        int stepDrift, stepDriftCounted, noMesh;
        Dictionary<int, int> stamps;
        AppendFingerprint(sb, out buckets, out stepDrift, out stepDriftCounted, out noMesh,
            out stamps);

        int raisedTiles;
        string nearestRaised;
        AppendWallTarget(sb, minX, maxX, minZ, maxZ, out raisedTiles, out nearestRaised);

        int cornerNodes, cornerSplit, cornerSinglyOwned;
        float worstCornerSpread;
        string worstCornerSpreadAt;
        AppendCornerOwners(sb, minX, maxX, minZ, maxZ, out cornerNodes, out cornerSplit,
            out cornerSinglyOwned, out worstCornerSpread, out worstCornerSpreadAt);

        int edgeSeen, edgeFlush, edgeCovered, edgeNoWall, edgeCrossed, edgeUnowned;
        float worstProbDelta;
        string worstProbAt, worstProbTiles, worstProbKind, worstProbWho;
        int peAx = 0, peAz = 0, peBx = 0, peBz = 0;
        float peTA = 0f, peTB = 0f, peNA = 0f, peNB = 0f;
        AppendEdgePass(sb, minX, maxX, minZ, maxZ, out edgeSeen, out edgeFlush, out edgeCovered,
            out edgeNoWall, out edgeCrossed, out edgeUnowned, out worstProbDelta, out worstProbAt,
            out peAx, out peAz, out peBx, out peBz, out worstProbTiles, out worstProbKind,
            out worstProbWho, out peTA, out peTB, out peNA, out peNB);

        int holes = edgeNoWall + edgeCrossed;
        if (holes > 0)
            AppendDrawnEdge(sb, peAx, peAz, peBx, peBz);

        string verdict = null;
        if (buckets.Count > 1)
            verdict = "A-stale: the loaded set has " + buckets.Count
                + " mesh buckets — it was built by more than one version of the generator, so C/D/E"
                + " below do not describe what is on screen; needs a play-session restart (rule 11)";
        else if (edgeCrossed > 0)
            verdict = "D-crossing: " + edgeCrossed + " tile edge(s) where the two owning tiles"
                + " disagree OPPOSITELY along the shared edge (one side higher at one end, lower at"
                + " the other), so the wall that IS emitted interpolates top→bot across a swap and"
                + " its band quad becomes a bowtie — one triangle is back-facing and culling drops it,"
                + " leaving the sliver. Worst Δ " + worstProbDelta.ToString("0.###") + " m at "
                + worstProbAt + " (tiles " + worstProbTiles + "; " + worstProbWho + "; tA/tB "
                + peTA.ToString("0.###") + "/" + peTB.ToString("0.###") + "  nA/nB "
                + peNA.ToString("0.###") + "/" + peNB.ToString("0.###")
                + "). Walk to that XZ to see the sliver";
        else if (edgeNoWall > 0)
            verdict = "D-noWall: " + edgeNoWall + " tile edge(s) where the two tops differ but"
                + " max(this) == max(neighbour), so NEITHER side passes EdgeIsRaised and NO wall is"
                + " emitted at all — a bare vertical difference with nothing covering it. Worst Δ "
                + worstProbDelta.ToString("0.###") + " m at " + worstProbAt + " (tiles "
                + worstProbTiles + "; " + worstProbWho + "; tA/tB " + peTA.ToString("0.###") + "/"
                + peTB.ToString("0.###") + "  nA/nB " + peNA.ToString("0.###") + "/"
                + peNB.ToString("0.###") + "). Walk to that XZ to see the sliver";
        else if (cornerSplit > 0)
            verdict = "C-ownership: " + cornerSplit + " world corner(s) where the owning tiles"
                + " disagree by up to " + worstCornerSpread.ToString("0.###") + " m (worst at "
                + worstCornerSpreadAt + ") — a deform rewrote only some of a corner's four owners."
                + " This alone is a step and not yet a hole: every tile edge in the band still read"
                + " flush or a covered step (D crossings 0, D no-walls 0)";
        else if (noMesh > 0)
            verdict = "C: " + noMesh + " loaded chunk(s) have no root mesh at all";
        else
            verdict = "clean: one generator version; every interior world corner's four owners"
                + " agree to " + worstCornerSpread.ToString("0.###") + " m; all " + edgeSeen
                + " comparable tile edges in the band are flush (" + edgeFlush + ") or a covered step"
                + " (" + edgeCovered + "), 0 crossings, 0 no-walls";

        sb.Append("\nVERDICT ").Append(verdict);
        return sb.ToString();
    }

    /// <summary>
    /// Section B — is a Wall actually here? Reports the count of tiles in the band whose corner
    /// spread (the deform's signature: a raised linear feature reads as a large per-tile spread) is
    /// above the refinement trigger, and the nearest such tile's centre. This is the control that
    /// makes C/D's zeros meaningful: if this is zero, no deform is in the band and the audit looked
    /// at pristine terrain.
    /// </summary>
    private void AppendWallTarget(StringBuilder sb, int minX, int maxX, int minZ, int maxZ,
        out int raisedTiles, out string nearestRaised)
    {
        raisedTiles = 0;
        nearestRaised = "none";
        Vector3 focus = _focus != null ? _focus.position : Vector3.zero;
        float bestSqr = float.MaxValue;

        for (int cz = minZ; cz <= maxZ; cz++)
        {
            for (int cx = minX; cx <= maxX; cx++)
            {
                if (!_loadedData.TryGetValue(new ChunkCoord(cx, cz), out ChunkData tile)
                    || !tile.IsValid)
                    continue;
                float mn = tile.Heights[0], mx = tile.Heights[0];
                for (int k = 1; k < tile.Heights.Length; k++)
                {
                    if (tile.Heights[k] < mn) mn = tile.Heights[k];
                    if (tile.Heights[k] > mx) mx = tile.Heights[k];
                }
                if (mx - mn <= EffectiveRefineThreshold)
                    continue;
                raisedTiles++;
                float ddx = (cx + 0.5f) - focus.x;
                float ddz = (cz + 0.5f) - focus.z;
                float d2 = ddx * ddx + ddz * ddz;
                if (d2 < bestSqr)
                {
                    bestSqr = d2;
                    nearestRaised = "(" + cx + "," + cz + ") spread " + (mx - mn).ToString("0.###")
                        + " m  d " + Mathf.Sqrt(d2).ToString("0.#") + " m";
                }
            }
        }

        sb.Append("\nB target  raised tiles ").Append(raisedTiles)
          .Append(" (spread > refineAt ").Append(EffectiveRefineThreshold.ToString("0.##"))
          .Append(" m)  nearest raised apex ").Append(nearestRaised)
          .Append("  [control: a Wall in the band must count > 0]");
        if (raisedTiles == 0)
            sb.Append("\n     no tile in the band exceeds the trigger — no Wall-shaped deform is")
              .Append(" here,")
              .Append("\n     so C/D are reading pristine ground and their zeros do not clear a cast");
    }

    /// <summary>
    /// Section C — the raw ownership test. For every interior world corner in the band, read the
    /// height each of its (up to four) owning TILES stores for that corner and report the two-sided
    /// worst disagreement. A watertight world has all owners equal; a deform that rewrote only some
    /// owners leaves the others stale and shows up here as a split. The count of corners owned by
    /// exactly one tile is printed as the control (a valid corner with all owners present is the
    /// normal case).
    /// </summary>
    private void AppendCornerOwners(StringBuilder sb, int minX, int maxX, int minZ, int maxZ,
        out int cornerNodes, out int cornerSplit, out int cornerSinglyOwned,
        out float worstSpread, out string worstSpreadAt)
    {
        cornerNodes = 0;
        cornerSplit = 0;
        cornerSinglyOwned = 0;
        worstSpread = 0f;
        worstSpreadAt = "none";
        float[] ys = new float[4];
        bool[] has = new bool[4];

        for (int cz = minZ; cz <= maxZ; cz++)
        {
            for (int cx = minX; cx <= maxX; cx++)
            {
                int owners = ReadOwnerCorner(cx, cz, ys, has);
                if (owners == 0)
                    continue;
                cornerNodes++;
                if (owners == 1)
                    cornerSinglyOwned++;
                float mn = float.MaxValue, mx = float.MinValue;
                for (int i = 0; i < 4; i++)
                {
                    if (!has[i]) continue;
                    if (ys[i] < mn) mn = ys[i];
                    if (ys[i] > mx) mx = ys[i];
                }
                float spread = mx - mn;
                if (spread > WallFaceAuditEdgeTol)
                    cornerSplit++;
                if (spread > worstSpread)
                {
                    worstSpread = spread;
                    worstSpreadAt = "(" + cx + "," + cz + ")"
                        + (owners < 4 ? " [owned by " + owners + " tiles]" : "");
                }
            }
        }

        sb.Append("\nC corners  nodes ").Append(cornerNodes)
          .Append("  split ").Append(cornerSplit)
          .Append("  flush ").Append(cornerNodes - cornerSplit)
          .Append("  singlyOwned ").Append(cornerSinglyOwned)
          .Append("  worstSpread ").Append(worstSpread.ToString("0.###")).Append(" m @ ")
          .Append(worstSpreadAt).Append("  (tol ").Append(WallFaceAuditEdgeTol).Append(" m)");
    }

    /// <summary>
    /// Section D — the render-visible test. For every shared tile edge it reads this tile's two edge
    /// corners and the neighbour's two matching corners (the pairing <c>EdgeHeights</c> uses) and
    /// classifies:
    ///   flush    — both ends agree within tolerance: the two top edges coincide, nothing to cover.
    ///   covered  — the ends differ but exactly one side passes <c>EdgeIsRaised</c> (max decides) and
    ///              the ends do not swap, so the wall it emits is monotone: a step, and it is covered.
    ///   CROSSING — the ends disagree OPPOSITELY. A wall is emitted, but top&rarr;bot swaps along the
    ///              edge, so the band quad is a bowtie and one triangle is culled. A hole.
    ///   NO-WALL  — the ends differ but max(this) == max(neighbour), so NEITHER side passes
    ///              <c>EdgeIsRaised</c>: nothing is emitted over a real difference. A hole.
    ///
    /// Every incident edge is visited exactly once: North/East edges are canonical from this (the
    /// south/west) side, and South/West edges are only counted when the neighbour is missing (as
    /// unowned) — otherwise their own canonical pass counts them. The tile loop runs one tile OUT of
    /// the band on the -X/-Z side so the band's boundary edges are still reached.
    /// </summary>
    private void AppendEdgePass(StringBuilder sb, int minX, int maxX, int minZ, int maxZ,
        out int edgeSeen, out int edgeFlush, out int edgeCovered, out int edgeNoWall,
        out int edgeCrossed, out int edgeUnowned, out float worstProbDelta,
        out string worstProbAt, out int weAx, out int weAz, out int weBx, out int weBz,
        out string worstProbTiles, out string worstProbKind, out string worstProbWho,
        out float wtA, out float wtB, out float wnA, out float wnB)
    {
        edgeSeen = 0;
        edgeFlush = 0;
        edgeCovered = 0;
        edgeNoWall = 0;
        edgeCrossed = 0;
        edgeUnowned = 0;
        worstProbDelta = 0f;
        worstProbAt = "none";
        worstProbTiles = "none";
        worstProbKind = "none";
        worstProbWho = "none";
        weAx = weAz = weBx = weBz = 0;
        wtA = wtB = wnA = wnB = 0f;

        for (int cz = minZ - 1; cz <= maxZ; cz++)
        {
            for (int cx = minX - 1; cx <= maxX; cx++)
            {
                if (!_loadedData.TryGetValue(new ChunkCoord(cx, cz), out ChunkData tile)
                    || !tile.IsValid)
                    continue;

                for (int edge = 0; edge < 4; edge++)
                {
                    int nlx = cx, nlz = cz;
                    if (edge == 0) nlz = cz + 1;        // North
                    else if (edge == 1) nlx = cx + 1;   // East
                    else if (edge == 2) nlz = cz - 1;   // South
                    else nlx = cx - 1;                  // West

                    bool nbrLoaded = _loadedData.TryGetValue(new ChunkCoord(nlx, nlz),
                        out ChunkData nbr) && nbr.IsValid;
                    bool canonical = (edge == 0 || edge == 1);
                    if (!nbrLoaded)
                    {
                        // Nothing to compare against. Count once, from whichever side can see it.
                        edgeUnowned++;
                        continue;
                    }
                    if (!canonical)
                        continue; // the neighbour's own North/East pass counts this edge

                    edgeSeen++;

                    float tA, tB, nA, nB;
                    EdgePair(tile, nbr, edge, out tA, out tB, out nA, out nB);
                    float dA = tA - nA;
                    float dB = tB - nB;

                    // The two EdgeIsRaised tests on this edge, copied: this tile's, then the
                    // neighbour's own test (its slots on this edge are the SAME world corners).
                    float maxT = Mathf.Max(tA, tB);
                    float maxN = Mathf.Max(nA, nB);
                    bool emitted = maxT > maxN + WallFaceAuditRaiseEps
                                   || maxN > maxT + WallFaceAuditRaiseEps;

                    bool flush = Mathf.Abs(dA) <= WallFaceAuditEdgeTol
                                 && Mathf.Abs(dB) <= WallFaceAuditEdgeTol;
                    bool crossing = dA * dB < 0f;

                    string kind = null;
                    if (flush)
                    {
                        edgeFlush++;
                    }
                    else if (!emitted)
                    {
                        edgeNoWall++;
                        kind = "NO-WALL";
                    }
                    else if (crossing)
                    {
                        edgeCrossed++;
                        kind = "CROSSING";
                    }
                    else
                    {
                        edgeCovered++;
                    }

                    if (kind == null)
                        continue;

                    float mag = Mathf.Max(Mathf.Abs(dA), Mathf.Abs(dB));
                    if (mag > worstProbDelta)
                    {
                        worstProbDelta = mag;
                        worstProbAt = EdgeMidpoint(cx, cz, edge);
                        worstProbTiles = "(" + cx + "," + cz + ")/(" + nlx + "," + nlz + ")";
                        worstProbKind = kind;
                        worstProbWho = !emitted ? "no side emits a wall"
                            : (maxT > maxN + WallFaceAuditRaiseEps ? "this side's wall" : "neighbour's wall");
                        EdgeEndpointsWorld(cx, cz, edge, out weAx, out weAz, out weBx, out weBz);
                        wtA = tA; wtB = tB; wnA = nA; wnB = nB;
                    }
                }
            }
        }

        sb.Append("\nD edges  seen ").Append(edgeSeen)
          .Append("  flush ").Append(edgeFlush)
          .Append("  covered ").Append(edgeCovered)
          .Append("  CROSSING ").Append(edgeCrossed)
          .Append("  NO-WALL ").Append(edgeNoWall)
          .Append("  unowned ").Append(edgeUnowned)
          .Append("  (tol ").Append(WallFaceAuditEdgeTol).Append(" m, raiseEps ")
          .Append(WallFaceAuditRaiseEps).Append(" m)");
        if (edgeNoWall + edgeCrossed > 0)
            sb.Append("\n     worst problem: ").Append(worstProbKind).Append(" Δ ")
              .Append(worstProbDelta.ToString("0.###")).Append(" m @ ").Append(worstProbAt)
              .Append("  tiles ").Append(worstProbTiles).Append("  [").Append(worstProbWho)
              .Append(']');
        if (edgeSeen == 0)
            sb.Append("\n     no interior edge had both tiles loaded — nothing comparable in the band");
    }

    /// <summary>
    /// Section E — confirms the sliver is DRAWN. A side wall is built ONLY from quads whose two end
    /// points are this edge's own endpoints (<c>ChunkMeshGenerator.cs:619-622</c>), so every wall
    /// triangle for this edge has all three of its vertices in the edge's vertical plane. Counting
    /// those triangles therefore answers "is a wall drawn across this edge" directly:
    ///   0   → nothing covers the edge (D-noWall's mechanism, confirmed at mesh level);
    ///   >0  → a wall IS drawn, so a crossing edge means it is the bowtie whose culled triangle is
    ///         the sliver (D-crossing's mechanism).
    /// The wall's Y extent is printed so a reader sees numbers rather than a verdict word.
    /// </summary>
    private void AppendDrawnEdge(StringBuilder sb, int ax, int az, int bx, int bz)
    {
        // North edges run along X at a fixed Z; East edges run along Z at a fixed X.
        bool alongX = (az == bz);
        int lineFixed = alongX ? az : ax;
        int tMin = alongX ? Mathf.Min(ax, bx) : Mathf.Min(az, bz);
        int tMax = alongX ? Mathf.Max(ax, bx) : Mathf.Max(az, bz);

        sb.Append("\nE drawn  worst edge (").Append(ax).Append(',').Append(az).Append(")-(")
          .Append(bx).Append(',').Append(bz).Append(')')
          .Append(alongX ? "  on z=" : "  on x=").Append(lineFixed);

        int scannedChunks = 0, inactive = 0, noMeshChunks = 0, wallTris = 0, trisScanned = 0;
        float wallMinY = float.MaxValue, wallMaxY = float.MinValue;
        int chunkSize = TerrainChunkCoord.ChunkSize;

        foreach (KeyValuePair<TerrainChunkCoord, ChunkObject> kv in _loadedChunks)
        {
            ChunkObject c = kv.Value;
            if (c == null)
                continue;
            if (!c.gameObject.activeInHierarchy)
            {
                inactive++;
                continue;
            }
            Mesh mesh = c.RootMesh;
            if (mesh == null)
            {
                noMeshChunks++;
                continue;
            }

            // Mesh vertices are CHUNK-LOCAL (0..chunkSize); the world position comes from the
            // owning coordinate, exactly as RenderedCornerAudit does it — not from transform.
            float ox = kv.Key.X * chunkSize;
            float oz = kv.Key.Z * chunkSize;
            float chunkMinX = ox, chunkMaxX = ox + chunkSize;
            float chunkMinZ = oz, chunkMaxZ = oz + chunkSize;
            if (alongX)
            {
                if (tMax < chunkMinX || tMin > chunkMaxX) continue;
                if (lineFixed < chunkMinZ - 1f || lineFixed > chunkMaxZ + 1f) continue;
            }
            else
            {
                if (tMax < chunkMinZ || tMin > chunkMaxZ) continue;
                if (lineFixed < chunkMinX - 1f || lineFixed > chunkMaxX + 1f) continue;
            }

            List<Vector3> verts = _wallAuditVerts;
            verts.Clear();
            mesh.GetVertices(verts);
            int[] tris = mesh.GetTriangles(0); // single-submesh merged chunk mesh
            scannedChunks++;

            for (int i = 0; i + 2 < tris.Length; i += 3)
            {
                int i0 = tris[i], i1 = tris[i + 1], i2 = tris[i + 2];
                if (i0 < 0 || i1 < 0 || i2 < 0 || i0 >= verts.Count || i1 >= verts.Count
                    || i2 >= verts.Count)
                    continue;
                Vector3 v0 = verts[i0], v1 = verts[i1], v2 = verts[i2];
                trisScanned++;

                // All three vertices must sit in the edge's vertical plane to be a wall of THIS edge.
                bool inPlane = alongX
                    ? Mathf.Abs(oz + v0.z - lineFixed) <= WallFaceAuditLineEps
                      && Mathf.Abs(oz + v1.z - lineFixed) <= WallFaceAuditLineEps
                      && Mathf.Abs(oz + v2.z - lineFixed) <= WallFaceAuditLineEps
                    : Mathf.Abs(ox + v0.x - lineFixed) <= WallFaceAuditLineEps
                      && Mathf.Abs(ox + v1.x - lineFixed) <= WallFaceAuditLineEps
                      && Mathf.Abs(ox + v2.x - lineFixed) <= WallFaceAuditLineEps;
                if (!inPlane)
                    continue;

                // And it must actually span the middle of the edge, not a stub at one end.
                float u0 = alongX ? ox + v0.x : oz + v0.z;
                float u1 = alongX ? ox + v1.x : oz + v1.z;
                float u2 = alongX ? ox + v2.x : oz + v2.z;
                float lo = Mathf.Min(u0, Mathf.Min(u1, u2));
                float hi = Mathf.Max(u0, Mathf.Max(u1, u2));
                if (hi < tMin + 0.2f || lo > tMax - 0.2f)
                    continue;

                wallTris++;
                float y0 = v0.y, y1 = v1.y, y2 = v2.y;
                float loY = Mathf.Min(y0, Mathf.Min(y1, y2));
                float hiY = Mathf.Max(y0, Mathf.Max(y1, y2));
                if (loY < wallMinY) wallMinY = loY;
                if (hiY > wallMaxY) wallMaxY = hiY;
            }
        }

        sb.Append("\n     chunks scanned ").Append(scannedChunks)
          .Append("  skipped inactive ").Append(inactive)
          .Append("  noMesh ").Append(noMeshChunks)
          .Append("  tris ").Append(trisScanned)
          .Append("  WALL tris on this edge ").Append(wallTris);
        if (wallTris > 0)
            sb.Append("  y[").Append(wallMinY.ToString("0.###")).Append(" .. ")
              .Append(wallMaxY.ToString("0.###")).Append("]")
              .Append("  => a wall is drawn here, so a crossing edge means it is the bowtie band");
        else
            sb.Append("  => NO wall drawn across this edge, confirming a bare uncovered difference");
        if (trisScanned == 0)
            sb.Append("\n     [control broken?] no triangle was readable in the band — the zeros"
                      + " above say nothing");
    }

    /// <summary>
    /// Reads the height each of a world corner's four owning tiles stores for that corner. Owner
    /// i is: SW tile (cx,cz) slot 3, SE tile (cx-1,cz) slot 2, NW tile (cx,cz-1) slot 0, NE tile
    /// (cx-1,cz-1) slot 1. Returns how many owners are loaded (0..4) and fills <paramref name="ys"/>
    /// / <paramref name="has"/>.
    /// </summary>
    private int ReadOwnerCorner(int cx, int cz, float[] ys, bool[] has)
    {
        int n = 0;
        for (int i = 0; i < 4; i++)
        {
            has[i] = false;
            int ox = (i == 1 || i == 3) ? cx - 1 : cx;
            int oz = (i == 2 || i == 3) ? cz - 1 : cz;
            if (_loadedData.TryGetValue(new ChunkCoord(ox, oz), out ChunkData d) && d.IsValid)
            {
                ys[i] = d.Heights[WallOwnerSlot[i]];
                has[i] = true;
                n++;
            }
        }
        return n;
    }

    /// <summary>
    /// Two matching edge-corner pairs for the shared edge between <paramref name="tile"/> (at
    /// (cx,cz)) and its neighbour in <paramref name="edge"/> direction. Mirrors
    /// <c>ChunkMeshGenerator.EdgeHeights</c> exactly: <paramref name="tA"/>/<paramref name="tB"/>
    /// are this tile's two edge corners, <paramref name="nA"/>/<paramref name="nB"/> the same two
    /// WORLD corners as the neighbour stores them.
    /// </summary>
    private static void EdgePair(ChunkData tile, ChunkData nbr, int edge,
        out float tA, out float tB, out float nA, out float nB)
    {
        float[] h = tile.Heights;
        float[] g = nbr.Heights;
        switch (edge)
        {
            case 0: // North: this NW,NE vs neighbour's SW,SE
                tA = h[0]; tB = h[1]; nA = g[3]; nB = g[2]; break;
            case 1: // East: this NE,SE vs neighbour's NW,SW
                tA = h[1]; tB = h[2]; nA = g[0]; nB = g[3]; break;
            case 2: // South: this SW,SE vs neighbour's NW,NE
                tA = h[3]; tB = h[2]; nA = g[0]; nB = g[1]; break;
            default: // West: this NW,SW vs neighbour's NE,SE
                tA = h[0]; tB = h[3]; nA = g[1]; nB = g[2]; break;
        }
    }

    private static string EdgeMidpoint(int cx, int cz, int edge)
    {
        if (edge == 0)
            return "(" + (cx + 0.5f).ToString("0.0") + "," + (cz + 1f).ToString("0.0") + ")";
        if (edge == 1)
            return "(" + (cx + 1f).ToString("0.0") + "," + (cz + 0.5f).ToString("0.0") + ")";
        if (edge == 2)
            return "(" + (cx + 0.5f).ToString("0.0") + "," + cz.ToString("0.0") + ")";
        return "(" + cx.ToString("0.0") + "," + (cz + 0.5f).ToString("0.0") + ")";
    }

    /// <summary>World endpoints of a tile's edge: North (cx,cz+1)-(cx+1,cz+1), East
    /// (cx+1,cz+1)-(cx+1,cz), South (cx,cz)-(cx+1,cz), West (cx,cz+1)-(cx,cz). Matches
    /// <c>ChunkMeshGenerator.EdgeEnds</c> in order.</summary>
    private static void EdgeEndpointsWorld(int cx, int cz, int edge,
        out int ax, out int az, out int bx, out int bz)
    {
        if (edge == 0)
        {
            ax = cx; az = cz + 1; bx = cx + 1; bz = cz + 1;
        }
        else if (edge == 1)
        {
            ax = cx + 1; az = cz + 1; bx = cx + 1; bz = cz;
        }
        else if (edge == 2)
        {
            ax = cx; az = cz; bx = cx + 1; bz = cz;
        }
        else
        {
            ax = cx; az = cz + 1; bx = cx; bz = cz;
        }
    }

    private readonly List<Vector3> _wallAuditVerts = new List<Vector3>(4096);
}

using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>
/// Terrain seam audit (1hj) — the measurement pass behind "there are gaps between the terrain
/// chunks". A see-through slit has at least four possible causes that look identical from the
/// player's side, and picking the wrong one means a fix that changes nothing:
///
///   (A) REAL&lt;-&gt;REAL corner divergence — a deformed chunk's shared 31x31 corner lattice does not
///       agree with its neighbour's at the same world tile, so the two surfaces part by a sliver.
///   (B) A chunk that is present but does not RENDER — root inactive, mesh missing, or a rebuilt
///       mesh with fewer vertices / a smaller bounds than the chunk it stands for (a "short" root).
///   (C) A chunk position with NOTHING in it at all — an interior load hole rather than a seam.
///   (D) REAL&lt;-&gt;FAR rim divergence — the far-shell cell meeting the last real ring renders its
///       own height for the shared corner nodes, so the boundary is a step even though both
///       meshes are "correct" on their own.
///
/// <see cref="SeamAudit"/> measures all four at once, on the main thread, from the live chunk
/// dictionaries and the uploaded far-cell meshes. It is strictly READ-ONLY: it never mutates
/// chunk, lattice, mesh or collider state, and it never forces a rebuild, so a reported value is
/// exactly what the frame the user pressed the key was rendering.
///
/// Deliberately NOT a validator in the load path: the load path already owns correctness per
/// chunk (a build-time accept step is the only place that can afford it), and a hot per-chunk
/// cross-chunk comparison would serialise the poll. This is the on-demand QA readout, surfaced by
/// <c>NewWorldTestGround</c> on its own configurable key (<c>EnableSeamAudit</c> / F2 by default —
/// F5-F12 belong to the editor cutscene shortcuts).
/// </summary>
public partial class WorldStreamer
{
    /// <summary>Max chunk coords listed per audit section before the rest are summarised as a
    /// count. A readout that names a few offenders is a screenshot; one that names 400 is a wall
    /// of text that hides them.</summary>
    private const int SeamAuditMaxListed = 6;

    /// <summary>How close (in metres) a rebuilt root's X/Z bounds must still reach the chunk edges
    /// before the audit calls the root short. A full root spans exactly 0..30 m in both axes; the
    /// slack absorbs float error only, it is not a fudge factor for a missing band.</summary>
    private const float SeamAuditBoundsSlack = 0.05f;

    /// <summary>
    /// One multi-line, human-readable report of the current terrain seam state. Sections, in the
    /// order a player would describe the symptom:
    ///
    ///   A. real&lt;-&gt;real   — worst shared-corner height mismatch over every loaded chunk pair,
    ///                         plus how many pairs / nodes were actually compared.
    ///   B. roots           — loaded chunks whose root is hidden, mesh-less, vertex-short, or
    ///                         bounds-short (i.e. it is loaded but not drawing its full chunk).
    ///   C. interior holes  — chunk positions inside the loaded ring that hold nothing while
    ///                         surrounded by loaded neighbours (not a seam: a missing chunk).
    ///   D. rim             — far cells at the real/far boundary compared against the real chunks
    ///                         they meet, and any outer-ring chunk with no far cell at all.
    ///
    /// A VERDICT line names the first section that failed, so the screenshot answers the question
    /// without the reader having to interpret four sections.
    /// </summary>
    public string SeamAudit()
    {
        int view = RenderDistance != null ? RenderDistance.Radius : 3;
        int near = Mathf.Min(Mathf.Max(NearRingRadius, 0), view);
        TerrainChunkCoord centre = _focus != null
            ? TerrainChunkCoord.FromWorld(_focus.position)
            : new TerrainChunkCoord(0, 0);

        var sb = new StringBuilder(1024);
        sb.Append("SEAM AUDIT  loaded ").Append(_loadedChunks.Count)
          .Append("  dormant ").Append(_dormantChunks.Count)
          .Append("  pending ").Append(_pendingChunks.Count)
          .Append("  inflight ").Append(_chunksInFlight.Count)
          .Append("  ready ").Append(_readyChunks.Count)
          .Append("  farCells ").Append(_farSectors.Count)
          .Append("  focus ").Append(centre)
          .Append("  ring ").Append(near).Append('/').Append(near + 1)
          .Append("  step ").Append(EffectiveLowPolyStep)
          .Append('\n');

        string verdict = AuditRealToReal(sb);
        verdict = AuditRoots(sb, verdict);
        verdict = AuditInteriorHoles(sb, centre, near, verdict);
        verdict = AuditFarRim(sb, centre, near, verdict);

        sb.Append("VERDICT ").Append(verdict
            ?? "clean: corner lattice consistent, all roots full-span, no interior holes, rim agrees");
        return sb.ToString();
    }

    // --- A. real <-> real -------------------------------------------------------------------------

    /// <summary>
    /// Compares the 31 shared corner nodes of every loaded chunk pair (A) and returns the verdict
    /// for this section. A chunk's lattice is stamped from the canonical noise of the tiles that
    /// own each node, so a PRISTINE pair is identical to the last float; any non-zero maximum
    /// means one side's lattice was re-stamped by an edit that never reached its neighbour.
    /// </summary>
    private string AuditRealToReal(StringBuilder sb)
    {
        int pairs = 0;
        int nodes = 0;
        float worst = 0f;
        TerrainChunkCoord worstA = new TerrainChunkCoord(0, 0);
        TerrainChunkCoord worstB = new TerrainChunkCoord(0, 0);
        string worstDir = "-";
        int worstIndex = -1;
        int withoutLattice = 0;

        foreach (KeyValuePair<TerrainChunkCoord, ChunkObject> kv in _loadedChunks)
        {
            ChunkObject a = kv.Value;
            if (a == null)
                continue;
            if (!a.HasLattice)
            {
                withoutLattice++;
                continue;
            }

            // East neighbour shares this chunk's gx == ChunkSize column.
            TerrainChunkCoord east = new TerrainChunkCoord(kv.Key.X + 1, kv.Key.Z);
            if (_loadedChunks.TryGetValue(east, out ChunkObject b) && b != null)
            {
                pairs++;
                CompareSharedEdge(a, b, true,
                    ref nodes, ref worst, ref worstA, ref worstB,
                    ref worstDir, ref worstIndex, ref withoutLattice);
            }

            // North neighbour shares this chunk's gz == ChunkSize row.
            TerrainChunkCoord north = new TerrainChunkCoord(kv.Key.X, kv.Key.Z + 1);
            if (_loadedChunks.TryGetValue(north, out ChunkObject c) && c != null)
            {
                pairs++;
                CompareSharedEdge(a, c, false,
                    ref nodes, ref worst, ref worstA, ref worstB,
                    ref worstDir, ref worstIndex, ref withoutLattice);
            }
        }

        sb.Append("A real<->real  pairs ").Append(pairs)
          .Append("  nodes ").Append(nodes);
        if (withoutLattice > 0)
            sb.Append("  (noLattice ").Append(withoutLattice).Append(')');
        if (worst <= 0f)
        {
            sb.Append("  worst dY 0  OK (every shared corner node matches)\n");
            return null;
        }

        sb.Append("  WORST dY ").Append(worst.ToString("0.####"))
          .Append(" m at ").Append(worstA).Append('-').Append(worstDir)
          .Append(worstB).Append(" node ").Append(worstIndex).Append('\n');
        return "A: real<->real corner mismatch (worst " + worst.ToString("0.####")
            + " m at " + worstA + "-" + worstDir + worstB + " node " + worstIndex + ")";
    }

    /// <summary>
    /// Walks the 31 nodes of one shared chunk boundary and folds the largest mismatch into the
    /// running worst. <paramref name="east"/> picks the shared side: true compares
    /// <paramref name="a"/>'s gx == ChunkSize column against <paramref name="b"/>'s gx == 0 column,
    /// false compares the gz == ChunkSize row against gz == 0.
    ///
    /// A chunk with no lattice (voxel chunks build a stepped mesh, not a corner grid) cannot be
    /// compared node-by-node, so the pair is skipped and counted rather than reported as a 0
    /// mismatch — a skipped node must never look like a passing one.
    /// </summary>
    private static void CompareSharedEdge(
        ChunkObject a, ChunkObject b, bool east,
        ref int nodes, ref float worst,
        ref TerrainChunkCoord worstA, ref TerrainChunkCoord worstB,
        ref string worstDir, ref int worstIndex, ref int withoutLattice)
    {
        if (!b.HasLattice)
        {
            withoutLattice++;
            return;
        }

        int cs = TerrainChunkCoord.ChunkSize;
        for (int i = 0; i <= cs; i++)
        {
            float ya = east ? a.LatticeY(cs, i) : a.LatticeY(i, cs);
            float yb = east ? b.LatticeY(0, i) : b.LatticeY(i, 0);
            if (float.IsNaN(ya) || float.IsNaN(yb))
                continue;
            nodes++;

            float d = Mathf.Abs(ya - yb);
            if (d <= worst)
                continue;
            worst = d;
            // Keep the OFFENDING chunk pair, not the sweep's last one: CompareSharedEdge only ever
            // knows its two own chunks, so it stamps them straight from the ChunkObjects.
            worstA = a.ChunkCoord;
            worstB = b.ChunkCoord;
            worstDir = east ? "E" : "N";
            worstIndex = i;
        }
    }

    // --- B. roots ---------------------------------------------------------------------------------

    /// <summary>
    /// Flags every loaded chunk whose root is not drawing its whole 30 m footprint (B). This is the
    /// "seam looks fine from above, and there is a hole from inside it" case: the chunk exists in
    /// the dictionary, so streaming thinks the ground is covered.
    /// </summary>
    private string AuditRoots(StringBuilder sb, string verdict)
    {
        int bad = 0;
        int checkedCount = 0;
        var listed = new List<string>();

        foreach (KeyValuePair<TerrainChunkCoord, ChunkObject> kv in _loadedChunks)
        {
            ChunkObject c = kv.Value;
            if (c == null)
            {
                bad++;
                if (listed.Count < SeamAuditMaxListed)
                    listed.Add(kv.Key + ": no ChunkObject");
                continue;
            }
            checkedCount++;

            string problem = null;
            if (!c.gameObject.activeInHierarchy)
                problem = "root inactive in hierarchy";
            else if (!c.gameObject.activeSelf)
                problem = "root inactive";

            Mesh mesh = c.RootMesh;
            if (problem == null && mesh == null)
            {
                problem = "no mesh on filter";
            }
            else if (problem == null)
            {
                if (c.MeshStep > 0)
                {
                    int perRow = TerrainChunkCoord.ChunkSize / c.MeshStep;
                    int expect = perRow * perRow * 4;
                    if (mesh.vertexCount != expect)
                        problem = "verts " + mesh.vertexCount + " != " + expect;
                }
                Bounds b = mesh.bounds;
                if (b.min.x > SeamAuditBoundsSlack
                    || b.max.x < TerrainChunkCoord.ChunkSize - SeamAuditBoundsSlack
                    || b.min.z > SeamAuditBoundsSlack
                    || b.max.z < TerrainChunkCoord.ChunkSize - SeamAuditBoundsSlack)
                {
                    problem = (problem == null ? "" : problem + ", ")
                        + "bounds x " + b.min.x.ToString("0.##") + ".."
                        + b.max.x.ToString("0.##") + " z " + b.min.z.ToString("0.##")
                        + ".." + b.max.z.ToString("0.##");
                }
            }

            if (problem == null)
                continue;
            bad++;
            if (listed.Count < SeamAuditMaxListed)
                listed.Add(kv.Key + ": " + problem);
        }

        sb.Append("B roots  checked ").Append(checkedCount).Append("  bad ").Append(bad);
        if (bad == 0)
        {
            sb.Append("  OK (every loaded root is active and spans 0..30 m)\n");
            return verdict;
        }
        sb.Append('\n');
        for (int i = 0; i < listed.Count; i++)
            sb.Append("    ").Append(listed[i]).Append('\n');
        if (bad > listed.Count)
            sb.Append("    ... and ").Append(bad - listed.Count).Append(" more\n");
        return verdict ?? ("B: " + bad + " loaded root(s) not drawing their full chunk");
    }

    // --- C. interior holes -----------------------------------------------------------------------

    /// <summary>
    /// Scans the loaded ring for chunk positions that hold nothing (C). A hole is reported only
    /// when the position is uncovered AND at least three of its four orthogonal neighbours are
    /// LOADED — the ring's leading edge always has uncovered neighbours (that is the streaming
    /// frontier) and a chunk sitting in the ready queue is dequeued by the next poll, so a weaker
    /// test would flag normal streaming as a hole.
    /// </summary>
    private string AuditInteriorHoles(StringBuilder sb, TerrainChunkCoord centre, int near, string verdict)
    {
        int reach = near + 1;
        int holes = 0;
        int frontier = 0;
        var listed = new List<string>();

        for (int dz = -reach; dz <= reach; dz++)
        {
            for (int dx = -reach; dx <= reach; dx++)
            {
                TerrainChunkCoord tc = new TerrainChunkCoord(centre.X + dx, centre.Z + dz);
                if (IsChunkCovered(tc))
                    continue;

                int loadedNeighbours = 0;
                if (IsChunkLoaded(new TerrainChunkCoord(tc.X + 1, tc.Z))) loadedNeighbours++;
                if (IsChunkLoaded(new TerrainChunkCoord(tc.X - 1, tc.Z))) loadedNeighbours++;
                if (IsChunkLoaded(new TerrainChunkCoord(tc.X, tc.Z + 1))) loadedNeighbours++;
                if (IsChunkLoaded(new TerrainChunkCoord(tc.X, tc.Z - 1))) loadedNeighbours++;

                if (loadedNeighbours >= 3)
                {
                    holes++;
                    if (listed.Count < SeamAuditMaxListed)
                        listed.Add(tc + " (ring " + Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dz)) + ")");
                }
                else if (loadedNeighbours == 0)
                {
                    frontier++;
                }
            }
        }

        sb.Append("C holes  interior ").Append(holes)
          .Append("  frontier ").Append(frontier);
        if (holes > 0 && _readyChunks.Count > 0)
            sb.Append("  (ready queue non-empty: a hole still awaiting finalize would be transient)");
        if (holes == 0)
        {
            sb.Append("  OK (nothing missing inside the loaded ring)\n");
            return verdict;
        }
        sb.Append('\n');
        for (int i = 0; i < listed.Count; i++)
            sb.Append("    ").Append(listed[i]).Append('\n');
        if (holes > listed.Count)
            sb.Append("    ... and ").Append(holes - listed.Count).Append(" more\n");
        return verdict ?? (holes + " interior hole(s): loaded ground with no chunk at " + listed[0]);
    }

    /// <summary>Loaded OR retained-dormant OR mid-build. A position in any of these states is
    /// covered ground; only the absence of all of them is a hole.</summary>
    private bool IsChunkCovered(TerrainChunkCoord tc)
        => _loadedChunks.ContainsKey(tc)
        || _dormantChunks.ContainsKey(tc)
        || _pendingChunks.Contains(tc)
        || _chunksInFlight.ContainsKey(tc);

    private bool IsChunkLoaded(TerrainChunkCoord tc)
        => _loadedChunks.TryGetValue(tc, out ChunkObject c) && c != null;

    // --- D. real <-> far rim ----------------------------------------------------------------------

    /// <summary>
    /// Compares the far-shell cells that meet the outermost real ring against the real chunks they
    /// touch (D), and flags any outer-ring chunk with no far cell beyond it. Far cells are built on
    /// the same 3 m lattice but are never re-stamped by a deformation, so a dug/cut chunk at the
    /// rim is expected to disagree here — this section separates that from a real-corner bug.
    /// </summary>
    private string AuditFarRim(StringBuilder sb, TerrainChunkCoord centre, int near, string verdict)
    {
        int keep = near + 1;
        var cells = new HashSet<FarCell>();
        int rimChunks = 0;
        int noFarCell = 0;
        var listed = new List<string>();

        // Every loaded chunk ON the last real ring, plus the ring of chunks that meets the far
        // shell, owns four far-cell neighbours. Scanning ring keep as well is what picks up the
        // DIAGONAL corner cells (the chunk at (cx+1, cz+1) is no neighbour of any ring-near chunk
        // along an axis), and a missing corner cell is the most visible hole of the lot.
        for (int dz = -keep; dz <= keep; dz++)
        {
            for (int dx = -keep; dx <= keep; dx++)
            {
                int cheb = Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dz));
                if (cheb != near && cheb != keep)
                    continue;
                TerrainChunkCoord tc = new TerrainChunkCoord(centre.X + dx, centre.Z + dz);
                if (!IsChunkLoaded(tc))
                    continue;
                bool onLastRealRing = cheb == near;
                if (onLastRealRing)
                    rimChunks++;

                bool any = false;
                for (int i = 0; i < 4; i++)
                {
                    int ox = i == 0 ? 1 : i == 1 ? -1 : 0;
                    int oz = i == 2 ? 1 : i == 3 ? -1 : 0;
                    FarCell? cell = FarCellForChunk(tc.X + ox, tc.Z + oz, centre, near, keep);
                    if (!cell.HasValue)
                        continue;
                    any = true;
                    cells.Add(cell.Value);
                }
                // Only the last real ring MUST have far cover beyond it. A ring-keep chunk's
                // outward neighbours are past `keep`, where FarCellForChunk correctly answers
                // "no cell" — that is the far shell's own frontier, not a missing cover.
                if (onLastRealRing && !any)
                {
                    noFarCell++;
                    if (listed.Count < SeamAuditMaxListed)
                        listed.Add(tc + ": no far cell beyond the rim");
                }
            }
        }

        float worst = 0f;
        int compared = 0;
        int missing = 0;
        string worstAt = "-";

        foreach (FarCell cell in cells)
        {
            if (!_farSectors.TryGetValue(cell, out GameObject go) || go == null)
            {
                missing++;
                continue;
            }
            MeshFilter mf = go.GetComponent<MeshFilter>();
            Mesh mesh = mf != null ? mf.sharedMesh : null;
            if (mesh == null || !mesh.isReadable)
            {
                missing++;
                continue;
            }

            FarCellRings(cell, centre, out _, out int maxRing);
            int step = FarSectorStep(cell.Span, maxRing);
            int originX = Mathf.RoundToInt(cell.X * TerrainChunkCoord.ChunkSize * ChunkData.Size);
            int originZ = Mathf.RoundToInt(cell.Z * TerrainChunkCoord.ChunkSize * ChunkData.Size);

            Vector3[] verts = mesh.vertices;
            for (int v = 0; v < verts.Length; v++)
            {
                // Far-cell vertex -> the world lattice node it renders, and that node's address
                // inside the neighbouring real chunk. Both are exact: the cell GO sits on its block
                // origin (cell.X*30 m, 0, cell.Z*30 m) and its vertices land on multiples of the
                // far step, so a vertex that is NOT on a lattice node is a facet interior point.
                int worldX = originX + Mathf.RoundToInt(verts[v].x);
                int worldZ = originZ + Mathf.RoundToInt(verts[v].z);
                int localX = worldX - originX;
                int localZ = worldZ - originZ;
                if (localX % step != 0 || localZ % step != 0)
                    continue;

                int chunkX = FloorDiv(worldX, TerrainChunkCoord.ChunkSize);
                int chunkZ = FloorDiv(worldZ, TerrainChunkCoord.ChunkSize);
                if (!_loadedChunks.TryGetValue(
                        new TerrainChunkCoord(chunkX, chunkZ), out ChunkObject real) || real == null)
                    continue;

                float realY = real.LatticeY(localX, localZ);
                if (float.IsNaN(realY))
                    continue;
                compared++;

                float d = Mathf.Abs(realY - verts[v].y);
                if (d > worst)
                {
                    worst = d;
                    worstAt = cell + " node " + worldX + "," + worldZ;
                }
            }
        }

        sb.Append("D rim  rimChunks ").Append(rimChunks)
          .Append("  cells ").Append(cells.Count)
          .Append("  missing ").Append(missing)
          .Append("  noFarCell ").Append(noFarCell)
          .Append("  compared ").Append(compared)
          .Append("  worst dY ").Append(worst.ToString("0.####"));
        if (missing == 0 && noFarCell == 0 && worst <= 0f)
        {
            sb.Append("  OK (far cells agree with the real ring they meet)\n");
            return verdict;
        }
        if (missing > 0 || noFarCell > 0)
        {
            sb.Append('\n');
            if (noFarCell > 0)
            {
                for (int i = 0; i < listed.Count; i++)
                    sb.Append("    ").Append(listed[i]).Append('\n');
                if (noFarCell > listed.Count)
                    sb.Append("    ... and ").Append(noFarCell - listed.Count).Append(" more\n");
            }
            if (worst > 0f)
                sb.Append("    worst at ").Append(worstAt).Append('\n');
            if (verdict == null)
            {
                verdict = "D: far rim incomplete (missing cells " + missing
                    + ", no far cell " + noFarCell + ")";
            }
            return verdict;
        }
        sb.Append("  (far/real disagreement)\n");
        return verdict ?? ("D: far rim disagrees with the real ring (worst "
            + worst.ToString("0.####") + " m at " + worstAt + ")");
    }
}

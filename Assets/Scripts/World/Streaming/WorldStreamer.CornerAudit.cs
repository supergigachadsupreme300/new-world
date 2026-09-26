using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>
/// Rendered-corner audit (1hv) — the measurement pass behind "at every chunk corner the edge does
/// not match, you can see void through the gap".
///
/// <para><b>Why this exists when <see cref="SeamAudit"/> already exists.</b> The seam audit's
/// section A compares <c>ChunkObject.LatticeY</c> — the 31x31 corner LATTICE. That is the data
/// layer, and rule 8 is explicit that a green lattice is not evidence about what is drawn: a
/// renderer that derives its corner vertex from somewhere other than the lattice it stamps will
/// report "worst dY 0 OK" and still part at the corner. Section B checks each root's bounds and
/// vertex COUNT, which catches a root that is short or hidden but says nothing about whether the
/// four vertices that meet at a corner agree with each other. So the two questions this file answers
/// are the two the seam audit structurally cannot:
///   (R1) COVERAGE — does every loaded chunk actually place a rendered vertex AT each of its four
///        chunk corners? A corner the surface never reaches is a hole, whatever the lattice says.
///   (R2) AGREEMENT — at each world corner node, do the rendered corner heights of the (up to four)
///        chunks meeting there agree with EACH OTHER, and does each agree with its OWN chunk's
///        lattice? Cross-chunk disagreement means the render path is not sharing one corner height;
///        own-lattice disagreement means the render path copies the corner from the wrong place.
/// </para>
///
/// <para><b>Plus the staleness fingerprint.</b> AGENTS rule 11: editing the terrain render algorithm
/// changes nothing already on screen, so a resident set built by two different versions of the
/// generator shows up here as more than one (MeshStep, vertexCount) bucket. That is the one
/// mechanism this repo can create on demand, and it is indistinguishable from a renderer bug by
/// eye, so it gets its own line rather than being another guess.
/// </para>
///
/// <para><b>Strictly READ-ONLY</b> (rule 7): it never mutates chunk, lattice, mesh or collider
/// state, never re-stamps, never forces a poll and never rebuilds, so the numbers describe exactly
/// the frame the key was pressed on. It reads <c>Mesh.vertices</c> through a reused list
/// (<c>GetVertices</c> does not allocate a fresh array per call), so pressing it costs one mesh scan
/// per loaded chunk and nothing else.
/// </para>
///
/// <para><b>Address arithmetic</b> (rule 8 — a copy's addressing IS the contract). A chunk root sits
/// at <c>(tc.X * 30 * ChunkData.Size, 0, tc.Z * 30 * ChunkData.Size)</c> and <c>ChunkData.Size</c>
/// is 1, so mesh-local X/Z + tc*30 == world X/Z and mesh-local Y == world Y. Corners are therefore
/// mesh-local (0|30, 0|30), world nodes are <c>tc * 30 + local</c>, and the four chunks touching a
/// node at (nx,nz) are X,Z in {n/30, n/30 - 1} — exact because nx/nz are multiples of 30.
/// </para>
/// </summary>
public partial class WorldStreamer
{
    /// <summary>How close (metres) a rendered vertex must sit to a chunk corner in X/Z to count as
    /// covering it. The facet vertices ARE lattice nodes, so this is float slack, not a fudge
    /// factor — the same contract as <see cref="SeamAuditBoundsSlack"/>.</summary>
    private const float RenderedCornerReach = 0.05f;

    /// <summary>How close (metres) two rendered corner heights must be to count as agreeing. Well
    /// above float noise on world-metre heights, well below anything a player could call a gap.</summary>
    private const float RenderedCornerTolerance = 0.01f;

    /// <summary>Max offenders listed per section before the rest are summarised as a count.</summary>
    private const int RenderedCornerMaxListed = 3;

    /// <summary>Reused scan buffer — one mesh's worth of vertices, never reallocated per chunk.</summary>
    private readonly List<Vector3> _renderedCornerScan = new List<Vector3>(4096);

    /// <summary>Accumulator for one world corner node (a lattice-space world X/Z multiple of 30).</summary>
    private struct RenderedCornerNode
    {
        public int Nx, Nz;        // world corner node, metres
        public int Present;       // loaded chunks that placed a rendered vertex here
        public float MinTopY;     // lowest per-chunk TOP height contributed here
        public float MaxTopY;     // highest per-chunk TOP height contributed here
        public float WorstDelta;  // worst |rendered top - own lattice| among the contributors
        public TerrainChunkCoord WorstDeltaChunk;
    }

    /// <summary>
    /// One multi-line report of the RENDERED layer at chunk corners. Sections:
    ///   A. corners — coverage (does every chunk reach its own corners), then the worst
    ///                cross-chunk height disagreement and the worst own-lattice disagreement.
    ///   B. fingerprint — distinct (facet step, vertex count) buckets across the loaded set; more
    ///                    than one means the resident terrain was built by more than one version of
    ///                    the generator (rule 11).
    /// A VERDICT line names the first thing that failed, so a screenshot answers the question.
    /// </summary>
    public string RenderedCornerAudit()
    {
        var sb = new StringBuilder(768);
        int loaded = _loadedChunks.Count;
        sb.Append("RENDERED CORNER AUDIT  loaded ").Append(loaded)
          .Append("  dormant ").Append(_dormantChunks.Count)
          .Append("  step ").Append(EffectiveLowPolyStep);

        var nodes = new Dictionary<long, RenderedCornerNode>(256);
        int corners = 0;
        int noMesh = 0;
        int inactive = 0;
        int cs = TerrainChunkCoord.ChunkSize;

        foreach (KeyValuePair<TerrainChunkCoord, ChunkObject> kv in _loadedChunks)
        {
            ChunkObject c = kv.Value;
            if (c == null)
                continue;
            if (!c.gameObject.activeInHierarchy)
                inactive++;
            Mesh mesh = c.RootMesh;
            if (mesh == null)
            {
                // Section B of the seam audit owns "loaded but not drawing"; count it so the
                // corner pass is never silently short one chunk's corners.
                noMesh++;
                continue;
            }

            _renderedCornerScan.Clear();
            mesh.GetVertices(_renderedCornerScan);

            for (int corner = 0; corner < 4; corner++)
            {
                // 0 = SW (0,0), 1 = SE (30,0), 2 = NW (0,30), 3 = NE (30,30). Mesh-local.
                int lx = (corner & 1) == 0 ? 0 : cs;
                int lz = (corner & 2) == 0 ? 0 : cs;
                corners++;

                // The TOPMOST vertex at the corner is the surface: a smooth chunk's root also carries
                // side walls, which hang DOWN from the same edge, so the min would read the bottom
                // of a wall as the corner's height. The low-poly root has no walls (1hi.1 skips
                // them), so top == the only height there.
                float topY = float.MinValue;
                int hits = 0;
                for (int v = 0; v < _renderedCornerScan.Count; v++)
                {
                    Vector3 p = _renderedCornerScan[v];
                    if (Mathf.Abs(p.x - lx) > RenderedCornerReach
                        || Mathf.Abs(p.z - lz) > RenderedCornerReach)
                        continue;
                    hits++;
                    if (p.y > topY) topY = p.y;
                }

                int nx = kv.Key.X * cs + lx;
                int nz = kv.Key.Z * cs + lz;
                long key = RenderedCornerKey(nx, nz);
                if (!nodes.TryGetValue(key, out RenderedCornerNode node))
                {
                    node.Nx = nx;
                    node.Nz = nz;
                    node.MinTopY = float.MaxValue;
                    node.MaxTopY = float.MinValue;
                }

                if (hits > 0)
                {
                    node.Present++;
                    if (topY < node.MinTopY) node.MinTopY = topY;
                    if (topY > node.MaxTopY) node.MaxTopY = topY;

                    // The chunk's OWN lattice at the same local corner. NaN when absent (voxel
                    // chunks have no lattice) — skip rather than report a fake mismatch.
                    float lat = c.LatticeY(lx, lz);
                    if (!float.IsNaN(lat))
                    {
                        float d = Mathf.Abs(lat - topY);
                        if (d > node.WorstDelta)
                        {
                            node.WorstDelta = d;
                            node.WorstDeltaChunk = kv.Key;
                        }
                    }
                }
                nodes[key] = node;
            }
        }

        if (inactive > 0)
            sb.Append("  (inactive ").Append(inactive).Append(')');
        if (noMesh > 0)
            sb.Append("  (noMesh ").Append(noMesh).Append(')');
        sb.Append('\n');

        // Second pass: how many chunks SHOULD be at each node, then the two disagreements. Read-only
        // walk — nothing is written back into the dictionary here, so no enumerator can be
        // invalidated mid-loop.
        int shortCorners = 0;
        int spreadCorners = 0;
        float worstSpread = 0f;
        float worstDelta = 0f;
        string worstSpreadAt = "-";
        string worstDeltaAt = "-";
        int presentTotal = 0;
        var listed = new List<string>();

        foreach (KeyValuePair<long, RenderedCornerNode> kv in nodes)
        {
            RenderedCornerNode n = kv.Value;
            int expected = LoadedChunksTouchingCorner(n.Nx, n.Nz);
            presentTotal += n.Present;

            string where = "(" + n.Nx + "," + n.Nz + ")";
            if (n.Present < expected)
            {
                shortCorners++;
                if (listed.Count < RenderedCornerMaxListed)
                    listed.Add("no corner vertex " + where + "  " + n.Present + "/" + expected
                        + " chunks");
            }

            // A node with fewer than two contributors cannot disagree with anyone, and a lone
            // contributor's Min/Max are the same vertex, so both are skipped by construction.
            if (n.Present >= 2)
            {
                float spread = n.MaxTopY - n.MinTopY;
                if (spread > RenderedCornerTolerance)
                {
                    spreadCorners++;
                    if (spread > worstSpread)
                    {
                        worstSpread = spread;
                        worstSpreadAt = where;
                    }
                }
            }

            if (n.WorstDelta > RenderedCornerTolerance && n.WorstDelta > worstDelta)
            {
                worstDelta = n.WorstDelta;
                worstDeltaAt = n.WorstDeltaChunk + where;
            }
        }

        sb.Append("A corners  nodes ").Append(nodes.Count)
          .Append("  rendered ").Append(presentTotal).Append('/').Append(corners)
          .Append("  no-vertex nodes ").Append(shortCorners)
          .Append("  cross-chunk dY ").Append(worstSpread.ToString("0.####"));
        if (spreadCorners > 0)
            sb.Append(" (").Append(spreadCorners).Append(" nodes)");
        sb.Append("  own-lattice dY ").Append(worstDelta.ToString("0.####"))
          .Append('\n');
        if (worstSpread > RenderedCornerTolerance)
            sb.Append("    worst cross-chunk at ").Append(worstSpreadAt).Append('\n');
        if (worstDelta > RenderedCornerTolerance)
            sb.Append("    worst own-lattice at ").Append(worstDeltaAt).Append('\n');
        for (int i = 0; i < listed.Count; i++)
            sb.Append("    ").Append(listed[i]).Append('\n');

        // B. build fingerprint: distinct (facet step, vertex count) buckets. More than one bucket is a
        // resident set built by more than one version of the generator. The step-drift count catches
        // the OTHER staleness shape, where every resident chunk is uniformly OLD: nothing streams in,
        // so there is only one bucket, but the step it was built at is not the step the generator is
        // configured for now. That is the "I changed the knob and the world did not change" symptom.
        var buckets = new Dictionary<string, int>(8);
        int stepDrift = 0;
        int stepDriftCounted = 0;
        int wantStep = EffectiveLowPolyStep;
        foreach (KeyValuePair<TerrainChunkCoord, ChunkObject> kv in _loadedChunks)
        {
            ChunkObject c = kv.Value;
            if (c == null)
                continue;
            Mesh mesh = c.RootMesh;
            string label = "step " + c.MeshStep + " verts " + (mesh != null ? mesh.vertexCount : -1);
            int seen;
            buckets.TryGetValue(label, out seen);
            buckets[label] = seen + 1;

            // Voxel chunks render a stepped mesh from their own path and carry no facet step, so
            // they are not drift candidates.
            if (!VoxelTerrainEnabled && !c.VoxelMesh)
            {
                stepDriftCounted++;
                if (c.MeshStep != wantStep)
                    stepDrift++;
            }
        }

        sb.Append("B fingerprint  buckets ").Append(buckets.Count);
        int printed = 0;
        foreach (KeyValuePair<string, int> kv in buckets)
        {
            if (printed >= RenderedCornerMaxListed)
            {
                sb.Append("  ...");
                break;
            }
            sb.Append("  [").Append(kv.Key).Append(" x").Append(kv.Value).Append(']');
            printed++;
        }
        if (stepDriftCounted > 0)
            sb.Append("  stepDrift ").Append(stepDrift).Append('/').Append(stepDriftCounted)
              .Append(" (want step ").Append(wantStep).Append(')');
        sb.Append('\n');

        string verdict = null;
        if (shortCorners > 0)
            verdict = "R1: " + shortCorners + " world corner node(s) with no rendered vertex on at "
                + "least one chunk — the surface does not reach the chunk corner";
        else if (worstSpread > RenderedCornerTolerance)
            verdict = "R2: rendered corner heights disagree ACROSS chunks by " + worstSpread.ToString("0.####")
                + " m at " + worstSpreadAt + " — the render path is not sharing one corner height";
        else if (worstDelta > RenderedCornerTolerance)
            verdict = "R2: rendered corners disagree with their OWN lattice by " + worstDelta.ToString("0.####")
                + " m (" + worstDeltaAt + ") — the render path copies the corner from the wrong place";
        else if (buckets.Count > 1)
            verdict = "R-stale: the loaded set has " + buckets.Count + " vertex-count buckets — it was "
                + "built by more than one version of the generator; needs a full resident drop (rule 11)";
        else if (stepDrift > 0)
            verdict = "R-stale: " + stepDrift + " of " + stepDriftCounted + " loaded chunk(s) were built "
                + "at a facet step the generator is no longer configured for (want " + wantStep
                + ") — the resident terrain predates the current settings; needs a full drop (rule 11)";
        else if (noMesh > 0)
            verdict = "R: " + noMesh + " loaded chunk(s) have no root mesh (see the seam audit's B section)";

        sb.Append("VERDICT ").Append(verdict
            ?? "clean: every chunk corner is rendered, at one height, matching its own lattice");
        return sb.ToString();
    }

    /// <summary>Unique key for a world corner node. nx/nz are multiples of 30, so packing the two
    /// 32-bit halves is collision-free including for negative coordinates (two's complement).</summary>
    private static long RenderedCornerKey(int nx, int nz) => ((long)nx << 32) | (uint)nz;

    /// <summary>
    /// How many LOADED chunks meet at the world corner node (nx,nz). A node on a chunk boundary is
    /// shared by up to four chunks: the one whose min edge it is (n/30) and the one whose max edge it
    /// is (n/30 - 1) on each axis. Integer division is exact here because nx/nz are multiples of
    /// <see cref="TerrainChunkCoord.ChunkSize"/>, and C# truncates toward zero, so a negative node
    /// still names the right pair.
    /// </summary>
    private int LoadedChunksTouchingCorner(int nx, int nz)
    {
        int bx = nx / TerrainChunkCoord.ChunkSize;
        int bz = nz / TerrainChunkCoord.ChunkSize;
        int n = 0;
        for (int dz = 0; dz <= 1; dz++)
            for (int dx = 0; dx <= 1; dx++)
                if (_loadedChunks.ContainsKey(new TerrainChunkCoord(bx - dx, bz - dz)))
                    n++;
        return n;
    }
}

using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>
/// Rendered-corner + coverage audit (1hy) — the measurement pass behind "in every chunk corner the
/// edge will not match, the player can see the void through that gap", re-added after 1hx removed
/// the F2/F3/F4 lanes along with the 3 -&gt; 6 facet step.
///
/// <para><b>What the user reported, and why "gap" is the load-bearing word.</b> A crack that is a
/// few centimetres tall is a HEIGHT disagreement — two surfaces that should coincide sit at slightly
/// different Y, and you see a slit. The report is the other thing: a hole with nothing behind it.
/// Those have disjoint causes, and the static pass found no defect in the corner arithmetic, so the
/// audit is built to separate four candidates that look identical by eye:
///   A. The resident set was built by more than one version of the generator (rule 11), so what is on
///      screen is not what the code says is on screen. This is the PREMISE test for everything else:
///      if the drawn meshes are not all the audited surface, sections B and C describe a world that
///      does not exist, so it is checked and reported FIRST.
///   B. A footprint inside the band the streamer owns has NEITHER a visible real chunk NOR a live
///      far cell — a genuine void, produced by a lifecycle (dormant, unload, far-shell shadow) that
///      nobody covered. This is the direct test of "a gap", and the only one that can return a hole
///      in a chunk set that is otherwise perfectly watertight.
///   C. The corner layer — does every loaded chunk place a rendered vertex AT each of its four chunk
///      corners (R1 coverage), and do the chunks meeting at a world node agree on the corner HEIGHT
///      (R2). Rule 8 is explicit that a green lattice is not evidence about what is drawn: a render
///      path that copies its corner from anywhere other than the lattice it stamps reports "worst dY
///      0 OK" and still parts at the corner.
/// </para>
///
/// <para><b>Why B scans the band it does.</b> The void walk covers rings
/// <c>0 .. view + FarOuterKeep</c>, which is exactly the union of what the two owners promise — real
/// chunks out to <c>view + 1</c> (the hysteresis keep) and far cells from <c>near + 1</c> to
/// <c>view + FarOuterKeep</c>. That band is covered by construction and must therefore never contain
/// a hole, which makes it the only place a "void" readout means something. It deliberately does NOT
/// re-derive <c>ChunkLodManager.EffectiveCullDistance</c>: that formula is private, and a second
/// spelling of it here would be a copy that rots the moment the LOD side changes (rule 8). Footprints
/// past the band are invisible by design — the real chunks there are DORMANT and hidden and no far
/// cell owns them, so including them would print a ring of "voids" at 630 m that is correct, expected
/// and not what the player is looking at. The band is printed so the number cannot be misread.
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
    /// factor.</summary>
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
        public int PristineHits;   // contributors whose height EQUALS untouched world noise here
        public int EditedHits;     // contributors whose height differs from it
        public int ModifiedChunks; // contributors whose chunk carries any saved tile mod
        public int PristineFromModified; // 1i7: contributors that are BOTH pristine and modified —
                                         // the signature of a mod's stored pre-edit corner being
                                         // stamped over a shared node, which is 1i6's short-circuit
                                         // rather than a missing owner.
    }

    /// <summary>
    /// One multi-line report of the resident terrain's DRAWN state, in the order the questions must
    /// be asked:
    ///   A. fingerprint — distinct (facet step, vertex count) buckets across the loaded set, plus
    ///                   step drift. More than one bucket means two versions of the generator built
    ///                   this world, which voids B and C as evidence (rule 11).
    ///   B. void      — footprints in the fully-owned ring with nothing drawing them, nearest first.
    ///   C. corners   — R1 coverage at each world corner node, then cross-chunk and own-lattice R2.
    /// A VERDICT line names the first thing that failed, with the offending world XZ, so one
    /// screenshot plus one walk to that XZ answers the question.
    /// </summary>
    public string RenderedCornerAudit()
    {
        var sb = new StringBuilder(1024);
        int view = RenderDistance != null ? RenderDistance.Radius : 3;
        int near = Mathf.Min(Mathf.Max(NearRingRadius, 0), view);
        TerrainChunkCoord centre = _focus != null
            ? TerrainChunkCoord.FromWorld(_focus.position)
            : new TerrainChunkCoord(0, 0);

        sb.Append("RENDERED CORNER + VOID AUDIT  loaded ").Append(_loadedChunks.Count)
          .Append("  dormant ").Append(_dormantChunks.Count)
          .Append("  far cells ").Append(_farSectors.Count)
          .Append("  step ").Append(EffectiveLowPolyStep)
          .Append("  focus chunk ").Append(centre.X).Append(',').Append(centre.Z);

        Dictionary<string, int> buckets;
        int stepDrift, stepDriftCounted, noMesh;
        Dictionary<int, int> stamps;
        AppendFingerprint(sb, out buckets, out stepDrift, out stepDriftCounted, out noMesh,
            out stamps);

        int voidCount, voidClaimedDead, voidUnowned;
        int nearestVoidRing;
        AppendVoidWalk(sb, centre, near, view, out voidCount, out nearestVoidRing,
            out voidClaimedDead, out voidUnowned);

        float worstSpread, worstDelta;
        string worstSpreadAt, worstDeltaAt;
        int shortCorners;
        int crossOneSided, crossBothEdited, crossNeitherEdited;
        string crossOneSidedAt;
        Dictionary<long, RenderedCornerNode> nodes;
        AppendCornerPass(sb, out shortCorners, out worstSpread, out worstSpreadAt,
            out worstDelta, out worstDeltaAt, out crossOneSided, out crossBothEdited,
            out crossNeitherEdited, out crossOneSidedAt, out nodes);

        // 1i1. C compares real chunks against real chunks, so it is blind to the seam the player
        // actually reports: the near/far boundary, where the outer ring of loaded chunks meets a
        // far cell. Section D reads the far cell's own mesh and compares the two surfaces.
        int bndNodes, bndNoFar, bndOneSided, bndBothEdited, bndNeither;
        float worstRealFar;
        string worstRealFarAt;
        AppendBoundaryPass(sb, centre, near, view, nodes, out bndNodes, out bndNoFar,
            out worstRealFar, out worstRealFarAt, out bndOneSided, out bndBothEdited,
            out bndNeither);

        string verdict = null;
        if (buckets.Count > 1)
            verdict = "A-stale: the loaded set has " + buckets.Count + " vertex-count buckets — it was "
                + "built by more than one version of the generator, so B and C below do not describe "
                + "what is on screen; needs a full resident drop (rule 11)";
        else if (stepDrift > 0)
            verdict = "A-stale: " + stepDrift + " of " + stepDriftCounted + " loaded chunk(s) were built "
                + "at a facet step the generator is no longer configured for (want " + EffectiveLowPolyStep
                + ") — the resident terrain predates the current settings; needs a full drop (rule 11)";
        else if (voidClaimedDead > 0)
            verdict = "B: " + voidClaimedDead + " footprint(s) are CLAIMED by a far cell but have no live "
                + "cell drawing them - the shell owns ground it is not rendering, which is a true hole. "
                + "Nearest is ring " + nearestVoidRing + "; the listed XZ are the coordinates to walk to";
        else if (voidUnowned > 0)
            verdict = "B-band: " + voidUnowned + " footprint(s) have no live far cell AND no far cell "
                + "claims them - the far shell does not reach as far as the owned ring (keep "
                + (view + FarOuterKeep) + "), so this is a shorter horizon rather than a missing "
                + "surface. Nearest is ring " + nearestVoidRing;
        else if (bndNoFar > 0)
            verdict = "D-R0: " + bndNoFar + " near/far boundary node(s) have no live far cell on their "
                + "far side — the shell does not reach back to the loaded ring, so the boundary is a "
                + "true edge with nothing beyond it";
        else if (worstRealFar > 2f * RenderedCornerTolerance)
            verdict = "D-R1: the near/far boundary steps by " + worstRealFar.ToString("0.####")
                + " m at " + worstRealFarAt + " (worst of " + bndNodes + " boundary node(s)) — a loaded "
                + "chunk's corner and the far cell's grid disagree on the height of the SAME world "
                + "point, which is a see-through crack because neither surface has a wall"
                + CrossCause(bndOneSided, bndBothEdited, bndNeither, null);
        else if (shortCorners > 0)
            verdict = "C-R1: " + shortCorners + " world corner node(s) with no rendered vertex on at "
                + "least one chunk — the surface does not reach the chunk corner";
        else if (worstSpread > RenderedCornerTolerance)
            verdict = "C-R2: rendered corner heights disagree ACROSS chunks by "
                + worstSpread.ToString("0.####") + " m at " + worstSpreadAt
                + " — the render path is not sharing one corner height"
                + CrossCause(crossOneSided, crossBothEdited, crossNeitherEdited, crossOneSidedAt);
        else if (worstDelta > RenderedCornerTolerance)
            verdict = "C-R2: rendered corners disagree with their OWN lattice by "
                + worstDelta.ToString("0.####") + " m (" + worstDeltaAt
                + ") — the render path copies the corner from the wrong place";
        else if (noMesh > 0)
            verdict = "C: " + noMesh + " loaded chunk(s) have no root mesh at all";

        sb.Append("VERDICT ").Append(verdict
            ?? "clean: one generator version, every footprint in the owned band draws something, every "
             + "chunk corner is rendered at one height matching its own lattice, and the near/far "
             + "boundary is flush to " + worstRealFar.ToString("0.####") + " m");
        return sb.ToString();
    }

    /// <summary>
    /// Section A. Distinct (facet step, vertex count) buckets across the loaded set. More than one
    /// bucket is a resident set built by more than one version of the generator, which is the one
    /// mechanism this repo can create on demand and which is indistinguishable from a renderer bug by
    /// eye. The step-drift count catches the OTHER staleness shape, where every resident chunk is
    /// uniformly OLD: nothing streams in, so there is only one bucket, but the step it was built at
    /// is not the step the generator is configured for now — the "I changed the knob and the world
    /// did not change" symptom.
    /// </summary>
    private void AppendFingerprint(StringBuilder sb, out Dictionary<string, int> buckets,
        out int stepDrift, out int stepDriftCounted, out int noMesh,
        out Dictionary<int, int> stamps)
    {
        buckets = new Dictionary<string, int>(8);
        stamps = new Dictionary<int, int>(4);
        stepDrift = 0;
        stepDriftCounted = 0;
        noMesh = 0;
        int wantStep = EffectiveLowPolyStep;

        foreach (KeyValuePair<TerrainChunkCoord, ChunkObject> kv in _loadedChunks)
        {
            ChunkObject c = kv.Value;
            if (c == null)
                continue;
            Mesh mesh = c.RootMesh;
            if (mesh == null)
                noMesh++;
            // 1i7: bucket by GENERATOR REVISION as well as by shape. A render-algorithm edit leaves
            // every already-loaded chunk on the old geometry (AGENTS rule 11), so a world that has
            // not been restarted is a mix of two revisions that looks seamless and measures like a
            // broken generator. Every later section is a claim about the resident set, so this has
            // to be readable before them, not inferred afterwards.
            string label = "stamp " + c.BuildStamp + " step " + c.MeshStep
                + " verts " + (mesh != null ? mesh.vertexCount : -1);
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

        sb.Append("\nA fingerprint  buckets ").Append(buckets.Count);
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
        if (noMesh > 0)
            sb.Append("  noMesh ").Append(noMesh);

        // 1i7: name the build-mix explicitly. One stamp means every loaded chunk came from the code
        // now running; two or more means the resident set straddles a generator edit, and then NO
        // section below describes the world a fresh session would build.
        stamps.Clear();
        foreach (KeyValuePair<TerrainChunkCoord, ChunkObject> kv in _loadedChunks)
        {
            ChunkObject c = kv.Value;
            if (c == null)
                continue;
            stamps.TryGetValue(c.BuildStamp, out int n);
            stamps[c.BuildStamp] = n + 1;
        }
        if (stamps.Count == 0)
        {
            sb.Append("  buildStamp none");
        }
        else if (stamps.Count == 1)
        {
            foreach (KeyValuePair<int, int> kv in stamps)
                sb.Append("  buildStamp ").Append(kv.Key).Append(" x").Append(kv.Value)
                  .Append(" (uniform: resident set matches the code now running)");
        }
        else
        {
            sb.Append("  buildStamp MIXED:");
            foreach (KeyValuePair<int, int> kv in stamps)
                sb.Append(' ').Append(kv.Key).Append(" x").Append(kv.Value);
            sb.Append("  <-- sections B/C/D span two generator revisions; restart before trusting them");
        }
        sb.Append('\n');
    }

    /// <summary>
    /// Section B — the direct test of "a gap". Walks every chunk footprint in the band the streamer
    /// and the far shell jointly own (rings <c>0 .. view + FarOuterKeep</c>) and asks a single
    /// question per footprint: does ANYTHING draw it? A real chunk counts when it is in
    /// <c>_loadedChunks</c> and its hierarchy is active; a far cell counts when the cell that owns
    /// the footprint exists, is active, and is not shadowed by a live coarser cell (1er retention).
    /// A footprint with neither is a hole the player can see the void through, and that is the only
    /// failure mode that can open in an otherwise watertight chunk set.
    ///
    /// <para>The root-renderer state is reported separately rather than folded into the test: a LOD
    /// detail band deliberately disables the root mesh and draws a child mesh instead, so a disabled
    /// root is not a void. It is counted as its own signal because a root that is disabled with no
    /// band active would be.</para>
    ///
    /// <para><b>Reading a positive result.</b> The far shell fills asynchronously (its initial fill
    /// is documented at roughly 1.5-4 s, and it coasts in the background), so a report taken in the
    /// first moments after a teleport can legitimately show the OUTER edge of the band not drawn yet
    /// — those are reported at the highest rings only. A void anywhere inside the near ring, or one
    /// that persists once the world has settled, is not that.</para>
    /// </summary>
    private void AppendVoidWalk(StringBuilder sb, TerrainChunkCoord centre, int near, int view,
        out int voidCount, out int nearestVoidRing, out int claimedDeadOut, out int unownedOut)
    {
        int keepFar = view + FarOuterKeep;
        int maxRing = keepFar;
        int total = 0, realOnly = 0, farOnly = 0, both = 0, unowned = 0, claimedDead = 0, rootHidden = 0;
        var voids = new List<TerrainChunkCoord>();

        for (int z = centre.Z - maxRing; z <= centre.Z + maxRing; z++)
        {
            for (int x = centre.X - maxRing; x <= centre.X + maxRing; x++)
            {
                if (Mathf.Max(Mathf.Abs(x - centre.X), Mathf.Abs(z - centre.Z)) > maxRing)
                    continue;
                total++;

                bool realOk = false;
                if (_loadedChunks.TryGetValue(new TerrainChunkCoord(x, z), out ChunkObject c)
                    && c != null && c.gameObject.activeInHierarchy)
                {
                    realOk = true;
                    MeshRenderer mr = c.GetComponent<MeshRenderer>();
                    if (mr == null || !mr.enabled)
                        rootHidden++;
                }

                // 1i2: ask the DIRECT question - is ANY far cell active over this footprint - rather
                // than running the ownership predicate and then also demanding its cell be switched
                // on. Those are different things. FarShadowedByCoarse exists precisely because the
                // owning cell is usually a reserved shadow while a COARSER live cell covers the same
                // ground (1eq/1er demote handoff), so requiring !FarShadowedByCoarse reported every
                // such footprint as a void. The 1i1 readout turned that into 2965 false positives -
                // the same rule-7 mistake 1hy made, one level in: the walk admitted ONE family of
                // owner and called everything else absent.
                FarCell? owner = FarCellForChunk(x, z, centre, near, keepFar);
                bool farOk = AnyFarCellDrawsAt(x, z);

                if (realOk && farOk) both++;
                else if (realOk) realOnly++;
                else if (farOk) farOnly++;
                else
                {
                    // Two different failures, and conflating them is what made the previous report
                    // unreadable. Claimed-dead is a hole: the shell says it owns this ground and
                    // nothing is drawing it. Unowned is a band that stops short of the promised
                    // radius - worth knowing, but not a missing surface.
                    if (owner.HasValue)
                        claimedDead++;
                    else
                        unowned++;
                    voids.Add(new TerrainChunkCoord(x, z));
                }
            }
        }

        voidCount = voids.Count;
        claimedDeadOut = claimedDead;
        unownedOut = unowned;
        nearestVoidRing = int.MaxValue;
        if (voidCount > 0)
        {
            // Nearest first: the player is standing at the focus, so the closest void is the one
            // they can walk to and look at.
            voids.Sort((a, b) =>
            {
                int ra = Mathf.Max(Mathf.Abs(a.X - centre.X), Mathf.Abs(a.Z - centre.Z));
                int rb = Mathf.Max(Mathf.Abs(b.X - centre.X), Mathf.Abs(b.Z - centre.Z));
                if (ra != rb)
                    return ra - rb;
                if (a.Z != b.Z)
                    return a.Z - b.Z;
                return a.X - b.X;
            });
            nearestVoidRing = Mathf.Max(
                Mathf.Abs(voids[0].X - centre.X), Mathf.Abs(voids[0].Z - centre.Z));
        }

        sb.Append("B void  ring 0..").Append(maxRing)
          .Append("  footprints ").Append(total)
          .Append("  real+far ").Append(both)
          .Append("  real only ").Append(realOnly)
          .Append("  far only ").Append(farOnly)
          .Append("  NOT DRAWN ").Append(voidCount);
        if (claimedDead > 0)
            sb.Append("  (claimed-dead ").Append(claimedDead)
              .Append(" = the shell claims this ground and NOTHING is drawing it: a real hole)");
        if (unowned > 0)
            sb.Append("  (unowned ").Append(unowned)
              .Append(" = no far cell claims this; the band stops short of the promised radius)");
        if (rootHidden > 0)
            sb.Append("  rootHidden ").Append(rootHidden);
        sb.Append('\n');
        for (int i = 0; i < voids.Count && i < RenderedCornerMaxListed; i++)
        {
            int wx = voids[i].X * TerrainChunkCoord.ChunkSize;
            int wz = voids[i].Z * TerrainChunkCoord.ChunkSize;
            sb.Append("    not drawn chunk (").Append(voids[i].X).Append(',').Append(voids[i].Z)
              .Append(")  world (").Append(wx).Append(',').Append(wz).Append(") -> ")
              .Append(wx).Append(' ').Append(wz).Append('\n');
        }
    }

    /// <summary>
    /// Section C. For every loaded chunk, locate a rendered vertex within
    /// <see cref="RenderedCornerReach"/> of each of its four mesh-local corners, then compare the
    /// chunks that meet at each world node.
    /// </summary>
    private void AppendCornerPass(StringBuilder sb, out int shortCorners, out float worstSpread,
        out string worstSpreadAt, out float worstDelta, out string worstDeltaAt,
        out int crossOneSided, out int crossBothEdited, out int crossNeitherEdited,
        out string crossOneSidedAt, out Dictionary<long, RenderedCornerNode> nodesOut)
    {
        var nodes = new Dictionary<long, RenderedCornerNode>(256);
        int corners = 0;
        int inactive = 0;
        int noMesh = 0;
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
                // A loaded chunk with no mesh contributes no corners, so the coverage pass would
                // otherwise be silently short exactly one chunk's worth of corners. Count it.
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
                    if (p.y > topY)
                        topY = p.y;
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
                    if (topY < node.MinTopY)
                        node.MinTopY = topY;
                    if (topY > node.MaxTopY)
                        node.MaxTopY = topY;

                    // Does this contributor's height equal UNTOUCHED world noise at the node? This is
                    // the discriminator between the two families of cause for a cross-chunk
                    // disagreement, and it is exact rather than inferred: a one-sided edit leaves
                    // one chunk on the pristine value and the other on the edited one, whereas a
                    // structural cause (wrong owner slot, different seed) would move BOTH off the
                    // pristine value or leave both exactly on it. A disagreement where one side sits
                    // on pristine noise is therefore proof that only one side was written.
                    bool isPristine = Mathf.Abs(topY - TerrainNoiseGenerator.GetHeight(Seed, nx, nz))
                        <= RenderedCornerTolerance;
                    if (isPristine)
                        node.PristineHits++;
                    else
                        node.EditedHits++;

                    bool isModified = _modifiedChunks.Contains(kv.Key);
                    if (isModified)
                        node.ModifiedChunks++;

                    // 1i7. "one-sided edit" alone does not say WHY the pristine side stayed
                    // pristine, and the two candidates need opposite fixes:
                    //  - The pristine side is itself a MODIFIED chunk. It holds a mod, so its build
                    //    stamped this node from that mod's stored corner - which is the tile's
                    //    PRE-EDIT height - and then 1i6's `continue` short-circuited the seam walk
                    //    because the corner was no longer NaN. The edit was available; the
                    //    short-circuit refused to look.
                    //  - The pristine side is NOT modified. Nothing stamped it, so the seam walk
                    //    either never ran (stale mesh) or ran and failed to find the edit (save
                    //    not flushed, or the wrong owner consulted).
                    // Those are different bugs, and a count that merges them can only be argued
                    // about, so they are counted separately.
                    if (isPristine && isModified)
                        node.PristineFromModified++;

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
            sb.Append("    (inactive loaded ").Append(inactive).Append(')');
        if (noMesh > 0)
            sb.Append("    (noMesh ").Append(noMesh).Append(')');
        sb.Append('\n');

        // Second pass: how many chunks SHOULD be at each node, then the two disagreements. Read-only
        // walk — nothing is written back into the dictionary here, so no enumerator can be
        // invalidated mid-loop.
        shortCorners = 0;
        int spreadCorners = 0;
        worstSpread = 0f;
        worstDelta = 0f;
        worstSpreadAt = "-";
        worstDeltaAt = "-";
        int presentTotal = 0;
        var listed = new List<string>();

        // Classification of the disagreeing nodes (1i0). The mechanism is named, not guessed.
        int oneSidedEdit = 0, bothEdited = 0, neitherEdited = 0;
        int oneSidedStampClobber = 0;   // 1i7: the pristine side is itself modified, so a mod's
                                        // stored pre-edit corner was stamped over the node.
        int oneSidedNoOwner = 0;        // 1i7: the pristine side is untouched, so no stamp happened.
        string oneSidedAt = "-";
        string stampClobberAt = "-";
        foreach (KeyValuePair<long, RenderedCornerNode> kv in nodes)
        {
            RenderedCornerNode n = kv.Value;
            if (n.Present < 2 || (n.MaxTopY - n.MinTopY) <= RenderedCornerTolerance)
                continue;
            if (n.PristineHits > 0 && n.EditedHits > 0)
            {
                oneSidedEdit++;
                if (oneSidedAt == "-")
                    oneSidedAt = "(" + n.Nx + "," + n.Nz + ")";
                if (n.PristineFromModified > 0)
                {
                    oneSidedStampClobber++;
                    if (stampClobberAt == "-")
                        stampClobberAt = "(" + n.Nx + "," + n.Nz + ")";
                }
                else
                {
                    oneSidedNoOwner++;
                }
            }
            else if (n.PristineHits == 0)
                bothEdited++;
            else
                neitherEdited++;
        }
        crossOneSided = oneSidedEdit;
        crossBothEdited = bothEdited;
        crossNeitherEdited = neitherEdited;
        crossOneSidedAt = oneSidedAt;

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

        sb.Append("C corners  nodes ").Append(nodes.Count)
          .Append("  rendered ").Append(presentTotal).Append('/').Append(corners)
          .Append("  no-vertex nodes ").Append(shortCorners)
          .Append("  cross-chunk dY ").Append(worstSpread.ToString("0.####"));
        if (spreadCorners > 0)
            sb.Append(" (").Append(spreadCorners).Append(" nodes)");
        sb.Append("  own-lattice dY ").Append(worstDelta.ToString("0.####")).Append('\n');
        if (worstSpread > RenderedCornerTolerance)
            sb.Append("    worst cross-chunk at ").Append(worstSpreadAt)
                .Append(CrossCause(crossOneSided, crossBothEdited, crossNeitherEdited, crossOneSidedAt))
                .Append("  [1i7 split: stamp-clobbered ").Append(oneSidedStampClobber)
                .Append(" (first at ").Append(stampClobberAt)
                .Append(") no-owner ").Append(oneSidedNoOwner).Append(']')

              .Append('\n');
        if (worstDelta > RenderedCornerTolerance)
            sb.Append("    worst own-lattice at ").Append(worstDeltaAt).Append('\n');
        for (int i = 0; i < listed.Count; i++)
            sb.Append("    ").Append(listed[i]).Append('\n');

        nodesOut = nodes;
    }

    /// <summary>
    /// Section D (1i1). The near/far boundary. Section C is blind here by construction: it walks
    /// <c>_loadedChunks</c> only, so the seam where the outer ring of loaded chunks meets a far
    /// cell — which is exactly where the player reports the gap — is never compared against
    /// anything. This reads the far cell's own uploaded mesh and compares the two surfaces at the
    /// same world point.
    ///
    /// A boundary node is one where at least one of the four chunks touching it is NOT loaded, so
    /// the far side of that node is the far shell's to draw. The far cell is located with the
    /// renderer's own <c>FarCellForChunk</c> and read at world minus the cell's block origin
    /// (<c>cell.X*30</c>, per <c>CreateFarSector</c>), which is the same local-origin convention a
    /// real chunk uses — so no second spelling of the placement is introduced here (rule 7).
    /// </summary>
    private void AppendBoundaryPass(StringBuilder sb, TerrainChunkCoord centre, int near, int view,
        Dictionary<long, RenderedCornerNode> nodes, out int bndNodes, out int bndNoFar,
        out float worstRealFar, out string worstRealFarAt, out int bndOneSided,
        out int bndBothEdited, out int bndNeither)
    {
        int keepFar = view + FarOuterKeep;
        int cs = TerrainChunkCoord.ChunkSize;
        bndNodes = 0;
        bndNoFar = 0;
        worstRealFar = 0f;
        worstRealFarAt = "-";
        int oneSided = 0, bothEdited = 0, neitherEdited = 0, noiseFloor = 0;
        string oneSidedAt = "-";
        var missing = new List<string>();

        foreach (KeyValuePair<long, RenderedCornerNode> kv in nodes)
        {
            RenderedCornerNode n = kv.Value;
            if (n.Present == 0)
                continue;

            // A node is on the near/far boundary exactly when a loaded chunk does not occupy all
            // four quadrants. LoadedChunksTouchingCorner is the renderer-side count of the loaded
            // quadrants; 4 means purely real-vs-real, which is section C's business.
            if (LoadedChunksTouchingCorner(n.Nx, n.Nz) >= 4)
                continue;
            bndNodes++;

            float farY;
            if (!TryReadFarHeight(n.Nx, n.Nz, centre, near, keepFar, out farY))
            {
                bndNoFar++;
                if (missing.Count < RenderedCornerMaxListed)
                    missing.Add("no far surface at (" + n.Nx + "," + n.Nz + ")");
                continue;
            }

            // Compare against the far edge of the real surface. On a straight boundary edge the two
            // loaded quadrants agree, so Min==Max; at a square CORNER only one quadrant is loaded
            // and Min==Max too. Taking the larger of the two deltas therefore covers both without
            // caring which shape the node is.
            float dLo = Mathf.Abs(farY - n.MinTopY);
            float dHi = Mathf.Abs(farY - n.MaxTopY);
            float d = Mathf.Max(dLo, dHi);
            if (d > RenderedCornerTolerance && d > worstRealFar)
            {
                worstRealFar = d;
                worstRealFarAt = "(" + n.Nx + "," + n.Nz + ")";
            }
            if (d <= RenderedCornerTolerance)
                continue;

            // 1i2: the pristine test has a WIDTH, so a node can be "stepped" and "both sides
            // pristine" at the same time. Two values each within tol of the same pristine value can
            // differ by up to 2*tol, so anything at or below 2*tol cannot be classified and must
            // not be filed under a mechanism name - the 1i1 readout called 19 such nodes
            // "NO side edited (not an edit - look at the lattice/seed)", which read as a finding
            // and was arithmetic: those nodes are 1-2 cm and are the noise floor of two facets
            // rounded to the same lattice. Only above 2*tol is the classification forced.
            if (d <= 2f * RenderedCornerTolerance)
            {
                noiseFloor++;
                continue;
            }

            // Same pristine test as section C, applied across the seam instead of within it: the
            // far band has no save mods of its own (a dig cannot reach past the near ring), so a far
            // surface sitting exactly on pristine noise against an edited real corner is a corner
            // whose edit never reached the shell.
            bool farPristine = Mathf.Abs(farY - TerrainNoiseGenerator.GetHeight(Seed, n.Nx, n.Nz))
                <= RenderedCornerTolerance;
            bool realPristine = Mathf.Abs(n.MaxTopY
                - TerrainNoiseGenerator.GetHeight(Seed, n.Nx, n.Nz)) <= RenderedCornerTolerance;
            if (farPristine && !realPristine)
            {
                oneSided++;
                if (oneSidedAt == "-")
                    oneSidedAt = "(" + n.Nx + "," + n.Nz + ")";
            }
            else if (!farPristine && !realPristine)
                bothEdited++;
            else
                neitherEdited++;
        }

        bndOneSided = oneSided;
        bndBothEdited = bothEdited;
        bndNeither = neitherEdited;

        sb.Append("D boundary  nodes ").Append(bndNodes)
          .Append("  no-far-surface ").Append(bndNoFar)
          .Append("  real-vs-far dY ").Append(worstRealFar.ToString("0.####"))
          .Append("  (under 2cm noise floor ").Append(noiseFloor).Append(')').Append('\n');
        if (worstRealFar > RenderedCornerTolerance)
            sb.Append("    worst at ").Append(worstRealFarAt)
              .Append(CrossCause(oneSided, bothEdited, neitherEdited, oneSidedAt)).Append('\n');
        for (int i = 0; i < missing.Count; i++)
            sb.Append("    ").Append(missing[i]).Append('\n');
    }

    /// <summary>
    /// Is ANY far cell switched on over this chunk? Deliberately does not consult
    /// <c>FarCellForChunk</c>: the three cell levels that can geometrically cover a chunk are
    /// checked directly, because the ownership predicate and "is anything drawn" answer different
    /// questions. A cell's owner is routinely inactive *on purpose* — <c>FarShadowedByCoarse</c> is
    /// true whenever a live coarser cell already covers the same ground — so asking only about the
    /// owner asks about a shadow and misses the surface. This reads the three levels and stops at
    /// the first live one; it never re-derives the renderer's shadow rule, it only enumerates the
    /// candidates (rule 7).
    /// </summary>
    private bool AnyFarCellDrawsAt(int x, int z)
    {
        if (_farSectors.TryGetValue(new FarCell(x, z, 1), out GameObject g1)
            && g1 != null && g1.activeSelf)
            return true;
        if (_farSectors.TryGetValue(new FarCell(FloorDiv(x, 3) * 3, FloorDiv(z, 3) * 3, 3),
            out GameObject g3) && g3 != null && g3.activeSelf)
            return true;
        if (_farSectors.TryGetValue(new FarCell(FloorDiv(x, 6) * 6, FloorDiv(z, 6) * 6, 6),
            out GameObject g6) && g6 != null && g6.activeSelf)
            return true;
        return false;
    }

    /// <summary>Topmost rendered vertex of the live far cell that owns this world point, read from the
    /// cell's own uploaded mesh. Topmost, for the same reason section C is: a far cell's mesh can
    /// carry side walls on some builds, and the bottom of a wall is not the surface.
    /// </summary>
    private bool TryReadFarHeight(int nx, int nz, TerrainChunkCoord centre, int near, int keepFar,
        out float y)
    {
        y = 0f;
        int cs = TerrainChunkCoord.ChunkSize;

        // Any of the four quadrants will do when it is not loaded — they all describe the same
        // world point — but only an ACTIVE cell is actually drawing, so an inactive one is skipped
        // in favour of a sibling that is live.
        for (int q = 0; q < 4; q++)
        {
            int qx = (q & 1) == 0 ? -1 : 0;
            int qz = (q & 2) == 0 ? -1 : 0;
            TerrainChunkCoord probe =
                new TerrainChunkCoord(FloorDiv(nx, cs) + qx, FloorDiv(nz, cs) + qz);
            if (_loadedChunks.ContainsKey(probe))
                continue;

            FarCell? owner = FarCellForChunk(probe.X, probe.Z, centre, near, keepFar);
            if (!owner.HasValue)
                continue;
            if (!_farSectors.TryGetValue(owner.Value, out GameObject go) || go == null)
                continue;
            if (!go.activeSelf || FarShadowedByCoarse(owner.Value))
                continue;
            MeshFilter mf = go.GetComponent<MeshFilter>();
            Mesh mesh = mf != null ? mf.sharedMesh : null;
            if (mesh == null)
                continue;

            // World minus the cell's block origin, which is the cell's own local frame.
            float lx = nx - owner.Value.X * cs;
            float lz = nz - owner.Value.Z * cs;
            _renderedCornerScan.Clear();
            mesh.GetVertices(_renderedCornerScan);
            float top = float.MinValue;
            for (int v = 0; v < _renderedCornerScan.Count; v++)
            {
                Vector3 p = _renderedCornerScan[v];
                if (Mathf.Abs(p.x - lx) > RenderedCornerReach
                    || Mathf.Abs(p.z - lz) > RenderedCornerReach)
                    continue;
                if (p.y > top)
                    top = p.y;
            }
            if (top == float.MinValue)
                continue;
            // The far GameObject sits at y=0, so local height is world height.
            y = top;
            return true;
        }
        return false;
    }

    /// <summary>Names the MECHANISM behind a cross-chunk corner disagreement from how its
    /// contributors sit relative to pristine world noise. Read straight off the counts, never
    /// guessed: a node with one contributor exactly on untouched noise and another off it can only
    /// be a corner that was written on ONE side — the other chunk's tile still holds the pristine
    /// value because nothing propagated across the chunk seam. If instead every contributor is off
    /// pristine, the cause is not an edit at all and the edit path is not where to look.</summary>
    private static string CrossCause(int oneSided, int bothEdited, int neitherEdited, string oneSidedAt)
    {
        int total = oneSided + bothEdited + neitherEdited;
        if (total == 0)
            return "";
        string m = "  [cause of those " + total + " node(s):";
        if (oneSided > 0)
            m += " one-sided edit " + oneSided + " (first at " + oneSidedAt + ")";
        if (bothEdited > 0)
            m += " ALL sides edited apart " + bothEdited;
        if (neitherEdited > 0)
            m += " NO side edited " + neitherEdited + " (not an edit - look at the lattice/seed)";
        return m + "]";
    }

    /// <summary>Unique key for a world corner node. nx/nz are multiples of 30, so packing the two
    /// 32-bit halves is collision-free including for negative coordinates (two's complement).</summary>
    private static long RenderedCornerKey(int nx, int nz) => ((long)nx << 32) | (uint)nz;

    /// <summary>
    /// How many LOADED chunks meet at the world corner node (nx,nz). A node on a chunk boundary is
    /// shared by up to four chunks: the one whose min edge it is (n/30) and the one whose max edge
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

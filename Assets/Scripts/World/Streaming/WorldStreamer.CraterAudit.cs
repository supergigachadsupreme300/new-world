using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>
/// Crater/deform audit (1in) — the measurement pass behind "the dent is rendered by stretching the
/// surface of the surrounding tiles, it doesn't bring the crashed feeling, and it is hard to deform
/// even more".
///
/// <para><b>What the user reported.</b> A projectile/Earth/tool dent does not read as material being
/// displaced. It reads as the GROUND AROUND THE IMPACT SINKING — neighbouring tiles tilting toward
/// the hit point — and it has no rim, no floor and no walls. "Stretching the surface of nearby
/// tiles" is the load-bearing phrase: it is a statement about which mesh carries the shape, not
/// about how deep the shape is.</para>
///
/// <para><b>Why a static read of the code cannot close this.</b> The deformation path is small
/// enough to read end to end, and reading it says a crater writes per-corner elevations on the 1 m
/// lattice (<c>WorldStreamer.Deform.cs</c> Crater branch: <c>target = current - s * CraterStep</c>
/// with a smoothstepped cone). But three separate mechanisms decide whether that becomes the
/// picture the player is looking at, and each can silently be NOT the one on screen:
///   <b>(a)</b> whether the carve reaches a node the render path actually samples (1hx moved the
///   facet step 3 -&gt; 6 and the dent silently stopped existing — the FX still played, so the
///   tell was an absent carve, not a broken one);
///   <b>(b)</b> whether the resident set was built by the generator now running (rule 11: a render
///   edit leaves loaded chunks on the old geometry, so "the code says X" and "the screen shows X"
///   are different claims until the build fingerprint agrees);
///   <b>(c)</b> whether the reported shape comes from the data layer or only from interpolation
///   between samples.
/// This audit measures all three against the terrain actually resident at the moment the key was
/// pressed, so the readout names the mechanism instead of the fix picking one for it.</para>
///
/// <para><b>Section order is premise-first, not prettiness-first (rule 7).</b> A. fingerprint runs
/// BEFORE the shape sections, because if the resident set was built by two versions of the generator
/// then B/C/D are evidence about a world that is not on screen — the same mistake 1hy made when two
/// sections that "looked more like tests" ran ahead of the premise check.</para>
///
/// <para><b>Scope, and which owners the walk admits.</b> The crater walk admits exactly one owner
/// family: corners owned by a LOADED chunk tile (<c>_loadedData</c>). It deliberately does not read
/// corners that have no loaded owner. That is not tidiness — it is correctness:
/// <see cref="WorldStreamer.CurrentHeightOf"/> falls back to pristine noise sampled at
/// <c>(cx + 0.5, cz + 0.5)</c>, while pristine reference in <c>GetDigDepth</c> is sampled at
/// <c>(cx, cz)</c>. Those are two DIFFERENT points of the same noise field, so an unowned corner
/// reports a difference between two unrelated heights and reads as a deep phantom dig. Admitting
/// those corners would bury the real finding under noise-minus-noise. The searched band is printed
/// so a zero cannot be misread as "the crater is elsewhere".</para>
///
/// <para><b>Reading a zero.</b> This audit separates <i>no crater here</i> from <i>crater present
/// but reaches no rendered node</i> from <i>crater present and rendered</i>. Those are three
/// different verdicts and conflating them is how a measurement stops meaning anything (rule 7 —
/// see the 1i1/1i2 retraction). "Reaches no rendered node" is a HOLE; "reached but shallower than
/// authored" is a SHORTER HORIZON and is reported as its own count, never folded into the hole
/// count.</para>
///
/// <para><b>Strictly READ-ONLY (rule 7).</b> No chunk, lattice, mesh, collider, save or deform
/// state is mutated; nothing is re-stamped, no poll is forced, no mesh is rebuilt. It reads the
/// in-memory tile data and the uploaded meshes and nothing else, so the numbers describe exactly
/// the frame the key was pressed on.</para>
///
/// <para><b>Threading.</b> Main thread only, called from the test ground's <c>Update</c>. This is
/// the same discipline 1i4 established: <c>_loadedData</c> is read here while chunk builds run on the
/// ThreadPool, so this must never be reached from a background job.</para>
/// </summary>
public partial class WorldStreamer
{
    /// <summary>Metres either side of the focus point that the crater walk searches. Wide enough to
    /// contain a 1.9 m projectile reach with room for the walker's own tile, and small enough that
    /// a distant crater cannot be mistaken for the one at the player's feet.</summary>
    private const float CraterAuditSearchRadius = 6f;

    /// <summary>A corner counts as "dished" when it sits this far below its pristine noise height.
    /// Well above float noise on world-metre heights and well below the 1.1 m CraterStep, so a real
    /// dent registers and untouched terrain does not.</summary>
    private const float CraterAuditDigThreshold = 0.05f;

    /// <summary>Radial sample step for the profile section, in metres.</summary>
    private const float CraterAuditProfileStep = 0.5f;

    /// <summary>Radial samples taken per profile ring.</summary>
    private const int CraterAuditProfileSamples = 8;

    /// <summary>Accumulator for one profile ring (a circle of radius r around the impact point).</summary>
    private struct CraterProfileRing
    {
        public int Owned;      // samples in this ring that had a LOADED corner owner
        public float MinDig;
        public float MaxDig;
        public float SumDig;
    }

    /// <summary>
    /// One multi-line report of what a terrain dent is actually made of, in the order the questions
    /// must be asked:
    ///   A. fingerprint  — is the resident set built by one version of the generator, and is the
    ///                     facet path the one the code is configured for (the PREMISE for B/C).
    ///   B. resolution   — for the deepest dished corner near the focus: how far it is below
    ///                     pristine, how many corners the dish spans, and whether the render path
    ///                     samples the nodes the dish was written to.
    ///   C. profile      — the dish's radial depth profile, so "monotone cone" and "bowl with a rim"
    ///                     are distinguishable from the numbers rather than by eye.
    ///   D. expressibility — whether the shape could exist AT ALL in the current data model: the
    ///                     maximum corner spread inside the dish against the adaptive-refinement
    ///                     trigger, and whether any corner rose above pristine (a rim).
    /// A VERDICT line names the mechanism, with the world XZ to walk to.
    /// </summary>
    public string CraterAudit()
    {
        var sb = new StringBuilder(1024);

        Vector3 focus = _focus != null ? _focus.position : Vector3.zero;
        bool voxel = VoxelTerrainEnabled;

        sb.Append("CRATER / DEFORM AUDIT  loaded ").Append(_loadedChunks.Count)
          .Append("  dormant ").Append(_dormantChunks.Count)
          .Append("  step ").Append(EffectiveLowPolyStep)
          .Append("  refineAt ").Append(EffectiveRefineThreshold.ToString("0.##"))
          .Append("  voxel ").Append(voxel ? "ON" : "off");

        AppendCraterFingerprint(sb);
        AppendCraterResolution(sb, focus, out float deepestDig, out int deepestCX, out int deepestCZ,
            out float dishSpan, out int bandCorners);
        AppendCraterProfile(sb, deepestCX, deepestCZ, dishSpan);
        AppendCraterExpressibility(sb, deepestCX, deepestCZ, dishSpan, voxel);
        AppendCraterVerdict(sb, focus, deepestDig, deepestCX, deepestCZ, dishSpan,
            bandCorners, voxel);

        return sb.ToString();
    }

    /// <summary>
    /// Section A — the PREMISE. A render-algorithm edit leaves every already-loaded chunk on the
    /// geometry it was built with (rule 11), so a dent's appearance can be evidence about code that
    /// is not running. Reports the distinct (build stamp, facet step) buckets across the loaded set
    /// and the current effective step, so sections B/C/D can be read as claims about the world that
    /// is actually on screen.
    /// </summary>
    private void AppendCraterFingerprint(StringBuilder sb)
    {
        var buckets = new Dictionary<string, int>(4);
        var stamps = new Dictionary<int, int>(4);
        int wantStep = EffectiveLowPolyStep;

        foreach (KeyValuePair<TerrainChunkCoord, ChunkObject> kv in _loadedChunks)
        {
            ChunkObject c = kv.Value;
            if (c == null) continue;
            Mesh mesh = c.RootMesh;
            string label = "stamp " + c.BuildStamp + " step " + c.MeshStep
                + " verts " + (mesh != null ? mesh.vertexCount : -1);
            buckets.TryGetValue(label, out int seen);
            buckets[label] = seen + 1;

            stamps.TryGetValue(c.BuildStamp, out int n);
            stamps[c.BuildStamp] = n + 1;
        }

        sb.Append("\nA fingerprint  buckets ").Append(buckets.Count);
        int printed = 0;
        foreach (KeyValuePair<string, int> kv in buckets)
        {
            if (printed >= 3) { sb.Append("  ..."); break; }
            sb.Append("  [").Append(kv.Key).Append(" x").Append(kv.Value).Append(']');
            printed++;
        }
        sb.Append("  wantStep ").Append(wantStep);

        if (stamps.Count <= 1)
        {
            sb.Append("  buildStamp uniform");
        }
        else
        {
            sb.Append("  buildStamp MIXED");
            foreach (KeyValuePair<int, int> kv in stamps)
                sb.Append(' ').Append(kv.Key).Append("x").Append(kv.Value);
            sb.Append("  <-- B/C/D span two generator revisions; restart before trusting them");
        }
        sb.Append('\n');
    }

    /// <summary>
    /// Section B — resolution. Finds the deepest dished corner inside the searched band and reports
    /// what the render path does with it: the facet step in force, the number of lattice corners the
    /// dish spans, and the worst-case distance from the dish's deepest corner to the nearest node
    /// that path samples.
    ///
    /// <para>The band admits only corners owned by a loaded tile. An unowned corner would report
    /// pristine noise sampled at (cx+0.5, cz+0.5) against a reference sampled at (cx, cz) — two
    /// different points of the noise field — and read as a phantom dig.</para>
    /// </summary>
    private void AppendCraterResolution(StringBuilder sb, Vector3 focus, out float deepestDig,
        out int deepestCX, out int deepestCZ, out float dishSpan, out int bandCorners)
    {
        deepestDig = 0f;
        deepestCX = 0;
        deepestCZ = 0;
        dishSpan = 0f;
        bandCorners = 0;

        float r = CraterAuditSearchRadius;
        int minX = Mathf.FloorToInt(focus.x - r);
        int maxX = Mathf.FloorToInt(focus.x + r);
        int minZ = Mathf.FloorToInt(focus.z - r);
        int maxZ = Mathf.FloorToInt(focus.z + r);

        int dished = 0;
        float maxDig = 0f;
        for (int cz = minZ; cz <= maxZ; cz++)
        {
            for (int cx = minX; cx <= maxX; cx++)
            {
                if (!TryLoadedCornerHeight(cx, cz, out float y)) continue;
                bandCorners++;
                float dig = TerrainNoiseGenerator.GetHeight(Seed, cx, cz) - y;
                if (dig > CraterAuditDigThreshold)
                {
                    dished++;
                    if (dig > maxDig)
                    {
                        maxDig = dig;
                        deepestCX = cx;
                        deepestCZ = cz;
                    }
                }
            }
        }

        deepestDig = maxDig;
        sb.Append("\nB resolution  band ").Append(r.ToString("0.#")).Append(" m  corners ")
          .Append(bandCorners).Append(" owned  dished ").Append(dished);
        if (dished == 0)
        {
            sb.Append("  <no dished corner in band: no crater here>");
            return;
        }

        // How many corners does the dish actually SPAN, and how deep is the deepest one? A dish that
        // spans a handful of corners cannot carry a rim and a floor at the same time — that is the
        // resolution question this section exists to answer with a number.
        float span = 0f;
        int spanCount = 0;
        for (int cz = minZ; cz <= maxZ; cz++)
        {
            for (int cx = minX; cx <= maxX; cx++)
            {
                if (!TryLoadedCornerHeight(cx, cz, out float y)) continue;
                if (TerrainNoiseGenerator.GetHeight(Seed, cx, cz) - y <= CraterAuditDigThreshold) continue;
                // Corner-to-corner offset in metres: corners ARE the integer lattice (Size == 1), so
                // the difference of indices is the distance. No half-metre fudge (see section C).
                float dx = cx - deepestCX;
                float dz = cz - deepestCZ;
                float d = Mathf.Sqrt(dx * dx + dz * dz);
                if (d > span) span = d;
                spanCount++;
            }
        }

        dishSpan = span;
        sb.Append("  deepest ").Append(maxDig.ToString("0.###")).Append(" m at ")
          .Append(deepestCX).Append(',').Append(deepestCZ)
          .Append("  span ").Append(span.ToString("0.#")).Append(" m over ")
          .Append(spanCount).Append(" corners");

        // The render path's own visibility bound (rule 12). A facet surface holds only every
        // step-th lattice node, so the nearest node to any point of the dish can be up to
        // step/sqrt(2) away (both axes can be half a step off). If the dish is NARROWER than that,
        // it wrote its whole shape into lattice no triangle is built from: it does not look wrong,
        // it simply stops existing. Compare the dish's radius against that gap, not against `step`
        // and not against the step's value.
        int step = EffectiveLowPolyStep;
        sb.Append("  dishRadius ").Append(span.ToString("0.#")).Append(" m");
        if (step > 0)
        {
            float gap = step / Mathf.Sqrt(2f);
            sb.Append("  nodeGap ").Append(gap.ToString("0.##")).Append(" m");
            if (span < gap)
                sb.Append("  <-- dish narrower than the sampling gap: part of it is NOT DRAWN");
            else
                sb.Append("  dish reaches a sampled node");
        }
        else
        {
            // At step 0 every 1 m corner is sampled, so nothing is invisible for want of resolution.
            // The dish is drawn in full - carried entirely by interpolation between 1 m corners,
            // which is a SHAPE limit, not a visibility limit. Saying it this way matters: the fix is
            // not "raise the resolution of a carve that is already visible".
            sb.Append("  nodeGap n/a (step 0 draws every corner)  drawn in full; limit is SHAPE");
        }
    }

    /// <summary>
    /// Section C — the radial depth profile of the dish, ring by ring. This is the section that
    /// separates the two readings the user gave: a MONOTONE CONE (depth falls off smoothly to zero,
    /// nothing rises) versus a BOWL WITH A RIM (depth flattens in the middle and the outer rings
    /// come back UP toward or above pristine). Read as numbers rather than by eye.
    /// </summary>
    private void AppendCraterProfile(StringBuilder sb, int cx, int cz, float dishSpan)
    {
        // Scan to the dish's own edge plus one ring: past that every sample is undisturbed terrain
        // and a longer walk only dilutes the numbers that matter. If the dish is absent (span 0),
        // one ring still runs so the report says "nothing there" instead of printing no table.
        float maxR = Mathf.Max(dishSpan + CraterAuditProfileStep, CraterAuditProfileStep);
        int rings = Mathf.Max(1, Mathf.CeilToInt(maxR / CraterAuditProfileStep));

        sb.Append("\nC profile  ring  radius  owned  minDig  maxDig  meanDig");
        int anyOwned = 0;
        float deepest = 0f;
        float deepestR = 0f;
        for (int ri = 0; ri <= rings; ri++)
        {
            float radius = ri * CraterAuditProfileStep;
            var ring = new CraterProfileRing
            {
                MinDig = float.MaxValue,
                MaxDig = float.MinValue,
            };

            for (int s = 0; s < CraterAuditProfileSamples; s++)
            {
                float ang = (Mathf.PI * 2f * s) / CraterAuditProfileSamples;
                // Lattice corners sit AT integer world coords (ChunkData.Size is 1 and the chunk
                // builder seeds corner (gx,gz) from GetHeight(seed, tc.X*cs+gx, tc.Z*cs+gz)), so the
                // nearest corner in a direction is the one found by rounding the offset - NOT by
                // FloorToInt on a half-shifted point, which would bias every ring outward.
                int px = cx + Mathf.RoundToInt(Mathf.Cos(ang) * radius);
                int pz = cz + Mathf.RoundToInt(Mathf.Sin(ang) * radius);
                if (!TryLoadedCornerHeight(px, pz, out float y)) continue;
                ring.Owned++;
                float dig = TerrainNoiseGenerator.GetHeight(Seed, px, pz) - y;
                if (dig < ring.MinDig) ring.MinDig = dig;
                if (dig > ring.MaxDig) ring.MaxDig = dig;
                ring.SumDig += dig;
                if (dig > deepest) { deepest = dig; deepestR = radius; }
            }

            if (ring.Owned > 0)
            {
                anyOwned++;
                sb.Append("        ").Append(radius.ToString("0.#").PadLeft(5))
                  .Append("  ").Append(ring.Owned.ToString().PadLeft(5))
                  .Append("  ").Append(ring.MinDig.ToString("0.###").PadLeft(6))
                  .Append("  ").Append(ring.MaxDig.ToString("0.###").PadLeft(6))
                  .Append("  ").Append((ring.SumDig / ring.Owned).ToString("0.###").PadLeft(7));
            }
        }
        sb.Append("\n     deepest ring at r ").Append(deepestR.ToString("0.#"))
          .Append(" m, dig ").Append(deepest.ToString("0.###")).Append(" m over ")
          .Append(anyOwned).Append(" readable rings");
    }

    /// <summary>
    /// Section D — expressibility. Even a perfect measurement of a smooth cone does not say a rim
    /// is MISSING; it says the cone is all that was written. This section asks whether the current
    /// data model and render path could express the shape at all:
    ///   <list type="bullet">
    ///   <item>max corner SPREAD inside the dish vs the adaptive-refinement trigger. The refined
    ///   2x2 block is the only sub-tile geometry the render path has, and it fires on spread — so
    ///   below the trigger the dish is carried entirely by interpolation between 1 m corners.</item>
    ///   <item>any corner ABOVE pristine inside the band. A rim is a positive raise; a crater that
    ///   only ever lowers can never produce one, and this counts that directly instead of
    ///   inferring it from the shape.</item>
    ///   <item>any whole-metre discontinuity, the only thing that makes the render path emit
    ///   vertical side walls — i.e. whether a floor or wall exists at all.</item>
    ///   </list>
    /// </summary>
    private void AppendCraterExpressibility(StringBuilder sb, int cx, int cz, float dishSpan, bool voxel)
    {
        // SCOPE: every check below walks the crater's OWN footprint (the dish's span, plus half a
        // metre so the rim ring is included), not the whole search band around the player. The band
        // is a search area for "where is the crater"; it is not the crater. A second raised shape
        // nearby (the Wall/Ring/Pillar deform profiles DO raise) would otherwise be counted here as
        // "this crater has a rim", which is a different crater's number read as this one's.
        float r = Mathf.Max(dishSpan + 0.5f, 1f);
        int minX = cx - Mathf.CeilToInt(r);
        int maxX = cx + Mathf.CeilToInt(r);
        int minZ = cz - Mathf.CeilToInt(r);
        int maxZ = cz + Mathf.CeilToInt(r);

        // Max corner spread per tile, and how many dished corners sit in tiles that exceed the
        // refinement trigger. The trigger is what decides whether a sub-tile block is emitted.
        float maxSpread = 0f;
        int tilesRefinable = 0;
        int tilesSeen = 0;
        float wallGap = 0f;
        int walls = 0;

        for (int cz2 = minZ; cz2 <= maxZ; cz2++)
        {
            for (int cx2 = minX; cx2 <= maxX; cx2++)
            {
                if (!_loadedData.TryGetValue(new ChunkCoord(cx2, cz2), out ChunkData tile)
                    || !tile.IsValid) continue;
                tilesSeen++;
                float mn = tile.Heights[0], mx = tile.Heights[0];
                for (int k = 1; k < tile.Heights.Length; k++)
                {
                    if (tile.Heights[k] < mn) mn = tile.Heights[k];
                    if (tile.Heights[k] > mx) mx = tile.Heights[k];
                }
                float spread = mx - mn;
                if (spread > maxSpread) maxSpread = spread;
                if (spread > EffectiveRefineThreshold) tilesRefinable++;
            }
        }

        // Count corners above pristine (a rim) and the largest adjacent-corner gap (a side wall).
        int raised = 0;
        for (int cz2 = minZ; cz2 <= maxZ; cz2++)
        {
            for (int cx2 = minX; cx2 <= maxX; cx2++)
            {
                if (!TryLoadedCornerHeight(cx2, cz2, out float y)) continue;
                float delta = y - TerrainNoiseGenerator.GetHeight(Seed, cx2, cz2);
                if (delta > CraterAuditDigThreshold) raised++;
                if (TryLoadedCornerHeight(cx2 + 1, cz2, out float yx))
                {
                    float gap = Mathf.Abs(y - yx);
                    if (gap > wallGap) wallGap = gap;
                    if (gap >= 1f) walls++;
                }
                if (TryLoadedCornerHeight(cx2, cz2 + 1, out float yz))
                {
                    float gap = Mathf.Abs(y - yz);
                    if (gap > wallGap) wallGap = gap;
                    if (gap >= 1f) walls++;
                }
            }
        }

        sb.Append("\nD expressibility  tiles ").Append(tilesSeen)
          .Append("  maxCornerSpread ").Append(maxSpread.ToString("0.###"))
          .Append(" m  refineAt ").Append(EffectiveRefineThreshold.ToString("0.##"))
          .Append(" m  tilesOverTrigger ").Append(tilesRefinable);
        sb.Append("\n     raised corners ").Append(raised)
          .Append("  maxAdjacentGap ").Append(wallGap.ToString("0.###"))
          .Append(" m  metreGaps ").Append(walls)
          .Append(voxel ? "  (voxel path: walls are stepped, not smooth)" : "");
        if (tilesRefinable == 0 && maxSpread <= EffectiveRefineThreshold)
        {
            sb.Append("\n     no tile exceeds the refinement trigger: the dish is carried")
              .Append("\n     entirely by interpolation between 1 m corners (no sub-tile block)");
        }
        if (raised == 0)
        {
            sb.Append("\n     no corner rose above pristine: the shape is a pure depression,")
              .Append("\n     so a raised rim is not present anywhere in the band");
        }
    }

    /// <summary>
    /// The VERDICT line. Names the mechanism in the order the evidence supports it, and keeps the
    /// three outcomes distinct: no crater in band / crater present but reaching no sampled node /
    /// crater present and rendered.
    /// </summary>
    private void AppendCraterVerdict(StringBuilder sb, Vector3 focus, float deepestDig,
        int cx, int cz, float dishSpan, int bandCorners, bool voxel)
    {
        sb.Append("\nVERDICT ");
        if (bandCorners == 0)
        {
            sb.Append("no loaded terrain in the searched band around ")
              .Append(focus.x.ToString("0.#")).Append(',').Append(focus.z.ToString("0.#"))
              .Append(" — nothing measured; this is NOT a statement about the crater");
            return;
        }
        if (deepestDig <= CraterAuditDigThreshold)
        {
            sb.Append("no dished corner within ").Append(CraterAuditSearchRadius.ToString("0.#"))
              .Append(" m — no crater here to measure (walk to the dent and press again)");
            return;
        }

        int step = EffectiveLowPolyStep;
        sb.Append("crater at ").Append(cx).Append(',').Append(cz)
          .Append("  deepest ").Append(deepestDig.ToString("0.###")).Append(" m")
          .Append("  dishRadius ").Append(dishSpan.ToString("0.#")).Append(" m")
          .Append("  bandCorners ").Append(bandCorners)
          .Append("  nodeGap ").Append(step > 0
              ? (step / Mathf.Sqrt(2f)).ToString("0.##") + " m"
              : "n/a (every corner drawn)");

        if (step > 0 && step / Mathf.Sqrt(2f) > CraterAuditSearchRadius)
        {
            sb.Append("  <-- facet step coarser than the search band: the dish may extend");
            sb.Append("\n      past the band, so section B's dishRadius is a LOWER bound only");
        }
        sb.Append("\n     the dish IS in the data. Whether it reads as displaced material or as");
        sb.Append("\n     sinking neighbours is decided by sections C and D, not by this line.");
    }

    /// <summary>
    /// Current height of a lattice corner, but ONLY when a loaded tile owns it. Returns false for
    /// an unowned corner rather than falling back to noise: the deform path's own fallback samples
    /// (cx + 0.5, cz + 0.5) while pristine reference is sampled at (cx, cz), so reading an unowned
    /// corner would compare two different points of the noise field and invent a dig.
    ///
    /// <para>Owner table mirrors <see cref="CurrentHeightOf"/> exactly — SW slot of tile (cx,cz),
    /// SE of (cx-1,cz), NW of (cx,cz-1), NE of (cx-1,cz-1).</para>
    /// </summary>
    private bool TryLoadedCornerHeight(int cx, int cz, out float height)
    {
        if (_loadedData.TryGetValue(new ChunkCoord(cx, cz), out ChunkData d0) && d0.IsValid)
        {
            height = d0.Heights[3]; return true;
        }
        if (_loadedData.TryGetValue(new ChunkCoord(cx - 1, cz), out ChunkData d1) && d1.IsValid)
        {
            height = d1.Heights[2]; return true;
        }
        if (_loadedData.TryGetValue(new ChunkCoord(cx, cz - 1), out ChunkData d2) && d2.IsValid)
        {
            height = d2.Heights[0]; return true;
        }
        if (_loadedData.TryGetValue(new ChunkCoord(cx - 1, cz - 1), out ChunkData d3) && d3.IsValid)
        {
            height = d3.Heights[1]; return true;
        }
        height = 0f;
        return false;
    }
}
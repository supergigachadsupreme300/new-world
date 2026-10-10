using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Unit-space, CHUNKY LOW-POLY part meshes for the player model (1dw + 1dx + 1ep): every body part
/// that used to be a scaled cube is now a faceted ellipsoid surface that occupies the same half-extent
/// cube [-0.5, 0.5] as the old shared unit cube, so the part GameObject's localScale = the old size
/// vector reproduces the exact same world dimensions the cube had. Each part is sculpted with
/// terrain-style "dent" ops (the WorldStreamer.DeformAt crater carve, generalised to 3D): ellipsoid
/// distance to an anchor, the same s = t²(3−2t) smoothstep, offset along the corner's original
/// radial. The sculpted corner skeleton is then emitted as FLAT-SHADED low-poly panels (1dx): every
/// band cell becomes a square panel or a pair of triangle panels (deterministic hash, squares
/// dominant), with a small deterministic tangent jitter on the SHARED corners so the mosaic reads
/// hand-cut while staying watertight — no cracks, no see-through. Since (1ep) the lattice spacing
/// itself is NO LONGER UNIFORM: band heights and segment widths follow a deterministic ±20%
/// schedule, so the panels come out as different-sized cells of a hand-cut stone mosaic instead of
/// an orderly same-size grid. The shared-corner topology is preserved (every cell still covers its
/// area exactly — watertight), the poles and the torso endpoints (t = 0 / t = 1) stay fixed, so
/// silhouettes and pivot contracts are unaffected and every part stays deterministic per profile.
///
/// Meshes are STATIC and SIZE-INDEPENDENT (a cached mesh serves every gender, race ratio and model
/// variant — the parts are sized purely through Transform.localScale, which is exactly how
/// ApplyRaceLook / ApplyRaceRatioRecurse / WeaponRigBuilder.ScaleForWorld keep working). Main thread
/// only (built from MapBuilder on first use).
/// </summary>
public static class PlayerPartMesher
{
    private const int Rings = 7;              // latitude rows incl. poles → 6 low-poly bands (1dx)
    private const int Segs = 12;              // cells around the equator (1dx)
    private const float Half = 0.5f;
    private const float FacetJitter = 0.03f;  // unit-space tangent nudge per shared corner (1dx)
    private const float SplitChance = 0.35f;  // per-cell chance to become two triangle panels (1dx)
    // 1ep: NON-UNIFORM lattice — interior band heights (phi / torso-t) and segment widths (theta)
    // vary deterministically by up to ±Irregularity, so panels differ in size like a hand-cut stone
    // mosaic. Endpoints (poles, torso t=0/t=1) are preserved and corners stay shared → watertight.
    private const float BandIrregularity = 0.20f;
    private const float SegIrregularity = 0.20f;

    /// <summary>1ep: deterministic non-uniform step weight for schedule index i — 1 ± irregularity,
    /// hash-driven per part, clamped ≥ 0.4 so a row/column can wobble but never collapse to a sliver.</summary>
    private static float StepWeight(int i, int seed, int salt, float irregularity)
    {
        float h = Hash01(i * 37 + 13, seed, salt) - 0.5f; // [-0.5, 0.5]
        return Mathf.Max(0.4f, 1f + irregularity * (h * 2f));
    }

    /// <summary>1ep: schedule of <paramref name="count"/> step weights, normalized to sum to 1
    /// (caller multiplies by the angular/height range). Same seed+salt ⇒ identical every build.</summary>
    private static float[] Steps(int count, int seed, int salt, float irregularity)
    {
        var w = new float[count];
        float sum = 0f;
        for (int i = 0; i < count; i++)
        {
            w[i] = StepWeight(i, seed, salt, irregularity);
            sum += w[i];
        }
        for (int i = 0; i < count; i++)
            w[i] /= sum;
        return w;
    }

    /// <summary>1ep: cumulative positions from normalized steps — result[0] = 0, result[count] = total.
    /// Every interior corner stays a single shared position; only the spacing changes (watertight).</summary>
    private static float[] Positions(float[] weights, float total)
    {
        var p = new float[weights.Length + 1];
        for (int i = 0; i < weights.Length; i++)
            p[i + 1] = p[i] + weights[i] * total;
        return p;
    }

    private readonly struct Dent
    {
        public readonly Vector3 Anchor;
        public readonly Vector3 Radius;
        public readonly float Strength;

        public Dent(Vector3 anchor, Vector3 radius, float strength)
        {
            Anchor = anchor;
            Radius = radius;
            Strength = strength;
        }
    }

    private static readonly Dictionary<string, Dent[]> _profiles = new Dictionary<string, Dent[]>
    {
        { "Skirt", new[]
        {
            new Dent(new Vector3(0f, -0.26f, 0f), new Vector3(0.34f, 0.14f, 0.30f), 0.05f),   // bell flare
            new Dent(new Vector3(0f, 0.28f, 0f), new Vector3(0.30f, 0.12f, 0.28f), -0.04f),  // waist fit
        } },
        { "SkirtHem", new[]
        {
            new Dent(new Vector3(0f, -0.28f, 0f), new Vector3(0.34f, 0.12f, 0.30f), 0.06f),   // hem flare
            new Dent(new Vector3(0f, 0.28f, 0f), new Vector3(0.30f, 0.12f, 0.28f), -0.04f),  // top taper
        } },
        { "Head", new[]
        {
            new Dent(new Vector3(0f, 0.12f, 0f), new Vector3(0.42f, 0.24f, 0.40f), 0.04f),         // cranium widen
            new Dent(new Vector3(0f, -0.08f, 0.02f), new Vector3(0.32f, 0.16f, 0.26f), 0.03f),    // cheek/chin widen
            new Dent(new Vector3(0f, -0.20f, 0.14f), new Vector3(0.14f, 0.10f, 0.10f), 0.04f),    // chin bump
            new Dent(new Vector3(-0.13f, 0.02f, 0.16f), new Vector3(0.10f, 0.08f, 0.06f), -0.05f),// eye socket L
            new Dent(new Vector3(0.13f, 0.02f, 0.16f), new Vector3(0.10f, 0.08f, 0.06f), -0.05f), // eye socket R
            new Dent(new Vector3(0f, 0.03f, 0.17f), new Vector3(0.07f, 0.10f, 0.05f), 0.03f),     // nose
            new Dent(new Vector3(0f, -0.14f, 0.10f), new Vector3(0.20f, 0.10f, 0.14f), -0.02f),   // jaw taper
        } },
        { "Neck", new[]
        {
            new Dent(new Vector3(0f, 0f, 0f), new Vector3(0.30f, 0.26f, 0.30f), -0.04f), // mid pinch
        } },
        { "UpperArm", new[]
        {
            new Dent(new Vector3(0f, 0.28f, 0f), new Vector3(0.26f, 0.12f, 0.26f), 0.05f),  // deltoid
            new Dent(new Vector3(0f, -0.30f, 0f), new Vector3(0.30f, 0.10f, 0.30f), -0.04f),// elbow taper
        } },
        { "Forearm", new[]
        {
            new Dent(new Vector3(0f, -0.28f, 0f), new Vector3(0.30f, 0.12f, 0.30f), -0.06f),// wrist taper
            new Dent(new Vector3(0f, 0.05f, -0.04f), new Vector3(0.28f, 0.18f, 0.20f), 0.03f),// muscle
        } },
        { "Hand", new[]
        {
            new Dent(new Vector3(0f, 0.02f, 0.14f), new Vector3(0.30f, 0.26f, 0.20f), 0.06f), // palm
            new Dent(new Vector3(0f, -0.16f, 0.18f), new Vector3(0.30f, 0.14f, 0.12f), 0.04f),// knuckle
        } },
        { "Thigh", new[]
        {
            new Dent(new Vector3(0f, 0.28f, 0f), new Vector3(0.28f, 0.12f, 0.28f), -0.03f),  // hip taper
            new Dent(new Vector3(0f, -0.30f, 0f), new Vector3(0.30f, 0.10f, 0.30f), -0.05f), // knee taper
            new Dent(new Vector3(0f, 0f, 0.08f), new Vector3(0.28f, 0.24f, 0.16f), 0.04f),   // quad raise
        } },
        { "Shin", new[]
        {
            new Dent(new Vector3(0f, 0.26f, 0f), new Vector3(0.30f, 0.12f, 0.30f), 0.05f),   // knee cap
            new Dent(new Vector3(0f, -0.28f, 0f), new Vector3(0.30f, 0.10f, 0.30f), -0.05f), // ankle taper
            new Dent(new Vector3(0f, 0f, -0.10f), new Vector3(0.28f, 0.22f, 0.16f), 0.04f),  // calf
        } },
        { "Shoe", new[]
        {
            new Dent(new Vector3(0f, -0.02f, 0.30f), new Vector3(0.30f, 0.16f, 0.14f), 0.05f), // toe
            new Dent(new Vector3(0f, -0.26f, 0.04f), new Vector3(0.34f, 0.14f, 0.24f), 0.04f), // sole
            new Dent(new Vector3(0f, 0.28f, 0f), new Vector3(0.28f, 0.12f, 0.28f), -0.03f),   // ankle
        } },
        { "Hair", new[]
        {
            new Dent(new Vector3(0f, 0.26f, 0f), new Vector3(0.38f, 0.14f, 0.38f), -0.04f), // crown taper
            new Dent(new Vector3(0f, 0.06f, 0f), new Vector3(0.34f, 0.20f, 0.30f), 0.03f),  // side round
        } },
        { "HairSide", new[]
        {
            new Dent(new Vector3(0f, -0.28f, 0f), new Vector3(0.30f, 0.14f, 0.30f), -0.04f),// bottom taper
        } },
        { "HairBack", new[]
        {
            new Dent(new Vector3(0f, 0f, -0.05f), new Vector3(0.34f, 0.28f, 0.30f), -0.04f),// slim inward
        } },
        { "HairBand", new Dent[0] },
        { "Cylinder", new Dent[0] }, // round neck column (1e1) — 12 flat side facets + closed caps, not ellipsoid
        { "Ponytail", new[]
        {
            new Dent(new Vector3(0f, 0.28f, 0f), new Vector3(0.30f, 0.12f, 0.30f), 0.03f),  // top
            new Dent(new Vector3(0f, -0.28f, 0f), new Vector3(0.30f, 0.14f, 0.30f), -0.05f),// tail taper
        } },
        { "EyeWhite", new[]
        {
            new Dent(new Vector3(0f, 0f, 0.12f), new Vector3(0.40f, 0.36f, 0.16f), 0.03f),  // front bulge
        } },
        { "EyeIris", new[]
        {
            new Dent(new Vector3(0f, 0f, 0.10f), new Vector3(0.36f, 0.36f, 0.14f), 0.04f),  // front bulge
        } },
        { "Joint", new Dent[0] }, // plain faceted ball joint (1dy) — no dents
    };

    private static readonly Dictionary<string, Mesh> _cache = new Dictionary<string, Mesh>();

    /// <summary>
    /// Shared, cached unit-space part mesh for a profile (built once, main thread only).
    /// The three torso ids ("Body"/"SitTorso"/"Chest") route to the flat-facet SHOULDERED torso
    /// builder (1e7 routing fix: they are not in <c>_profiles</c>, so the old code fell back and the
    /// shouldered silhouette never rendered). Unknown/empty ids fall back to the plain (unsculpted)
    /// ellipsoid.
    /// </summary>
    public static Mesh BuildEllipsoid(string profileId)
    {
        if (profileId == "Body" || profileId == "SitTorso" || profileId == "Chest")
        {
            if (_cache.TryGetValue(profileId, out var tm) && tm != null)
                return tm;
            var torso = BuildTorso(profileId);
            torso.hideFlags |= HideFlags.HideAndDontSave;
            _cache[profileId] = torso;
            return torso;
        }
        if (string.IsNullOrEmpty(profileId) || !_profiles.ContainsKey(profileId))
            profileId = "HairBand";
        if (_cache.TryGetValue(profileId, out var cached) && cached != null)
            return cached;
        var mesh = Generate(profileId);
        mesh.hideFlags |= HideFlags.HideAndDontSave;
        _cache[profileId] = mesh;
        return mesh;
    }

    private static Mesh Generate(string profileId)
    {
        if (profileId == "Cylinder")
            return BuildCylinder();
        if (profileId == "Body" || profileId == "SitTorso" || profileId == "Chest")
            return BuildTorso(profileId);
        _profiles.TryGetValue(profileId, out var dents);
        int seed = AnchorSeed(profileId);
        int grid = (Rings + 1) * Segs;

        // Lattice of ellipsoid corners (Rings+1 rows of Segs around), poles handled separately.
        // 1ep: spacing is no longer uniform — per-band heights (phi) and per-segment widths (theta)
        // follow a deterministic ±Irregularity schedule, so panels come out as different-sized cells
        // of a hand-cut stone mosaic. Rows share one theta schedule → cells stay in aligned azimuth
        // planes (quads near-planar), and endpoints (poles at phi 0/π) are preserved.
        float[] phi = Positions(Steps(Rings, seed, 0x1E0F01, BandIrregularity), Mathf.PI);
        float[] theta = Positions(Steps(Segs, seed, 0x1E0F02, SegIrregularity), 2f * Mathf.PI);
        var corners = new Vector3[grid];
        for (int lat = 0; lat <= Rings; lat++)
        {
            float py = Mathf.Cos(phi[lat]) * Half;
            float pr = Mathf.Sin(phi[lat]) * Half;
            for (int s = 0; s < Segs; s++)
                corners[lat * Segs + s] = new Vector3(Mathf.Cos(theta[s]) * pr, py, Mathf.Sin(theta[s]) * pr);
        }
        // UVs ride the same schedule (u = θ/2π, v = 1 − φ/π) so texel density follows panel size.
        var uf = new float[Segs];
        for (int s = 0; s < Segs; s++) uf[s] = theta[s] / (2f * Mathf.PI);
        var vf = new float[Rings + 1];
        for (int lat = 0; lat <= Rings; lat++) vf[lat] = 1f - phi[lat] / Mathf.PI;
        Vector3 poleN = new Vector3(0f, Half, 0f);
        Vector3 poleS = new Vector3(0f, -Half, 0f);

        // Terrain-style sculpt (1dw): influence from ellipsoid distance → the DeformAt smoothstep
        // s = t²(3−2t) → offset along the corner's original radial, exactly like a heightfield dent.
        for (int lat = 1; lat < Rings; lat++)
        {
            for (int s = 0; s < Segs; s++)
            {
                Vector3 p = corners[lat * Segs + s];
                Vector3 radial = p.sqrMagnitude > 1e-6f ? p.normalized : Vector3.up;
                if (dents != null)
                {
                    for (int k = 0; k < dents.Length; k++)
                    {
                        Dent d = dents[k];
                        Vector3 off = p - d.Anchor;
                        Vector3 ri = new Vector3(Mathf.Max(0.001f, d.Radius.x),
                            Mathf.Max(0.001f, d.Radius.y), Mathf.Max(0.001f, d.Radius.z));
                        float n = Mathf.Sqrt(
                            off.x * off.x / (ri.x * ri.x) +
                            off.y * off.y / (ri.y * ri.y) +
                            off.z * off.z / (ri.z * ri.z));
                        float inf = 1f - Mathf.Clamp01(n);
                        float sm = inf * inf * (3f - 2f * inf);
                        p += radial * (sm * d.Strength);
                    }
                }
                corners[lat * Segs + s] = p;
            }
        }

        // Deterministic tangent jitter (1dx): each SHARED corner gets a small nudge so the panel
        // boundaries look hand-cut. Corners stay shared between neighbours → the mosaic is
        // watertight (no gaps to peer through), and identical every build.
        for (int lat = 1; lat < Rings; lat++)
        {
            for (int s = 0; s < Segs; s++)
            {
                int k = lat * Segs + s;
                Vector3 r = corners[k].normalized;
                Vector3 t1 = Vector3.Cross(r, Vector3.up);
                if (t1.sqrMagnitude < 1e-6f)
                    t1 = Vector3.Cross(r, Vector3.right);
                t1.Normalize();
                Vector3 t2 = Vector3.Cross(r, t1).normalized;
                float h1 = Hash01(k, seed, 0x1234AB) - 0.5f;
                float h2 = Hash01(k, seed, 0x5678CD) - 0.5f;
                corners[k] += (t1 * h1 + t2 * h2) * (2f * FacetJitter);
            }
        }

        int cap = grid * 3;
        var verts = new List<Vector3>(cap);
        var uvs = new List<Vector2>(cap);
        var norms = new List<Vector3>(cap);
        var tris = new List<int>(cap);

        // North pole fan (all triangles).
        for (int s = 0; s < Segs; s++)
            EmitTriangle(poleN, corners[Segs + s], corners[Segs + (s + 1) % Segs],
                CornerUV(0, s, uf, vf), CornerUV(1, s, uf, vf), CornerUV(1, s + 1, uf, vf), verts, uvs, norms, tris);

        // Side bands: each cell becomes a SQUARE panel, or two TRIANGLE panels split along a
        // deterministically chosen diagonal (1dx). Squares dominate; triangles sprinkle in.
        for (int b = 1; b < Rings; b++)
        {
            for (int s = 0; s < Segs; s++)
            {
                int s1 = (s + 1) % Segs;
                int ctl = (b - 1) * Segs + s, ctr = (b - 1) * Segs + s1;
                int cbl = b * Segs + s, cbr = b * Segs + s1;
                if (Hash01(b * 131 + s, seed, 0x5EEDF) < SplitChance)
                {
                    if (Hash01(b * 131 + s, seed, 0xCAFE) < 0.5f) // diagonal TL→BR
                    {
                        EmitTriangle(corners[ctl], corners[ctr], corners[cbr],
                            CornerUV(b - 1, s, uf, vf), CornerUV(b - 1, s1, uf, vf), CornerUV(b, s1, uf, vf), verts, uvs, norms, tris);
                        EmitTriangle(corners[ctl], corners[cbr], corners[cbl],
                            CornerUV(b - 1, s, uf, vf), CornerUV(b, s1, uf, vf), CornerUV(b, s, uf, vf), verts, uvs, norms, tris);
                    }
                    else // diagonal TR→BL
                    {
                        EmitTriangle(corners[ctr], corners[cbr], corners[cbl],
                            CornerUV(b - 1, s1, uf, vf), CornerUV(b, s1, uf, vf), CornerUV(b, s, uf, vf), verts, uvs, norms, tris);
                        EmitTriangle(corners[ctr], corners[cbl], corners[ctl],
                            CornerUV(b - 1, s1, uf, vf), CornerUV(b, s, uf, vf), CornerUV(b - 1, s, uf, vf), verts, uvs, norms, tris);
                    }
                }
                else
                {
                    EmitQuad(corners[ctl], corners[ctr], corners[cbr], corners[cbl],
                        CornerUV(b - 1, s, uf, vf), CornerUV(b - 1, s1, uf, vf), CornerUV(b, s1, uf, vf), CornerUV(b, s, uf, vf),
                        verts, uvs, norms, tris);
                }
            }
        }

        // South pole fan (all triangles).
        for (int s = 0; s < Segs; s++)
            EmitTriangle(poleS, corners[(Rings - 1) * Segs + s], corners[(Rings - 1) * Segs + (s + 1) % Segs],
                CornerUV(Rings, s, uf, vf), CornerUV(Rings - 1, s, uf, vf), CornerUV(Rings - 1, s + 1, uf, vf), verts, uvs, norms, tris);

        var mesh = new Mesh { name = "PlayerPart_" + profileId };
        mesh.SetVertices(verts);
        mesh.SetUVs(0, uvs);
        mesh.SetNormals(norms);
        mesh.SetTriangles(tris, 0, true);
        mesh.RecalculateBounds();
        return mesh;
    }

    /// <summary>UV from the 1ep non-uniform schedule: u follows the segment fractions (wrapping
    /// at Segs), v follows the height fractions (top = 1) — texel density tracks actual panel size.</summary>
    private static Vector2 CornerUV(int lat, int s, float[] uf, float[] vf) =>
        new Vector2(uf[s % Segs], vf[lat]);

    /// <summary>
    /// Unit-space round cylinder (1e1): the neck switched from the square masonry pillar to a round
    /// column — 12 flat side facets (matching the faceted-band count) plus closed top/bottom caps.
    /// Still spans the same half-extent cube [-0.5, 0.5], so localScale = size vector reproduces
    /// dimensions exactly like every other part mesh.
    /// </summary>
    private static Mesh BuildCylinder()
    {
        var verts = new List<Vector3>();
        var uvs = new List<Vector2>();
        var norms = new List<Vector3>();
        var tris = new List<int>();

        Vector3 topC = new Vector3(0f, Half, 0f);
        Vector3 botC = new Vector3(0f, -Half, 0f);

        // 12 flat side facets (each a quad, per-face outward winding via EmitQuad's centroid check).
        // 1ep: facets ride the same non-uniform theta schedule as the other parts so the neck's
        // widths vary in step with the rest of the mosaic.
        int seed = AnchorSeed("Cylinder");
        float[] col = Positions(Steps(Segs, seed, 0x1E0F02, SegIrregularity), 2f * Mathf.PI);
        var uf = new float[Segs];
        for (int s = 0; s < Segs; s++) uf[s] = col[s] / (2f * Mathf.PI);
        for (int s = 0; s < Segs; s++)
        {
            int s1 = (s + 1) % Segs;
            Vector2 u0 = new Vector2(uf[s], 0f);
            Vector2 u1 = new Vector2(uf[s1], 0f);
            EmitQuad(
                new Vector3(Mathf.Cos(col[s]) * Half, -Half, Mathf.Sin(col[s]) * Half),
                new Vector3(Mathf.Cos(col[s1]) * Half, -Half, Mathf.Sin(col[s1]) * Half),
                new Vector3(Mathf.Cos(col[s1]) * Half, Half, Mathf.Sin(col[s1]) * Half),
                new Vector3(Mathf.Cos(col[s]) * Half, Half, Mathf.Sin(col[s]) * Half),
                u0, u1, u1, u0, verts, uvs, norms, tris);
        }

        // Closed caps (flat triangle fans, no global winding assumption — per-triangle centroid flip).
        for (int s = 0; s < Segs; s++)
        {
            int s1 = (s + 1) % Segs;
            Vector3 a0 = new Vector3(Mathf.Cos(col[s]) * Half, Half, Mathf.Sin(col[s]) * Half);
            Vector3 a1 = new Vector3(Mathf.Cos(col[s1]) * Half, Half, Mathf.Sin(col[s1]) * Half);
            Vector3 b0 = new Vector3(Mathf.Cos(col[s]) * Half, -Half, Mathf.Sin(col[s]) * Half);
            Vector3 b1 = new Vector3(Mathf.Cos(col[s1]) * Half, -Half, Mathf.Sin(col[s1]) * Half);
            EmitTriangle(topC, a1, a0, Vector2.zero, Vector2.one, Vector2.zero, verts, uvs, norms, tris);
            EmitTriangle(botC, b0, b1, Vector2.zero, Vector2.one, Vector2.zero, verts, uvs, norms, tris);
        }

        var mesh = new Mesh { name = "PlayerPart_Cylinder" };
        mesh.SetVertices(verts);
        mesh.SetUVs(0, uvs);
        mesh.SetNormals(norms);
        mesh.SetTriangles(tris, 0, true);
        mesh.RecalculateBounds();
        return mesh;
    }

    /// <summary>
    /// Unit-space SHOULDERED torso (1e2 + 1e4): the shoulder/hip pivots sit OUTSIDE the old ellipsoid
    /// (pivot radii ~0.8–1.6 unit vs the 0.5 lattice radius) — dents could never bridge that, so the
    /// torso/chest parts are no longer ellipsoids but a flat-facet silhouette that reaches the limb
    /// pivots. Same construction language as BuildCylinder: 12 segments, 8 flat bands (9 rows — a
    /// finer schedule than the shared 7-band ellipsoid/cylinder so the waist taper and shoulder dome
    /// slope smoothly), the same deterministic shared-corner jitter mosaic (watertight), a closed
    /// bottom cap, and a small closed CROWN disc at the top: 1e4 replaced the old flat top plateau
    /// (1e2) with a SLOPED SHOULDER DOME that tucks under the neck cylinder (crown W ≈ 0.20 ≈ the neck
    /// radius — no hat-brim collar ring, no seam gap). The `"Body"` parts are built TALLER in
    /// MapBuilder (standing 0.8 tall, centre torso-local 0.13 = root 0.18; seated 0.6 tall at root
    /// 0.25) precisely so the shoulder pivots sit on the dome band — NOT on the small crown row
    /// (which is why build sizes matter to this silhouette). Silhouette per profile (t = normalized
    /// height, 0 bottom → 1 top, rows exactly on t = i/8), W = per-axis unit reach along ±x, D along
    /// ±z; world half-width at a band = size.x · W:
    ///   Body:      hip 0.46 → straight taper → shoulder shelf 0.72 (widest, t=0.75) → dome 0.58 →
    ///              crown 0.20 (1kv: TRAPEZIUM front silhouette — the old hip flare 0.68 and pinched
    ///              waist 0.40 are gone, so width now rises MONOTONICALLY from the hips to the widest
    ///              point at the shoulder shelf. This is the change a uniform resize cannot make,
    ///              because size only scales W. The standing pivots land at t≈0.78 on W 0.69 → world
    ///              0.44·0.69 = 0.304, so the ±0.28 ball pokes ~2 cm out of the dome as a visible cap;
    ///              the seated body 0.6 tall puts its pivots at t≈0.87 on W 0.59 → world 0.34·0.59 =
    ///              0.200, ball well outside the dome. Crown world 0.088 (seated 0.068) snugs under
    ///              the neck/head base. Gender-uniform 0.44 wide.)
    ///   SitTorso:  hip 0.40 → 0.46 top (narrow-bottom trapezium; the hips sit below this row and are
    ///              covered by the thighs, and the 0.46 top tucks under the Chest bottom).
    ///   Chest:     narrow bottom 0.48 → mid 0.64 (sit pivots at t≈0.68 → W 0.62 → world
    ///              0.39·0.62 = 0.244 vs ball ±0.25 → ~1 cm cap) → crown 0.18.
    /// Height still spans y ±0.5 so size.y scales it exactly like the old cube/ellipsoid.
    /// </summary>
    private static Mesh BuildTorso(string profileId)
    {
        const int bands = 8;
        float[] tB = { 0f, .125f, .250f, .375f, .500f, .625f, .750f, .875f, 1f };
        float[] wB, dB;
        if (profileId == "SitTorso")
        {
            wB = new[] { .40f, .42f, .44f, .45f, .46f, .46f, .46f, .46f, .46f };
            dB = new[] { .40f, .41f, .42f, .43f, .44f, .44f, .44f, .44f, .44f };
        }
        else if (profileId == "Chest")
        {
            wB = new[] { .48f, .52f, .56f, .60f, .63f, .64f, .60f, .44f, .18f };
            dB = new[] { .42f, .44f, .46f, .48f, .50f, .50f, .48f, .40f, .18f };
        }
        else
        {
            wB = new[] { .46f, .50f, .55f, .59f, .63f, .68f, .72f, .58f, .20f };
            dB = new[] { .42f, .44f, .46f, .48f, .50f, .51f, .52f, .42f, .20f };
        }

        var verts = new List<Vector3>();
        var uvs = new List<Vector2>();
        var norms = new List<Vector3>();
        var tris = new List<int>();
        int seed = AnchorSeed(profileId);
        int rows = bands + 1;

        // Rows are ellipse cross-sections (12 segs) whose width/depth follow the silhouette.
        // 1ep: rows sit on a deterministic non-uniform height schedule (endpoints t=0 and t=1
        // FIXED — the hip row, crown disc and the neck/pivot contract are untouched; silhouette is
        // piecewise-linear so the moved interior rows still interpolate cleanly). Columns share the
        // same theta schedule as the ellipsoid parts → panel sizes vary, mosaic stays watertight.
        float[] col = Positions(Steps(Segs, seed, 0x1E0F02, SegIrregularity), 2f * Mathf.PI);
        float[] tRow = Positions(Steps(bands, seed, 0x1E0F01, BandIrregularity), 1f);
        var corners = new Vector3[rows * Segs];
        for (int lat = 0; lat < rows; lat++)
        {
            float t = tRow[lat];
            float y = -Half + t;
            float W = Silhouette(t, tB, wB);
            float D = Silhouette(t, tB, dB);
            for (int s = 0; s < Segs; s++)
                corners[lat * Segs + s] = new Vector3(Mathf.Cos(col[s]) * W, y, Mathf.Sin(col[s]) * D);
        }
        var uf = new float[Segs];
        for (int s = 0; s < Segs; s++) uf[s] = col[s] / (2f * Mathf.PI);
        var vf = new float[rows];
        for (int lat = 0; lat < rows; lat++) vf[lat] = 1f - tRow[lat];

        // Deterministic shared-corner jitter (same scheme/seeds as the ellipsoid lattice → watertight).
        for (int lat = 1; lat < bands; lat++)
        {
            for (int s = 0; s < Segs; s++)
            {
                int k = lat * Segs + s;
                Vector3 r = corners[k].normalized;
                Vector3 t1 = Vector3.Cross(r, Vector3.up);
                if (t1.sqrMagnitude < 1e-6f)
                    t1 = Vector3.Cross(r, Vector3.right);
                t1.Normalize();
                Vector3 t2 = Vector3.Cross(r, t1).normalized;
                float h1 = Hash01(k, seed, 0x1234AB) - 0.5f;
                float h2 = Hash01(k, seed, 0x5678CD) - 0.5f;
                corners[k] += (t1 * h1 + t2 * h2) * (2f * FacetJitter);
            }
        }

        // Bands — the same square-panel / triangle-panel mosaic as the ellipsoid parts, over the
        // finer 8-band (9-row) torso schedule. 1e8: bound is `<= bands` so the 8th band (the steep
        // crown cone between t=0.875 and the t=1.0 crown row) is EMITTED — before, the dome band to
        // crown ring was skipped and the crown disc floated over an open gap (visible hole ring).
        // (The ellipsoid Generate loop keeps `b < Rings`: its lat=0/Rings rows are degenerate poles.)
        for (int b = 1; b <= bands; b++)
        {
            for (int s = 0; s < Segs; s++)
            {
                int s1 = (s + 1) % Segs;
                int ctl = (b - 1) * Segs + s, ctr = (b - 1) * Segs + s1;
                int cbl = b * Segs + s, cbr = b * Segs + s1;
                if (Hash01(b * 131 + s, seed, 0x5EEDF) < SplitChance)
                {
                    if (Hash01(b * 131 + s, seed, 0xCAFE) < 0.5f)
                    {
                        EmitTriangle(corners[ctl], corners[ctr], corners[cbr],
                            CornerUV(b - 1, s, uf, vf), CornerUV(b - 1, s1, uf, vf), CornerUV(b, s1, uf, vf), verts, uvs, norms, tris);
                        EmitTriangle(corners[ctl], corners[cbr], corners[cbl],
                            CornerUV(b - 1, s, uf, vf), CornerUV(b, s1, uf, vf), CornerUV(b, s, uf, vf), verts, uvs, norms, tris);
                    }
                    else
                    {
                        EmitTriangle(corners[ctr], corners[cbr], corners[cbl],
                            CornerUV(b - 1, s1, uf, vf), CornerUV(b, s1, uf, vf), CornerUV(b, s, uf, vf), verts, uvs, norms, tris);
                        EmitTriangle(corners[ctr], corners[cbl], corners[ctl],
                            CornerUV(b - 1, s1, uf, vf), CornerUV(b, s, uf, vf), CornerUV(b - 1, s, uf, vf), verts, uvs, norms, tris);
                    }
                }
                else
                {
                    EmitQuad(corners[ctl], corners[ctr], corners[cbr], corners[cbl],
                        CornerUV(b - 1, s, uf, vf), CornerUV(b - 1, s1, uf, vf), CornerUV(b, s1, uf, vf), CornerUV(b, s, uf, vf),
                        verts, uvs, norms, tris);
                }
            }
        }

        // Closed caps: bottom rim (waist/hip base) and a SMALL CROWN disc the neck cylinder tucks over
        // (per-triangle winding flips keep both orientations correct).
        int topOff = bands * Segs;
        for (int s = 0; s < Segs; s++)
        {
            int s1 = (s + 1) % Segs;
            EmitTriangle(new Vector3(0f, Half, 0f), corners[topOff + s1], corners[topOff + s],
                CornerUV(bands, s, uf, vf), CornerUV(bands, s1, uf, vf), CornerUV(bands, s, uf, vf), verts, uvs, norms, tris);
            EmitTriangle(new Vector3(0f, -Half, 0f), corners[s], corners[s1],
                CornerUV(0, s, uf, vf), CornerUV(0, s, uf, vf), CornerUV(0, s1, uf, vf), verts, uvs, norms, tris);
        }

        var mesh = new Mesh { name = "PlayerPart_Torso" };
        mesh.SetVertices(verts);
        mesh.SetUVs(0, uvs);
        mesh.SetNormals(norms);
        mesh.SetTriangles(tris, 0, true);
        mesh.RecalculateBounds();
        return mesh;
    }

    /// <summary>Piecewise-linear silhouette value for normalized height <paramref name="t"/>,
    /// interpolating the control-point arrays <paramref name="tB"/>/<paramref name="vB"/>.</summary>
    private static float Silhouette(float t, float[] tB, float[] vB)
    {
        if (t <= tB[0]) return vB[0];
        for (int i = 1; i < tB.Length; i++)
        {
            if (t <= tB[i])
                return Mathf.Lerp(vB[i - 1], vB[i], Mathf.InverseLerp(tB[i - 1], tB[i], t));
        }
        return vB[vB.Length - 1];
    }

    /// <summary>Emit one triangle with its OWN vertices and a flat (face) normal — the visible
    /// facet seams of the low-poly look. Winding is flipped per-panel against the outward radial.</summary>
    private static void EmitTriangle(Vector3 a, Vector3 b, Vector3 c, Vector2 ua, Vector2 ub, Vector2 uc,
        List<Vector3> verts, List<Vector2> uvs, List<Vector3> norms, List<int> tris)
    {
        Vector3 n = Vector3.Cross(b - a, c - a);
        if (Vector3.Dot(n, (a + b + c) * 0.3333333f) < 0f)
        {
            Vector3 t = b; b = c; c = t;
            Vector2 ut = ub; ub = uc; uc = ut;
            n = -n;
        }
        int iv = verts.Count;
        verts.Add(a); verts.Add(b); verts.Add(c);
        uvs.Add(ua); uvs.Add(ub); uvs.Add(uc);
        Vector3 nn = n.sqrMagnitude > 1e-10f ? n.normalized : Vector3.up;
        norms.Add(nn); norms.Add(nn); norms.Add(nn);
        tris.Add(iv); tris.Add(iv + 1); tris.Add(iv + 2);
    }

    /// <summary>Emit one square panel: 4 vertices, two coplanar triangles sharing a single flat
    /// normal so the cell renders as one flat square, not two shaded triangles.</summary>
    private static void EmitQuad(Vector3 tl, Vector3 tr, Vector3 br, Vector3 bl,
        Vector2 utl, Vector2 utr, Vector2 ubr, Vector2 ubl,
        List<Vector3> verts, List<Vector2> uvs, List<Vector3> norms, List<int> tris)
    {
        Vector3 n1 = Vector3.Cross(tr - tl, br - tl);
        Vector3 n2 = Vector3.Cross(bl - br, tl - br);
        Vector3 n = n1 + n2;
        bool flip = Vector3.Dot(n, (tl + tr + br + bl) * 0.25f) < 0f;
        if (flip)
            n = -n;
        int iv = verts.Count;
        verts.Add(tl); verts.Add(tr); verts.Add(br); verts.Add(bl);
        uvs.Add(utl); uvs.Add(utr); uvs.Add(ubr); uvs.Add(ubl);
        Vector3 nn = n.sqrMagnitude > 1e-10f ? n.normalized : Vector3.up;
        norms.Add(nn); norms.Add(nn); norms.Add(nn); norms.Add(nn);
        if (flip)
        {
            tris.Add(iv); tris.Add(iv + 2); tris.Add(iv + 1);
            tris.Add(iv); tris.Add(iv + 3); tris.Add(iv + 2);
        }
        else
        {
            tris.Add(iv); tris.Add(iv + 1); tris.Add(iv + 2);
            tris.Add(iv); tris.Add(iv + 2); tris.Add(iv + 3);
        }
    }

    /// <summary>Deterministic [0,1) hash — keeps every build of a profile byte-identical, so the
    /// cached facet pattern never drifts between sessions.</summary>
    private static float Hash01(int x, int seed, int salt)
    {
        unchecked
        {
            uint h = (uint)(x * 73856093 ^ seed * 19349663 ^ salt * 83492791);
            h = (h << 13) ^ (h >> 17);
            h *= 0x5bd1e995u;
            h ^= h >> 15;
            return (h & 0xffffffu) / (float)0xffffffu;
        }
    }

    private static int AnchorSeed(string profileId)
    {
        unchecked
        {
            int h = 7;
            for (int k = 0; k < profileId.Length; k++)
                h = (h * 31 + profileId[k]) & 0x7fffffff;
            return h;
        }
    }
}
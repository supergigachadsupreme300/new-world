using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Unit-space, CHUNKY LOW-POLY part meshes for the player model (1dw + 1dx): every body part that
/// used to be a scaled cube is now a faceted ellipsoid surface that occupies the same half-extent
/// cube [-0.5, 0.5] as the old shared unit cube, so the part GameObject's localScale = the old size
/// vector reproduces the exact same world dimensions the cube had. Each part is sculpted with
/// terrain-style "dent" ops (the WorldStreamer.DeformAt crater carve, generalised to 3D): ellipsoid
/// distance to an anchor, the same s = t²(3−2t) smoothstep, offset along the corner's original
/// radial. The sculpted corner skeleton is then emitted as FLAT-SHADED low-poly panels (1dx): every
/// band cell becomes a square panel or a pair of triangle panels (deterministic hash, squares
/// dominant), with a small deterministic tangent jitter on the SHARED corners so the mosaic reads
/// hand-cut while staying watertight — no cracks, no see-through.
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
        { "Body", new[]
        {
            new Dent(new Vector3(0f, -0.06f, 0f), new Vector3(0.30f, 0.18f, 0.20f), -0.06f), // waist pinch
            new Dent(new Vector3(0f, 0.10f, 0.05f), new Vector3(0.28f, 0.20f, 0.22f), 0.04f),  // chest raise
            new Dent(new Vector3(0f, -0.16f, 0f), new Vector3(0.28f, 0.12f, 0.20f), -0.03f),  // hip taper
        } },
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
        { "Pillar", new Dent[0] }, // square masonry neck column (1dz) — flat box stack, not ellipsoid
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
        { "SitTorso", new[]
        {
            new Dent(new Vector3(0f, -0.06f, 0f), new Vector3(0.30f, 0.18f, 0.20f), -0.05f), // waist pinch
            new Dent(new Vector3(0f, 0.10f, 0.05f), new Vector3(0.28f, 0.20f, 0.22f), 0.04f),// chest
        } },
        { "Joint", new Dent[0] }, // plain faceted ball joint (1dy) — no dents
        { "Chest", new[]
        {
            new Dent(new Vector3(0f, 0.08f, 0.06f), new Vector3(0.28f, 0.20f, 0.20f), 0.04f), // pec raise
            new Dent(new Vector3(0f, -0.08f, 0f), new Vector3(0.28f, 0.14f, 0.20f), -0.03f), // mid pinch
        } },
    };

    private static readonly Dictionary<string, Mesh> _cache = new Dictionary<string, Mesh>();

    /// <summary>
    /// Shared, cached unit-space ellipsoid mesh for a part profile (built once, main thread only).
    /// Unknown/empty ids fall back to the plain (unsculpted) ellipsoid.
    /// </summary>
    public static Mesh BuildEllipsoid(string profileId)
    {
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
        if (profileId == "Pillar")
            return BuildPillar();
        _profiles.TryGetValue(profileId, out var dents);
        int seed = AnchorSeed(profileId);
        int grid = (Rings + 1) * Segs;

        // Lattice of ellipsoid corners (Rings+1 rows of Segs around), poles handled separately.
        var corners = new Vector3[grid];
        for (int lat = 0; lat <= Rings; lat++)
        {
            float phi = lat * Mathf.PI / Rings;
            float py = Mathf.Cos(phi) * Half;
            float pr = Mathf.Sin(phi) * Half;
            for (int s = 0; s < Segs; s++)
            {
                float theta = s * (2f * Mathf.PI) / Segs;
                corners[lat * Segs + s] = new Vector3(Mathf.Cos(theta) * pr, py, Mathf.Sin(theta) * pr);
            }
        }
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
                CornerUV(0, s), CornerUV(1, s), CornerUV(1, s + 1), verts, uvs, norms, tris);

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
                            CornerUV(b - 1, s), CornerUV(b - 1, s1), CornerUV(b, s1), verts, uvs, norms, tris);
                        EmitTriangle(corners[ctl], corners[cbr], corners[cbl],
                            CornerUV(b - 1, s), CornerUV(b, s1), CornerUV(b, s), verts, uvs, norms, tris);
                    }
                    else // diagonal TR→BL
                    {
                        EmitTriangle(corners[ctr], corners[cbr], corners[cbl],
                            CornerUV(b - 1, s1), CornerUV(b, s1), CornerUV(b, s), verts, uvs, norms, tris);
                        EmitTriangle(corners[ctr], corners[cbl], corners[ctl],
                            CornerUV(b - 1, s1), CornerUV(b, s), CornerUV(b - 1, s), verts, uvs, norms, tris);
                    }
                }
                else
                {
                    EmitQuad(corners[ctl], corners[ctr], corners[cbr], corners[cbl],
                        CornerUV(b - 1, s), CornerUV(b - 1, s1), CornerUV(b, s1), CornerUV(b, s),
                        verts, uvs, norms, tris);
                }
            }
        }

        // South pole fan (all triangles).
        for (int s = 0; s < Segs; s++)
            EmitTriangle(poleS, corners[(Rings - 1) * Segs + s], corners[(Rings - 1) * Segs + (s + 1) % Segs],
                CornerUV(Rings, s), CornerUV(Rings - 1, s), CornerUV(Rings - 1, s + 1), verts, uvs, norms, tris);

        var mesh = new Mesh { name = "PlayerPart_" + profileId };
        mesh.SetVertices(verts);
        mesh.SetUVs(0, uvs);
        mesh.SetNormals(norms);
        mesh.SetTriangles(tris, 0, true);
        mesh.RecalculateBounds();
        return mesh;
    }

    private static Vector2 CornerUV(int lat, int s) => new Vector2((float)(s % Segs) / Segs, 1f - (float)lat / Rings);

    /// <summary>
    /// Unit-space square masonry pillar (1dz): a foot slab (full width), a straight shaft, and a cap/
    /// abacus slab — the neck switched from the faceted round ellipsoid to this straight column. Same
    /// half-extent cube [-0.5, 0.5] contract, so localScale = size vector still reproduces dimensions.
    /// </summary>
    private static Mesh BuildPillar()
    {
        var verts = new List<Vector3>();
        var uvs = new List<Vector2>();
        var norms = new List<Vector3>();
        var tris = new List<int>();

        // Foot slab (full width) → shaft → cap (abacus), stacked and fully closed. The interior
        // faces between the slabs are hidden (backface-culled) and cost nothing visible.
        BuildBox(new Vector3(-0.50f, -0.50f, -0.50f), new Vector3(0.50f, -0.34f, 0.50f), verts, uvs, norms, tris);
        BuildBox(new Vector3(-0.30f, -0.34f, -0.30f), new Vector3(0.30f, 0.32f, 0.30f), verts, uvs, norms, tris);
        BuildBox(new Vector3(-0.42f, 0.32f, -0.42f), new Vector3(0.42f, 0.50f, 0.42f), verts, uvs, norms, tris);

        var mesh = new Mesh { name = "PlayerPart_Pillar" };
        mesh.SetVertices(verts);
        mesh.SetUVs(0, uvs);
        mesh.SetNormals(norms);
        mesh.SetTriangles(tris, 0, true);
        mesh.RecalculateBounds();
        return mesh;
    }

    /// <summary>Emit one axis-aligned box as 6 flat-shaded quads (reuses EmitQuad's per-face outward
    /// winding check against each face centroid).</summary>
    private static void BuildBox(Vector3 min, Vector3 max,
        List<Vector3> verts, List<Vector2> uvs, List<Vector3> norms, List<int> tris)
    {
        Vector2 u0 = Vector2.zero, u1 = Vector2.one;
        EmitQuad(new Vector3(max.x, min.y, min.z), new Vector3(max.x, min.y, max.z), new Vector3(max.x, max.y, max.z), new Vector3(max.x, max.y, min.z), u0, u1, u1, u0, verts, uvs, norms, tris); // +X
        EmitQuad(new Vector3(min.x, min.y, max.z), new Vector3(min.x, min.y, min.z), new Vector3(min.x, max.y, min.z), new Vector3(min.x, max.y, max.z), u0, u1, u1, u0, verts, uvs, norms, tris); // -X
        EmitQuad(new Vector3(min.x, max.y, min.z), new Vector3(max.x, max.y, min.z), new Vector3(max.x, max.y, max.z), new Vector3(min.x, max.y, max.z), u0, u1, u1, u0, verts, uvs, norms, tris); // +Y
        EmitQuad(new Vector3(min.x, min.y, max.z), new Vector3(max.x, min.y, max.z), new Vector3(max.x, min.y, min.z), new Vector3(min.x, min.y, min.z), u0, u1, u1, u0, verts, uvs, norms, tris); // -Y
        EmitQuad(new Vector3(min.x, min.y, max.z), new Vector3(max.x, min.y, max.z), new Vector3(max.x, max.y, max.z), new Vector3(min.x, max.y, max.z), u0, u1, u1, u0, verts, uvs, norms, tris); // +Z
        EmitQuad(new Vector3(max.x, min.y, min.z), new Vector3(min.x, min.y, min.z), new Vector3(min.x, max.y, min.z), new Vector3(max.x, max.y, min.z), u0, u1, u1, u0, verts, uvs, norms, tris); // -Z
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
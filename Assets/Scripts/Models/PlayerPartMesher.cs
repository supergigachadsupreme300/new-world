using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Unit-space ellipsoid part meshes for the player model (1dw): every body part that used to be a
/// scaled cube is now a tessellated ellipsoid surface that occupies the same half-extent cube
/// [-0.5, 0.5] as the old shared unit cube, so the part GameObject's localScale = the old size
/// vector reproduces the exact same world dimensions the cube had. Each part is then sculpted with
/// terrain-style "dent" ops (the WorldStreamer.DeformAt crater carve, generalised to 3D): per-vertex
/// influence from a normalized ellipsoid distance to an anchor, the same s = t²(3−2t) smoothstep,
/// then an offset along the vertex's original radial direction.
///
/// Meshes are STATIC and SIZE-INDEPENDENT (a cached mesh serves every gender, race ratio and model
/// variant — the parts are sized purely through Transform.localScale, which is exactly how
/// ApplyRaceLook / ApplyRaceRatioRecurse / WeaponRigBuilder.ScaleForWorld keep working). Main thread
/// only (built from MapBuilder on first use).
/// </summary>
public static class PlayerPartMesher
{
    private const int Rings = 9; // ring rows between the poles
    private const int Segs = 16; // columns around the equator
    private const float Half = 0.5f;

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
        _profiles.TryGetValue(profileId, out var dents);

        int rowCount = Rings - 1;
        int vertexCount = rowCount * Segs + 2;
        var verts = new Vector3[vertexCount];
        var uvs = new Vector2[vertexCount];
        var indices = new List<int>(rowCount * Segs * 2 * 2 + 2 * Segs);

        int poleN = 0;
        verts[poleN] = new Vector3(0f, Half, 0f);
        uvs[poleN] = new Vector2(Half, 1f);

        for (int lat = 1; lat < Rings; lat++)
        {
            float phi = lat * Mathf.PI / Rings;
            float py = Mathf.Cos(phi) * Half;
            float pr = Mathf.Sin(phi) * Half;
            for (int th = 0; th < Segs; th++)
            {
                float theta = th * (2f * Mathf.PI) / Segs;
                int v = (lat - 1) * Segs + th + 1;
                verts[v] = new Vector3(Mathf.Cos(theta) * pr, py, Mathf.Sin(theta) * pr);
                uvs[v] = new Vector2(th / (float)Segs, 1f - phi / Mathf.PI);
            }
        }
        int poleS = vertexCount - 1;
        verts[poleS] = new Vector3(0f, -Half, 0f);
        uvs[poleS] = new Vector2(Half, 0f);

        for (int th = 0; th < Segs; th++) // north cap
        {
            indices.Add(poleN);
            indices.Add(1 + th);
            indices.Add(1 + (th + 1) % Segs);
        }
        for (int lat = 1; lat < Rings - 1; lat++) // latitude bands
        {
            int row = (lat - 1) * Segs + 1;
            int nrow = row + Segs;
            for (int th = 0; th < Segs; th++)
            {
                int th2 = (th + 1) % Segs;
                int a = row + th, b = nrow + th, c = nrow + th2, d = row + th2;
                indices.Add(a); indices.Add(b); indices.Add(c);
                indices.Add(a); indices.Add(c); indices.Add(d);
            }
        }
        int lastRow = (rowCount - 1) * Segs + 1;
        for (int th = 0; th < Segs; th++) // south cap
        {
            indices.Add(poleS);
            indices.Add(lastRow + (th + 1) % Segs);
            indices.Add(lastRow + th);
        }

        // Winding check: the first face's normal must point away from the mesh origin.
        Vector3 f0 = verts[indices[0]];
        Vector3 fn = Vector3.Cross(verts[indices[1]] - f0, verts[indices[2]] - f0);
        if (Vector3.Dot(fn, f0) < 0f)
        {
            for (int t = 0; t + 2 < indices.Count; t += 3)
            {
                int tmp = indices[t + 1];
                indices[t + 1] = indices[t + 2];
                indices[t + 2] = tmp;
            }
        }

        // Terrain-style sculpt (1dw): influence from ellipsoid distance → the DeformAt smoothstep
        // s = t²(3−2t) → offset along the vertex's original radial, exactly like a heightfield dent.
        var radial = new Vector3[vertexCount];
        for (int v = 0; v < vertexCount; v++)
        {
            Vector3 p = verts[v];
            radial[v] = p.sqrMagnitude > 1e-6f ? p.normalized : new Vector3(0f, p.y >= 0f ? 1f : -1f, 0f);
        }
        if (dents != null && dents.Length > 0)
        {
            for (int v = 0; v < vertexCount; v++)
            {
                Vector3 p = verts[v];
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
                    float s = inf * inf * (3f - 2f * inf);
                    p += radial[v] * (s * d.Strength);
                }
                verts[v] = p;
            }
        }

        var mesh = new Mesh { name = "PlayerPart_" + profileId };
        mesh.SetVertices(verts);
        mesh.SetUVs(0, uvs);
        mesh.SetTriangles(indices, 0, true);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }
}
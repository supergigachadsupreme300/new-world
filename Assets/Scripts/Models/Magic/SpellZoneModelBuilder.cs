using UnityEngine;

/// <summary>
/// The area-effect spell's drawn body — the pull funnel and the persistent ground zone.
/// <para>Extracted verbatim from <c>Magic/Cast/SpellZone.cs</c> in <b>1jd</b>: "zone model" matched
/// nothing under <c>Models/</c> while the model shipped. Shapes, names, counts and the shared
/// <see cref="SkillFx.SharedSpriteMaterial"/> all unchanged.</para>
/// </summary>
public static class SpellZoneModelBuilder
{
    /// <summary>Funnel: tapering stack of 7 spinning flat rings + 6 orbiting debris blocks
    /// (tornado / vortex). Placed in the parent's local frame; every collider is destroyed and the
    /// zone's own tick never queries a piece, so this is body-only by construction.</summary>
    public static void BuildFunnel(Transform parent, float radius, Material sharedMat)
    {
        const float height = 2.8f;
        const int rings = 7;
        for (int i = 0; i < rings; i++)
        {
            float t = i / (float)(rings - 1);
            float r = Mathf.Lerp(0.12f, radius * 0.85f, t);

            var ring = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ring.name = "SpellRing_" + i;
            DestroyCollider(ring);
            ring.transform.SetParent(parent, false);
            ring.transform.localPosition = new Vector3(0f, r * height, 0f);
            ring.transform.localScale = new Vector3(r, 1f, r);
            ring.transform.localRotation = Quaternion.Euler(0f, 45f - i * 14f, 0f);
            ApplyShared(ring, sharedMat);
        }

        for (int i = 0; i < 6; i++)
        {
            float t = i / 5f;
            var block = GameObject.CreatePrimitive(PrimitiveType.Cube);
            block.name = "SpellDebris_" + i;
            DestroyCollider(block);
            block.transform.SetParent(parent, false);
            float ang = i * 60f + 30f;
            float orbit = Mathf.Lerp(radius * 0.7f, 0.25f, t);
            block.transform.localPosition = new Vector3(
                Mathf.Cos(ang * Mathf.Deg2Rad) * orbit,
                t * height * 0.8f,
                Mathf.Sin(ang * Mathf.Deg2Rad) * orbit);
            block.transform.localScale = Vector3.one * Mathf.Lerp(0.22f, 0.08f, t);
            ApplyShared(block, sharedMat);
        }
    }

    /// <summary>Disc: a single wide flat ring on the ground for persistent AoE zones, plus the
    /// floating halo above it. Two pieces under two names, so the caller that animates the halo can
    /// still find it by name.</summary>
    public static void BuildGroundZone(Transform parent, float radius, Material sharedMat)
    {
        var disc = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        disc.name = "SpellDisc";
        DestroyCollider(disc);
        disc.transform.SetParent(parent, false);
        disc.transform.localScale = new Vector3(radius * 2f, 0.02f, radius * 2f);
        ApplyShared(disc, sharedMat);

        var halo = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        halo.name = "SpellHalo";
        DestroyCollider(halo);
        halo.transform.SetParent(parent, false);
        halo.transform.localPosition = new Vector3(0f, 0.9f, 0f);
        halo.transform.localScale = new Vector3(radius * 1.6f, 0.03f, radius * 1.6f);
        ApplyShared(halo, sharedMat);
    }

    private static void DestroyCollider(GameObject go)
    {
        Collider col = go.GetComponent<Collider>();
        if (col != null) Object.Destroy(col);
    }

    /// <summary>1ie: the zone's pieces take the per-spell <c>Core</c> colour through a cache keyed by
    /// colour, so the whole funnel shares ONE material. <c>sharedMaterial</c> (not
    /// <c>material</c>) is deliberate — assigning <c>material</c> would clone a Material per piece and
    /// the shared-cache rationale above would silently stop holding.</summary>
    private static void ApplyShared(GameObject go, Material sharedMat)
    {
        var r = go.GetComponent<MeshRenderer>();
        if (r != null && sharedMat != null) r.sharedMaterial = sharedMat;
    }
}
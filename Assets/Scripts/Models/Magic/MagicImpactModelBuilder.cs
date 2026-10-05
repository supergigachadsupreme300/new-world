using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Procedural models for the eight <see cref="SpellImpactStyle"/> impact-flash families. Split out of
/// SpellImpactFx by 1jb so the impact models are findable by name. Previously the geometry lived as
/// seven private instance methods on <c>SpellImpactFx.ImpactFlash</c> — a nested
/// <c>MonoBehaviour</c> inside a pooling class — so the only way to find "what does an impact look
/// like" was to know it was filed under the pool.
///
/// What moved here, and what deliberately did not:
///
/// 1. MOVED: the per-style part dispatch (the <c>SpellImpactStyle</c> switch) and every part builder
///    (Ring/Disc/Cylinder/Sphere/Blade/Column/ShardCluster, plus StripCollider and Register).
///
/// 2. STAYED in SpellImpactFx: the pool (<see cref="SpellImpactFx.PoolCap"/>,
///    <see cref="SpellImpactFx.PerFrameBudget"/>), <c>Acquire</c>/<c>Recycle</c>, and ImpactFlash's
///    Play/Update. Those are lifetime, not geometry: the flash grows, spins its shards, fades and
///    returns itself to the pool. A model builder has no business owning that.
///
/// ONE STRUCTURAL CHANGE, and it is the point of the split rather than a side effect. The part
/// builders used to push into three instance lists on the flash — <c>_materials</c>, <c>_parts</c>,
/// <c>_spins</c> — which the fade and tumble loops then walked by the SAME INDEX. Three parallel
/// lists that must stay index-aligned is precisely rule 8's "the addressing rule of a copy IS the
/// contract", and it was invisible: adding a part without a matching spin flag, or a builder that
/// registered a transform but not a material, would have shown up as a wrong-coloured or
/// non-tumbling fragment with nothing wrong anywhere. <see cref="Build"/> now returns ONE list of
/// <see cref="Part"/>, each entry carrying its own transform, material and spin flag, so alignment
/// is unrepresentable rather than merely correct. ImpactFlash keeps a single
/// <c>List&lt;MagicImpactModelBuilder.Part&gt;</c>.
///
/// Per-instance materials are load-bearing and are preserved exactly: the fade writes
/// <c>Material.color</c> in place, so a shared or cached material would corrupt every other live
/// flash of the same colour (see SpellImpactFx's class remarks and SkillFx.SharedSpriteMaterial).
/// Each part gets a fresh <c>new Material(shader)</c>, as before.
/// </summary>
internal static class MagicImpactModelBuilder
{
    /// <summary>
    /// One built part: the transform to tumble or leave flat, the per-instance material the fade
    /// mutates, and whether it spins. A struct rather than three parallel lists precisely so these
    /// three facts cannot drift apart.
    /// </summary>
    internal readonly struct Part
    {
        /// <summary>The part's own transform, parented under the flash root.</summary>
        public readonly Transform T;

        /// <summary>Per-instance material; the fade mutates <c>.color</c> in place.</summary>
        public readonly Material Mat;

        /// <summary>True for tumbling shards; false for rings/foot/column, which stay flat.</summary>
        public readonly bool Spins;

        public Part(Transform t, Material mat, bool spins)
        {
            T = t;
            Mat = mat;
            Spins = spins;
        }
    }

    /// <summary>
    /// Build the geometry for one impact style under <paramref name="root"/> and return what was
    /// built. The caller owns the root and the returned parts; nothing here is pooled, faded or
    /// ticked, so the same call serves a pooled flash and a one-shot preview alike.
    /// </summary>
    internal static List<Part> Build(Transform root, SpellImpactStyle style, Shader shader)
    {
        var parts = new List<Part>();

        switch (style)
        {
            case SpellImpactStyle.Burst:
                ShardCluster(root, shader, 5, 0.62f, parts);
                break;
            case SpellImpactStyle.Ring:
                Ring(root, shader, 1f, "Ring", parts);
                break;
            case SpellImpactStyle.Sphere:
                Sphere(root, shader, "Core", 0.7f, parts);
                Sphere(root, shader, "Halo", 1f, parts);
                break;
            case SpellImpactStyle.Cross:
                Disc(root, shader, "Disc", 0.95f, parts);
                Blade(root, shader, "BarA", 0f, parts);
                Blade(root, shader, "BarB", 90f, parts);
                break;
            case SpellImpactStyle.Shards:
                ShardCluster(root, shader, 7, 1f, parts);
                Ring(root, shader, 0.55f, "Trace", parts);
                break;
            case SpellImpactStyle.Bloom:
                Sphere(root, shader, "Core", 0.55f, parts);
                Sphere(root, shader, "Mid", 0.85f, parts);
                Sphere(root, shader, "Out", 1.15f, parts);
                break;
            case SpellImpactStyle.Pillar:
                Column(root, shader, parts);
                Ring(root, shader, 0.8f, "Foot", parts);
                break;
            default:
                Ring(root, shader, 1f, "Ring", parts);
                break;
        }

        return parts;
    }

    // ------------------------------------------------------------ part builders

    private static void Ring(Transform parent, Shader shader, float rel, string name, List<Part> parts)
        => Cylinder(parent, shader, name, rel, 0.06f, parts);

    private static void Disc(Transform parent, Shader shader, string name, float rel, List<Part> parts)
        => Cylinder(parent, shader, name, rel, 0.05f, parts);

    private static void Cylinder(Transform parent, Shader shader, string name, float rel, float thickness,
        List<Part> parts)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        StripCollider(go);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localScale = new Vector3(rel, thickness, rel);
        Register(go.transform, shader, spins: false, parts: parts);
    }

    private static void Sphere(Transform parent, Shader shader, string name, float rel, List<Part> parts)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        StripCollider(go);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localScale = Vector3.one * rel;
        Register(go.transform, shader, spins: false, parts: parts);
    }

    private static void Blade(Transform parent, Shader shader, string name, float yawDeg, List<Part> parts)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        StripCollider(go);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localRotation = Quaternion.Euler(0f, yawDeg, 0f);
        go.transform.localScale = new Vector3(1.9f, 0.05f, 0.16f);
        Register(go.transform, shader, spins: false, parts: parts);
    }

    private static void Column(Transform parent, Shader shader, List<Part> parts)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        StripCollider(go);
        go.name = "Column";
        go.transform.SetParent(parent, false);
        // Tall and thin, lifted so it stands ON the hit point rather than straddling it.
        go.transform.localPosition = new Vector3(0f, 0.9f, 0f);
        go.transform.localScale = new Vector3(0.34f, 1.15f, 0.34f);
        Register(go.transform, shader, spins: false, parts: parts);
    }

    private static void ShardCluster(Transform parent, Shader shader, int count, float rel, List<Part> parts)
    {
        for (int i = 0; i < count; i++)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            StripCollider(go);
            go.name = "Shard" + i;
            go.transform.SetParent(parent, false);
            float ang = i * (360f / count) + (i % 2) * 12f;
            float rad = rel * (0.35f + (i % 3) * 0.16f);
            float a = ang * Mathf.Deg2Rad;
            go.transform.localPosition = new Vector3(Mathf.Cos(a) * rad, 0.05f + i * 0.03f, Mathf.Sin(a) * rad);
            go.transform.localRotation = Quaternion.Euler(28f + i * 9f, ang, 12f * i);
            float s = rel * (0.26f - i * 0.02f);
            go.transform.localScale = new Vector3(s, s * 0.7f, s);
            Register(go.transform, shader, spins: true, parts: parts);
        }
    }

    private static void StripCollider(GameObject go)
    {
        Collider col = go.GetComponent<Collider>();
        if (col == null) return;
        // Disable BEFORE Destroy. Destroy is deferred to the end of the frame, so a collider left
        // enabled stays in the physics scene for the rest of it — harmless only because
        // ImpactFlash.Build() deactivates the root before the next FixedUpdate, which is an invariant
        // two files away. Making the removal immediate keeps it local.
        col.enabled = false;
        Object.Destroy(col);
    }

    private static void Register(Transform t, Shader shader, bool spins, List<Part> parts)
    {
        MeshRenderer r = t.GetComponent<MeshRenderer>();
        if (r == null) return;
        // Per-instance material: the fade mutates .color in place, so a shared one would
        // corrupt every other live flash of the same colour.
        Material mat = new Material(shader);
        r.material = mat;
        parts.Add(new Part(t, mat, spins));
    }
}

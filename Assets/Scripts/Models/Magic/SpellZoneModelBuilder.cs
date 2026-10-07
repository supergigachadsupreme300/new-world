using UnityEngine;

/// <summary>
/// The area-effect spell's drawn body — the pull funnel and the persistent ground zone.
/// <para>Extracted verbatim from <c>Magic/Cast/SpellZone.cs</c> in <b>1jd</b>: "zone model" matched
/// nothing under <c>Models/</c> while the model shipped. Shapes, names, counts and the shared
/// <see cref="SkillFx.SharedSpriteMaterial"/> all unchanged.
/// <para><b>1kc adds <see cref="BuildConflagration"/></b> — the Firestorm/Conflagration look (ground
///   circle + edge particles rising in a vortex), authored on Conflagration alone through
///   <c>SpellLook.ZoneBody.VortexCircle</c>.</para></para>
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

    /// <summary>The 1kc Conflagration/Firestorm body: a flat ground circle whose edge particles fly
    /// up in a vortex, converging as they rise. Authored on Conflagration alone
    /// (<c>SpellLook.ZoneBody.VortexCircle</c>); every other zone keeps
    /// <see cref="BuildFunnel"/>/<see cref="BuildGroundZone"/>. Two colours, both through the
    /// colour-keyed shared cache: the circle takes the spell's Core, the rising flames the hot end
    /// (<see cref="SpellLook.HotCore"/>) — the same hot-end choice SpellBeam's funnel makes.</summary>
    public static void BuildConflagration(Transform parent, float radius, Color core, Color hot)
    {
        Material coreMat = SkillFx.SharedSpriteMaterial(core);
        Material hotMat = SkillFx.SharedSpriteMaterial(hot);

        var disc = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        disc.name = "SpellCircle";
        DestroyCollider(disc);
        disc.transform.SetParent(parent, false);
        disc.transform.localScale = new Vector3(radius * 2f, 0.02f, radius * 2f);
        ApplyShared(disc, coreMat);

        const int embers = 16;
        const float risesPerSecond = 0.45f;
        const float turnsPerRise = 1.6f;
        const float shrink = 0.72f;
        float height = Mathf.Max(2.5f, radius * 1.6f);
        for (int i = 0; i < embers; i++)
        {
            var ember = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ember.name = "SpellEmber_" + i;
            DestroyCollider(ember);
            ember.transform.SetParent(parent, false);
            ember.transform.localScale = Vector3.one * 0.16f;
            ember.transform.localRotation = Quaternion.Euler(0f, i * 22.5f, 0f);
            ApplyShared(ember, hotMat);
            ember.AddComponent<VortexRiser>().Init(i / (float)embers, risesPerSecond, turnsPerRise, radius, height, shrink);
        }
    }

    /// <summary>Moves one ember of the 1kc conflagration body (rule 19, twice): a per-frame effect
    /// belongs to the loop that runs while it MOVES, not in the shape factory — so the factory
    /// attaches this component and this component owns the rise, the same split
    /// <c>SpellStormModelBuilder.BoltFader</c> uses for the storm bolts. The ember climbs the edge
    /// circle and spirals inward toward the apex as it rises; the zone root's own pull-spin adds to
    /// the whirl. Dies with the zone (it is a child of the zone root — nothing detaches it).</summary>
    private sealed class VortexRiser : MonoBehaviour
    {
        private float _phase;
        private float _speed;
        private float _turns;
        private float _radius;
        private float _height;
        private float _shrink;
        private Vector3 _baseScale;

        public void Init(float phase, float speed, float turns, float radius, float height, float shrink)
        {
            _phase = phase;
            _speed = speed;
            _turns = turns;
            _radius = radius;
            _height = height;
            _shrink = shrink;
            _baseScale = transform.localScale;
        }

        private void Update()
        {
            float cyc = Mathf.Repeat(_phase + Time.time * _speed, 1f);
            float ang = cyc * _turns * Mathf.PI * 2f;
            float orbit = _radius * (1f - _shrink * cyc);
            transform.localPosition = new Vector3(
                Mathf.Cos(ang) * orbit,
                cyc * _height,
                Mathf.Sin(ang) * orbit);
            // Gentle shimmer so each tongue of flame reads alive — small and slow compared to the
            // beam's 23/41 Hz flutter (1jz/1kb), never a body-size "breathe".
            float shimmer = 1f + 0.15f * Mathf.Sin(ang * 5f + _phase * 7f);
            transform.localScale = _baseScale * shimmer;
        }
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
using UnityEngine;

/// <summary>
/// The area-effect spell's drawn body — the pull funnel and the persistent ground zone.
/// <para>Extracted verbatim from <c>Magic/Cast/SpellZone.cs</c> in <b>1jd</b>: "zone model" matched
/// nothing under <c>Models/</c> while the model shipped. Shapes, names, counts and the shared
/// <see cref="SkillFx.SharedSpriteMaterial"/> all unchanged.
/// <para><b>1kc adds <see cref="BuildConflagration"/></b> — the Firestorm/Conflagration look (ground
///   circle + edge particles rising in a vortex), authored on Conflagration alone through
///   <c>ZoneBody.VortexCircle</c>.</para></para>
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

    /// <summary>The 1kc Conflagration body: a flat ground circle with a rising curtain of ember
    /// tongues. Authored on Conflagration alone (<c>ZoneBody.VortexCircle</c>). (1kf: Firestorm
    /// draws <see cref="BuildFunnel"/> instead.) Two colours, both through the
    /// colour-keyed shared cache: the circle takes the spell's Core, the rising flames the hot end
    /// (<see cref="SpellLook.HotCore"/>) — the same hot-end choice SpellBeam's funnel makes.
    /// <para><b>1l3</b> ("only create one ember ray on the circle edge, i want more"): every ember
    /// used to ride the SAME helix, so evenly-phased cubes traced ONE spiral strand. They are now
    /// dealt into <c>rays</c> PARALLEL strands (a per-ember angular offset, <c>angOffset</c>), so the
    /// rim carries several distinct tongues of flame.</para>
    /// <para><b>1l6t</b> ("stop being sucked into the centre … double the particles … make them
    /// appear from below, not all at once"): the tongues now rise <b>straight up at the ring
    /// radius</b> — the old convergent helix (<c>orbit = radius·(1 − 0.80·cyc²)</c>) pulled every
    /// ember to the centre as it climbed; the count doubled (48 → 96 = 12 strands of 8); and each
    /// ember now <b>starts at the ground and climbs</b>, its own age-gated rise plus a bottom/top
    /// scale envelope, so the curtain builds up from the ground instead of every ember popping into
    /// place at once. The apex stays <c>max(3.2, radius·2.2)</c> with 1.8 turns per rise.</para></summary>
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

        const int rays = 12;                   // 1l6t: parallel flame strands around the ring (was 6)
        const int perRay = 8;                  // 8 tongues spread along each strand's climb
        const int embers = rays * perRay;      // 96 total (doubled from 48)
        const float risesPerSecond = 0.45f;
        const float turnsPerRise = 1.8f;
        const float buildSeconds = 0.7f;       // the curtain fills from the ground over this window
        float height = Mathf.Max(3.2f, radius * 2.2f);
        for (int i = 0; i < embers; i++)
        {
            int ray = i % rays;
            int along = i / rays;
            float angOffset = ray * (2f * Mathf.PI / rays);              // separate the strands
            float startDelay = along / (float)(perRay - 1) * buildSeconds; // tiers emerge bottom-first
            var ember = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ember.name = "SpellEmber_" + i;
            DestroyCollider(ember);
            ember.transform.SetParent(parent, false);
            ember.transform.localScale = Vector3.one * 0.16f;
            ember.transform.localRotation = Quaternion.Euler(0f, angOffset * Mathf.Rad2Deg, 0f);
            ApplyShared(ember, hotMat);
            ember.AddComponent<VortexRiser>().Init(angOffset, startDelay, risesPerSecond, turnsPerRise, radius, height, i);
        }
    }

    /// <summary>Moves one ember of the conflagration body (rule 19: the shape factory builds, the
    /// component that runs while the pieces MOVE animates — the same split <c>BoltFader</c> uses for
    /// the storm bars). 1l6t: the ember climbs the ring radius <b>straight up</b> — the old helix
    /// pulled it toward the centre as it rose, the "sucked into the centre" report — starting from
    /// the ground after its own <c>startDelay</c> so the curtain builds bottom-first, and fading in
    /// low / out high so it reads as a tongue of flame that <b>appears from below and rises</b>. The
    /// zone root's own pull-spin adds to the whirl. Dies with the zone (it is a child of the zone
    /// root — nothing detaches it).</summary>
    private sealed class VortexRiser : MonoBehaviour
    {
        // Fraction of the climb spent fading in at the ground and out at the top, so the ends of
        // the curtain melt in/out rather than every ember snapping to full size.
        private const float FadeFraction = 0.22f;

        private float _angOffset;
        private float _startDelay;
        private float _speed;
        private float _turns;
        private float _radius;
        private float _height;
        private float _seed;
        private Vector3 _baseScale;
        private float _age;

        public void Init(float angOffset, float startDelay, float speed, float turns,
            float radius, float height, float seed)
        {
            _angOffset = angOffset;
            _startDelay = startDelay;
            _speed = speed;
            _turns = turns;
            _radius = radius;
            _height = height;
            _seed = seed;
            _baseScale = transform.localScale;
            // Parked at zero until its turn, so nothing shows above the ground before its tier's
            // delay elapses — this is what makes the curtain build from the ground up.
            transform.localScale = Vector3.zero;
        }

        private void Update()
        {
            _age += Time.deltaTime;
            float t = _age - _startDelay;
            if (t < 0f) return;   // still waiting below the ground — stays at zero scale

            float cyc = Mathf.Repeat(t * _speed, 1f);
            float ang = _angOffset + cyc * _turns * Mathf.PI * 2f;
            // 1l6t: constant radius — rise straight up at the ring edge, no inward convergence.
            transform.localPosition = new Vector3(
                Mathf.Cos(ang) * _radius,
                cyc * _height,
                Mathf.Sin(ang) * _radius);
            // Appears low, vanishes high, so the eye reads a stream rising from the ground rather
            // than a set of cubes that all pop into place at once.
            float env = Mathf.Clamp01(cyc / FadeFraction) * Mathf.Clamp01((1f - cyc) / FadeFraction);
            // Gentle shimmer so each tongue of flame reads alive — small and slow compared to the
            // beam's 23/41 Hz flutter (1jz/1kb), never a body-size "breathe".
            float shimmer = 1f + 0.15f * Mathf.Sin(ang * 5f + _seed * 7f);
            transform.localScale = _baseScale * (env * shimmer);
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
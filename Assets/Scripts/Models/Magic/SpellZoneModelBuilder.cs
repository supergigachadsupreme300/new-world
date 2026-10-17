using UnityEngine;

/// <summary>
/// The area-effect spell's drawn body — the pull funnel and the persistent ground zone.
/// <para>Extracted verbatim from <c>Magic/Cast/SpellZone.cs</c> in <b>1jd</b>: "zone model" matched
/// nothing under <c>Models/</c> while the model shipped. Shapes, names, counts and the shared
/// <see cref="SkillFx.SharedSpriteMaterial"/> all unchanged.
/// <para><b>1kc adds <see cref="BuildConflagration"/></b> — the Firestorm/Conflagration look (ground
///   circle + ember rings rising in a vortex), first authored on Conflagration through
///   <c>ZoneBody.VortexCircle</c> (1l6u: the rings are spawned continuously by
///   <c>ConflagrationEmitter</c>). <b>1l100</b>: Blizzard wears the same body through the STORM axis
///   (<c>StormBody.VortexCircle</c>, built by <c>SpellStorm</c>) — one builder, three gates (the
///   Conflagration zone, the storm axis, the QA bench), the same one-implementation-many-gates shape
///   <see cref="BuildFrozenField"/> has.</para>
/// <para><b>1l6z adds <see cref="BuildFrozenField"/></b> — the Deep Freeze look (a frost disc ringed
///   by a rosette of ice spikes), first authored through <c>ZoneBody.FrozenField</c>. <b>1l70</b>:
///   Deep Freeze moved from Zone to Storm delivery, so today the field is mounted as a STORM body
///   (<c>StormBody.FrozenField</c>, built by <c>SpellStorm</c>) — one builder, two delivery gates —
///   and the <c>ZoneBody.FrozenField</c> variant is dormant: nobody authors it, the same shape is
///   drawn under the storm's strikes.</para></para>
/// </summary>
public static class SpellZoneModelBuilder
{
    /// <summary>Funnel: tapering stack of 7 spinning flat rings + 6 orbiting debris blocks
    /// (tornado / vortex). Placed in the parent's local frame; every collider is destroyed and the
    /// zone's own tick never queries a piece, so this is body-only by construction. (1l85: each ring is
    /// scaled to overlap the next, so the taper reads as one body with no gaps between the rings.)</summary>
    public static void BuildFunnel(Transform parent, float radius, Material sharedMat)
    {
        const float height = 2.8f;
        const int rings = 7;
        const float bottomR = 0.12f;
        float topR = radius * 0.85f;
        // 1l85: ring centres are at r*height with r linear in the index, so consecutive centres sit a
        // UNIFORM span apart vertically. The rings used to be flat 1-unit cubes, so once that span
        // opened past 1 the stack showed daylight between every pair ("the firestorm skill models has
        // gaps between cubes"). Give each ring a thickness that OVERLAPS its neighbour — the same
        // overlap trick the beam funnel uses at its mouth — so the tornado reads as one body.
        // Clamped because radius is caller-supplied (a pull zone can be narrow): a span that shrinks
        // toward zero, or inverts, must never reach a Cube as a zero/negative scale.
        float ringSpan = Mathf.Abs(height * (topR - bottomR)) / (rings - 1);
        float ringThickness = Mathf.Max(ringSpan, 0.1f) * 1.25f;
        for (int i = 0; i < rings; i++)
        {
            float t = i / (float)(rings - 1);
            float r = Mathf.Lerp(bottomR, topR, t);

            var ring = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ring.name = "SpellRing_" + i;
            DestroyCollider(ring);
            ring.transform.SetParent(parent, false);
            ring.transform.localPosition = new Vector3(0f, r * height, 0f);
            ring.transform.localScale = new Vector3(r, ringThickness, r);
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

    /// <summary>1l6z: the Deep Freeze body — a frozen field. A flat frost disc (<c>Core</c>) ringed by
    /// a rosette of translucent ice spikes (<c>Edge</c>) jutting straight up at varied heights, with a
    /// taller central spire, so the zone reads as ground frozen solid rather than the two stacked discs
    /// of <see cref="BuildGroundZone"/> (a ground disc plus a floating halo, which the user described as
    /// "just two discs placing on top of each other"). Authored-only, first through
    /// <c>ZoneBody.FrozenField</c> (1l6z) and since 1l70 through <c>StormBody.FrozenField</c>; the storm
    /// gate is shown by <c>SpellStorm</c> sharing this exact builder. Every collider is destroyed, so
    /// this is body-only by construction.
    /// <para><b>1l102 adds the needles' rise-and-sink</b> ("adjust so that the needles would go up then
    /// go down when spell end"). When <paramref name="lifetime"/> &gt; 0 the spikes and the spire grow
    /// up out of the ground over <c>RiseTime</c> and sink back down over <c>SinkTime</c> before the
    /// storm ends, driven by one <see cref="FrozenFieldRise"/> component on <paramref name="parent"/>
    /// (rule 19: per-frame work lives in the loop that runs while the body MOVES, on the caller's own
    /// root, not in this shape factory). Every spike is built at full authored height and the component
    /// only scales it — the build is identical to the static field, so the QA bench pass
    /// (<paramref name="lifetime"/> = 0, no component) mounts the exact geometry the storm will, at its
    /// finished size. Each spike scales in Y about its base (<c>position.y == 0.5 * scaledHeight</c>), so
    /// the bottom stays planted.</summary>
    public static void BuildFrozenField(Transform parent, float radius, Color core, Color edge,
        float lifetime = 0f)
    {
        Material coreMat = SkillFx.SharedSpriteMaterial(core);
        Material edgeMat = SkillFx.SharedSpriteMaterial(edge);

        var disc = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        disc.name = "SpellDisc";
        DestroyCollider(disc);
        disc.transform.SetParent(parent, false);
        disc.transform.localScale = new Vector3(radius * 2f, 0.02f, radius * 2f);
        ApplyShared(disc, coreMat);

        // 1l102: every raised piece, with the authored height that its Y is scaled from. 10 + 6 spikes
        // + the spire; a fixed array keeps this allocating-free and needs no collection import.
        var risers = new Transform[17];
        var heights = new float[17];
        int n = 0;

        // Two rings of spikes: an outer rosette near the rim and a shorter inner cluster, each spike
        // taller toward the centre. Each is an elongated cube, tilted a little so the field is not
        // rotationally symmetric. Sizes/heights are jittered once at build so the field reads as ice.
        for (int ring = 0; ring < 2; ring++)
        {
            int count = ring == 0 ? 10 : 6;
            float rr = ring == 0 ? radius * 0.82f : radius * 0.44f;
            float baseH = ring == 0 ? 0.7f : 1.1f;
            for (int i = 0; i < count; i++)
            {
                float ang = (i / (float)count) * Mathf.PI * 2f + ring * 0.45f;
                float h = baseH * Random.Range(0.75f, 1.3f);
                float w = Random.Range(0.10f, 0.16f);

                var spike = GameObject.CreatePrimitive(PrimitiveType.Cube);
                spike.name = "SpellSpike_" + ring + "_" + i;
                DestroyCollider(spike);
                spike.transform.SetParent(parent, false);
                spike.transform.localPosition = new Vector3(
                    Mathf.Cos(ang) * rr, h * 0.5f, Mathf.Sin(ang) * rr);
                spike.transform.localScale = new Vector3(w, h, w);
                spike.transform.localRotation = Quaternion.Euler(
                    Random.Range(-9f, 9f), ang * Mathf.Rad2Deg, Random.Range(-9f, 9f));
                ApplyShared(spike, edgeMat);
                if (lifetime > 0f)
                {
                    risers[n] = spike.transform;
                    heights[n] = h;
                    n++;
                }
            }
        }

        // A taller central spire so the field reads as growing toward the middle.
        var spire = GameObject.CreatePrimitive(PrimitiveType.Cube);
        spire.name = "SpellSpire";
        DestroyCollider(spire);
        spire.transform.SetParent(parent, false);
        spire.transform.localPosition = new Vector3(0f, 0.9f, 0f);
        spire.transform.localScale = new Vector3(0.22f, 1.8f, 0.22f);
        spire.transform.localRotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
        ApplyShared(spire, edgeMat);

        if (lifetime > 0f)
        {
            risers[n] = spire.transform;
            heights[n] = 1.8f;
            n++;
            parent.gameObject.AddComponent<FrozenFieldRise>()
                .Init(risers, heights, lifetime);
        }
    }

    /// <summary>The 1kc Conflagration body: a flat ground circle with a rising curtain of spinning
    /// ember rings. Authored on Conflagration (<c>ZoneBody.VortexCircle</c>) and, since 1l100, on
    /// Blizzard through the STORM axis (<c>StormBody.VortexCircle</c>). (1kf: Firestorm draws
    /// <see cref="BuildFunnel"/> instead.) Two colours, both through the colour-keyed shared cache:
    /// the circle takes the spell's Core, the rising rings a <paramref name="hot"/> the CALLER picks
    /// from the spell's own palette — Conflagration passes <see cref="SpellLook.HotCore"/> (Fire's
    /// yellow embers, the same hot-end choice SpellBeam's funnel makes), while Blizzard passes
    /// <see cref="SpellLook.Edge"/> because HotCore rotates a hue toward yellow and would paint an ice
    /// storm fire-coloured.
    /// <para><b>1l3</b> ("only create one ember ray on the circle edge, i want more"): the embers were
    /// dealt into parallel strands by a per-ember angular offset, so the rim carried several distinct
    /// tongues of flame instead of one shared helix.</para>
    /// <para><b>1l6t</b> ("stop being sucked into the centre … double the particles … appear from
    /// below"): the tongues rose <b>straight up at the ring radius</b> (the old convergent helix
    /// pulled them to the centre), the count doubled, and each ember started at the ground behind a
    /// bottom/top scale envelope.</para>
    /// <para><b>1l6u</b> ("stop creating particle rays with waves … create particle circles spinning
    /// when they were created, and spawn continuously till spell end"): the pre-placed vertical strands
    /// are gone. A <see cref="ConflagrationEmitter"/> on the zone root now spawns a fresh horizontal
    /// <b>ring</b> of embers every <c>SpawnInterval</c>; each ring (<see cref="RisingRing"/>) rises as
    /// one unit and <b>spins</b> about the vertical axis, and is slightly irregular (jittered
    /// radius/angle) so the spin is visible rather than hidden by rotational symmetry. The emitter
    /// lives on the zone root, so rings keep spawning until the zone is destroyed — i.e. until the
    /// spell ends.</para>
    /// <para><b>1l6v</b> ("create ring way faster and make it rise faster, and ensure the particle size
    /// are equal"): <c>SpawnInterval</c> dropped 0.33 → 0.14 s and <c>RiseDuration</c> 2.2 → 1.3 s, and
    /// the per-ember size jitter is gone — every ember is the same size (the radius/angle/tilt jitter
    /// alone keeps the spin readable).</para></summary>
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

        // 1l6u: the ember curtain is an emitter, not a fixed set of cubes. Guarded so a second call on
        // the same root (the QA bench builds a fresh modelRoot per spell; a defensively re-built zone
        // should not stack two spawners) re-points the existing emitter instead of adding another.
        var emitter = parent.GetComponent<ConflagrationEmitter>();
        if (emitter == null) emitter = parent.gameObject.AddComponent<ConflagrationEmitter>();
        emitter.Init(radius, Mathf.Max(3.2f, radius * 2.2f), hotMat);
    }

    /// <summary>1l111: Downpour's body (<c>StormBody.Rain</c>) — a low, lobed rain shelf hung with a
    /// curtain of falling streaks. Body-only like every builder here: every collider is destroyed and
    /// the storm's own tick decides who is struck, so no piece is ever queried. Both parts are placed
    /// in the parent's local frame so the body rides the storm root for free; only the streaks move,
    /// through <see cref="RainFall"/> (rule 19: the per-frame work lives on the component that moves —
    /// a per-frame loop in this factory would also be dead code on the QA bench, where no storm ever
    /// ticks). <c>height</c> is the shelf's altitude: the storm passes it, so the live body and the
    /// pedestal body are one shape at two altitudes.</summary>
    public static void BuildRainCloud(Transform parent, float radius, float height,
        Color cloudColor, Color core)
    {
        Material cloudMat = SkillFx.SharedSpriteMaterial(cloudColor);
        Material coreMat = SkillFx.SharedSpriteMaterial(core);

        // Three tiers of jittered lobes, each thinner and narrower than the one above, so the shelf
        // reads as a ceiling of cloud rather than a floating box. Every size is radius-relative: a
        // wider storm hangs a wider shelf.
        for (int tier = 0; tier < 3; tier++)
        {
            float y = height + (1f - tier) * 0.42f;
            float span = radius * (1.75f - tier * 0.28f);
            int count = 8 - tier;
            for (int i = 0; i < count; i++)
            {
                float ang = (i / (float)count) * Mathf.PI * 2f + tier * 0.5f;
                float rr = span * Random.Range(0.55f, 0.95f);
                Cube(parent, "RainCloud_" + tier + "_" + i,
                    new Vector3(Mathf.Cos(ang) * rr, y + Random.Range(-0.12f, 0.12f), Mathf.Sin(ang) * rr),
                    new Vector3(Random.Range(0.55f, 0.95f), Random.Range(0.30f, 0.52f), Random.Range(0.55f, 0.95f)),
                    cloudMat);
            }
        }

        // The curtain: thin vertical streaks in the spell's Core, recycled forever by RainFall. The
        // top sits under the lowest lobe and the bottom just above the ground, so a streak is never
        // seen poking through the shelf or sinking into the dirt.
        float top = height - 0.55f;
        float bottom = 0.12f;
        for (int i = 0; i < 26; i++)
        {
            float ang = Random.Range(0f, Mathf.PI * 2f);
            float rr = radius * Mathf.Sqrt(Random.value) * 0.95f;
            var streak = Cube(parent, "RainStreak_" + i,
                new Vector3(Mathf.Cos(ang) * rr, Random.Range(bottom, top), Mathf.Sin(ang) * rr),
                new Vector3(0.09f, 0.55f, 0.09f), coreMat);
            streak.gameObject.AddComponent<RainFall>().Init(top, bottom, Random.Range(3.6f, 5.4f));
        }
    }

    /// <summary>1l111: Thunderhead's body (<c>StormBody.Thunderhead</c>) — a stacked cumulonimbus that
    /// replaces the generic cloud puff for this one spell. Three lobed bands (wide flat base,
    /// billowing middle, narrow cap) in the storm cloud's tint, with a ring of lit blocks in the
    /// spell's Core under the base: the bolts read as falling from that underside, and
    /// <c>SpellStorm._cloudY</c> puts the strike origin at exactly this altitude. Like
    /// <see cref="BuildRainCloud"/> this is body-only and motion-free; the spawner on the storm root
    /// keeps the root free to be the thing the strikes hang from.</summary>
    public static void BuildThunderhead(Transform parent, float radius, float height,
        Color cloudColor, Color core)
    {
        Material cloudMat = SkillFx.SharedSpriteMaterial(cloudColor);
        Material coreMat = SkillFx.SharedSpriteMaterial(core);

        for (int band = 0; band < 3; band++)
        {
            float y = height + band * 0.78f;
            float span = radius * (1.55f - band * 0.38f);
            int count = 9 - band * 2;
            for (int i = 0; i < count; i++)
            {
                float ang = (i / (float)count) * Mathf.PI * 2f + band * 0.7f;
                float rr = span * Random.Range(0.45f, 0.95f);
                Cube(parent, "Thunderhead_" + band + "_" + i,
                    new Vector3(Mathf.Cos(ang) * rr, y + Random.Range(-0.15f, 0.15f), Mathf.Sin(ang) * rr),
                    new Vector3(Random.Range(0.65f, 1.05f), Random.Range(0.55f, 0.95f), Random.Range(0.65f, 1.05f)),
                    cloudMat);
            }
        }

        // The lit underside the bolts fall from — a short ring of Core-coloured blocks tucked under
        // the base band, plus one brighter block at the very centre so the strike origin has a seat.
        for (int i = 0; i < 5; i++)
        {
            float ang = (i / 5f) * Mathf.PI * 2f;
            float rr = radius * 0.55f;
            Cube(parent, "ThunderheadGlow_" + i,
                new Vector3(Mathf.Cos(ang) * rr, height - 0.42f, Mathf.Sin(ang) * rr),
                new Vector3(0.34f, 0.16f, 0.34f), coreMat);
        }
        Cube(parent, "ThunderheadSeat", new Vector3(0f, height - 0.48f, 0f),
            new Vector3(0.52f, 0.14f, 0.52f), coreMat);
    }

    /// <summary>1l112/1l116: Static Nova's body (<see cref="ZoneBody.NovaRing"/>) — the ground wave
    /// FLOOR (the Fire Wave family, <c>MagicProjectileModelBuilder.Wave</c>) bent into a RING, so the
    /// spell reads as a rolling wave travelling outward in a circle rather than a flat spreading disc
    /// (1l116: the user asked for exactly that — "wave like firewave but in a circle"). A band of small
    /// voxel cubes sits on the ground from <c>radius − band</c> out to <c>radius</c>, lifting into a
    /// crest at the leading (outer) edge, and a <see cref="WaveRipple"/> in RADIAL mode rolls a
    /// brightness+height crest outward through it (driven by <c>SpellWave</c>'s update loop — rule 19;
    /// the QA pedestal draws it motionless).
    /// <para><b>1l116 the caller SCALES this body.</b> <c>SpellCaster.ResolveNova</c> authors the ring at
    /// the sweep's final radius (<c>SpellWave.MaxRadius</c>) and <c>SpellWave</c> scales the root from
    /// ~0 to 1 as the front grows, so the drawn leading edge rides the damage front. The builder's own
    /// local layout is static — build it at the authoring extent, let the moving component scale it
    /// (rule 19). The band width and the ring/station counts are LOOK constants (not the spell's damage
    /// <c>Radius</c>), held here so the body has one spelling.</para>
    /// <para><b>Per-cell materials, deliberately.</b> The crest only reads if each cell is tinted up
    /// toward the spell's Edge on the crest (a pure height offset is invisible on flat-topped cubes whose
    /// normals never change — the same reason <c>Wave</c> tints). That needs a per-cell material, which
    /// is the one place this builder departs from the shared-material convention the rest of the file
    /// follows (1ie); the tint is why. Cell count is held to <c>rings × stations = 288</c>, the same
    /// budget Fire Wave's base floor uses.</para></summary>
    public static void BuildNovaRing(Transform parent, float radius, Color core, Color edge)
    {
        // LOOK constants — the drawn ring, not the spell's damage Radius.
        const int rings = 4;
        const int stations = 72;
        const float thickness = 0.14f;
        const float crestLift = 0.34f;
        const float amplitude = 0.16f;
        const float rippleSpeed = 7f;

        float outerR = Mathf.Max(0.3f, radius);
        float band = Mathf.Clamp(outerR * 0.30f, 1.0f, 4.2f);
        float innerR = Mathf.Max(0.15f, outerR - band);
        float dr = (outerR - innerR) / rings;

        // `radius` may arrive at the degenerate minimum (0.3): the clamps above keep `innerR <= outerR`
        // and the tangential pitch positive for every input, so no ring/station produces a negative or
        // NaN size (rule: a helper must report 0 for degenerate input, never a mirrored cube).
        Material proto = SkillFx.SharedSpriteMaterial(core);
        Shader shader = proto != null ? proto.shader : null;
        Color trough = Color.Lerp(core, Color.black, 0.55f);
        Color crestColor = edge;

        int count = rings * stations;
        var cells = new Transform[count];
        var restY = new float[count];
        var mats = new Material[count];
        int n = 0;
        for (int ir = 0; ir < rings; ir++)
        {
            float r = innerR + (ir + 0.5f) * dr;
            // Crest profile across the band: 0 at the trailing (inner) edge, crestLift at the leading
            // (outer) edge — so the wave lip is the leading edge of the ring.
            float rf = band > 0.0001f ? (r - innerR) / band : 1f;
            float rest = crestLift * rf;
            float tangential = 2f * Mathf.PI * r / stations;
            float size = Mathf.Max(0.05f, Mathf.Min(dr, tangential) * 1.06f);
            for (int s = 0; s < stations; s++)
            {
                float ang = (s / (float)stations) * Mathf.PI * 2f;
                var cube = Cube(parent, "NovaCell_" + n,
                    new Vector3(Mathf.Cos(ang) * r, rest, Mathf.Sin(ang) * r),
                    new Vector3(size, thickness, size), proto);

                // Per-cell material so the ripple can tint each cell (see the class remarks). A null
                // shader means the project's FX shader is missing entirely; fall back to shared material
                // and skip the tint rather than construct a Material(null).
                Material m = null;
                var renderer = cube.GetComponent<MeshRenderer>();
                if (shader != null && renderer != null)
                {
                    m = new Material(shader) { color = trough };
                    renderer.material = m;
                }
                mats[n] = m;
                cells[n] = cube;
                restY[n] = rest;
                n++;
            }
        }

        // One ripple, radial: the crest rolls OUTWARD from the centre as the ring expands (1l116).
        parent.gameObject.AddComponent<WaveRipple>().Configure(
            cells, restY, mats, trough, crestColor, amplitude, rippleSpeed, Mathf.Max(1f, band),
            radial: true);
    }

    /// <summary>1l112: Thunder Vortex's body (<see cref="ZoneBody.LightningCyclone"/>) — a bright core
    /// column wrapped by rings of jagged vertical bolts, widest at the base and narrowing upward, so
    /// the stack reads as a lightning funnel. It is a Vortex body: <c>SpellZone</c> spins the zone root
    /// at 240 deg/s, and because this body is STATIC the bolts themselves churn (rule 19 — the spin
    /// belongs to the component the spell already runs, not to this factory).</summary>
    public static void BuildLightningCyclone(Transform parent, float radius, Color core)
    {
        Material coreMat = SkillFx.SharedSpriteMaterial(core);
        Material brightMat = SkillFx.SharedSpriteMaterial(Color.Lerp(core, Color.white, 0.7f));

        // The column down the eye of the vortex.
        Cube(parent, "CycloneCore", new Vector3(0f, 1.1f, 0f), new Vector3(0.22f, 2.2f, 0.22f), brightMat);

        int rings = 5;
        for (int ring = 0; ring < rings; ring++)
        {
            float t = ring / (float)(rings - 1);
            float rr = Mathf.Lerp(radius * 0.9f, radius * 0.28f, t);
            float y = Mathf.Lerp(0.15f, 2.4f, t);
            int count = Mathf.RoundToInt(Mathf.Lerp(10f, 5f, t));
            for (int i = 0; i < count; i++)
            {
                float ang = (i / (float)count) * Mathf.PI * 2f + ring * 0.6f;
                float h = Random.Range(0.55f, 1.25f);
                var bolt = Cube(parent, "CycloneBolt_" + ring + "_" + i,
                    new Vector3(Mathf.Cos(ang) * rr, y, Mathf.Sin(ang) * rr),
                    new Vector3(Random.Range(0.08f, 0.14f), h, Random.Range(0.08f, 0.14f)),
                    i % 3 == 0 ? brightMat : coreMat);
                bolt.localRotation = Quaternion.Euler(
                    Random.Range(-14f, 14f), ang * Mathf.Rad2Deg, Random.Range(-14f, 14f));
            }
        }
    }

    /// <summary>1l112: Devastation's body (<see cref="StormBody.Maelstrom"/>) — a larger, angrier
    /// cumulonimbus than <see cref="BuildThunderhead"/>: FOUR cloud bands (vs three) with a wider span
    /// and a jagged down-toothed underside in the spell's Core. Mounted at the caller's altitude
    /// (<c>SpellStorm.MaelstromHeight</c>), which is also where its strike bolts fall from. Body-only
    /// and motion-free; like the Thunderhead it does not spin.</summary>
    public static void BuildMaelstrom(Transform parent, float radius, float height,
        Color cloudColor, Color core)
    {
        Material cloudMat = SkillFx.SharedSpriteMaterial(cloudColor);
        Material coreMat = SkillFx.SharedSpriteMaterial(core);

        for (int band = 0; band < 4; band++)
        {
            float y = height + band * 0.7f;
            float span = radius * (1.75f - band * 0.34f);
            int count = 11 - band * 2;
            for (int i = 0; i < count; i++)
            {
                float ang = (i / (float)count) * Mathf.PI * 2f + band * 0.55f;
                float rr = span * Random.Range(0.4f, 0.98f);
                Cube(parent, "Maelstrom_" + band + "_" + i,
                    new Vector3(Mathf.Cos(ang) * rr, y + Random.Range(-0.18f, 0.18f), Mathf.Sin(ang) * rr),
                    new Vector3(Random.Range(0.7f, 1.15f), Random.Range(0.5f, 0.95f), Random.Range(0.7f, 1.15f)),
                    cloudMat);
            }
        }

        // A jagged down-toothed belly: short Core spikes under the base band, so the maelstrom's
        // underside crackles and the strike origin has a seat to hang from.
        int teeth = 8;
        for (int i = 0; i < teeth; i++)
        {
            float ang = (i / (float)teeth) * Mathf.PI * 2f;
            float rr = radius * Random.Range(0.35f, 0.75f);
            float depth = Random.Range(0.5f, 1.1f);
            Cube(parent, "MaelstromTooth_" + i,
                new Vector3(Mathf.Cos(ang) * rr, height - 0.5f - depth * 0.5f, Mathf.Sin(ang) * rr),
                new Vector3(0.22f, depth, 0.22f), coreMat);
        }
        Cube(parent, "MaelstromSeat", new Vector3(0f, height - 0.55f, 0f),
            new Vector3(0.6f, 0.18f, 0.6f), coreMat);
    }

    /// <summary>1l111: one shared cube primitive, parented, collider-destroyed and material-applied —
    /// the four lines every builder in this file repeats, factored so the two new bodies do not carry
    /// forty copies of them. Returns the transform so a caller can attach a motion component.</summary>
    private static Transform Cube(Transform parent, string name, Vector3 localPos, Vector3 size,
        Material sharedMat)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        DestroyCollider(go);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        go.transform.localScale = size;
        ApplyShared(go, sharedMat);
        return go.transform;
    }

    /// <summary>1l6u: spawns the conflagration's ember rings continuously. Rule 19: the shape factory
    /// builds the static disc, and the component that runs while the pieces MOVE owns the motion — but
    /// this motion is a <i>stream</i>, so its home is a spawner on the zone root rather than a
    /// per-piece component. It spawns one ring every <c>SpawnInterval</c> seconds; each ring rises +
    /// spins and then destroys itself (<see cref="RisingRing"/>), so a bounded handful are alive at
    /// steady state. The spawner dies with the zone root, which is exactly "spawn continuously till
    /// spell end".</summary>
    private sealed class ConflagrationEmitter : MonoBehaviour
    {
        private const int PerRing = 20;          // embers around one ring
        private const float SpawnInterval = 0.14f; // 1l6v: rings spawn "way faster" (was 0.33)
        private const float RiseDuration = 1.3f;   // 1l6v: rings rise "faster" (was 2.2) — ~4.4 m/s
        private const float EmberSize = 0.16f;

        private float _radius;
        private float _height;
        private Material _hotMat;
        private float _spawnTimer;
        private int _ringIndex;

        public void Init(float radius, float height, Material hotMat)
        {
            _radius = radius;
            _height = height;
            _hotMat = hotMat;
        }

        private void Update()
        {
            _spawnTimer -= Time.deltaTime;
            if (_spawnTimer > 0f) return;
            _spawnTimer = SpawnInterval;
            SpawnRing();
        }

        private void SpawnRing()
        {
            var ring = new GameObject("SpellRing_" + _ringIndex++);
            ring.transform.SetParent(transform, false);
            for (int i = 0; i < PerRing; i++)
            {
                // Slight irregularity (radius / angle / tilt) so the ring is NOT rotationally
                // symmetric — a perfectly even ring of identical cubes looks static however fast it
                // spins. The jitter is baked at spawn and rides with the ring, so the spin is legible.
                // 1l6v: size is NO LONGER jittered — every ember is the same size; the radius/angle/
                // tilt jitter is enough to keep the spin readable.
                float ang = (i / (float)PerRing) * Mathf.PI * 2f + Random.Range(-0.08f, 0.08f);
                float r = _radius * Random.Range(0.9f, 1.1f);
                var ember = GameObject.CreatePrimitive(PrimitiveType.Cube);
                ember.name = "SpellEmber_" + i;
                DestroyCollider(ember);
                ember.transform.SetParent(ring.transform, false);
                ember.transform.localPosition = new Vector3(
                    Mathf.Cos(ang) * r,
                    Random.Range(-0.05f, 0.05f),
                    Mathf.Sin(ang) * r);
                ember.transform.localRotation = Quaternion.Euler(
                    Random.Range(-12f, 12f), ang * Mathf.Rad2Deg, Random.Range(-12f, 12f));
                ember.transform.localScale = Vector3.one * EmberSize;   // 1l6v: uniform size
                ApplyShared(ember, _hotMat);
            }
            ring.AddComponent<RisingRing>().Init(_height, RiseDuration);
        }
    }

    /// <summary>1l6u: one spawned ember ring. Climbs as a unit to the apex while spinning about the
    /// vertical axis (on top of the zone root's own 240 deg/s pull-spin), fading in low / out high so
    /// the column reads continuous rather than as discrete pops. Destroys itself at the end of its
    /// climb — the emitter keeps replacing it. It fades by scaling each child EMBER, never the ring
    /// transform, so the circle's radius stays constant while it fades (scaling the ring would shrink
    /// the circle toward its centre).</summary>
    private sealed class RisingRing : MonoBehaviour
    {
        private const float FadeFraction = 0.22f;
        private const float SpinSpeed = 1.4f;   // rad/s, added to the zone root's pull-spin

        private float _height;
        private float _duration;
        private float _age;
        private Transform[] _embers;
        private Vector3[] _baseScales;

        public void Init(float height, float duration)
        {
            _height = height;
            _duration = duration;
            int n = transform.childCount;
            _embers = new Transform[n];
            _baseScales = new Vector3[n];
            for (int i = 0; i < n; i++)
            {
                _embers[i] = transform.GetChild(i);
                _baseScales[i] = _embers[i].localScale;
            }
        }

        private void Update()
        {
            _age += Time.deltaTime;
            float cyc = _age / _duration;
            if (cyc >= 1f)
            {
                Destroy(gameObject);
                return;
            }

            transform.localPosition = new Vector3(0f, cyc * _height, 0f);
            transform.localRotation = Quaternion.Euler(0f, _age * SpinSpeed * Mathf.Rad2Deg, 0f);

            float env = Mathf.Clamp01(cyc / FadeFraction) * Mathf.Clamp01((1f - cyc) / FadeFraction);
            for (int i = 0; i < _embers.Length; i++)
                _embers[i].localScale = _baseScales[i] * env;
        }
    }

    /// <summary>1l102: the frozen field's needle rise/sink. Grows every spike (and the spire) up out
    /// of the ground over <c>RiseTime</c>, then sinks it back down over <c>SinkTime</c> as
    /// <c>lifetime</c> approaches — the user's "needles would go up then go down when spell end".
    /// Scaling only Y and keeping <c>position.y == 0.5 * scaledHeight</c> keeps each needle's bottom
    /// planted on the ground as it grows (translating would lift it off). It lives on the storm's own
    /// root, so it is destroyed with the body — <c>SpellStorm</c> ends at the same <c>lifetime</c>.
    /// These pieces never had a collider, so this is drawn-only by construction.</summary>
    private sealed class FrozenFieldRise : MonoBehaviour
    {
        private const float RiseTime = 0.35f;
        private const float SinkTime = 0.5f;

        private Transform[] _risers;
        private float[] _baseHeights;
        private float _life;
        private float _age;

        public void Init(Transform[] risers, float[] baseHeights, float life)
        {
            _risers = risers;
            _baseHeights = baseHeights;
            _life = life;
        }

        private void Update()
        {
            _age += Time.deltaTime;
            // One envelope: up from 0 over RiseTime, held, then back to 0 over the last SinkTime.
            // Min() of the two ramps means a lifetime shorter than the sum simply never reaches full.
            float up = RiseTime > 0f ? _age / RiseTime : 1f;
            float down = SinkTime > 0f ? (_life - _age) / SinkTime : 1f;
            float env = Mathf.Clamp01(Mathf.Min(up, down));
            for (int i = 0; i < _risers.Length; i++)
            {
                Transform t = _risers[i];
                if (t == null) continue;
                float h = _baseHeights[i] * env;
                Vector3 s = t.localScale;
                t.localScale = new Vector3(s.x, h, s.z);
                Vector3 p = t.localPosition;
                t.localPosition = new Vector3(p.x, h * 0.5f, p.z);
            }
        }
    }

    /// <summary>1l111: scrolls one rain streak downward and wraps it back to the shelf, forever. Each
    /// streak owns one of these (attached by <see cref="BuildRainCloud"/>); it writes only
    /// localPosition, so the curtain keeps working if the storm root moves and dies with it. The fall
    /// is distance per second, not distance per frame, so the spacing between streaks stays even at
    /// any framerate.</summary>
    private sealed class RainFall : MonoBehaviour
    {
        private float _top = 4f;
        private float _bottom = 0.1f;
        private float _speed = 4f;

        public void Init(float top, float bottom, float speed)
        {
            _top = top;
            _bottom = bottom;
            _speed = speed;
        }

        private void Update()
        {
            Vector3 p = transform.localPosition;
            p.y -= _speed * Time.deltaTime;
            if (p.y < _bottom) p.y = _top;
            transform.localPosition = p;
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
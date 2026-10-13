using UnityEngine;

/// <summary>
/// The area-effect spell's drawn body — the pull funnel and the persistent ground zone.
/// <para>Extracted verbatim from <c>Magic/Cast/SpellZone.cs</c> in <b>1jd</b>: "zone model" matched
/// nothing under <c>Models/</c> while the model shipped. Shapes, names, counts and the shared
/// <see cref="SkillFx.SharedSpriteMaterial"/> all unchanged.
/// <para><b>1kc adds <see cref="BuildConflagration"/></b> — the Firestorm/Conflagration look (ground
///   circle + ember rings rising in a vortex), authored on Conflagration alone through
///   <c>ZoneBody.VortexCircle</c> (1l6u: the rings are spawned continuously by
///   <c>ConflagrationEmitter</c>).</para>
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

    /// <summary>1l6z: the Deep Freeze body — a frozen field. A flat frost disc (<c>Core</c>) ringed by
    /// a rosette of translucent ice spikes (<c>Edge</c>) jutting straight up at varied heights, with a
    /// taller central spire, so the zone reads as ground frozen solid rather than the two stacked discs
    /// of <see cref="BuildGroundZone"/> (a ground disc plus a floating halo, which the user described as
    /// "just two discs placing on top of each other"). Authored-only, first through
    /// <c>ZoneBody.FrozenField</c> (1l6z) and since 1l70 through <c>StormBody.FrozenField</c>; the storm
    /// gate is shown by <c>SpellStorm</c> sharing this exact builder. Every collider is destroyed, so
    /// this is body-only by construction.</summary>
    public static void BuildFrozenField(Transform parent, float radius, Color core, Color edge)
    {
        Material coreMat = SkillFx.SharedSpriteMaterial(core);
        Material edgeMat = SkillFx.SharedSpriteMaterial(edge);

        var disc = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        disc.name = "SpellDisc";
        DestroyCollider(disc);
        disc.transform.SetParent(parent, false);
        disc.transform.localScale = new Vector3(radius * 2f, 0.02f, radius * 2f);
        ApplyShared(disc, coreMat);

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
    }

    /// <summary>The 1kc Conflagration body: a flat ground circle with a rising curtain of spinning
    /// ember rings. Authored on Conflagration alone (<c>ZoneBody.VortexCircle</c>). (1kf: Firestorm
    /// draws <see cref="BuildFunnel"/> instead.) Two colours, both through the colour-keyed shared
    /// cache: the circle takes the spell's Core, the rising rings the hot end
    /// (<see cref="SpellLook.HotCore"/>) — the same hot-end choice SpellBeam's funnel makes.
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
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Partial: projectile spawning, body/particle visuals, and the flight-time OrbFx behaviour (§3.8).
/// Split from SpellCaster.cs - see the main partial for fields and focus/cooldown state.
/// </summary>
public partial class SpellCaster
{
    private DamageResult FireProjectile(float power, SpellData spell, Vector3 pos, Vector3 fwd, float charge, float sizeScale)
    {
        // Earth projectiles (TerrainShape.Crater) carve their crater where the shard STRIKES —
        // the impact point, not the caster's own footing. The carve lives in
        // SpellEffect.ResolveProjectileImpact (down-probed onto the ground there), so casting
        // never dents the ground under the player.

        // Spawn on the aim line only (no vertical lift) so the trajectory passes through the
        // casting circle's center (the circle is anchored on the same origin as this cast).
        // The small forward muzzle offset mirrors the ranged Muzzle and keeps the bolt clear of
        // the caster's own collider; the SpellEffect's caster-root skip and ground probe handle
        // self/terrain contacts from there.
        pos += fwd * 0.5f;
        GameObject go;
        if (spell.CastEffectPrefab != null)
        {
            go = Instantiate(spell.CastEffectPrefab, pos, Quaternion.LookRotation(fwd));
            if (go.GetComponent<SpellEffect>() == null)
            {
                var fx = go.AddComponent<SpellEffect>();
                fx.Initialize(spell, power, fwd, this, sizeScale);
            }
        }
        else
        {
            go = new GameObject("SpellProjectile");
            go.transform.position = pos;
            go.transform.rotation = Quaternion.LookRotation(fwd);
            AttachDefaultProjectileVisual(go, spell.Type, spell.Shape, spell.SummonFallingRock);
            go.AddComponent<SpellEffect>().Initialize(spell, power, fwd, this, sizeScale);
        }

        // Charge scales the whole projectile body (authored prefab or generated visual).
        if (charge > 0f)
            go.transform.localScale *= sizeScale;

        if (go.TryGetComponent<SpellEffect>(out var proj))
            proj.Launch(spell.ProjectileSpeed);

        return new DamageResult();
    }

    /// <summary>Public access to the default projectile visual (used by summoned turrets).</summary>
    public void DecorateProjectile(GameObject go, DamageType type, ProjectileShape shape = ProjectileShape.Auto)
    {
        AttachDefaultProjectileVisual(go, type, shape);
    }

    /// <summary>
    /// Stand-alone render-only spell visual for the test ground's magic model bench (1dk): builds
    /// the exact projectile body + comet-exhaust particles a live cast carries, with no
    /// <see cref="SpellEffect"/>, no collider, and no launch — it simply sits at its root so each
    /// spell's model can be looked at and edited. `shape` resolves like a real cast (Auto → element
    /// default); <paramref name="rockBody"/> dresses it as the rough burning rock sky-rock spells
    /// (summonFallingRock: Meteor / Asteroid / Comet) summon.
    /// </summary>
    public static GameObject CreateProjectileDisplay(DamageType type, ProjectileShape shape = ProjectileShape.Auto,
        bool rockBody = false)
    {
        var go = new GameObject("MagicModelDisplay");
        AttachDefaultProjectileVisual(go, type, shape, rockBody);
        return go;
    }

    /// <summary>
    /// Build a shape-aware visible projectile body + comet-exhaust particles for spells with no
    /// authored CastEffectPrefab, so magic skills read on screen. `shape` is the ProjectileShape
    /// from SpellData (§3.8): Auto resolves to the element default so every projectile still has a
    /// sane look; explicit shapes follow the spell's NAME ("Frost Bolt" = a Bolt, "Ice Lance" = a
    /// Lance, "Stone Shard" = a Debris clump...). Renderer-only: the root keeps no collider so
    /// SpellEffect's flight raycast never self-hits. Static — the visual has no instance state.
    /// </summary>
    private static void AttachDefaultProjectileVisual(GameObject go, DamageType type, ProjectileShape shape, bool rockBody = false)
    {
        Color color = DamageNumber.ColorFor(type);
        Shader shader = Shader.Find("Sprites/Default") ?? Shader.Find("Unlit/Color");
        if (shader == null)
            return;

        var body = BuildProjectileBody(ResolveShape(type, shape), shader, color, rockBody);
        body.SetParent(go.transform, false);

        AttachProjectileParticles(body, type, color);
    }

    /// <summary>Element default shape used when a spell leaves Shape = Auto.</summary>
    private static ProjectileShape AutoShapeFor(DamageType type)
    {
        switch (type)
        {
            case DamageType.Fire: return ProjectileShape.Sphere;    // fireball
            case DamageType.Ice: return ProjectileShape.Shard;      // generic frost chip
            case DamageType.Lightning: return ProjectileShape.Bolt; // crackling bolt
            case DamageType.Wind: return ProjectileShape.Blade;     // wind blade
            case DamageType.Water: return ProjectileShape.Splash;   // droplet
            case DamageType.Earth: return ProjectileShape.Debris;   // tumbling rock chunks
            case DamageType.Physical: return ProjectileShape.Dart;  // arrow / bolt line
            default: return ProjectileShape.Sphere;
        }
    }

    private static ProjectileShape ResolveShape(DamageType type, ProjectileShape shape)
        => shape == ProjectileShape.Auto ? AutoShapeFor(type) : shape;

    /// <summary>Color-matched visual body for a projectile by resolved shape. <paramref name="rockBody"/>
    /// dresses the shape as a rough burning rock (sky-rock spells like Comet that summon a boulder).</summary>
    private static Transform BuildProjectileBody(ProjectileShape shape, Shader shader, Color color, bool rockBody = false)
    {
        switch (shape)
        {
            case ProjectileShape.Bolt: return Bolt("JaggedBolt", shader, color);
            case ProjectileShape.Shard: return Shard("Shard", shader, color);
            case ProjectileShape.Lance: return Lance("IceLance", shader, color);
            case ProjectileShape.Spear: return Spear("Spear", shader, color);
            case ProjectileShape.Blade: return Blade("WindBlade", shader, color);
            case ProjectileShape.Splash: return Splash("WaterSplash", shader, color);
            case ProjectileShape.Comet: return Comet("Comet", shader, color, rockBody);
            case ProjectileShape.Missile: return Missile("ArcaneMissiles", shader, color);
            case ProjectileShape.Dart: return Dart("Dart", shader, color);
            case ProjectileShape.Debris: return Debris("RockDebris", shader);
            default: return Orb("Orb", PrimitiveType.Sphere, Vector3.one * 0.22f, shader, color,
                OrbFx.Mode.Ember); // fireball: fast warm flicker, not the plain gentle breathe
        }
    }

    /// <summary>Primitive with its collider stripped, parented to `parent` at local zero.</summary>
    private static Transform Primitive(PrimitiveType kind, string name, Transform parent)
    {
        var go = GameObject.CreatePrimitive(kind);
        go.name = name;
        Collider col = go.GetComponent<Collider>();
        if (col != null)
            Destroy(col);
        go.transform.SetParent(parent, false);
        return go.transform;
    }

    private static void Materialize(Transform t, Shader shader, Color color)
        => t.GetComponent<MeshRenderer>().material = new Material(shader) { color = color };

    private static Transform Orb(string name, PrimitiveType shape, Vector3 scale, Shader shader,
        Color color, OrbFx.Mode mode)
    {
        var orb = GameObject.CreatePrimitive(shape);
        orb.name = name;
        Collider col = orb.GetComponent<Collider>();
        if (col != null)
            Destroy(col);
        orb.transform.localScale = scale;
        orb.GetComponent<MeshRenderer>().material = new Material(shader) { color = color };
        orb.AddComponent<OrbFx>().Pulse = mode;
        return orb.transform;
    }

    /// <summary>Diamond-shaped shard that drills forward.</summary>
    private static Transform Shard(string name, Shader shader, Color color)
    {
        var shard = GameObject.CreatePrimitive(PrimitiveType.Cube);
        shard.name = name;
        Collider col = shard.GetComponent<Collider>();
        if (col != null)
            Destroy(col);
        shard.transform.localScale = new Vector3(0.12f, 0.38f, 0.12f);
        shard.transform.localRotation = Quaternion.Euler(0f, 45f, 0f);
        // Translucent glassy frost chip: "Sprites/Default" blends via the material color alpha.
        Color glass = new Color(color.r, color.g, color.b, 0.5f);
        shard.GetComponent<MeshRenderer>().material = new Material(shader) { color = glass };
        shard.AddComponent<OrbFx>().Pulse = OrbFx.Mode.Shard;
        return shard.transform;
    }

    /// <summary>
    /// Tumbling cluster of rock chunks — the Earth school's projectile (Stone Shard / stone shards).
    /// Mirrors the world's breakable-rock debris look (WorldBuilder.SpawnRockDebris): random grey
    /// <c>Color.Lerp(Color.gray, Color.black, rand)</c> cubes of mixed sizes, each tumbling around its
    /// own random axis (OrbFx.Tumble), clustered with the leader ahead and the tail trailing so the
    /// clump reads as one forward-striking debris blob. Two chunks are dusted with the Earth accent
    /// color so it still reads as magic, not just a terrain chunk.
    /// </summary>
    private static Transform Debris(string name, Shader shader)
    {
        var root = new GameObject(name).transform;
        Color earth = DamageNumber.ColorFor(DamageType.Earth);
        int count = 5 + UnityEngine.Random.Range(0, 3); // 5-7 chunks
        for (int i = 0; i < count; i++)
        {
            var chunk = Primitive(PrimitiveType.Cube, "Chunk" + i, root);
            float s = Mathf.Lerp(0.05f, 0.11f, UnityEngine.Random.value);
            if (i == 0) s = 0.14f; // leader chunk
            if (s < 0.075f) s = UnityEngine.Random.Range(0.05f, 0.09f);
            chunk.localScale = Vector3.one * s;
            chunk.localRotation = UnityEngine.Random.rotation;
            chunk.localPosition = new Vector3(
                UnityEngine.Random.Range(-0.10f, 0.10f),
                UnityEngine.Random.Range(-0.10f, 0.10f),
                i == 0 ? 0.18f : UnityEngine.Random.Range(-0.42f, -0.08f));

            Color rock = Color.Lerp(Color.gray, Color.black, UnityEngine.Random.value * 0.5f);
            if (i == 1 || i == count - 1)
                rock = Color.Lerp(rock, earth, 0.55f); // earthy accent on two chunks
            Materialize(chunk, shader, rock);
            chunk.gameObject.AddComponent<OrbFx>().Pulse = OrbFx.Mode.Tumble;
        }
        root.gameObject.AddComponent<OrbFx>().Pulse = OrbFx.Mode.Plain;
        return root;
    }

    /// <summary>
    /// Jagged segmented bolt laid down the flight line — the same segment technique as the
    /// thunder-storm event's lightning bolt (RandomEventManager.SpawnJaggedBolt), so a "Bolt"
    /// reads as lightning (element-colored) rather than a plain sphere. Parent rotates to the
    /// aim, so the jitter lives in the local XY plane and the bolt streaks +Z.
    /// </summary>
    private static Transform Bolt(string name, Shader shader, Color color)
    {
        var root = new GameObject(name).transform;
        int segments = 8;
        float length = 1.25f;
        float jitter = 0.09f;
        Vector3 prev = new Vector3(0f, 0f, -length * 0.5f);
        for (int i = 0; i < segments; i++)
        {
            float t = i / (float)(segments - 1);
            Vector3 next = i == segments - 1
                ? new Vector3(0f, 0f, length * 0.5f)
                : new Vector3(
                    UnityEngine.Random.Range(-jitter, jitter),
                    UnityEngine.Random.Range(-jitter, jitter),
                    Mathf.Lerp(-length * 0.5f, length * 0.5f, t));
            var seg = Primitive(PrimitiveType.Cube, "Seg" + i, root);
            seg.localPosition = (prev + next) * 0.5f;
            Vector3 segDir = (next - prev).normalized;
            seg.localScale = new Vector3(
                Mathf.Lerp(0.17f, 0.05f, t),
                Mathf.Lerp(0.17f, 0.05f, t),
                Mathf.Max(0.1f, Vector3.Distance(prev, next)));
            if (segDir.sqrMagnitude > 0.001f && Mathf.Abs(segDir.z) < 0.999f)
                seg.rotation = Quaternion.LookRotation(segDir, Vector3.up);
            Materialize(seg, shader, color);
            prev = next;
        }
        root.gameObject.AddComponent<OrbFx>().Pulse = OrbFx.Mode.Bolt;
        return root;
    }

    /// <summary>Long, straight pointed spike (ice lance line) oriented along the flight line.</summary>
    private static Transform Lance(string name, Shader shader, Color color)
    {
        var root = new GameObject(name).transform;
        var shaft = Primitive(PrimitiveType.Cube, "Shaft", root);
        shaft.localScale = new Vector3(0.1f, 0.1f, 1.1f);
        Materialize(shaft, shader, color);
        var tip = Primitive(PrimitiveType.Cube, "Tip", root);
        tip.localPosition = new Vector3(0f, 0f, 0.62f);
        tip.localScale = new Vector3(0.12f, 0.12f, 0.22f);
        Materialize(tip, shader, color);
        root.gameObject.AddComponent<OrbFx>().Pulse = OrbFx.Mode.Shard;
        return root;
    }

    /// <summary>Tapered spear: broad diamond head + trailing shaft (shadow/void spears).</summary>
    private static Transform Spear(string name, Shader shader, Color color)
    {
        var root = new GameObject(name).transform;
        var shaft = Primitive(PrimitiveType.Cube, "Shaft", root);
        shaft.localScale = new Vector3(0.045f, 0.045f, 0.95f);
        var shaftColor = new Color(color.r * 0.5f, color.g * 0.5f, color.b * 0.5f, 1f);
        Materialize(shaft, shader, shaftColor);
        var head = Primitive(PrimitiveType.Cube, "Head", root);
        head.localPosition = new Vector3(0f, 0f, 0.48f);
        head.localScale = new Vector3(0.24f, 0.07f, 0.44f);
        head.localRotation = Quaternion.Euler(0f, 45f, 0f);
        Materialize(head, shader, color);
        root.gameObject.AddComponent<OrbFx>().Pulse = OrbFx.Mode.Plain;
        return root;
    }

    /// <summary>Flat slashing cross-blade that spins in its own plane (wind blades / scissor).</summary>
    private static Transform Blade(string name, Shader shader, Color color)
    {
        var root = new GameObject(name).transform;
        // Translucent ethereal wind: "Sprites/Default" blends via the material color alpha.
        Color air = new Color(color.r, color.g, color.b, 0.4f);
        var a = Primitive(PrimitiveType.Cube, "BladeA", root);
        a.localScale = new Vector3(0.42f, 0.05f, 0.03f);
        Materialize(a, shader, air);
        var b = Primitive(PrimitiveType.Cube, "BladeB", root);
        b.localScale = new Vector3(0.05f, 0.42f, 0.03f);
        Materialize(b, shader, air);
        root.gameObject.AddComponent<OrbFx>().Pulse = OrbFx.Mode.Swirl;
        return root;
    }

    /// <summary>Water droplet (oblate sphere) with a short trailing splash of smaller drops.</summary>
    private static Transform Splash(string name, Shader shader, Color color)
    {
        var root = new GameObject(name).transform;
        var drop = Primitive(PrimitiveType.Sphere, "Drop", root);
        drop.localScale = new Vector3(0.26f, 0.2f, 0.26f);
        Materialize(drop, shader, color);
        for (int i = 0; i < 3; i++)
        {
            var trail = Primitive(PrimitiveType.Sphere, "Trail" + i, root);
            trail.localPosition = new Vector3(
                UnityEngine.Random.Range(-0.05f, 0.05f),
                UnityEngine.Random.Range(-0.04f, 0.04f),
                -0.28f - i * 0.15f);
            trail.localScale = Vector3.one * Mathf.Lerp(0.09f, 0.04f, i / 2f);
            Materialize(trail, shader, color);
        }
        root.gameObject.AddComponent<OrbFx>().Pulse = OrbFx.Mode.Plain;
        return root;
    }

    /// <summary>Streaking fire/energy comet: bright core + fading tail (hard to miss on screen).
    /// For sky-rock spells (summonFallingRock, e.g. the meteor-line Comet) the core becomes a
    /// rough burning boulder so it reads as a rock tearing through the sky, not a light streak.</summary>
    private static Transform Comet(string name, Shader shader, Color color, bool rockBody = false)
    {
        var root = new GameObject(name).transform;
        if (rockBody)
        {
            Color rock = Color.Lerp(Color.Lerp(Color.gray, Color.black, 0.4f), color, 0.35f);
            var boulder = Primitive(PrimitiveType.Cube, "BoulderCore", root);
            boulder.localScale = new Vector3(0.28f, 0.24f, 0.3f);
            Materialize(boulder, shader, rock);
            for (int i = 0; i < 3; i++)
            {
                var chunk = Primitive(PrimitiveType.Cube, "BoulderChunk" + i, root);
                chunk.localScale = Vector3.one * UnityEngine.Random.Range(0.12f, 0.18f);
                chunk.localRotation = UnityEngine.Random.rotation;
                chunk.localPosition = new Vector3(
                    UnityEngine.Random.Range(-0.14f, 0.14f),
                    UnityEngine.Random.Range(-0.12f, 0.12f),
                    UnityEngine.Random.Range(-0.1f, 0.1f));
                Materialize(chunk, shader, Color.Lerp(rock, Color.black, UnityEngine.Random.value * 0.35f));
            }
        }
        else
        {
            var core = Primitive(PrimitiveType.Sphere, "Core", root);
            core.localScale = new Vector3(0.2f, 0.2f, 0.28f);
            Materialize(core, shader, color);
        }
        var streak = Primitive(PrimitiveType.Cube, "Streak", root);
        streak.localPosition = new Vector3(0f, 0f, -0.35f);
        streak.localScale = new Vector3(0.07f, 0.07f, 0.6f);
        Materialize(streak, shader, color * 0.6f);
        root.gameObject.AddComponent<OrbFx>().Pulse = OrbFx.Mode.Ember;
        return root;
    }

    /// <summary>Cluster of small darts representing a volley (arcane missiles / darts).</summary>
    private static Transform Missile(string name, Shader shader, Color color)
    {
        var root = new GameObject(name).transform;
        for (int i = 0; i < 3; i++)
        {
            var m = Primitive(PrimitiveType.Sphere, "Missile" + i, root);
            m.localPosition = new Vector3(i * 0.16f - 0.16f, 0f, 0f);
            m.localScale = Vector3.one * 0.12f;
            Materialize(m, shader, color);
        }
        root.gameObject.AddComponent<OrbFx>().Pulse = OrbFx.Mode.Bolt;
        return root;
    }

    /// <summary>Small sleek bolt-line for quick shots (ranged darts / talisman trails).</summary>
    private static Transform Dart(string name, Shader shader, Color color)
    {
        var root = new GameObject(name).transform;
        var body = Primitive(PrimitiveType.Cube, "Body", root);
        body.localScale = new Vector3(0.06f, 0.06f, 0.6f);
        Materialize(body, shader, color);
        var tip = Primitive(PrimitiveType.Cube, "Tip", root);
        tip.localPosition = new Vector3(0f, 0f, 0.32f);
        tip.localScale = new Vector3(0.08f, 0.08f, 0.12f);
        Materialize(tip, shader, color);
        root.gameObject.AddComponent<OrbFx>().Pulse = OrbFx.Mode.Bolt;
        return root;
    }

    private static float EmissionRate(DamageType type)
    {
        switch (type)
        {
            case DamageType.Fire: return 150f;
            case DamageType.Ice: return 45f;
            case DamageType.Lightning: return 120f;
            case DamageType.Dark: return 30f;
            case DamageType.Wind: return 40f;
            default: return 60f;
        }
    }

    private static float StartLifetime(DamageType type)
    {
        switch (type)
        {
            case DamageType.Fire: return 0.45f;
            case DamageType.Ice: return 0.70f;
            case DamageType.Lightning: return 0.25f;
            case DamageType.Dark: return 0.65f;
            case DamageType.Wind: return 0.80f;
            default: return 0.50f;
        }
    }

    private static float StartSpeed(DamageType type)
    {
        switch (type)
        {
            case DamageType.Fire: return 4f;
            case DamageType.Ice: return 2f;
            case DamageType.Lightning: return 6f;
            case DamageType.Dark: return 1.5f;
            case DamageType.Wind: return 1.5f;
            default: return 3f;
        }
    }

    private static float StartSize(DamageType type)
    {
        switch (type)
        {
            case DamageType.Fire: return 0.12f;
            case DamageType.Ice: return 0.06f;
            case DamageType.Lightning: return 0.04f;
            case DamageType.Dark: return 0.14f;
            case DamageType.Wind: return 0.18f;
            default: return 0.08f;
        }
    }

    private static int MaxParticles(DamageType type)
    {
        switch (type)
        {
            case DamageType.Lightning: return 300;
            case DamageType.Fire: return 700;
            default: return 250;
        }
    }

    /// <summary>
    /// Comet-exhaust particle stream on a default projectile: a cone shaped exhaust trailing
    /// backward from the body so the bolt reads as an energetic magic projectile while flying.
    /// Emits from the body local origin; the cone is flipped -Z so particles stream behind it.
    /// </summary>
    private static void AttachProjectileParticles(Transform body, DamageType type, Color color)
    {
        var fxGo = new GameObject(body.name + "_Fx");
        fxGo.transform.SetParent(body, false);

        var ps = fxGo.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.loop = true;
        main.playOnAwake = true;
        main.startLifetime = StartLifetime(type);
        main.startSpeed = StartSpeed(type);
        main.startSize = StartSize(type);
        main.gravityModifier = 0f;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.maxParticles = MaxParticles(type);

        var emission = ps.emission;
        emission.rateOverTime = EmissionRate(type);

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 18f;
        shape.radius = 0.1f;
        shape.rotation = new Vector3(0f, 0f, 180f);

        var colorOverLifetime = ps.colorOverLifetime;
        colorOverLifetime.enabled = true;
        var grad = new Gradient();
        grad.SetKeys(
            new[] { new GradientColorKey(color, 0f), new GradientColorKey(color, 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
        colorOverLifetime.color = grad;

        var sizeOverLifetime = ps.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f,
            new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 0.4f)));

        Shader additive = Shader.Find("Particles/Additive") ?? Shader.Find("Sprites/Default");
        if (additive == null) return;
        var renderer = ps.GetComponent<ParticleSystemRenderer>();
        renderer.material = new Material(additive) { color = color };
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
    }

    /// <summary>
    /// Tiny flight animation for a projectile body: a per-type scale pulse (flicker / crackle /
    /// breathe) and, for the ice shard, a drill spin around its long axis.
    /// </summary>
    private sealed class OrbFx : MonoBehaviour
    {
        public enum Mode
        {
            Plain,  // gentle breathe
            Ember,  // fast irregular flicker
            Shard,  // slight breathe + drill spin
            Bolt,   // fast crackle pulse
            Wisp,   // slow pulsing
            Swirl,  // gentle pulse + fast funnel spin
            Tumble  // gentle pulse + spin around a per-object random axis (rock debris chunks)
        }

        public Mode Pulse;

        private Vector3 _baseScale;
        private Vector3 _spinAxis = Vector3.up;

        private void Start()
        {
            _baseScale = transform.localScale;
            if (Pulse == Mode.Tumble)
                _spinAxis = UnityEngine.Random.onUnitSphere;
        }

        private void Update()
        {
            float t = Time.time;
            float pulse;
            float spin = 0f;
            switch (Pulse)
            {
                case Mode.Ember:
                    pulse = 1f + 0.14f * Mathf.Sin(t * 11f) + 0.08f * Mathf.Sin(t * 17.3f);
                    break;
                case Mode.Shard:
                    pulse = 1f + 0.04f * Mathf.Sin(t * 4.2f);
                    spin = 160f;
                    break;
                case Mode.Bolt:
                    pulse = 1f + 0.22f * Mathf.Sin(t * 24f) * Mathf.Sin(t * 7f);
                    break;
                case Mode.Wisp:
                    pulse = 1f + 0.10f * Mathf.Sin(t * 2.6f);
                    break;
                case Mode.Swirl:
                    pulse = 1f + 0.10f * Mathf.Sin(t * 5.6f);
                    spin = 220f;
                    break;
                case Mode.Tumble:
                    pulse = 1f + 0.05f * Mathf.Sin(t * 5.1f);
                    spin = 120f;
                    break;
                default:
                    pulse = 1f + 0.06f * Mathf.Sin(t * 3.4f);
                    break;
            }
            transform.localScale = _baseScale * Mathf.Max(0.1f, pulse);
            if (spin != 0f)
            {
                if (Pulse == Mode.Tumble)
                    transform.Rotate(_spinAxis, spin * Time.deltaTime, Space.Self);
                else
                    transform.Rotate(0f, spin * Time.deltaTime, 0f, Space.Self);
            }
        }
    }
}
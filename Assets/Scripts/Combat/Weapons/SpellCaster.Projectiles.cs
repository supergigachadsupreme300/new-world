using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Partial: projectile spawning and the static per-shape projectile body visuals (§3.8).
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
            // 1ig: the body's shape, colour and size all come from the spell's resolved look, not
            // from the school. A cast with an authored CastEffectPrefab above still bypasses this
            // entirely, so the prefab hook keeps top precedence.
            AttachDefaultProjectileVisual(go, SpellLook.Resolve(spell), spell.SummonFallingRock);
            go.AddComponent<SpellEffect>().Initialize(spell, power, fwd, this, sizeScale);
        }

        // Charge scales the whole projectile body (authored prefab or generated visual).
        if (charge > 0f)
            go.transform.localScale *= sizeScale;

        if (go.TryGetComponent<SpellEffect>(out var proj))
            proj.Launch(spell.ProjectileSpeed);

        return new DamageResult();
    }

    /// <summary>1ig: the summoning spell's real identity, for a summoned turret's bolt.</summary>
    /// <para>(1ig: the sibling <c>DecorateProjectile(GameObject, DamageType, ProjectileShape)</c> was
    /// deleted. It had no callers, and the two overloads sat adjacent enough that a future call site
    /// could pick either — silently getting a school stand-in instead of the summoning spell's own
    /// identity, which is the exact confusion 1ib exists to remove. One name, one identity.)</para>
    public void DecorateProjectile(GameObject go, SpellData spell)
    {
        AttachDefaultProjectileVisual(go, SpellLook.Resolve(spell));
    }

    /// <summary>
    /// Stand-alone render-only spell visual with <b>no spell behind it</b>: the identity-less school
    /// stand-in. Builds the exact static projectile body a live cast carries (1eb — projectiles now
    /// carry no particles and no in-flight pulse), with no <see cref="SpellEffect"/>, no collider and
    /// no launch — it simply sits at its root.
    ///
    /// <para><b>1ij: currently zero callers.</b> The magic-model bench used this one, which is why
    /// every Fire spell on it drew the same school stand-in body regardless of its own display shape
    /// (THINKING.md 1ij H49); it now calls the <see cref="SpellData"/> overload. Kept because it is the
    /// public entry point to <c>SpellLook.Resolve(DamageType, ProjectileShape)</c> — rule 13's
    /// precedence step 3 — and a spell-less caller (a non-spell turret bolt, a QA strip) will want it.
    /// <b>If a grep still finds nothing at the next cleanup, delete it</b> rather than letting it sit
    /// as an unread alternative next to the per-spell overload.</para>
    /// </summary>
    public static GameObject CreateProjectileDisplay(DamageType type, ProjectileShape shape = ProjectileShape.Auto,
        bool rockBody = false)
    {
        var go = new GameObject("MagicModelDisplay");
        AttachDefaultProjectileVisual(go, SpellLook.Resolve(type, shape), rockBody);
        return go;
    }

    /// <summary>
    /// 1ig/1ij: the bench's per-spell display. Takes the <see cref="SpellData"/> so the bench shows
    /// the SAME body a real cast produces for that spell, rather than a school stand-in — which is
    /// the whole point of a per-spell identity bench.
    /// <para><b>This is the overload a caller should reach for by default.</b> Written in 1ig with no
    /// caller and not wired up until 1ij — which is exactly why the identity-less overload above was
    /// still live and why <c>DecorateProjectile(GameObject, DamageType, ProjectileShape)</c> looked
    /// merely unused rather than superseded. <paramref name="rockBody"/> defaults from the spell's own
    /// <c>SummonFallingRock</c>, so callers normally pass just the spell.</para>
    /// </summary>
    public static GameObject CreateProjectileDisplay(SpellData spell, bool rockBody = false)
    {
        var go = new GameObject("MagicModelDisplay");
        AttachDefaultProjectileVisual(go, SpellLook.Resolve(spell),
            rockBody || (spell != null && spell.SummonFallingRock));
        return go;
    }

    /// <summary>
    /// Build a shape-aware visible projectile body for spells with no authored CastEffectPrefab,
    /// so magic skills read on screen. Since 1eb the body is fully static — no exhaust particles
    /// and no per-frame pulse animation — and since 1ec every body is a **voxel cube-cluster**:
    /// a front-leading cube in the school color with smaller, darker cubes stacked behind it.
    ///
    /// <para><b>1ig: every input comes from <see cref="SpellLook"/>.</b> This method used to take
    /// <c>(DamageType, ProjectileShape)</c> and resolve the shape itself through
    /// <c>AutoShapeFor</c>. That was a SECOND independent spelling of "what shape does an Auto
    /// projectile wear" — the exact rule-12 rot, one layer out from the palette. The per-school
    /// shape families in <see cref="SpellLook"/> are now the only such table, and
    /// <c>AutoShapeFor</c>/<c>ResolveShape</c> were deleted with it (rule 13: the table outlived
    /// the lookup).</para>
    ///
    /// Renderer-only: the root keeps no collider so SpellEffect's flight raycast never self-hits.
    /// Static — the visual has no instance state beyond the per-spell colour and scale.
    /// </summary>
    private static void AttachDefaultProjectileVisual(GameObject go, in SpellLook look, bool rockBody = false)
    {
        Color color = look.Core;
        Shader shader = Shader.Find("Sprites/Default") ?? Shader.Find("Unlit/Color");
        if (shader == null)
            return;

        // DisplayShape is display-only by contract: an authored spell.Shape won inside Resolve, and
        // the deterministic pick never returns Missile (that value also means homing). The root
        // SpellEffect still reads spell.Shape for _homing, untouched.
        var body = BuildProjectileBody(look.DisplayShape, shader, color, rockBody);
        body.SetParent(go.transform, false);
        float s = Mathf.Max(0.2f, look.Scale);
        if (Mathf.Abs(s - 1f) > 0.001f)
            body.localScale = Vector3.one * s;
    }

    /// <summary>Color-matched visual body for a projectile by resolved shape. <paramref name="rockBody"/>
    /// dresses the shape as a rough burning rock (the 1cy sky-rock projectile body). 1ir: no spell
    /// uses it any more — the meteor-line slot is now Flamethrower, a Beam with no projectile body at
    /// all — but the parameter stays, because it is a property of the <see cref="Comet"/> shape rather
    /// than of any one spell, and removing it would mean deleting the boulder body a future sky-rock
    /// projectile may want.</summary>
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
            default: return Cluster("Orb", shader, color, 0.24f, 5, 0.10f, 0.03f); // fireball + generic orbs
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

    /// <summary>
    /// Voxel-cluster projectile body (1ec): one leading cube in the full school color with
    /// progressively smaller, darker cubes stacked behind it (-Z), so a spell ball reads as a hot
    /// core fading into a tapering tail. Built once and fully static (1eb) — no per-frame component.
    /// `count` includes the leader; `fade` pushes each back cube toward black.
    /// </summary>
    private static Transform Cluster(string name, Shader shader, Color color, float lead, int count,
        float spacing, float jitter, float fade = 0.75f, float minCube = 0.05f)
    {
        var root = new GameObject(name).transform;
        for (int i = 0; i < count; i++)
        {
            float t = count <= 1 ? 0f : i / (float)(count - 1);
            float s = Mathf.Lerp(lead, minCube, t * t);
            Vector3 j = i == 0
                ? Vector3.zero
                : new Vector3(
                    UnityEngine.Random.Range(-jitter, jitter),
                    UnityEngine.Random.Range(-jitter * 0.6f, jitter * 0.6f),
                    0f);
            var cube = Primitive(PrimitiveType.Cube, "Cube" + i, root);
            cube.localPosition = new Vector3(j.x, j.y, -i * spacing);
            cube.localScale = Vector3.one * s;
            if (i > 0)
                cube.localRotation = Quaternion.Euler(0f, 0f, UnityEngine.Random.Range(0f, 360f));
            Color c = Color.Lerp(color, Color.black, t * fade);
            Materialize(cube, shader, c);
        }
        return root;
    }

    /// <summary>Append 2-3 small, darker cubes directly behind `root` (-Z) so elongated shapes
    /// (lance/spear/blade/dart) carry the same voxel fade as the cluster bodies.</summary>
    private static void AddTrailingFlecks(Transform root, Shader shader, Color color,
        float fromZ, int count = 2)
    {
        for (int i = 0; i < count; i++)
        {
            var fleck = Primitive(PrimitiveType.Cube, "Fleck" + i, root);
            fleck.localPosition = new Vector3(
                UnityEngine.Random.Range(-0.02f, 0.02f),
                UnityEngine.Random.Range(-0.02f, 0.02f),
                -fromZ - i * 0.09f);
            float f = count <= 1 ? 0f : i / (float)(count - 1);
            fleck.localScale = Vector3.one * Mathf.Lerp(0.06f, 0.035f, f);
            Materialize(fleck, shader, Color.Lerp(color, Color.black, 0.45f + f * 0.2f));
        }
    }

    /// <summary>
    /// Diamond-shaped shard that drills forward — a translucent glass lead chip with two smaller
    /// darker glass chips trailing behind it (1ec), keeping the frost chip read fading to the rear.
    /// </summary>
    private static Transform Shard(string name, Shader shader, Color color)
    {
        var root = new GameObject(name).transform;
        var lead = Primitive(PrimitiveType.Cube, "Lead", root);
        lead.localScale = new Vector3(0.12f, 0.38f, 0.12f);
        lead.localRotation = Quaternion.Euler(0f, 45f, 0f);
        // Translucent glassy frost chip: "Sprites/Default" blends via the material color alpha.
        Color glass = new Color(color.r, color.g, color.b, 0.5f);
        Materialize(lead, shader, glass);
        for (int i = 0; i < 2; i++)
        {
            var chip = Primitive(PrimitiveType.Cube, "Chip" + i, root);
            float ss = Mathf.Lerp(0.1f, 0.06f, i);
            chip.localScale = new Vector3(ss, ss * 2.4f, ss);
            chip.localRotation = Quaternion.Euler(0f, 45f - i * 14f, 0f);
            chip.localPosition = new Vector3(0f, 0f, -0.16f - i * 0.11f);
            float dim = 0.85f - i * 0.2f;
            Materialize(chip, shader, new Color(color.r * dim, color.g * dim, color.b * dim, 0.45f));
        }
        return root;
    }

    /// <summary>
    /// Clustered rock chunks — the Earth school's projectile (Stone Shard / stone shards).
    /// Mirrors the world's breakable-rock debris look (WorldBuilder.SpawnRockDebris): random grey
    /// <c>Color.Lerp(Color.gray, Color.black, rand)</c> cubes of mixed sizes, clustered with the
    /// leader ahead and the tail trailing so the clump reads as one forward-striking debris blob.
    /// Two chunks are dusted with the Earth accent color so it still reads as magic, not just a
    /// terrain chunk.
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
        }
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
        AddTrailingFlecks(root, shader, color, 0.6f);
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
        AddTrailingFlecks(root, shader, color, 0.55f);
        return root;
    }

    /// <summary>Flat slashing cross-blade (wind blades / scissor) with two small ghost cubes trailing
    /// behind it — translucent ethereal wind.</summary>
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
        AddTrailingFlecks(root, shader, air, 0.24f);
        return root;
    }

    /// <summary>Water droplet cube with a short trailing splash of smaller, darker cube drops (1ec —
    /// spheres retired from projectile visuals in favor of the voxel cluster look).</summary>
    private static Transform Splash(string name, Shader shader, Color color)
    {
        var root = new GameObject(name).transform;
        var drop = Primitive(PrimitiveType.Cube, "Drop", root);
        drop.localScale = new Vector3(0.26f, 0.2f, 0.26f);
        Materialize(drop, shader, color);
        for (int i = 0; i < 3; i++)
        {
            var trail = Primitive(PrimitiveType.Cube, "Trail" + i, root);
            trail.localPosition = new Vector3(
                UnityEngine.Random.Range(-0.05f, 0.05f),
                UnityEngine.Random.Range(-0.04f, 0.04f),
                -0.28f - i * 0.15f);
            trail.localScale = Vector3.one * Mathf.Lerp(0.09f, 0.04f, i / 2f);
            Materialize(trail, shader, Color.Lerp(color, Color.black, 0.3f + i * 0.2f));
        }
        return root;
    }

    /// <summary>Streaking fire/energy comet: bright core + fading tail (hard to miss on screen).
    /// The <paramref name="rockBody"/> variant dresses the core as a rough burning boulder. 1ir: the
    /// meteor-line skill that owned this shape is now Flamethrower, a Beam with no projectile body at
    /// all, so in practice every caller takes the light-streak branch. The rock variant is kept on the
    /// same reasoning 1f7 used — it belongs to the shape, not to a spell. Scorch / Burn / Frost Bite
    /// are the spells that wear this shape now.</summary>
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
            // The comet core is itself a small voxel cluster (1ec) so its read matches the sphere
            // default ball spells now use; the streak tail below still fades it to the rear.
            var core = Cluster("Core", shader, color, 0.2f, 3, 0.1f, 0.02f, fade: 0.6f);
            core.SetParent(root, false);
        }
        var streak = Primitive(PrimitiveType.Cube, "Streak", root);
        streak.localPosition = new Vector3(0f, 0f, -0.35f);
        streak.localScale = new Vector3(0.07f, 0.07f, 0.6f);
        Materialize(streak, shader, color * 0.6f);
        return root;
    }

    /// <summary>Cluster of small voxel darts representing a volley (arcane missiles / darts) — each
    /// dart is a 2-cube mini stack (1ec).</summary>
    private static Transform Missile(string name, Shader shader, Color color)
    {
        var root = new GameObject(name).transform;
        for (int i = 0; i < 3; i++)
        {
            var mini = Cluster("Missile" + i, shader, color, 0.12f, 2, 0.07f, 0.015f, fade: 0.6f);
            mini.localPosition = new Vector3(i * 0.16f - 0.16f, 0f, 0f);
            mini.SetParent(root, false);
        }
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
        AddTrailingFlecks(root, shader, color, 0.38f);
        return root;
    }
}
using UnityEngine;

/// <summary>
/// Procedural models for the ten PROJECTILE BODIES a spell can wear, plus the stand-alone display
/// entry points the QA bench draws. Split out of SpellCaster.Projectiles.cs by 1jb (1iz is what
/// found the magic weapon models that started this) so the projectile models are findable by name.
/// Previously they were fourteen static methods buried in a partial named after the SPELL CASTER -
/// a file whose name describes who fires a projectile, not what the projectile looks like.
///
/// What moved here, and what deliberately did not:
///
/// 1. MOVED: both <c>CreateProjectileDisplay</c> overloads, <c>AttachDefaultProjectileVisual</c>,
///    <c>BuildProjectileBody</c>, and every per-shape body builder (Bolt/Ball/Lance/Spear/Blade/
///    Splash/Comet/Missile/Dart/Debris/Wave/Soul/Hand/Shard) plus the shared primitives (Primitive/Materialize/Cluster/
///    AddTrailingFlecks). Measured self-contained before the move: every reference to all fourteen
///    lives inside this block, with exactly ONE external caller of the public API
///    (NewWorldTestGround's magic-model bench).
///
/// 2. STAYED in SpellCaster.Projectiles.cs: <c>FireProjectile</c> and <c>DecorateProjectile</c>.
///    Those are behaviour - they instantiate, parent, launch and scale a projectile for a live cast.
///    Moving a spawn path next to its geometry is how "where does the projectile come from" and "what
///    does the projectile look like" end up in different files with no shared name. Note that
///    <c>DecorateProjectile</c> is still the public door into
///    <see cref="AttachDefaultProjectileVisual"/> for a non-projectile bolt (SpellSummon's turret),
///    which is the one reason the geometry entry point is <c>internal</c> rather than private.
///
/// One thing in here that looks like a typo and is not: <c>Primitive</c> calls
/// <c>Object.Destroy(col)</c> rather than the bare <c>Destroy</c> it used to call. That bare call
/// resolved through SpellCaster's MonoBehaviour base; this is a plain static class, so the
/// unqualified form is CS0103 and would not compile. The qualification is the whole cost of the
/// move - see PROGRESS.md 1jb.
///
/// Renderer-only: every body is built from primitives whose colliders are destroyed, and the root
/// keeps none of its own, so SpellEffect's flight raycast never self-hits on the visual.
/// </summary>
internal static class MagicProjectileModelBuilder
{
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
    ///
    /// <para><b>1l88: <paramref name="waveExtent"/> is a Wave-only escape hatch.</b> A charged wave
    /// grows its block COUNT through the floor's extent rather than scaling the whole body up, so
    /// <c>ResolveWave</c> passes its charge size scale here instead of touching <c>go.localScale</c>.
    /// Every other shape ignores it (defaults to 1).</para>
    /// </summary>
    internal static void AttachDefaultProjectileVisual(GameObject go, in SpellLook look, bool rockBody = false,
        float waveExtent = 1f)
    {
        Color color = look.Core;
        Shader shader = Shader.Find("Sprites/Default") ?? Shader.Find("Unlit/Color");
        if (shader == null)
            return;

        // DisplayShape is display-only by contract: an authored spell.Shape won inside Resolve, and
        // the deterministic pick never returns Missile (that value also means homing). The root
        // SpellEffect still reads spell.Shape for _homing, untouched.
        var body = BuildProjectileBody(look.DisplayShape, shader, color, rockBody, waveExtent);
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
    /// projectile may want. <paramref name="waveExtent"/> is forwarded to <see cref="Wave"/> (and,
    /// 1l111, <see cref="StormWave"/> — both ground-hugging wave floors) only; every other branch
    /// ignores it.</summary>
    private static Transform BuildProjectileBody(ProjectileShape shape, Shader shader, Color color, bool rockBody = false,
        float waveExtent = 1f)
    {
        switch (shape)
        {
            case ProjectileShape.Bolt: return Bolt("JaggedBolt", shader, color);
            case ProjectileShape.Shard: return Shard("Shard", shader, color);
            case ProjectileShape.Ball: return Cluster("Ball", shader, color, 0.24f, 5, 0.10f, 0.03f); // 1l71: the round ball
            case ProjectileShape.Lance: return Lance("IceLance", shader, color);
            case ProjectileShape.Spear: return Spear("Spear", shader, color);
            case ProjectileShape.Blade: return Blade("WindBlade", shader, color);
            case ProjectileShape.Splash: return Splash("WaterSplash", shader, color);
            case ProjectileShape.Comet: return Comet("Comet", shader, color, rockBody);
            case ProjectileShape.Missile: return Missile("ArcaneMissiles", shader, color);
            case ProjectileShape.Soul: return Soul("ChillSoul", shader, color);
            case ProjectileShape.Hand: return Hand("ChillTouch", shader, color);
            case ProjectileShape.Dart: return Dart("Dart", shader, color);
            case ProjectileShape.Debris: return Debris("RockDebris", shader);
            case ProjectileShape.Wave: return Wave("FireWave", shader, color, waveExtent);
            case ProjectileShape.VoltBolt: return VoltBolt("VoltBolt", shader, color);
            case ProjectileShape.SparkSwarm: return SparkSwarm("VoltSpark", shader, color);
            case ProjectileShape.GreatBolt: return GreatBolt("GreatBolt", shader, color);
            case ProjectileShape.FuryLance: return FuryLance("FuryLance", shader, color);
            case ProjectileShape.StormWave: return StormWave("StormWave", shader, color, waveExtent);
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
            Object.Destroy(col);
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
    /// 1l71: a thin long flat triangle flying tip-first — the "frost &amp; stone chip" body. A stack of
    /// flat cubes in the local XZ plane that taper in WIDTH to a near-point at +Z (the leading edge),
    /// staying paper-thin in Y, so the silhouette reads as a long triangle rather than the round
    /// <see cref="Ball"/> cluster or the solid <see cref="Lance"/> spike. Plain display shape — no
    /// behaviour — so it is safe in the school families; the seven chip families (Ice, Lightning,
    /// Holy, Dark, Earth, Water, Arcane) now pick it where the 1ec diamond used to sit.
    /// </summary>
    private static Transform Shard(string name, Shader shader, Color color)
    {
        var root = new GameObject(name).transform;
        for (int i = 0; i < 5; i++)
        {
            float t = i / (float)(5 - 1);
            var seg = Primitive(PrimitiveType.Cube, "Seg" + i, root);
            seg.localPosition = new Vector3(0f, 0f, Mathf.Lerp(-0.22f, 0.30f, t));
            // Width 0.30 → 0.04 and depth 0.18 → 0.08: the stack narrows to a point at the leading
            // edge, while height stays ~0.05 so the sliver reads thin and flat, edge-on.
            seg.localScale = new Vector3(
                Mathf.Lerp(0.30f, 0.04f, t),
                Mathf.Lerp(0.06f, 0.035f, t),
                Mathf.Lerp(0.18f, 0.08f, t));
            Color c = Color.Lerp(color, Color.white, t * 0.25f); // tip brightens like a cutting edge
            Materialize(seg, shader, c);
        }
        AddTrailingFlecks(root, shader, color, 0.26f);
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

    /// <summary>1l110: a compact, fast, high-contrast zap — a SHORT chunky jagged streak whose body
    /// brightens toward the leading edge so the tip reads white-hot. Built like <see cref="Bolt"/> but
    /// shorter (0.9 vs 1.25), fewer/thicker segments, and a colour ramp toward
    /// <c>Color.Lerp(color, white, ...)</c> on the front half, so a rapid-fire Volt reads differently
    /// from a long forked chain bolt. Display-only: no behaviour rides on the shape.</summary>
    private static Transform VoltBolt(string name, Shader shader, Color color)
    {
        var root = new GameObject(name).transform;
        int segments = 6;
        float length = 0.9f;
        float jitter = 0.07f;
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
                Mathf.Lerp(0.20f, 0.09f, t),
                Mathf.Lerp(0.20f, 0.09f, t),
                Mathf.Max(0.1f, Vector3.Distance(prev, next)));
            if (segDir.sqrMagnitude > 0.001f && Mathf.Abs(segDir.z) < 0.999f)
                seg.rotation = Quaternion.LookRotation(segDir, Vector3.up);
            // Front half ramps to white-hot: the leading cube is the brightest point of the zap.
            Materialize(seg, shader, Color.Lerp(color, Color.white, t * t * 0.7f));
            prev = next;
        }
        return root;
    }

    /// <summary>1l110: a scattered swarm of tiny bright spark motes flying forward — Volt Spark's body.
    /// Unlike the ordered <see cref="Missile"/> volley (three aligned mini-stacks), this is a loose
    /// cloud of small cubes jittered around the travel axis (+Z) and brightened toward white, so a
    /// seeking spark reads as a crackle of static rather than a darts volley. Display-only: the homing
    /// that makes Volt Spark seek is <see cref="ProjectileShape.Missile"/> on <c>spell.Shape</c>, and
    /// this body is granted only through an authored <c>Look.DisplayShape</c>.</summary>
    private static Transform SparkSwarm(string name, Shader shader, Color color)
    {
        var root = new GameObject(name).transform;
        int count = 9;
        for (int i = 0; i < count; i++)
        {
            var mote = Primitive(PrimitiveType.Cube, "Spark" + i, root);
            mote.localPosition = new Vector3(
                UnityEngine.Random.Range(-0.13f, 0.13f),
                UnityEngine.Random.Range(-0.13f, 0.13f),
                UnityEngine.Random.Range(-0.16f, 0.22f));
            mote.localScale = Vector3.one * UnityEngine.Random.Range(0.045f, 0.09f);
            mote.localRotation = UnityEngine.Random.rotation;
            Color c = i % 3 == 0
                ? Color.Lerp(color, Color.white, 0.6f)
                : Color.Lerp(color, Color.white, UnityEngine.Random.Range(0.15f, 0.45f));
            Materialize(mote, shader, c);
        }
        return root;
    }

    /// <summary>1l110: a heavy great-bolt — a long, thick jagged streak around a white-hot core, the
    /// body Volt Bolt wears (Fury Bolt joins it in a later Lightning phase). Built like <see cref="Bolt"/> but longer (1.7 vs 1.25),
    /// thicker, with a thin bright core rod magnetised down the middle of the jitter and trailing
    /// flecks, so a charged heavy bolt reads as more mass than a plain zap. Display-only: no
    /// behaviour rides on the shape.</summary>
    private static Transform GreatBolt(string name, Shader shader, Color color)
    {
        var root = new GameObject(name).transform;
        int segments = 10;
        float length = 1.7f;
        float jitter = 0.11f;
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
            float w = Mathf.Lerp(0.26f, 0.10f, t);
            seg.localScale = new Vector3(w, w, Mathf.Max(0.1f, Vector3.Distance(prev, next)));
            if (segDir.sqrMagnitude > 0.001f && Mathf.Abs(segDir.z) < 0.999f)
                seg.rotation = Quaternion.LookRotation(segDir, Vector3.up);
            Materialize(seg, shader, color);
            prev = next;
        }
        // White-hot core: a thin bright rod down the flight axis, so the great bolt has a hot centre
        // the plain Bolt lacks (legibility at the thick segment widths).
        var core = Primitive(PrimitiveType.Cube, "Core", root);
        core.localScale = new Vector3(0.07f, 0.07f, length);
        Materialize(core, shader, Color.Lerp(color, Color.white, 0.75f));
        AddTrailingFlecks(root, shader, color, length * 0.5f + 0.05f, 3);
        return root;
    }

    /// <summary>1l112: Fury Bolt's body — a long forked lightning lance: a thick jagged shaft around a
    /// white-hot core (built like <see cref="GreatBolt"/>) but longer (2.0 vs 1.7) with two short
    /// branches forking off the shaft near the head, so the fury-bank bolt reads as heavier and
    /// branching rather than a plain Streak. Display-only: no behaviour rides on the shape.</summary>
    private static Transform FuryLance(string name, Shader shader, Color color)
    {
        var root = new GameObject(name).transform;
        int segments = 11;
        float length = 2.0f;
        float jitter = 0.13f;
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
            float w = Mathf.Lerp(0.28f, 0.11f, t);
            seg.localScale = new Vector3(w, w, Mathf.Max(0.1f, Vector3.Distance(prev, next)));
            if (segDir.sqrMagnitude > 0.001f && Mathf.Abs(segDir.z) < 0.999f)
                seg.rotation = Quaternion.LookRotation(segDir, Vector3.up);
            Materialize(seg, shader, color);
            prev = next;
        }

        // White-hot core rod down the flight axis (heavier than GreatBolt's so the lance has mass).
        var core = Primitive(PrimitiveType.Cube, "Core", root);
        core.localScale = new Vector3(0.08f, 0.08f, length);
        Materialize(core, shader, Color.Lerp(color, Color.white, 0.75f));

        // Two short branches forking off the shaft near the head — the "forked" in the lance.
        for (int b = 0; b < 2; b++)
        {
            float side = b == 0 ? 1f : -1f;
            var fork = Primitive(PrimitiveType.Cube, "Fork" + b, root);
            fork.localPosition = new Vector3(side * 0.18f, side * 0.10f, length * 0.28f);
            fork.localScale = new Vector3(0.09f, 0.09f, 0.5f);
            fork.localRotation = Quaternion.Euler(side * -22f, side * 26f, 0f);
            Materialize(fork, shader, Color.Lerp(color, Color.white, 0.35f));
        }

        AddTrailingFlecks(root, shader, color, length * 0.5f + 0.05f, 3);
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

    /// <summary>
    /// 1l6x: a drifting spectral wisp — Chill Soul's body. One cold soul instead of the arcane
    /// three-dart volley: a bright core leads (+Z) inside a soft translucent halo, with a long
    /// tapering frost tail and two stray motes trailing behind, so a homing missile reads as a
    /// spirit that hunts rather than a scatter of chips. <see cref="ProjectileShape.Soul"/> is
    /// display-only — no behaviour rides on it — and this body is reached only through an authored
    /// <c>SpellLookProfile.DisplayShape</c>.
    /// </summary>
    private static Transform Soul(string name, Shader shader, Color color)
    {
        var root = new GameObject(name).transform;

        // Soft outer halo: a large, dim, slightly turned shell = the soul's aura.
        var halo = Primitive(PrimitiveType.Cube, "Halo", root);
        halo.localScale = Vector3.one * 0.42f;
        halo.localRotation = Quaternion.Euler(0f, 45f, 45f);
        Materialize(halo, shader, new Color(color.r, color.g, color.b, 0.20f));

        // Bright cold core, leading the wisp.
        var core = Primitive(PrimitiveType.Cube, "Core", root);
        core.localPosition = new Vector3(0f, 0f, 0.05f);
        core.localScale = Vector3.one * 0.22f;
        Materialize(core, shader, Color.Lerp(color, Color.white, 0.45f));

        // Tapering frost tail: shrinking, darkening, increasingly transparent cubes to the rear.
        const int tail = 4;
        for (int i = 0; i < tail; i++)
        {
            float t = i / (float)(tail - 1);
            var seg = Primitive(PrimitiveType.Cube, "Tail" + i, root);
            seg.localPosition = new Vector3(
                UnityEngine.Random.Range(-0.03f, 0.03f),
                UnityEngine.Random.Range(-0.03f, 0.03f),
                -0.12f - i * 0.10f);
            seg.localScale = Vector3.one * Mathf.Lerp(0.16f, 0.05f, t);
            seg.localRotation = Quaternion.Euler(0f, 0f, UnityEngine.Random.Range(0f, 360f));
            Color c = Color.Lerp(color, Color.black, 0.35f + t * 0.4f);
            Materialize(seg, shader, new Color(c.r, c.g, c.b, Mathf.Lerp(0.7f, 0.3f, t)));
        }

        // Two stray motes drifting beside the tail.
        for (int i = 0; i < 2; i++)
        {
            var mote = Primitive(PrimitiveType.Cube, "Mote" + i, root);
            float side = i == 0 ? 1f : -1f;
            mote.localPosition = new Vector3(side * 0.12f, UnityEngine.Random.Range(-0.04f, 0.04f), -0.22f - i * 0.12f);
            mote.localScale = Vector3.one * 0.06f;
            Materialize(mote, shader, new Color(color.r, color.g, color.b, 0.55f));
        }
        return root;
    }

    /// <summary>
    /// 1l6y: a spectral reaching hand — Chill Touch's body. A flat translucent palm whose four
    /// fingers lead (+Z) into the foe, with a thumb to one side, a bright cold core in the palm and
    /// two ghostly flecks trailing, so a numbing touch reads as a hand reaching out rather than a
    /// frost chip. <see cref="ProjectileShape.Hand"/> is display-only — no behaviour rides on it —
    /// and this body is reached only through an authored <c>SpellLookProfile.DisplayShape</c>.
    /// <para><b>1l101 flips the hand up 90°.</b> The user ("palm-shape projectile … flip it up 90
    /// degree") wants the flat palm facing the foe rather than lying flat: all the geometry now hangs
    /// under one <c>HandGeometry</c> node rotated <c>Euler(90, 0, 0)</c>, which maps the palm's normal
    /// (+Y) to the travel axis (+Z) so the palm faces INTO the foe (the fingers, which led +Z, end
    /// pointing down — a natural reaching pose). The trailing flecks deliberately stay on the
    /// UN-rotated <c>root</c> so they still stream back along the true travel axis (-Z) behind the
    /// hand. This is a look-only change: <see cref="SpellEffect.RollsInFlight"/> stops the hand
    /// rolling about its travel axis in flight, and <c>ChargedProjectilePreview</c> reads the same
    /// predicate — so a held hand holds still and the pose the player sees is the pose that flies.</para>
    /// </summary>
    private static Transform Hand(string name, Shader shader, Color color)
    {
        var root = new GameObject(name).transform;

        // 1l101: all hand geometry lives under this node so ONE rotation flips the whole hand; the
        // palm's thin axis (Y) becomes the depth axis (Z), i.e. the palm faces forward along travel.
        var geom = new GameObject("HandGeometry").transform;
        geom.SetParent(root, false);
        geom.localRotation = Quaternion.Euler(90f, 0f, 0f);

        // Flat translucent palm (thin along the node's Y before the flip, i.e. now facing +Z).
        var palm = Primitive(PrimitiveType.Cube, "Palm", geom);
        palm.localScale = new Vector3(0.30f, 0.07f, 0.22f);
        Materialize(palm, shader, new Color(color.r, color.g, color.b, 0.5f));

        // Four fingers, splayed across the palm's front edge (leading +Z before the flip).
        for (int i = 0; i < 4; i++)
        {
            var finger = Primitive(PrimitiveType.Cube, "Finger" + i, geom);
            finger.localPosition = new Vector3(-0.105f + i * 0.07f, 0f, 0.18f);
            finger.localScale = new Vector3(0.05f, 0.05f, 0.17f);
            finger.localRotation = Quaternion.Euler(0f, (i - 1.5f) * 6f, 0f);
            Materialize(finger, shader, new Color(color.r, color.g, color.b, 0.55f));
        }

        // Thumb, jutted out to one side of the palm.
        var thumb = Primitive(PrimitiveType.Cube, "Thumb", geom);
        thumb.localPosition = new Vector3(0.17f, 0f, 0.02f);
        thumb.localScale = new Vector3(0.06f, 0.055f, 0.13f);
        thumb.localRotation = Quaternion.Euler(0f, -32f, 0f);
        Materialize(thumb, shader, new Color(color.r, color.g, color.b, 0.55f));

        // Bright cold core in the palm.
        var core = Primitive(PrimitiveType.Cube, "Core", geom);
        core.localScale = Vector3.one * 0.11f;
        Materialize(core, shader, Color.Lerp(color, Color.white, 0.45f));

        // Flecks stay on the ROOT: they trail the hand along the travel axis, which the flip must not
        // rotate into the palm plane.
        AddTrailingFlecks(root, shader, color, 0.18f);

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

    /// <summary>
    /// Ground-hugging wave front (1jy): a floor of voxel cubes that ripples as it travels. The parent
    /// wave's rotation is flattened to horizontal by <c>SpellWave</c> (whose <c>Initialize</c> re-aims
    /// <c>LookRotation</c> at the flattened direction), so the floor always lies flat on the terrain
    /// instead of inheriting the release aim's pitch. Assumes local +Z is the travel direction.
    /// <para>1l85 rework: the old body was a short diagonal ramp of 5 cubes — a wave SHAPE, not a wave.
    /// It is now a flat lattice of small cubes, and the crest is DRAWN by a traveling sine
    /// (<see cref="WaveRipple"/>, driven from <c>SpellWave</c>'s update loop) rather than baked into the
    /// geometry, so a crest actually rolls across the floor as the wave sweeps. The body stays
    /// static by construction: the ripple component has no <c>Update</c>, so the QA pedestal this
    /// builder also feeds draws the floor motionless.</para>
    /// <para>1l88: the cubes are packed FLUSH to their pitch (fill 1.01, so they meet with a hair of
    /// overlap and show no gutter — 1l86's fill 0.62 left a ~0.11 m gap between them) and there are
    /// more of them (12 x 24 over the same footprint, ~0.28 m cubes, vs 7 x 14 x ~0.30 m).</para>
    /// <para><b><paramref name="extent"/> scales the block COUNT, not the block size</b> (1l88): a
    /// charged wave is a WIDER, LONGER floor of the same cubes, not a scaled-up copy. `ResolveWave`
    /// passes its charge size scale here and no longer touches <c>go.localScale</c>, so the cubes stay
    /// a constant size at every charge. The count is capped (1400 cells); past the cap the cubes grow
    /// instead, so an unbounded hold-to-overcharge cannot spawn an unbounded floor.</para>
    /// <para>1l90: the floor is a CREST, not a slab. The centre columns rest higher
    /// (<c>crestLift</c>) and fall to zero at the side edges, and the side columns are drawn back in z
    /// (<c>frontBow</c>) so the front edge curves forward in the middle — a wave silhouette rather than
    /// a rectangle of equal-height cubes. Because the ripple's phase reads each cell's z, a bowed
    /// column also curves the travelling crest line to match the front. The QA pedestal (extent 1) shows
    /// this shape statically.</para>
    /// </summary>
    private static Transform Wave(string name, Shader shader, Color color, float extent = 1f)
    {
        var root = new GameObject(name).transform;

        // Base lattice (extent 1): 12 x 24 cubes over the wave's 3.4 x 7 m footprint, so a block is
        // ~0.28 x 0.29 m and packed flush (fill 1.01) rather than gapped.
        const int baseCols = 12;
        const int baseRows = 24;
        const float baseWidth = 3.4f;
        const float baseDepth = 7f;
        const float thickness = 0.12f;
        const float amplitude = 0.16f;
        const float speed = 7f;
        const float wavelength = 2.6f;
        const float fill = 1.01f;
        const int waveCellCap = 1400;
        // 1l90 silhouette: the floor is no longer a flat rectangle of equal-width rows — the centre of
        // the floor rides higher (crestLift) and falls away to nothing at the side edges, and the side
        // columns are pulled back (frontBow) so the leading edge curves instead of standing straight.
        const float crestLift = 0.35f;
        const float frontBow = 1.2f;

        // Charge/radius extent grows the floor AND the count together, so the pitch — and therefore
        // the block size — stays fixed while a charged wave is simply larger. Clamped so a pathological
        // multiplier cannot overflow the cell-count arithmetic below.
        extent = Mathf.Clamp(extent, 0.05f, 64f);
        int cols = Mathf.Max(1, Mathf.RoundToInt(baseCols * extent));
        int rows = Mathf.Max(1, Mathf.RoundToInt(baseRows * extent));
        float width = baseWidth * extent;
        float depth = baseDepth * extent;

        // Safety valve: above the cap, stop adding cubes and let them grow instead. At the full charge
        // (sizeScale 2.2) the floor is 26 x 53 = 1378 cells, just under the cap, so the count path is
        // untouched for ordinary casts; only a heavy overcharge falls back to a larger cube.
        if (cols * rows > waveCellCap)
        {
            float shrink = Mathf.Sqrt((float)waveCellCap / (cols * rows));
            cols = Mathf.Max(1, Mathf.RoundToInt(cols * shrink));
            rows = Mathf.Max(1, Mathf.RoundToInt(rows * shrink));
        }

        float sx = width / cols;
        float sz = depth / rows;
        float cellX = sx * fill;
        float cellZ = sz * fill;
        float halfW = width * 0.5f;

        // The floor's rest colour; the ripple lerps each cell up toward the school colour on the crest.
        Color trough = Color.Lerp(color, Color.black, 0.55f);
        var cells = new Transform[cols * rows];
        var restY = new float[cols * rows];
        var mats = new Material[cols * rows];
        int n = 0;
        for (int ix = 0; ix < cols; ix++)
        {
            float x = (ix + 0.5f) * sx - halfW;
            // Cross-section across the width: 1 at the centre, 0 at the side edges. It lifts the
            // centre into a crest and pulls the side columns back (the front bows, it is not a wall).
            // A bowed column also carries its own ripple phase, because the phase reads the cell's z —
            // so the travelling crest line curves to match the front instead of staying a straight bar.
            float xf = halfW > 0.0001f ? x / halfW : 0f;
            float rest = crestLift * (1f - xf * xf);
            float zBow = -frontBow * xf * xf;
            for (int iz = 0; iz < rows; iz++)
            {
                float z = (iz + 0.5f) * sz - depth * 0.5f + zBow;
                var cube = Primitive(PrimitiveType.Cube, "WaveCell_" + n, root);
                cube.localPosition = new Vector3(x, rest, z);
                cube.localScale = new Vector3(cellX, thickness, cellZ);
                Materialize(cube, shader, trough);
                mats[n] = cube.GetComponent<MeshRenderer>().material;
                cells[n] = cube;
                restY[n] = rest;
                n++;
            }
        }

        root.gameObject.AddComponent<WaveRipple>().Configure(
            cells, restY, mats, trough, color, amplitude, speed, wavelength * extent);
        return root;
    }

    /// <summary>1l111: the Storm Lash body (<see cref="ProjectileShape.StormWave"/>) — a ground-hugging
    /// floor lattice carrying a jagged crest of lightning spikes across its width, so the sweep reads as
    /// a lashing arc rather than <see cref="Wave"/>'s water-like sheet. Same contract as
    /// <see cref="Wave"/>: authored footprint × charge <paramref name="extent"/>, thin cubes, every
    /// collider destroyed, and a <see cref="WaveRipple"/> owns the per-cell shimmer — which
    /// <c>SpellWave</c>'s own Update drives on the live sweep and never on a QA pedestal (rule 19).
    ///
    /// <para><b>Colours are the spell's</b> (rule 13): the floor is the school colour darkened by the
    /// same 0.55 lerp <see cref="Wave"/> uses (a darkening, not a hue rotation, so it survives every
    /// school), and the crest is the school colour itself. Nothing here invents a palette.</para>
    ///
    /// <para>The floor's silhouette — centre lifted by <c>crestLift</c>, side columns pulled back by
    /// <c>frontBow</c> — is the same two-term cross-section Wave's floor uses, so both waves read as
    /// one family and a future tweak to the shape has one obvious sibling to keep in step.</para></summary>
    private static Transform StormWave(string name, Shader shader, Color color, float extent = 1f)
    {
        var root = new GameObject(name).transform;

        const int baseCols = 12;
        const int baseRows = 18;
        const int spikeSteps = 3;
        const float baseWidth = 5.2f;
        const float baseDepth = 6f;
        const float thickness = 0.14f;
        const float crestLift = 0.30f;
        const float frontBow = 1.1f;
        const float spikePitch = 0.28f;
        const int cellCap = 1400;

        extent = Mathf.Clamp(extent, 0.05f, 64f);
        int cols = Mathf.Max(1, Mathf.RoundToInt(baseCols * extent));
        int rows = Mathf.Max(1, Mathf.RoundToInt(baseRows * extent));

        // Safety valve, the same shape Wave uses: above the cap the grid thins and the cubes grow, so a
        // pathological charge cannot overflow the cell arithmetic below.
        int total = cols * rows + cols * spikeSteps;
        if (total > cellCap)
        {
            float shrink = Mathf.Sqrt((float)cellCap / total);
            cols = Mathf.Max(1, Mathf.RoundToInt(cols * shrink));
            rows = Mathf.Max(1, Mathf.RoundToInt(rows * shrink));
        }

        float width = baseWidth * extent;
        float depth = baseDepth * extent;
        float sx = width / cols;
        float sz = depth / rows;
        float halfW = width * 0.5f;
        Color trough = Color.Lerp(color, Color.black, 0.55f);

        int floorCells = cols * rows;
        int spikeCells = cols * spikeSteps;
        var cells = new Transform[floorCells + spikeCells];
        var restY = new float[floorCells + spikeCells];
        var mats = new Material[floorCells + spikeCells];
        int n = 0;

        for (int ix = 0; ix < cols; ix++)
        {
            float x = (ix + 0.5f) * sx - halfW;
            float xf = halfW > 0.0001f ? x / halfW : 0f;
            float rest = crestLift * (1f - xf * xf);
            float zBow = -frontBow * xf * xf;
            for (int iz = 0; iz < rows; iz++)
            {
                float z = (iz + 0.5f) * sz - depth * 0.5f + zBow;
                var cube = Primitive(PrimitiveType.Cube, "StormWaveCell_" + n, root);
                cube.localPosition = new Vector3(x, rest, z);
                cube.localScale = new Vector3(sx * 1.01f, thickness, sz * 1.01f);
                Materialize(cube, shader, trough);
                mats[n] = cube.GetComponent<MeshRenderer>().material;
                cells[n] = cube;
                restY[n] = rest;
                n++;
            }
        }

        // The crest: a short zigzag column per floor column, tallest over the centre and tapering to
        // nothing at the side edges, jittered so the leading line reads as a lashing bolt instead of a
        // smooth lip. The spikes join the SAME ripple arrays, so the crest shimmers with the floor
        // rather than standing frozen above it.
        for (int ix = 0; ix < cols; ix++)
        {
            float x = (ix + 0.5f) * sx - halfW;
            float xf = halfW > 0.0001f ? x / halfW : 0f;
            float centre = Mathf.Max(0f, 1f - xf * xf);
            float spike = Mathf.Max(0.22f, centre);
            float baseY = crestLift * centre;
            for (int s = 0; s < spikeSteps; s++)
            {
                float y = baseY + (s + 0.5f) * spikePitch * spike;
                var cube = Primitive(PrimitiveType.Cube, "StormWaveSpike_" + n, root);
                cube.localPosition = new Vector3(x + Random.Range(-0.09f, 0.09f), y,
                    Random.Range(-0.10f, 0.10f));
                cube.localScale = new Vector3(sx * 0.34f, spikePitch * 1.15f, sz * 0.34f);
                Materialize(cube, shader, color);
                mats[n] = cube.GetComponent<MeshRenderer>().material;
                cells[n] = cube;
                restY[n] = y;
                n++;
            }
        }

        // The ripple's wavelength is a fraction of the footprint, exactly as Wave's is, so a charged
        // (longer) wave still carries one crest line rather than several.
        root.gameObject.AddComponent<WaveRipple>().Configure(
            cells, restY, mats, trough, color, 0.14f, 8f, Mathf.Max(1.2f, depth * 0.42f));
        return root;
    }
}

using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The summon family's bodies: the totem, the following familiar's ring (1l6p: a vertical halo of
/// launch orbs, previously a flat ground circle), and (since 1je) the <see cref="SummonedAlly"/>
/// combat construct.
/// <para>The totem and the circle were extracted verbatim from <c>Magic/Cast/SpellSummon.cs</c> in
/// <b>1jd</b>. Before that the geometry lived inside the gameplay component, so "summon model"
/// matched nothing under <c>Models/</c> while the model itself had been shipping the whole time -
/// the discoverability failure 1iz describes. Nothing about the shape, the names, the scale or the
/// colour changed.</para>
/// <para><b>1je widened this class</b>, because the one summon body that had no builder at all was
/// the ally: it was a bare <c>CreatePrimitive(Cube)</c> sitting in the gameplay component, i.e.
/// exactly the discoverability hole again, one layer in. Its shape IS new - see
/// <see cref="BuildAlly"/> for why it hovers - so unlike 1jd this is an addition rather than a
/// move, and the component keeps all of its behaviour (move, face, strike, fade, die).</para>
/// <para><b>1l6p: the following circle is no longer sized by the spell's <c>Radius</c>.</b> It is a
/// fixed authored halo — a translucent vertical ring of launch orbs (see
/// <see cref="BuildFamiliarCircle"/>) — because <c>Radius</c> is display-only for a forward-spraying
/// familiar, and a vertical ring sized by its 5 m value would dwarf the player. The totem below owns
/// no live values of its own and is unchanged.</para>
/// </summary>
public static class SummonModelBuilder
{
    /// <summary>What a built summon hands back: every pulsing orb it owns, and the scale they pulse
    /// around. One record rather than two out-params because <c>SpellSummon.Update</c> writes
    /// <c>orb.localScale = baseScale * pulse</c> every frame — two parallel values read in lockstep
    /// is an alignment an extraction must not be able to violate (rule 17).
    /// <para><b>1l6p: an ARRAY of orbs, not one head.</b> The familiar now carries a ring of them and
    /// fires each bolt from a randomly chosen one, so a single <c>Transform</c> can no longer describe
    /// the body. The totem has exactly one (its head crystal), so the same loop pulses it.</para></summary>
    public struct Body
    {
        public Vector3 CoreBaseScale;
        public Transform[] Cores;
    }

    /// <summary>1l6p: how many launch orbs are arrayed around the familiar's ring. Every shot leaves a
    /// randomly chosen orb (see <c>SpellSummon.Muzzle</c>), so this sets how wide the stream's origin
    /// reads.</summary>
    private const int FamiliarCoreCount = 6;

    /// <summary>1l6p: the ring's authored radius, in metres. The ring is a fixed HALO, not a readout
    /// of the spell's <c>Radius</c> — see the class remarks.</summary>
    private const float FamiliarRingRadius = 0.75f;

    /// <summary>1l6p: the ring's centre height above the summon root, in metres. The root is snapped
    /// to the ground behind the caster, so the ring sits at roughly chest height BEHIND the body rather
    /// than resting on the floor like the flat AoE circle it replaces. Public because the QA spell band
    /// seats the ring on its pedestal through this value rather than re-spelling the height.</summary>
    public const float FamiliarRingHeight = 1.2f;

    /// <summary>1l6p: the launch orb's diameter. Six of these on a 0.75 m rim leave a visible gap
    /// between neighbours.</summary>
    private const float FamiliarCoreSize = 0.3f;

    /// <summary>1l6p: the disc alpha. The ring is a read of its own reach, so it must not hide the
    /// ground or the orbs behind it; the shader pick below prefers <c>Sprites/Default</c>, which
    /// alpha-blends, so a sub-1 alpha actually renders see-through.</summary>
    private const float FamiliarDiscAlpha = 0.35f;

    /// <summary>1ir/1l6p: the following-familiar read — a translucent vertical ring (facing the
    /// caster's forward) carrying <see cref="FamiliarCoreCount"/> launch orbs around its rim, from
    /// which the spray fires. Deliberately NOT SkillFx.RingFlash, which self-destructs and would give
    /// a one-frame flash instead of a persistent shape, and NOT the totem below: a pillar-and-shards
    /// totem that walks behind you reads as a carried statue, not as a portal behind you.
    /// <para><b>1l6p: fixed size, not the spell's radius.</b> The ring is authored small
    /// (<see cref="FamiliarRingRadius"/>) and centred at chest height behind the caster
    /// (<see cref="FamiliarRingHeight"/>); the spell's display-only <c>Radius</c> no longer has a
    /// drawn form here. The disc and every orb are children of ONE ring transform, so the drawn ring
    /// and the launch points cannot drift into two planes (rule 8: one derivation, not two).</para></summary>
    public static Body BuildFamiliarCircle(Transform parent, Color color)
    {
        Shader shader = Shader.Find("Sprites/Default") ?? Shader.Find("Unlit/Color");
        if (shader == null) return default(Body);

        var ring = new GameObject("FollowCircle").transform;
        ring.SetParent(parent, false);
        ring.localPosition = new Vector3(0f, FamiliarRingHeight, 0f);
        // 1l6p: X=90 (Y=0) lays the disc's round face in the ring's XY plane, i.e. a vertical hoop
        // facing the caster's forward — the "circle behind the player" read. A Cylinder's faces are
        // perpendicular to its local Y, so a Z-roll would stand it edge-on and a Y-spin would leave it
        // flat on the ground; the X-roll is the one that faces it forward.
        ring.localRotation = Quaternion.Euler(90f, 0f, 0f);

        var disc = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        disc.name = "FollowDisc";
        DestroyCollider(disc.transform);
        disc.transform.SetParent(ring, false);
        disc.transform.localScale = new Vector3(FamiliarRingRadius * 2f, 0.05f, FamiliarRingRadius * 2f);
        SetMaterial(disc.transform, shader, new Color(color.r, color.g, color.b, FamiliarDiscAlpha));

        var cores = new Transform[FamiliarCoreCount];
        for (int i = 0; i < FamiliarCoreCount; i++)
        {
            float ang = i * (2f * Mathf.PI / FamiliarCoreCount);
            var core = GameObject.CreatePrimitive(PrimitiveType.Sphere).transform;
            core.name = "FollowCore_" + i;
            DestroyCollider(core);
            core.SetParent(ring, false);
            core.localPosition = new Vector3(Mathf.Cos(ang) * FamiliarRingRadius, 0f,
                Mathf.Sin(ang) * FamiliarRingRadius);
            core.localScale = Vector3.one * FamiliarCoreSize;
            SetMaterial(core, shader, color);
            cores[i] = core;
        }

        return new Body { CoreBaseScale = Vector3.one * FamiliarCoreSize, Cores = cores };
    }

    /// <summary>The standing summon: a base disc, a pillar, a head, and three orbiting shards.</summary>
    public static Body BuildTotem(Transform parent, Color color)
    {
        Shader shader = Shader.Find("Sprites/Default") ?? Shader.Find("Unlit/Color");
        if (shader == null) return default(Body);

        var baseDisc = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        baseDisc.name = "SummonBase";
        DestroyCollider(baseDisc.transform);
        baseDisc.transform.SetParent(parent, false);
        baseDisc.transform.localPosition = new Vector3(0f, 0.08f, 0f);
        baseDisc.transform.localScale = new Vector3(0.9f, 0.07f, 0.9f);
        SetMaterial(baseDisc.transform, shader, color);

        var pillar = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        pillar.name = "SummonPillar";
        DestroyCollider(pillar.transform);
        pillar.transform.SetParent(parent, false);
        pillar.transform.localPosition = new Vector3(0f, 1f, 0f);
        pillar.transform.localScale = new Vector3(0.55f, 0.95f, 0.55f);
        SetMaterial(pillar.transform, shader, color);

        Transform head = GameObject.CreatePrimitive(PrimitiveType.Sphere).transform;
        head.name = "SummonHead";
        DestroyCollider(head);
        head.SetParent(parent, false);
        head.localPosition = new Vector3(0f, 2.1f, 0f);
        head.localScale = Vector3.one * 0.5f;
        SetMaterial(head, shader, color);

        for (int i = 0; i < 3; i++)
        {
            var shard = GameObject.CreatePrimitive(PrimitiveType.Cube);
            shard.name = "SummonOrbit_" + i;
            DestroyCollider(shard.transform);
            shard.transform.SetParent(parent, false);
            float ang = i * 120f;
            Vector2 c = new Vector2(Mathf.Cos(ang * Mathf.Deg2Rad), Mathf.Sin(ang * Mathf.Deg2Rad));
            shard.transform.localPosition = new Vector3(c.x * 0.75f, 1.2f, c.y * 0.75f);
            shard.transform.localScale = Vector3.one * 0.18f;
            SetMaterial(shard.transform, shader, color);
        }

        return new Body { CoreBaseScale = head.localScale, Cores = new[] { head } };
    }

    /// <summary>1l110: Static Coil's body — a stationary spire. A base disc under a central rod
    /// ringed by a tapering stack of coil turns, capped by one bright emitter orb from which the
    /// turret's bolts leave. Unlike <see cref="BuildTotem"/> it has no orbiting shards (nothing here
    /// moves) and unlike <see cref="BuildFamiliarCircle"/> it does not follow — it is bolted to the
    /// ground, so the read is "a device you deploy" rather than "a spirit that walks with you".
    /// <para>The emitter is the single pulsing core (the same <c>CoreBaseScale * pulse</c> contract
    /// every summon body honours; see <see cref="Body"/>). Reached only through an authored
    /// <c>SummonBody.TeslaCoil</c> (rule 13: one resolver, one drawn body).</para></summary>
    public static Body BuildTeslaCoil(Transform parent, Color color)
    {
        Shader shader = Shader.Find("Sprites/Default") ?? Shader.Find("Unlit/Color");
        if (shader == null) return default(Body);

        var baseDisc = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        baseDisc.name = "CoilBase";
        DestroyCollider(baseDisc.transform);
        baseDisc.transform.SetParent(parent, false);
        baseDisc.transform.localPosition = new Vector3(0f, 0.06f, 0f);
        baseDisc.transform.localScale = new Vector3(0.85f, 0.06f, 0.85f);
        SetMaterial(baseDisc.transform, shader, color);

        // Central rod the coil is wound around. Thin in XZ, tall in Y, so the rings read as turns
        // around a core rather than as a solid stack.
        var rod = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        rod.name = "CoilRod";
        DestroyCollider(rod.transform);
        rod.transform.SetParent(parent, false);
        rod.transform.localPosition = new Vector3(0f, 0.85f, 0f);
        rod.transform.localScale = new Vector3(0.14f, 0.85f, 0.14f);
        SetMaterial(rod.transform, shader, new Color(color.r * 0.6f, color.g * 0.6f, color.b * 0.6f, color.a));

        // Tapering coil turns: flat discs of decreasing radius climbing the rod, each a hair narrower
        // than the last, so the silhouette narrows toward the emitter like a real coil winding.
        const int turns = 4;
        for (int i = 0; i < turns; i++)
        {
            float t = i / (float)(turns - 1);
            var ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            ring.name = "CoilTurn" + i;
            DestroyCollider(ring.transform);
            ring.transform.SetParent(parent, false);
            ring.transform.localPosition = new Vector3(0f, 0.35f + i * 0.28f, 0f);
            float r = Mathf.Lerp(0.46f, 0.26f, t);
            ring.transform.localScale = new Vector3(r, 0.05f, r);
            SetMaterial(ring.transform, shader, color);
        }

        // Emitter: the bright cap and the pulsing core every summon body exposes.
        Transform emitter = GameObject.CreatePrimitive(PrimitiveType.Sphere).transform;
        emitter.name = "CoilEmitter";
        DestroyCollider(emitter);
        emitter.SetParent(parent, false);
        emitter.localPosition = new Vector3(0f, 1.55f, 0f);
        emitter.localScale = Vector3.one * 0.4f;
        SetMaterial(emitter, shader, Color.Lerp(color, Color.white, 0.4f));

        return new Body { CoreBaseScale = emitter.localScale, Cores = new[] { emitter } };
    }

    /// <summary>What <see cref="BuildAlly"/> hands back: every renderer it created, so the component
    /// can fade the whole body out and not just one limb. A single-renderer ally was fine while the
    /// body was one cube; six parts with one faded limb reads as a bug, so the handle is the whole set
    /// and the fade loop is the caller's (rule 13: the *lifetime* of an effect is behaviour).</summary>
    public struct AllyBody
    {
        public Transform Root;
        public Renderer[] Renderers;
    }

    /// <summary>The combat ally (<see cref="SummonedAlly"/>) - a levitating construct.
    /// <para><b>Why it hovers rather than walks.</b> The component moves with
    /// <c>MoveToward</c> + <c>Face</c> and has no animator, no rig and no walk cycle, so a bipedal
    /// skeleton would slide across the ground looking broken. A hovering body reads correctly from
    /// every angle while translating and rotating, which is all the component can actually do. If it
    /// ever gets an animator, the parts below are already named per-limb and can be re-parented.</para>
    /// <para><b>Two colours from one input, derived here.</b> <paramref name="body"/> is the colour the
    /// component already used; the ground ring and the front core are derived from it (darker / brighter
    /// in RGB, alpha untouched) so a caller still passes exactly one colour and cannot get a third
    /// spelling of this summon somewhere else.</para>
    /// <para>Authored in cube units with the head top at <b>2.06</b> (a Unity capsule is 2 units tall
    /// before scaling, so the shell spans 0.5-1.6 and the head sphere caps it), which under the
    /// component's existing <c>localScale = Vector3.one * 0.8f</c> lands the ally at <b>1.65 m</b> -
    /// human-adjacent, and the component's scale line is untouched by this task.</para>
    /// <para>The body sits ABOVE the root: the root is placed at <c>owner.position + right * 1.2f</c>,
    /// i.e. the player's feet, so the ground ring lands on the terrain while the shell floats. The
    /// cube this replaces was centred on the root and therefore half-buried.</para>
    /// <para><b>No colliders on any part</b>, matching the single cube it replaces, which had its own
    /// collider destroyed at spawn. Adding hitboxes here would silently make the ally targetable by
    /// enemies for the first time, which is a gameplay change, not a model.</para>
    /// <para><b>Known, pre-existing, newly visible:</b> the component has no ground snap - it
    /// <c>MoveTowards</c>es toward its target in a straight line - so on sloped ground the RING
    /// will float or sink. The half-buried cube hid this; a ground-contact part does not. Left
    /// alone (fixing it is a movement change), recorded in PROGRESS 1je.</para></summary>
    public static AllyBody BuildAlly(Transform parent, Color body)
    {
        var result = default(AllyBody);
        Shader shader = Shader.Find("Sprites/Default") ?? Shader.Find("Unlit/Color");
        if (shader == null) return result;

        Color trim = new Color(body.r * 0.55f, body.g * 0.55f, body.b * 0.55f, body.a);
        Color core = new Color(Mathf.Min(body.r * 1.15f, 1f), Mathf.Min(body.g * 1.15f, 1f),
            Mathf.Min(body.b * 1.15f, 1f), 1f);

        var parts = new List<Renderer>(6);

        // Ground ring: gives the hovering body a contact read, and is the part that tells you it is
        // alive rather than stuck in the terrain.
        var ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        ring.name = "AllyRing";
        DestroyCollider(ring.transform);
        ring.transform.SetParent(parent, false);
        ring.transform.localPosition = new Vector3(0f, 0.04f, 0f);
        ring.transform.localScale = new Vector3(0.9f, 0.04f, 0.9f);
        parts.Add(SetMaterial(ring.transform, shader, trim));

        // Main shell: a capsule, because a cube at this size reads as a crate no matter how it is lit.
        var shell = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        shell.name = "AllyShell";
        DestroyCollider(shell.transform);
        shell.transform.SetParent(parent, false);
        shell.transform.localPosition = new Vector3(0f, 1.05f, 0f);
        shell.transform.localScale = new Vector3(0.62f, 0.55f, 0.62f);
        parts.Add(SetMaterial(shell.transform, shader, body));

        // Head
        var head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        head.name = "AllyHead";
        DestroyCollider(head.transform);
        head.transform.SetParent(parent, false);
        head.transform.localPosition = new Vector3(0f, 1.85f, 0f);
        head.transform.localScale = Vector3.one * 0.42f;
        parts.Add(SetMaterial(head.transform, shader, body));

        // Front core: the "face", and the part that reads brightest when the ally turns toward you.
        var orb = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        orb.name = "AllyCore";
        DestroyCollider(orb.transform);
        orb.transform.SetParent(parent, false);
        orb.transform.localPosition = new Vector3(0f, 1.2f, 0.34f);
        orb.transform.localScale = Vector3.one * 0.24f;
        parts.Add(SetMaterial(orb.transform, shader, core));

        // Two shoulder pods, flattened and splayed. Sized so they clear the shell's 0.31 radius.
        for (int i = 0; i < 2; i++)
        {
            float side = i == 0 ? -1f : 1f;
            var pod = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            pod.name = "AllyPod" + (i == 0 ? "L" : "R");
            DestroyCollider(pod.transform);
            pod.transform.SetParent(parent, false);
            pod.transform.localPosition = new Vector3(side * 0.52f, 1.28f, 0f);
            pod.transform.localRotation = Quaternion.Euler(0f, 0f, side * 22f);
            pod.transform.localScale = new Vector3(0.26f, 0.42f, 0.26f);
            parts.Add(SetMaterial(pod.transform, shader, trim));
        }

        result.Root = parent;
        result.Renderers = parts.ToArray();
        return result;
    }

    private static void DestroyCollider(Transform t)
    {
        Collider col = t.GetComponent<Collider>();
        if (col != null) Object.Destroy(col);
    }

    private static Renderer SetMaterial(Transform t, Shader shader, Color color)
    {
        var r = t.GetComponent<MeshRenderer>();
        if (r != null) r.material = new Material(shader) { color = color };
        return r;
    }
}
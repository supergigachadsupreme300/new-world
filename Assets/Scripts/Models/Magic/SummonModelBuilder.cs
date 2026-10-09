using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The summon family's bodies: the totem, the following familiar's ground circle, and (since 1je)
/// the <see cref="SummonedAlly"/> combat construct.
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
/// <para><b>Everything else is still driven by <c>SpellSummon</c>'s live values</b>, deliberately:
/// the circle is sized from the same <c>Radius</c> the targeting code scans, so a future balance
/// change cannot move the drawn circle without moving the area it claims.</para>
/// </summary>
public static class SummonModelBuilder
{
    /// <summary>What a built summon hands back: the pulsing head, and the scale it pulses around.
    /// One record rather than two out-params because <c>SpellSummon.Update</c> writes
    /// <c>_head.localScale = _headBaseScale * pulse</c> every frame — two parallel values read in
    /// lockstep is an alignment an extraction must not be able to violate (rule 17).</summary>
    public struct Body
    {
        public Transform Head;
        public Vector3 HeadBaseScale;
    }

    /// <summary>1ir: the following-familiar read — a flat ground circle the size of the real
    /// targeting radius, plus a low orb to fire from. Deliberately NOT SkillFx.RingFlash, which
    /// self-destructs and would give a one-frame flash instead of a persistent circle, and NOT the
    /// totem below: a pillar-and-shards totem that walks behind you reads as a carried statue, not
    /// as an area you are standing in. Radius is the live targeting value, so the drawn circle is
    /// exactly the area FireAtNearest/NearestEnemy actually scan.</summary>
    public static Body BuildFamiliarCircle(Transform parent, float radius, Color color)
    {
        Shader shader = Shader.Find("Sprites/Default") ?? Shader.Find("Unlit/Color");
        if (shader == null) return default(Body);

        var disc = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        disc.name = "FollowCircle";
        DestroyCollider(disc.transform);
        disc.transform.SetParent(parent, false);
        disc.transform.localPosition = new Vector3(0f, 0.05f, 0f);
        disc.transform.localScale = new Vector3(radius * 2f, 0.05f, radius * 2f);
        disc.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
        SetMaterial(disc.transform, shader, color);

        Transform head = GameObject.CreatePrimitive(PrimitiveType.Sphere).transform;
        head.name = "FollowCore";
        DestroyCollider(head);
        head.SetParent(parent, false);
        head.localPosition = new Vector3(0f, 0.9f, 0f);
        head.localScale = Vector3.one * 0.6f;
        SetMaterial(head, shader, color);

        return new Body { Head = head, HeadBaseScale = head.localScale };
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

        return new Body { Head = head, HeadBaseScale = head.localScale };
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
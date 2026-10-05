using UnityEngine;

/// <summary>
/// The summon construct's body — the totem and the following familiar's ground circle.
/// <para>Extracted verbatim from <c>Magic/Cast/SpellSummon.cs</c> in <b>1jd</b>. Before this the
/// geometry lived inside the gameplay component, so "summon model" matched nothing under
/// <c>Models/</c> while the model itself had been shipping the whole time — the discoverability
/// failure 1iz describes. Nothing about the shape, the names, the scale or the colour changed.</para>
/// <para><b>Everything is still driven by <c>SpellSummon</c>'s live values</b>, deliberately: the
/// circle is sized from the same <c>Radius</c> the targeting code scans, so a future balance change
/// cannot move the drawn circle without moving the area it claims.</para>
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

    private static void DestroyCollider(Transform t)
    {
        Collider col = t.GetComponent<Collider>();
        if (col != null) Object.Destroy(col);
    }

    private static void SetMaterial(Transform t, Shader shader, Color color)
    {
        var r = t.GetComponent<MeshRenderer>();
        if (r != null) r.material = new Material(shader) { color = color };
    }
}
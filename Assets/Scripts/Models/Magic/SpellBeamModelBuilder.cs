using UnityEngine;

/// <summary>
/// The beam's drawn body — the straight channel beam and the swept cone's funnel.
/// <para>Extracted verbatim from <c>Magic/Cast/SpellBeam.cs</c> in <b>1jd</b>: "beam model" matched
/// nothing under <c>Models/</c> while the model shipped (1iz's failure). Shapes, names, colours,
/// counts and the local-frame convention are unchanged.</para>
/// <para><b>The hitbox never came from here.</b> <c>SpellBeam.TickCone</c> walks
/// <c>ConeRays * ConeSegments</c> analytic capsules; every collider on every piece below is
/// destroyed and no piece is ever queried. That independence is why the visual was movable.</para>
/// </summary>
public static class SpellBeamModelBuilder
{
    /// <summary>The straight beam's cylinder, with the base scale the pulse drives it from.
    /// One record rather than two out-values because <c>SpellBeam</c> writes
    /// <c>_body.localScale = _bodyBaseScale * pulse</c> on every frame — parallel values read in
    /// lockstep are an alignment an extraction must not be able to violate (rule 17).</summary>
    public struct LineBody
    {
        public Transform Body;
        public Vector3 BodyBaseScale;
    }

    /// <summary>The channel beam's line: a cylinder from the muzzle to the tip.
    /// Placed in WORLD space (<c>position</c>/<c>rotation</c>, as before) because the beam's axis
    /// comes from the caster's aim, not from the parent's local frame.</summary>
    public static LineBody BuildLineBody(Transform parent, Vector3 origin, Vector3 direction,
        float length, float width, Color color)
    {
        Shader shader = Shader.Find("Sprites/Default") ?? Shader.Find("Unlit/Color");
        var result = default(LineBody);
        if (shader == null) return result;

        Vector3 mid = origin + direction * (length * 0.5f);
        Quaternion rot = Quaternion.FromToRotation(Vector3.up, direction);

        Transform body = GameObject.CreatePrimitive(PrimitiveType.Cylinder).transform;
        body.name = "BeamBody";
        DestroyCollider(body);
        body.SetParent(parent, false);
        body.position = mid;
        body.rotation = rot;
        body.localScale = new Vector3(width * 2f, length * 0.5f, width * 2f);
        SetMaterial(body, shader, color);

        result.Body = body;
        result.BodyBaseScale = body.localScale;
        return result;
    }

    /// <summary>The shared tip orb, with its base scale.
    /// <para><b>Shared by BOTH beams on purpose.</b> <c>SpellBeam.PulseVisual</c> opens with
    /// <c>if (_endOrb == null) return;</c> and runs for the cone as well as the line, so folding this
    /// into the line builder alone would have left the cone's tip unpulsed and invisible to the
    /// pulse — the kind of asymmetry a move introduces silently. It is its own entry point here for
    /// exactly that reason.</para></summary>
    public static TipOrb BuildTipOrb(Transform parent, Vector3 endPoint, float width, Color color)
    {
        Shader shader = Shader.Find("Sprites/Default") ?? Shader.Find("Unlit/Color");
        var result = default(TipOrb);
        if (shader == null) return result;

        Transform orb = GameObject.CreatePrimitive(PrimitiveType.Sphere).transform;
        orb.name = "BeamEnd";
        DestroyCollider(orb);
        orb.SetParent(parent, false);
        orb.position = endPoint;
        orb.localScale = Vector3.one * Mathf.Max(width * 1.6f, 0.3f);
        SetMaterial(orb, shader, color);

        result.Orb = orb;
        result.BaseScale = orb.localScale;
        return result;
    }

    /// <summary>The orb plus the scale it pulses around.</summary>
    public struct TipOrb
    {
        public Transform Orb;
        public Vector3 BaseScale;
    }

    /// <summary>1is: build the cone's drawn body as the Great Tornado's silhouette lying along the beam
    /// axis: <see cref="SpellBeam.FunnelChunks"/> chunky discs stacked muzzle-to-tip, each randomly
    /// yawed so the stack reads as a twisting funnel, opening from <paramref name="mouthRadius"/> at the
    /// muzzle to <paramref name="tipRadius"/> at the far end, plus
    /// <see cref="SpellBeam.FunnelDebris"/> orbiting chunks and a leading ring. Returns the chunk
    /// transforms in build order so the live pulse can scale them radially without re-deriving the taper.
    ///
    /// <para><b>Return-order contract.</b> Indexes <c>0 .. FunnelChunks-1</c> are the stacked discs;
    /// <c>FunnelChunks .. FunnelChunks + FunnelDebris - 1</c> are the debris. <c>SpellBeam.PulseVisual</c>
    /// reads that layout with hard index arithmetic, so the two sides must agree — stated here because a
    /// builder that reorders its own output would fade one set of pieces and pulse the other.</para>
    ///
    /// <para><b>Deliberately NOT <c>MapBuilder.BuildTornado</c>.</b> That model adds
    /// <c>TornadoBehavior</c>, which applies real physics pull to rigidbodies (props, livestock — and
    /// the caster). A channeled beam you hold for its whole Focus cost would drag the player around with
    /// it. This reproduces the SILHOUETTE — stacked, yawed, widening — and nothing else.</para>
    ///
    /// <para><b>Two-tone, and the colours are the spell's.</b> <paramref name="core"/> is the body and
    /// <paramref name="hot"/> the leading/muzzle end, so a flame is born hot and cools as it travels.
    /// Both are passed in by the caller from SpellLook; nothing here invents a colour, per rule 13.</para>
    ///
    /// <para>Placed in the parent's LOCAL frame (+Z forward), which is what lets the QA bench mount
    /// this on a pedestal and the live beam on its own transform under one coordinate convention.
    /// Public and static on purpose: the bench mounts THIS rather than a proxy that could drift from
    /// what ships (the 1f7 <c>BuildRockBody</c> / 1ij acceptance-readout precedent). Moved here from
    /// the beam component in 1jd — the bench call site moved with it, so the one spelling of the
    /// funnel is this file's, not a private copy the bench could drift from.</para></summary>
    public static Transform[] BuildFunnelVisual(Transform parent, float length, float mouthRadius,
        float tipRadius, Color core, Color hot)
    {
        Shader shader = Shader.Find("Sprites/Default") ?? Shader.Find("Unlit/Color");
        if (shader == null) return new Transform[0];

        int total = SpellBeam.FunnelChunks + SpellBeam.FunnelDebris;
        var chunks = new Transform[total];
        float chunkLen = length / SpellBeam.FunnelChunks;
        // Deterministic yaw from the index, NOT Random: the funnel must rebuild identically on the
        // live beam and on the bench, or the QA readout would be a picture of a different spell.
        for (int i = 0; i < SpellBeam.FunnelChunks; i++)
        {
            float t = (i + 1) / (float)SpellBeam.FunnelChunks;
            float rad = Mathf.Lerp(mouthRadius, tipRadius, t);
            float mid = (i + 0.5f) * chunkLen;

            Transform seg = GameObject.CreatePrimitive(PrimitiveType.Cylinder).transform;
            seg.name = "FunnelChunk_" + i;
            seg.SetParent(parent, false);
            seg.localPosition = new Vector3(0f, 0f, mid);
            // A cylinder's axis is +Y; the funnel's axis is +Z. Lay it over first, then yaw about the
            // beam axis so the disc spins in its own plane without tilting off the beam line.
            seg.localRotation = Quaternion.Euler(0f, YawFor(i), 90f);
            // 0.55 keeps the discs overlapping into one continuous funnel rather than reading as a
            // row of separate plates; without it the gaps are visible at the mouth.
            seg.localScale = new Vector3(rad * 2f, chunkLen * 0.55f, rad * 2f);
            DestroyCollider(seg);
            // Hot at the muzzle, cooling downstream: t=0 is yellow, t=1 is the spell's own colour.
            SetMaterial(seg, shader, Color.Lerp(hot, core, Mathf.Clamp01(t * 0.85f)));
            chunks[i] = seg;
        }

        for (int d = 0; d < SpellBeam.FunnelDebris; d++)
        {
            float t = (d + 1) / (float)(SpellBeam.FunnelDebris + 1);
            float rad = Mathf.Lerp(mouthRadius, tipRadius, t) * 1.25f;
            float ang = YawFor(100 + d) + d * 120f;
            float s = Mathf.Max(rad * 0.34f, 0.08f);

            Transform chunk = GameObject.CreatePrimitive(PrimitiveType.Cube).transform;
            chunk.name = "FunnelDebris_" + d;
            chunk.SetParent(parent, false);
            chunk.localPosition = new Vector3(
                Mathf.Cos(ang * Mathf.Deg2Rad) * rad, Mathf.Sin(ang * Mathf.Deg2Rad) * rad, t * length);
            chunk.localRotation = Quaternion.Euler(ang * 0.7f, ang, ang * 0.4f);
            chunk.localScale = new Vector3(s, s, s);
            DestroyCollider(chunk);
            SetMaterial(chunk, shader, core);
            chunks[SpellBeam.FunnelChunks + d] = chunk;
        }

        // Leading ring so the far end reads as an opening rather than a flat cut.
        Transform ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder).transform;
        ring.name = "FunnelMouth";
        ring.SetParent(parent, false);
        ring.localPosition = new Vector3(0f, 0f, length);
        ring.localRotation = Quaternion.Euler(0f, 0f, 90f);
        ring.localScale = new Vector3(tipRadius * 2.1f, 0.02f, tipRadius * 2.1f);
        DestroyCollider(ring);
        SetMaterial(ring, shader, core);

        return chunks;
    }

    /// <summary>1is: deterministic per-index yaw, so the funnel's twist is identical every rebuild
    /// (live beam and bench alike). A <c>Random</c> draw here would make the QA model a picture of a
    /// slightly different spell than the one that ships.</summary>
    public static float YawFor(int seed) => Mathf.Repeat(seed * 137.508f, 360f);

    private static void DestroyCollider(Transform t)
    {
        Collider col = t.GetComponent<Collider>();
        if (col != null) Object.Destroy(col);
    }

    private static Material SetMaterial(Transform t, Shader shader, Color color)
    {
        var mat = new Material(shader) { color = color };
        var r = t.GetComponent<MeshRenderer>();
        if (r != null) r.material = mat;
        return mat;
    }
}
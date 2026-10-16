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
    /// <summary>The straight beam's cylinder, with the base scale <c>SpellBeam</c> holds it at.
    /// One record rather than two out-values because <c>SpellBeam</c> writes
    /// <c>_body.localScale = _bodyBaseScale</c> every frame — since 1kb the body no longer breathes
    /// (the shrink-and-enlarge pulse is gone; the scale just holds), and parallel out-values read in
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
    /// axis: <see cref="SpellBeam.FunnelChunks"/> chunky discs stacked muzzle-to-tip, each yawed so the
    /// stack reads as a twisting funnel, opening from <paramref name="mouthRadius"/> at the
    /// muzzle to <paramref name="tipRadius"/> at the far end, plus
    /// <see cref="SpellBeam.FunnelDebris"/> orbiting chunks. Returns the pieces in
    /// build order because <see cref="SpellBeam.PulseVisual"/> addresses them by hard index (indexes
    /// <c>0 .. FunnelChunks-1</c> are the discs; <c>FunnelChunks .. FunnelChunks + FunnelDebris - 1</c>
    /// are the debris), so the two sides must agree — a builder that reordered its own output would
    /// move one set of pieces and orbit the other.
    ///
    /// <para><b>1l98: the stack is no longer static.</b> The pieces are positioned by
    /// <see cref="FlowFunnelVisual"/> at phase 0 here and re-positioned every frame on the live beam,
    /// so the model is a moving outward spray — puffs born at the muzzle, growing as they travel, then
    /// fading at the tip — instead of a fixed taper that only changed scale in place. The layout is ONE
    /// function, so the still this builder hands the bench and the live cast cannot drift (rule 8); the
    /// per-frame call lives on the beam, not here (rule 19). Before 1l98 the live side neither scaled
    /// nor re-derived the pieces (1kb/1l85: the body HELD its build scale).</para>
    ///
    /// <para><b>Deliberately NOT <c>MapBuilder.BuildTornado</c>.</b> That model adds
    /// <c>TornadoBehavior</c>, which applies real physics pull to rigidbodies (props, livestock — and
    /// the caster). A channeled beam you hold for its whole Focus cost would drag the player around with
    /// it. This reproduces the SILHOUETTE — a stack of widening, yawed pieces — and nothing else.</para>
    ///
    /// <para><b>Two-tone, and the colours are the spell's.</b> <paramref name="core"/> is the body and
    /// <paramref name="hot"/> the muzzle end, so a flame is born hot and cools as it travels.
    /// Both are passed in by the caller from SpellLook; nothing here invents a colour, per rule 13.</para>
    ///
    /// <para>Placed in the parent's LOCAL frame (+Z forward), which is what lets the QA bench mount
    /// this on a pedestal and the live beam on its own transform under one coordinate convention.
    /// Public and static on purpose: the bench mounts THIS rather than a proxy that could drift from
    /// what ships (the 1f7 <c>BuildRockBody</c> / 1ij acceptance-readout precedent). Moved here from
    /// the beam component in 1jd — the bench call site moved with it, so the one spelling of the
    /// funnel is this file's, not a private copy the bench could drift from.</para></summary>
    public static FunnelChunk[] BuildFunnelVisual(Transform parent, float length, float mouthRadius,
        float tipRadius, Color core, Color hot)
    {
        Shader shader = Shader.Find("Sprites/Default") ?? Shader.Find("Unlit/Color");
        if (shader == null) return new FunnelChunk[0];

        int total = SpellBeam.FunnelChunks + SpellBeam.FunnelDebris;
        var chunks = new FunnelChunk[total];
        for (int i = 0; i < SpellBeam.FunnelChunks; i++)
        {
            // A cylinder's axis is +Y; the funnel's axis is +Z. FlowFunnelVisual lays each disc over
            // and yaws it, so the shape is spelled once (rule 8) for the live beam and the bench.
            Transform seg = GameObject.CreatePrimitive(PrimitiveType.Cylinder).transform;
            seg.name = "FunnelChunk_" + i;
            seg.SetParent(parent, false);
            DestroyCollider(seg);
            chunks[i] = new FunnelChunk { Tr = seg, Mat = SetMaterial(seg, shader, core) };
        }

        for (int d = 0; d < SpellBeam.FunnelDebris; d++)
        {
            Transform chunk = GameObject.CreatePrimitive(PrimitiveType.Cube).transform;
            chunk.name = "FunnelDebris_" + d;
            chunk.SetParent(parent, false);
            DestroyCollider(chunk);
            chunks[SpellBeam.FunnelChunks + d] = new FunnelChunk { Tr = chunk, Mat = SetMaterial(chunk, shader, core) };
        }

        // 1l103: the "leading ring" is gone. It was a SOLID opaque `CreatePrimitive(Cylinder)` disc at
        // z = length, perpendicular to the beam and NOT touched by the 1l98 flow — so once the stream's
        // pieces learned to fade at the tip, this static cap was the one opaque leftover and read as a
        // block capping the funnel end (the user's report). Its intent ("so the far end reads as an
        // opening rather than a flat cut") is already served by the tip fade: pieces now die at the far
        // end, so there is no flat cut to cover. Nothing else referenced the piece — `FlowFunnelVisual`
        // never placed it and the beam's piece arrays are discs + debris only.

        // 1l98: lay the stack out at flow phase 0, so what the builder hands back (and what the QA
        // bench mounts) is one readable still of the spray. The live beam advances THIS layout each
        // frame through the same function, so the still and the cast cannot drift (rule 8).
        // 1l99: a null axis array means every piece lies along the parent's +Z — the still is the
        // cast with a straight aim, which is the bench's case.
        FlowFunnelVisual(chunks, length, mouthRadius, tipRadius, core, hot, 0f, null);

        return chunks;
    }

    /// <summary>1is: deterministic per-index yaw, so the funnel's twist is identical every rebuild
    /// (live beam and bench alike). A <c>Random</c> draw here would make the QA model a picture of a
    /// slightly different spell than the one that ships.</summary>
    public static float YawFor(int seed) => Mathf.Repeat(seed * 137.508f, 360f);

    /// <summary>1l98: one drawn funnel piece — its transform and its own material TOGETHER, so the
    /// beam's flow loop repositions and re-tints in one indexed walk instead of two parallel arrays a
    /// caller in another class must keep in lockstep (rule 17). The material is written per frame (its
    /// colour follows the piece's travel), so it must travel with the piece, not sit in a side list.</summary>
    public struct FunnelChunk
    {
        public Transform Tr;
        public Material Mat;
    }

    /// <summary>1l98: place the funnel's pieces for a given flow position — PURE geometry, it creates
    /// and destroys nothing. <paramref name="cycles"/> is the spray's flow position (whole = full
    /// muzzle-to-tip passes); each piece gets a staggered phase, so the stack tiles the whole run at
    /// every instant and reads as ONE continuous outward spray:
    /// <list type="bullet">
    /// <item>a puff is BORN at the muzzle (<c>p=0</c>: small, moving outward),</item>
    /// <item>GROWS as it travels (radius lerps mouth&#8594;tip with <c>p</c> — the same ladder the
    /// analytic hit cone in <see cref="SpellBeam.TickCone"/> walks, so the drawn flame stays inside
    /// the cone that actually damages),</item>
    /// <item>cools from <paramref name="hot"/> toward <paramref name="core"/> with its own age,</item>
    /// <item>then FADES OUT at the tip and re-emerges at the root (see <see cref="FadeAlpha"/>).</item>
    /// </list>
    /// <para><b>Called from two places and they are the same shape.</b> <see cref="BuildFunnelVisual"/>
    /// calls it ONCE at phase 0 to lay out the still the QA bench mounts; <see cref="SpellBeam.PulseVisual"/>
    /// calls it PER FRAME while the beam is alive. One function, one spelling, so the pedestal still and
    /// the live cast cannot drift (rule 8). The per-frame call lives in the beam's loop — the only
    /// caller that MOVES — not in a builder (rule 19): this is a placement function, not an emitter, it
    /// holds no state and mounts nothing.</para>
    ///
    /// <para><b>1l99: each piece carries its OWN axis.</b> <paramref name="axes"/>, when supplied, is one
    /// launch direction per piece in the PARENT's local frame; a null or short array falls back to the
    /// parent's +Z (which is what <see cref="BuildFunnelVisual"/>'s still passes). The live beam fills it
    /// with the per-piece launch directions it captured (see <see cref="SpellBeam.PulseVisual"/>), so a
    /// puff already in flight keeps the direction it was born with while a turning player only re-aims
    /// the pieces born afterwards — the drawing follows the same rule the hit cone does not (the cone
    /// always walks the LIVE <c>Direction</c>; the pieces are cosmetic).</para></summary>
    public static void FlowFunnelVisual(FunnelChunk[] chunks, float length, float mouthRadius,
        float tipRadius, Color core, Color hot, float cycles, Vector3[] axes)
    {
        if (chunks == null) return;
        float chunkLen = length / SpellBeam.FunnelChunks;

        for (int i = 0; i < SpellBeam.FunnelChunks; i++)
        {
            if (i >= chunks.Length) break;
            Transform seg = chunks[i].Tr;
            if (seg == null) continue;

            Vector3 axis = AxisFor(axes, i);
            float p = Mathf.Repeat(PuffRawPhase(i, cycles), 1f);
            float rad = Mathf.Lerp(mouthRadius, tipRadius, p);
            seg.localPosition = axis * (p * length);
            // A cylinder's axis is +Y; the funnel's axis is the piece's own `axis`. LookRotation maps
            // +Z onto that axis (identity when the axis is +Z, which is why the bench still is
            // unchanged), then the disc is laid over (90 about Z) and yawed about the new axis and keeps
            // spinning as it travels so the stream reads as a twisting flame.
            seg.localRotation = Quaternion.LookRotation(axis) * Quaternion.Euler(0f, YawFor(i) + p * 220f, 90f);
            // 0.55 keeps the discs overlapping (a cylinder primitive is 2 tall, so the overlap is
            // 1.1 * chunkLen against a chunkLen spacing) into one continuous funnel, not a row of plates.
            seg.localScale = new Vector3(rad * 2f, chunkLen * 0.55f, rad * 2f);
            // Hot at birth, cooling downstream by the puff's own age: p=0 is yellow, p=1 is the spell's
            // own colour. The colour is the spell's (rule 13) — nothing here invents one.
            SetFade(chunks[i].Mat, Color.Lerp(hot, core, Mathf.Clamp01(p * 0.85f)), FadeAlpha(p));
        }

        for (int d = 0; d < SpellBeam.FunnelDebris; d++)
        {
            int idx = SpellBeam.FunnelChunks + d;
            if (idx >= chunks.Length) break;
            Transform chunk = chunks[idx].Tr;
            if (chunk == null) continue;

            Vector3 axis = AxisFor(axes, idx);
            // The orbit frame puts the axis on +Z, so the debris swings in the plane PERPENDICULAR to
            // the piece's own axis (identity frame when the axis is +Z, the bench's case).
            Quaternion frame = Quaternion.LookRotation(axis);
            float p = Mathf.Repeat(PuffRawPhase(idx, cycles), 1f);
            float rad = Mathf.Lerp(mouthRadius, tipRadius, p) * 1.25f;
            // Absolute orbit angle from the flow position (never a per-frame accumulation), so the
            // swirl cannot drift the way a read-live-and-multiply integrator would (rule 16's class);
            // wrapped to 360 so a long channel does not feed Quaternion.Euler ever-larger degrees.
            float ang = Mathf.Repeat(cycles * 540f + d * 120f, 360f);
            float s = Mathf.Max(rad * 0.34f, 0.08f);
            chunk.localPosition = axis * (p * length) + frame * new Vector3(
                Mathf.Cos(ang * Mathf.Deg2Rad) * rad, Mathf.Sin(ang * Mathf.Deg2Rad) * rad, 0f);
            chunk.localRotation = frame * Quaternion.Euler(ang * 0.7f, ang, ang * 0.4f);
            chunk.localScale = new Vector3(s, s, s);
            SetFade(chunks[idx].Mat, core, FadeAlpha(p));
        }
    }

    /// <summary>1l99: the launch axis for piece <paramref name="i"/> — the caller's per-piece direction
    /// when it supplied one, else the parent's +Z (the bench still's frame). A zero vector would make
    /// <c>Quaternion.LookRotation</c> log and fall back, so it is guarded here rather than at every
    /// call site.</summary>
    private static Vector3 AxisFor(Vector3[] axes, int i)
    {
        if (axes == null || i >= axes.Length) return Vector3.forward;
        Vector3 a = axes[i];
        return a.sqrMagnitude > 0.0001f ? a.normalized : Vector3.forward;
    }

    /// <summary>1l99: a funnel piece's flow phase BEFORE it is wrapped into <c>[0,1)</c> (discs are
    /// <c>0 .. FunnelChunks-1</c>, debris follows). <see cref="FlowFunnelVisual"/> wraps this to place
    /// the piece; <see cref="SpellBeam"/> reads <c>floor(PuffRawPhase)</c> as the piece's PASS, which
    /// increments at exactly the wrap where the piece returns to the muzzle — the one frame its launch
    /// axis may adopt the current aim. Both sides therefore share ONE spelling of the stagger (rule 8),
    /// so a piece's birth is never detected in a frame it is not at the root.</summary>
    public static float PuffRawPhase(int index, float cycles)
    {
        float stagger = index < SpellBeam.FunnelChunks
            ? index / (float)SpellBeam.FunnelChunks
            : (index - SpellBeam.FunnelChunks + 1) / (float)(SpellBeam.FunnelDebris + 1);
        return cycles + stagger;
    }

    /// <summary>1l98: a flame piece's opacity over its muzzle-to-tip travel — it fades IN as it is born
    /// at the root and OUT as it dies at the tip, so "spawn at root" and "disappear" are the two ends of
    /// one curve. The bands are narrow (0.12 / 0.18 of the pass) so most of the stream stays solid.</summary>
    public static float FadeAlpha(float p)
    {
        float born = p / 0.12f;
        float dies = (1f - p) / 0.18f;
        return Mathf.Clamp01(Mathf.Min(born, dies));
    }

    private static void SetFade(Material m, Color color, float alpha)
    {
        if (m == null) return;
        color.a = alpha;
        m.color = color;
    }

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
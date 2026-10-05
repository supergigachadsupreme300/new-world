using UnityEngine;

/// <summary>
/// The casting circle's drawn model — the disc, five rings/spoke-pairs and the rune tick group.
/// <para>Extracted from <c>Magic/Fx/CastingCircle.cs</c> in <b>1jd</b>: "casting circle model" matched
/// nothing under <c>Models/</c> while the model shipped, and this is the piece under EVERY spell cast.
/// Segment and tick counts are <b>passed in</b>, not copied: <c>CastingCircle.Apply</c> loops over the
/// same numbers to place vertices, so a second spelling here would be a copy that rots when one side
/// changes (rule 8).</para>
/// <para><b>One record per piece.</b> Ten pieces, each carrying its own root, renderer and material —
/// <c>CastingCircle</c> holds 21 fields that are read in pairs, and a handoff that cannot be misaligned
/// is worth more here than the terseness of a flat parameter list (rule 17).</para>
/// <para><b>Apply stays in the component.</b> This builds geometry; the charge-driven colour, spin and
/// vertex placement is per-frame behaviour driven by live state the builder never sees.</para>
/// </summary>
public static class CastingCircleModelBuilder
{
    /// <summary>One sub-model. <see cref="Line"/> is the typed handle for the rings and spokes and is
    /// null for the disc; <see cref="Renderer"/> is the base-type handle and is set for every piece.
    /// Both are handed back so the caller never casts per frame.</summary>
    public struct Piece
    {
        public GameObject Root;
        public Renderer Renderer;
        public LineRenderer Line;
        public Material Material;
    }

    /// <summary>All ten pieces of the circle, in build order.</summary>
    public struct Circle
    {
        public Piece Disc;
        public Piece OuterRing;
        public Piece InnerRing;
        public Piece Rune;
        public Piece HexRing;
        public Piece CrossA;
        public Piece CrossB;
        public Piece Arc;
        public Piece WaveB;
        public Piece WaveC;
    }

    /// <summary>Builds the whole circle under <paramref name="host"/>.
    /// <para><b>Every LineRenderer gets its OWN GameObject.</b> A single GameObject permits only one
    /// Renderer component, so a second <c>AddComponent&lt;LineRenderer&gt;</c> on the same object
    /// returns null in Unity 6 — which is why the original made an empty object per ring and that
    /// structure is load-bearing, not a style choice.</para>
    /// <para><b>The rune ticks only exist when a shader was found.</b> In the original they were inside
    /// the <c>shader != null</c> block, so with no shader the <c>Rune</c> group is created and left
    /// empty. That is preserved, group included — <c>CastingCircle</c> toggles the group.</para></summary>
    public static Circle Build(Transform host, int outerSegments, int innerSegments, int hexSegments,
        int arcSegments, int waveSegments, int runeTicks)
    {
        var result = default(Circle);
        Shader shader = Shader.Find("Sprites/Default") ?? Shader.Find("Unlit/Color");

        // --- shared core: the translucent disc ---
        var discGo = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        discGo.name = "Disc";
        var dcol = discGo.GetComponent<Collider>();
        if (dcol != null) Object.Destroy(dcol);
        discGo.transform.SetParent(host, false);
        discGo.transform.localPosition = Vector3.zero;
        result.Disc.Root = discGo;
        result.Disc.Renderer = discGo.GetComponent<MeshRenderer>();
        if (shader != null && result.Disc.Renderer != null)
        {
            result.Disc.Material = new Material(shader);
            result.Disc.Renderer.material = result.Disc.Material;
        }
        else if (result.Disc.Renderer != null)
        {
            result.Disc.Renderer.enabled = false;
        }

        // Crisp outer halo ring (owns its GameObject - see the class note on one Renderer per object).
        result.OuterRing = NewRing(host, "OuterRing", outerSegments, 0.06f, true, shader);

        // Inner rune ring that spins while charging.
        result.InnerRing = NewRing(host, "InnerRing", innerSegments, 0.03f, true, shader);

        // --- 1if: Rune - the spinning ring plus radial tick marks.
        var runeGo = new GameObject("Rune");
        runeGo.transform.SetParent(host, false);
        result.Rune.Root = runeGo;
        if (shader != null)
        {
            result.Rune.Material = new Material(shader);
            for (int i = 0; i < runeTicks; i++)
            {
                // One cube per tick rather than one LineRenderer: a LineRenderer draws a CONTINUOUS
                // line, so N separate marks would need N renderers. Eight tiny cubes cost less than
                // eight extra Renderer components and are toggled by their parent's SetActive.
                var tickGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
                var tc = tickGo.GetComponent<Collider>();
                if (tc != null) Object.Destroy(tc);
                tickGo.name = "Tick" + i;
                tickGo.transform.SetParent(runeGo.transform, false);
                tickGo.GetComponent<MeshRenderer>().material = result.Rune.Material;
                var tr = tickGo.transform;
                tr.localPosition = Vector3.zero;   // positioned each frame in Apply()
                tr.localScale = new Vector3(0.02f, 0.02f, 0.12f);
            }
        }

        // --- 1if: HexRing - a six-sided outline.
        result.HexRing = NewRing(host, "HexRing", hexSegments, 0.05f, true, shader);

        // --- 1if: Cross - two spokes through the centre.
        result.CrossA = NewSpoke(host, "CrossA", 0.04f, shader);
        result.CrossB = NewSpoke(host, "CrossB", 0.04f, shader);

        // --- 1if: Arc - a partial sweep that rotates with the charge.
        result.Arc = NewRing(host, "Arc", arcSegments, 0.05f, false, shader);

        // --- 1if: Wave - two extra rings that chase outward.
        result.WaveB = NewRing(host, "WaveB", waveSegments, 0.03f, true, shader);
        result.WaveC = NewRing(host, "WaveC", waveSegments, 0.025f, true, shader);

        return result;
    }

    /// <summary>A ring (or the arc, with <c>loop: false</c>) on its own child GameObject.</summary>
    private static Piece NewRing(Transform host, string name, int segments, float width, bool loop,
        Shader shader)
    {
        var go = new GameObject(name);
        go.transform.SetParent(host, false);
        var lr = go.AddComponent<LineRenderer>();
        ConfigureRing(lr, segments, width, loop);
        return Finish(go, lr, shader);
    }

    /// <summary>A straight centre-out spoke: two points, not a loop, drawn each frame in Apply().</summary>
    private static Piece NewSpoke(Transform host, string name, float width, Shader shader)
    {
        var go = new GameObject(name);
        go.transform.SetParent(host, false);
        var lr = go.AddComponent<LineRenderer>();
        lr.useWorldSpace = false;
        lr.loop = false;
        lr.positionCount = 2;
        lr.startWidth = width;
        lr.endWidth = width;
        lr.numCapVertices = 2;
        return Finish(go, lr, shader);
    }

    private static void ConfigureRing(LineRenderer lr, int segments, float width, bool loop)
    {
        lr.useWorldSpace = false;
        lr.loop = loop;
        lr.positionCount = segments;
        lr.startWidth = width;
        lr.endWidth = width;
        lr.numCapVertices = 2;
    }

    /// <summary>The material half of a ring, shared by every piece so one lookup covers all of them.
    /// <c>out mat</c> became the record's field: a null material is the signal the original
    /// <c>out</c> parameter carried, and a field cannot be forgotten at a call site.</summary>
    private static Piece Finish(GameObject go, LineRenderer lr, Shader shader)
    {
        var piece = default(Piece);
        piece.Root = go;
        piece.Renderer = lr;
        piece.Line = lr;
        piece.Material = shader != null ? new Material(shader) : null;
        if (piece.Material != null) lr.material = piece.Material;
        return piece;
    }
}
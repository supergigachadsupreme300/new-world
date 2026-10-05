using UnityEngine;

/// <summary>
/// The AoE targeting preview's drawn model — the footprint disc, the crisp outline ring and the
/// centre beacon.
/// <para>Extracted from <c>Combat/Weapons/AoeAimPreview.cs</c> in <b>1jd</b>: "aim preview model"
/// matched nothing under <c>Models/</c> while the model shipped.</para>
/// <para><b>One record per piece</b> (disc / ring / beacon), each carrying its own transform,
/// renderer and material. The preview's <c>Apply</c> mutates all three every frame; three separate
/// parallel lists would be an alignment the extraction must not be able to violate (rule 17).</para>
/// <para><b>The pulse stays with the component.</b> This builds geometry and hands back handles;
/// the per-frame colour and scale work is <c>AoeAimPreview</c>'s, because it owns the live radius
/// and the look.</para>
/// </summary>
public static class AoeAimPreviewModelBuilder
{
    /// <summary>One piece's handles. <see cref="Transform"/> is the piece's own transform — except
    /// for the ring, which is a <c>LineRenderer</c> on the HOST object rather than a child, so its
    /// transform is the host's and its <see cref="Renderer"/> is the <c>LineRenderer</c> itself.
    /// That asymmetry is why <c>Renderer</c> is the base type: the caller casts once, at build time,
    /// instead of every frame.</summary>
    public struct Piece
    {
        public Transform Transform;
        public Renderer Renderer;
        public Material Material;
    }

    /// <summary>All three pieces of the preview.</summary>
    public struct Preview
    {
        public Piece Disc;
        public Piece Ring;
        public Piece Beacon;
    }

    /// <summary>Builds the whole preview under <paramref name="host"/>.
    /// <para><b>The "no shader" branch disables the renderer instead of skipping the piece.</b> That
    /// is deliberate and preserved: a preview whose pieces silently do not exist would let the player
    /// lock a target with nothing on screen to aim by, whereas a disabled renderer leaves the gap
    /// obvious. <see cref="Material"/> comes back null on that path, which is the signal the caller
    /// already checks.</para></summary>
    public static Preview Build(Transform host, int ringSegments, float groundRaise)
    {
        var result = default(Preview);
        Shader shader = Shader.Find("Sprites/Default") ?? Shader.Find("Unlit/Color");

        // Translucent footprint disc (flat cylinder, no collider).
        var discGo = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        discGo.name = "Footprint";
        var dcol = discGo.GetComponent<Collider>();
        if (dcol != null) Object.Destroy(dcol);
        result.Disc.Transform = discGo.transform;
        result.Disc.Transform.SetParent(host, false);
        result.Disc.Transform.localPosition = new Vector3(0f, groundRaise, 0f);
        result.Disc.Transform.localScale = new Vector3(1f, 0.01f, 1f);
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

        // Crisp outline ring (LineRenderer circle in local space, on the host itself).
        var ring = host.gameObject.AddComponent<LineRenderer>();
        ring.useWorldSpace = false;
        ring.loop = true;
        ring.positionCount = ringSegments;
        ring.startWidth = 0.08f;
        ring.endWidth = 0.08f;
        ring.numCapVertices = 2;
        if (shader != null)
        {
            result.Ring.Material = new Material(shader);
            ring.material = result.Ring.Material;
        }
        result.Ring.Transform = host;
        result.Ring.Renderer = ring;

        // Pulsing centre beacon so the exact target point stays readable.
        var beaconGo = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        beaconGo.name = "Beacon";
        var bcol = beaconGo.GetComponent<Collider>();
        if (bcol != null) Object.Destroy(bcol);
        result.Beacon.Transform = beaconGo.transform;
        result.Beacon.Transform.SetParent(host, false);
        result.Beacon.Transform.localPosition = new Vector3(0f, 0.55f, 0f);
        result.Beacon.Transform.localScale = new Vector3(0.16f, 0.55f, 0.16f);
        result.Beacon.Renderer = beaconGo.GetComponent<MeshRenderer>();
        if (shader != null && result.Beacon.Renderer != null)
        {
            result.Beacon.Material = new Material(shader);
            result.Beacon.Renderer.material = result.Beacon.Material;
        }
        else if (result.Beacon.Renderer != null)
        {
            result.Beacon.Renderer.enabled = false;
        }

        return result;
    }
}
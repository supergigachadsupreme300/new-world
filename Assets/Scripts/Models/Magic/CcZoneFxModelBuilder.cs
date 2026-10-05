using UnityEngine;

/// <summary>
/// The crowd-control zone's drawn ring.
/// <para>Extracted from <c>Combat/Status/CCZone.cs</c> in <b>1jd</b>: "CC zone model" matched nothing
/// under <c>Models/</c> while the model shipped.</para>
/// <para><b>The colour comes in from the caller, deliberately.</b> A CC ring marks "something is
/// holding you here", not a school — so this builder invents no colour of its own and takes the one
/// <c>CCZone</c> chose. It is NOT a <c>SpellLook</c> colour and must not be routed through
/// <c>SpellLook.Resolve</c> (rule 13: one place derives a SPELL's identity, and this is not one).</para>
/// </summary>
public static class CcZoneFxModelBuilder
{
    /// <summary>A flat cylinder laid on the ground, parented to the zone's own transform and scaled
    /// to the zone's radius. 3 cm of lift off the ground keeps the ring off the terrain it lies on.
    /// <para>Body-only: the collider is destroyed and the zone applies its slow/stun by tracking
    /// enemies in range, never by querying this ring.</para></summary>
    public static void BuildCcZoneRing(Transform parent, float radius, Color color)
    {
        var ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        ring.name = "CCZoneFx";
        ring.transform.SetParent(parent, false);
        ring.transform.localPosition = new Vector3(0f, 0.03f, 0f);

        Collider col = ring.GetComponent<Collider>();
        if (col != null) Object.Destroy(col);

        ring.transform.localScale = new Vector3(radius * 2f, 0.02f, radius * 2f);
        Shader shader = Shader.Find("Sprites/Default") ?? Shader.Find("Unlit/Color");
        Renderer renderer = ring.GetComponent<MeshRenderer>();
        if (renderer != null && shader != null)
            renderer.material = new Material(shader) { color = color };
    }
}
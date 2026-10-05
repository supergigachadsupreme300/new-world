using UnityEngine;

/// <summary>
/// The generated ranged-weapon PROJECTILE models — the arrow and the throwing hammer.
/// <para>Extracted from <c>Combat/Weapons/RangedWeaponBehavior.cs</c> in <b>1jd</b>: both were
/// private methods of the firing behaviour, so nothing named "arrow model" or "projectile model"
/// existed anywhere in the repo while both models shipped every shot.</para>
/// <para>A separate file from <c>Models/WeaponModelBuilder.cs</c> on purpose: that class is the
/// held-weapon catalogue (eighteen builders, dispatched by weapon id), and these two are the things
/// that fly. They also need a shader lookup the held-weapon builders do not.</para>
/// <para><b>Neither reuses <c>WeaponModelBuilder.MakeBlock</c>.</b> It assigns
/// <c>MapBuilder.CreateSolidMaterial(color)</c> through <c>sharedMaterial</c>; these two assign a
/// freshly-found shader through <c>material</c>, i.e. a per-renderer instance. Reusing the helper
/// would have been a silent material change, which is exactly the kind of "tidy-up" that a diff
/// reads as a no-op.</para>
/// </summary>
public static class WeaponProjectileModelBuilder
{
    /// <summary>Build a generated arrow visual (used when no projectile prefab is assigned).
    /// Renderer-only: the root keeps no collider so <c>RangedProjectile</c>'s flight raycast never
    /// self-hits. Travels along <b>+Z</b> — the cylinder primitives are laid over because a cylinder's
    /// default axis is +Y.</summary>
    public static GameObject BuildArrowVisual()
    {
        var go = new GameObject("ArrowProjectile");
        Shader shader = Shader.Find("Sprites/Default") ?? Shader.Find("Unlit/Color");
        if (shader == null) return go;
        var wood = new Color(0.45f, 0.32f, 0.18f, 1f);
        var steel = new Color(0.82f, 0.82f, 0.85f, 1f);

        // Shaft along +Z (cylinder defaults to the Y axis).
        var shaft = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        shaft.name = "Shaft";
        DestroyCollider(shaft);
        shaft.transform.SetParent(go.transform, false);
        shaft.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        shaft.transform.localPosition = new Vector3(0f, 0f, -0.05f);
        shaft.transform.localScale = new Vector3(0.03f, 0.25f, 0.03f);
        shaft.GetComponent<MeshRenderer>().material = new Material(shader) { color = wood };

        // Pointed head at the front.
        var head = GameObject.CreatePrimitive(PrimitiveType.Cube);
        head.name = "Head";
        DestroyCollider(head);
        head.transform.SetParent(go.transform, false);
        head.transform.localPosition = new Vector3(0f, 0f, 0.22f);
        head.transform.localScale = new Vector3(0.1f, 0.1f, 0.12f);
        head.GetComponent<MeshRenderer>().material = new Material(shader) { color = steel };

        // Two thin crossed fletching blades near the tail.
        var fletchA = GameObject.CreatePrimitive(PrimitiveType.Cube);
        fletchA.name = "FletchA";
        DestroyCollider(fletchA);
        fletchA.transform.SetParent(go.transform, false);
        fletchA.transform.localPosition = new Vector3(0f, 0f, -0.24f);
        fletchA.transform.localScale = new Vector3(0.06f, 0.09f, 0.01f);
        fletchA.GetComponent<MeshRenderer>().material = new Material(shader) { color = steel };

        var fletchB = GameObject.CreatePrimitive(PrimitiveType.Cube);
        fletchB.name = "FletchB";
        DestroyCollider(fletchB);
        fletchB.transform.SetParent(go.transform, false);
        fletchB.transform.localPosition = new Vector3(0f, 0f, -0.24f);
        fletchB.transform.localScale = new Vector3(0.01f, 0.06f, 0.09f);
        fletchB.GetComponent<MeshRenderer>().material = new Material(shader) { color = steel };

        return go;
    }

    /// <summary>Generated spinning hammer-head projectile for the throwing hammer. Flies head-first
    /// along +Z (matching the projectile travel axis) and tumbles around that axis in flight.
    /// Renderer-only — no collider, so <c>RangedProjectile</c>'s raycast never self-hits.
    /// <para><b>Root always gets <c>TumbleSpin</c>, even with no shader.</b> The spin is added after
    /// the body block in the original and sits outside the <c>shader != null</c> guard, so a
    /// shader-less run still tumbles. Keeping that placement is why the guard encloses only the
    /// geometry.</para></summary>
    public static GameObject BuildHammerVisual()
    {
        var go = new GameObject("HammerProjectile");
        Shader shader = Shader.Find("Sprites/Default") ?? Shader.Find("Unlit/Color");
        if (shader != null)
        {
            var wood = new Color(0.45f, 0.32f, 0.18f, 1f);
            var steel = new Color(0.82f, 0.82f, 0.85f, 1f);
            var darkSteel = new Color(0.30f, 0.30f, 0.34f, 1f);

            // Handle along +Z (cylinder defaults to Y, rotate 90° about X).
            var handle = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            handle.name = "Handle";
            DestroyCollider(handle);
            handle.transform.SetParent(go.transform, false);
            handle.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            handle.transform.localPosition = new Vector3(0f, 0f, -0.28f);
            handle.transform.localScale = new Vector3(0.07f, 0.32f, 0.07f);
            handle.GetComponent<MeshRenderer>().material = new Material(shader) { color = wood };

            // Handle grip wrap.
            var wrap = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wrap.name = "Wrap";
            DestroyCollider(wrap);
            wrap.transform.SetParent(go.transform, false);
            wrap.transform.localPosition = new Vector3(0f, 0f, -0.44f);
            wrap.transform.localScale = new Vector3(0.09f, 0.09f, 0.16f);
            wrap.GetComponent<MeshRenderer>().material = new Material(shader) { color = darkSteel };

            // Hammer head at the front.
            var head = GameObject.CreatePrimitive(PrimitiveType.Cube);
            head.name = "Head";
            DestroyCollider(head);
            head.transform.SetParent(go.transform, false);
            head.transform.localPosition = new Vector3(0f, 0f, 0.22f);
            head.transform.localScale = new Vector3(0.30f, 0.22f, 0.20f);
            head.GetComponent<MeshRenderer>().material = new Material(shader) { color = steel };

            // Head rim + striking spike.
            var rim = GameObject.CreatePrimitive(PrimitiveType.Cube);
            rim.name = "HeadRim";
            DestroyCollider(rim);
            rim.transform.SetParent(go.transform, false);
            rim.transform.localPosition = new Vector3(0f, 0f, 0.32f);
            rim.transform.localScale = new Vector3(0.26f, 0.18f, 0.08f);
            rim.GetComponent<MeshRenderer>().material = new Material(shader) { color = darkSteel };

            var spike = GameObject.CreatePrimitive(PrimitiveType.Cube);
            spike.name = "Spike";
            DestroyCollider(spike);
            spike.transform.SetParent(go.transform, false);
            spike.transform.localPosition = new Vector3(0f, 0f, 0.36f);
            spike.transform.localScale = new Vector3(0.08f, 0.08f, 0.14f);
            spike.GetComponent<MeshRenderer>().material = new Material(shader) { color = darkSteel };
        }

        go.AddComponent<TumbleSpin>();
        return go;
    }

    private static void DestroyCollider(GameObject go)
    {
        var col = go.GetComponent<Collider>();
        if (col != null) Object.Destroy(col);
    }

    /// <summary>Slow tumble around the travel axis so a thrown hammer reads as spinning mid-air.
    /// <para>1jd: moved verbatim from <c>RangedWeaponBehavior.TumbleSpin</c> — it had exactly one user,
    /// the hammer this file builds. The rotation is the four-argument euler overload (360°/s about the
    /// X axis in <c>Space.Self</c>); rewriting it as an axis-angle form would read as equivalent and
    /// is not, because the axis differs. Keep the body exactly as it was.</para></summary>
    private sealed class TumbleSpin : MonoBehaviour
    {
        private void Update()
        {
            transform.Rotate(360f * Time.deltaTime, 0f, 0f, Space.Self);
        }
    }
}

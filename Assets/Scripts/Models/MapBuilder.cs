using System.Collections.Generic;
using UnityEngine;
using CountryLife.Helpers;

public enum PlayerGender { Male, Female }

public static partial class MapBuilder
{
    public static PlayerGender ActiveGender = PlayerGender.Male;

    private static readonly Dictionary<Color, Material> _colorMatCache = new Dictionary<Color, Material>();
    private static readonly Dictionary<Color, Texture2D> _colorTexCache = new Dictionary<Color, Texture2D>();
    private static bool _materialLogged;

    /// <summary>
    /// Build a solid-color material that reliably tints across pipelines. URP Lit needs its base
    /// color via <c>_BaseMap</c>/<c>_BaseColor</c> (plain <c>_Color</c> is ignored and stays white);
    /// a 1x1 color texture as <c>_BaseMap</c> guarantees the tint even when a URP version ignores
    /// the scalar. Standard/Unlit fall back to <c>_Color</c>.
    /// </summary>
    public static Material CreateSolidMaterial(Color color)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");
        if (shader == null) shader = Shader.Find("Unlit/Color");
        var mat = new Material(shader);
        mat.color = color;
        if (mat.HasProperty("_BaseMap"))
        {
            mat.SetTexture("_BaseMap", ColorTexture(color));
            mat.SetColor("_BaseColor", Color.white);
        }
        else if (mat.HasProperty("_BaseColor"))
        {
            mat.SetColor("_BaseColor", color);
        }
        mat.name = "BlockMat_" + ColorUtility.ToHtmlStringRGB(color);
        if (!_materialLogged)
        {
            _materialLogged = true;
            Debug.Log("[MapBuilder] Block material shader: '" + (shader != null ? shader.name : "<null>") +
                "' for color #" + ColorUtility.ToHtmlStringRGB(color));
        }
        return mat;
    }

    private static Texture2D ColorTexture(Color color)
    {
        if (_colorTexCache.TryGetValue(color, out var tex) && tex != null) return tex;
        var t = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        t.name = "BlockCol_" + ColorUtility.ToHtmlStringRGB(color);
        t.SetPixel(0, 0, color);
        t.Apply();
        t.hideFlags = HideFlags.HideAndDontSave;
        _colorTexCache[color] = t;
        return t;
    }

    public static void ApplyBlockColor(Renderer r, Color color)
    {
        if (r == null) return;
        if (_colorMatCache.TryGetValue(color, out var cached) && cached != null)
        {
            r.sharedMaterial = cached;
            return;
        }
        var mat = CreateSolidMaterial(color);
        _colorMatCache[color] = mat;
        r.sharedMaterial = mat;
    }

    // ═══════════════════════════════════════════════════════════════
    //  LOW-LEVEL BLOCK
    // ═══════════════════════════════════════════════════════════════

    private static Mesh _sharedCubeMesh;

    /// <summary>
    /// One geometry cube shared by every box the game builds. CreatePrimitive allocates a
    /// fresh hidden mesh per call; streaming chunks spawn hundreds of prop cubes, so sharing a
    /// single unit cube mesh removes that allocation churn (identical shape — transforms handle
    /// all scaling).
    /// </summary>
    public static Mesh SharedCubeMesh()
    {
        if (_sharedCubeMesh != null)
            return _sharedCubeMesh;
        // Peel the mesh once from a throwaway primitive, then reuse it forever.
        var temp = GameObject.CreatePrimitive(PrimitiveType.Cube);
        _sharedCubeMesh = temp.GetComponent<MeshFilter>().sharedMesh;
        _sharedCubeMesh.name = "SharedCube";
        Object.Destroy(temp);
        _sharedCubeMesh.hideFlags |= HideFlags.HideAndDontSave;
        return _sharedCubeMesh;
    }

    /// <summary>
    /// Build a cube using the shared unit mesh and a shared (cached) material. Equivalent to
    /// CreatePrimitive(Cube) + ApplyBlockColor, without the per-call mesh allocation and with
    /// no default-material instance. Adds a BoxCollider only when requested.
    /// </summary>
    public static GameObject MakeBlock(string name, Transform parent, Vector3 scale, Vector3 position, Color color, bool removeCollider = false, Quaternion rotation = default)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent);
        go.transform.localScale = scale;
        go.transform.localPosition = position;
        if (rotation != default) go.transform.localRotation = rotation;
        var mf = go.AddComponent<MeshFilter>();
        mf.sharedMesh = SharedCubeMesh();
        var r = go.AddComponent<MeshRenderer>();
        ApplyBlockColor(r, color);
        if (!removeCollider)
            go.AddComponent<BoxCollider>();
        return go;
    }

    /// <summary>
    /// Player-body builder (1dw): a smooth ellipsoid part instead of a cube. Same transform contract
    /// as <see cref="MakeBlock"/> (localScale = the part's size vector reproduces today's exact
    /// dimensions because the mesh occupies the same [-0.5, 0.5] cube), so race ratios, gender sizes
    /// and weapon hand-scale compensation keep working untouched. The mesh comes from the static
    /// shared cache in <see cref="PlayerPartMesher"/>; no collider (the CharacterController owns
    /// collision). Falls back to a plain ellipsoid for unknown profile ids.
    /// </summary>
    public static GameObject MakePart(string name, Transform parent, Vector3 scale, Vector3 position, Color color, string profileId, Quaternion rotation = default)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent);
        go.transform.localScale = scale;
        go.transform.localPosition = position;
        if (rotation != default) go.transform.localRotation = rotation;
        var mf = go.AddComponent<MeshFilter>();
        mf.sharedMesh = PlayerPartMesher.BuildEllipsoid(profileId);
        var r = go.AddComponent<MeshRenderer>();
        ApplyBlockColor(r, color);
        return go;
    }

    /// <summary>
    /// Cube builder for geometry that has a shared texture material (tree branches/leaves).
    /// Uses the shared unit cube mesh; assigns the material with <c>sharedMaterial</c> so no
    /// per-renderer Material copy is created (the old <c>r.material = x</c> leaked one copy per
    /// cube). Falls back to the cached solid-color material when no texture material exists.
    /// </summary>
    public static GameObject MakeCubeShared(string name, Transform parent, Vector3 scale,
        Vector3 position, Quaternion rotation, Material sharedMat, Color fallback, bool withCollider)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent);
        go.transform.localPosition = position;
        go.transform.localRotation = rotation;
        go.transform.localScale = scale;
        var mf = go.AddComponent<MeshFilter>();
        mf.sharedMesh = SharedCubeMesh();
        var r = go.AddComponent<MeshRenderer>();
        if (sharedMat != null)
            r.sharedMaterial = sharedMat;
        else
            ApplyBlockColor(r, fallback);
        if (withCollider)
            go.AddComponent<BoxCollider>();
        return go;
    }

    private static void SetTransparent(Renderer r, float alpha)
    {
        r.material = PickupVisualHelper.CreateTransparentMaterial(new Color(1f, 1f, 1f, alpha), 0.8f);
    }

    // ═══════════════════════════════════════════════════════════════
    //  SHARED HELPERS
    // ═══════════════════════════════════════════════════════════════

    public static Light BuildStreetLamp(Transform parent, Vector3 position)
    {
        Color lampC = new Color(0.18f, 0.16f, 0.14f);
        Color headC = new Color(0.9f, 0.35f, 0.2f);
        Color glowC = new Color(1f, 0.85f, 0.45f);
        MakeBlock("StreetLampBase", parent, new Vector3(0.36f, 0.08f, 0.36f), position + new Vector3(0f, 0.05f, 0f), lampC, true);
        MakeBlock("StreetLampPole", parent, new Vector3(0.14f, 2.9f, 0.14f), position + new Vector3(0f, 1.5f, 0f), lampC, true);
        MakeBlock("StreetLampHead", parent, new Vector3(0.36f, 0.28f, 0.36f), position + new Vector3(0f, 3.0f, 0f), headC, true);
        var glow = MakeBlock("StreetLampGlow", parent, new Vector3(0.2f, 0.16f, 0.2f), position + new Vector3(0f, 2.98f, 0f), glowC, true);
        DisableShadowCasting(glow);
        return AddGlowLight(parent, position + new Vector3(0f, 3.18f, 0f), 22f, 3.2f, new Color(1f, 0.85f, 0.45f), "StreetLight");
    }

    public static Light AddGlowLight(Transform parent, Vector3 position, float range, float intensity, Color color, string goName = "GlowPointLight")
    {
        var lampGo = new GameObject(goName);
        lampGo.transform.SetParent(parent);
        lampGo.transform.localPosition = position;
        var light = lampGo.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = color;
        light.intensity = intensity;
        light.range = range;
        light.shadows = LightShadows.None;
        return light;
    }

    public static void DisableShadowCasting(GameObject go)
    {
        if (go == null) return;
        var r = go.GetComponent<Renderer>();
        if (r == null) return;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    }

    private static void AddEntranceLight(Transform parent, Vector3 position)
    {
        var lampGo = new GameObject("RestaurantPointLight");
        lampGo.transform.SetParent(parent);
        lampGo.transform.localPosition = position;
        var light = lampGo.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = new Color(1f, 0.85f, 0.55f);
        light.intensity = 1.6f;
        light.range = 6f;
    }

    private static void BuildCypress(Transform parent, Vector3 position, float scale, string tag, Color leafC, Color trunkC)
    {
        var root = new GameObject("Cypress" + tag);
        root.transform.SetParent(parent);
        root.transform.localPosition = position;
        root.transform.localScale = Vector3.one * scale;
        MakeBlock("Trunk", root.transform, new Vector3(0.3f, 1.3f, 0.3f), new Vector3(0f, 0.65f, 0f), trunkC, true);
        MakeBlock("Leaf1", root.transform, new Vector3(1.9f, 1.3f, 1.9f), new Vector3(0f, 1.4f, 0f), leafC, true);
        MakeBlock("Leaf2", root.transform, new Vector3(1.4f, 1.2f, 1.4f), new Vector3(0f, 2.5f, 0f), leafC, true);
        MakeBlock("Leaf3", root.transform, new Vector3(0.95f, 1.2f, 0.95f), new Vector3(0f, 3.6f, 0f), leafC, true);
        MakeBlock("Leaf4", root.transform, new Vector3(0.5f, 1f, 0.5f), new Vector3(0f, 4.7f, 0f), leafC, true);
    }

    private static void AddWindowTrim(Transform parent, Vector3 center, float w, float h, Vector3 outward, Color frameC, Color stoneC, string suffix)
    {
        Vector3 basePos = center + outward * 0.3f;
        MakeBlock("WinSill" + suffix, parent, new Vector3(w + 0.5f, 0.12f, 0.35f),
            basePos + new Vector3(0f, -(h / 2f + 0.06f), 0f), stoneC, true);
        MakeBlock("WinHead" + suffix, parent, new Vector3(w + 0.5f, 0.16f, 0.35f),
            basePos + new Vector3(0f, h / 2f + 0.08f, 0f), stoneC, true);
        MakeBlock("ShutterL" + suffix, parent, new Vector3(0.28f, h, 0.08f),
            center + outward * 0.2f + new Vector3(-(w / 2f + 0.16f), 0f, 0f), frameC, true);
        MakeBlock("ShutterR" + suffix, parent, new Vector3(0.28f, h, 0.08f),
            center + outward * 0.2f + new Vector3(w / 2f + 0.16f, 0f, 0f), frameC, true);
    }


    private static readonly (string goName, string locKey)[] _signDefs = {
        ("CafeSignLabel", "QUÁN CÀ PHÊ"),
        ("StoreSignLabel", "TIỆN LỢI"),
        ("LibrarySignLabel", "THƯ VIỆN"),
        ("ClubNeonLabel", "DANCE NIGHT"),
        ("PoliceSignLabel", "CẢNH SÁT"),
        ("RestaurantSignLabel", "NHÀ HÀNG"),
        ("ShopSignLabel", "CỬA HÀNG"),
        ("FishingShopSignLabel", "CỬA HÀNG CÂU CÁ"),
    };

    public static void RefreshWorldSignTexts()
    {
        foreach (var (goName, locKey) in _signDefs)
        {
            var go = GameObject.Find(goName);
            if (go == null) continue;
            var tmp = go.GetComponent<TMPro.TextMeshPro>();
            if (tmp != null)
                tmp.text = Localization.T(locKey);
        }
    }
}

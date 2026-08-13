using System.Collections.Generic;
using UnityEngine;

public enum PlayerGender { Male, Female }

public static partial class MapBuilder
{
    public static PlayerGender ActiveGender = PlayerGender.Male;

    private static readonly Dictionary<Color, Material> _colorMatCache = new Dictionary<Color, Material>();

    public static void ApplyBlockColor(Renderer r, Color color)
    {
        if (r == null) return;
        if (_colorMatCache.TryGetValue(color, out var cached) && cached != null)
        {
            r.sharedMaterial = cached;
            return;
        }
        var mat = new Material(r.sharedMaterial);
        mat.color = color;
        mat.name = "BlockMat_" + color;
        _colorMatCache[color] = mat;
        r.sharedMaterial = mat;
    }

    // ═══════════════════════════════════════════════════════════════
    //  LOW-LEVEL BLOCK
    // ═══════════════════════════════════════════════════════════════

    public static GameObject MakeBlock(string name, Transform parent, Vector3 scale, Vector3 position, Color color, bool removeCollider = false, Quaternion rotation = default)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.SetParent(parent);
        go.transform.localScale = scale;
        go.transform.localPosition = position;
        if (rotation != default) go.transform.localRotation = rotation;
        var r = go.GetComponent<Renderer>();
        ApplyBlockColor(r, color);
        if (removeCollider)
            Object.Destroy(go.GetComponent<Collider>());
        return go;
    }

    private static void SetTransparent(Renderer r, float alpha)
    {
        var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        mat.SetFloat("_Surface", 1f);
        mat.SetFloat("_Blend", 0f);
        mat.SetFloat("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        mat.SetFloat("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        mat.SetFloat("_ZWrite", 0f);
        mat.SetFloat("_Cull", 0f);
        mat.SetFloat("_Metallic", 0f);
        mat.SetFloat("_Smoothness", 0.8f);
        mat.renderQueue = 3000;
        var c = mat.color;
        c.a = alpha;
        mat.color = c;
        r.material = mat;
    }

    public static GameObject MakeTriangleBlock(string name, Transform parent, Vector3 scale, Vector3 position, Color color, bool removeCollider = false)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent);
        go.transform.localScale = scale;
        go.transform.localPosition = position;

        var mesh = new Mesh();
        mesh.vertices = new[]
        {
            new Vector3(-0.5f, -0.5f, -0.5f),
            new Vector3(0.5f, -0.5f, -0.5f),
            new Vector3(0.0f, 0.5f, -0.5f),
            new Vector3(-0.5f, -0.5f, 0.5f),
            new Vector3(0.5f, -0.5f, 0.5f),
            new Vector3(0.0f, 0.5f, 0.5f)
        };
        mesh.triangles = new[]
        {
            0, 2, 1,
            3, 4, 5,
            0, 1, 4, 0, 4, 3,
            0, 3, 5, 0, 5, 2,
            1, 2, 5, 1, 5, 4
        };
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        var mf = go.AddComponent<MeshFilter>();
        mf.mesh = mesh;

        var mr = go.AddComponent<MeshRenderer>();
        mr.material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        if (mr.material != null) mr.material.color = color;

        if (removeCollider)
        {
            var col = go.GetComponent<Collider>();
            if (col != null) Object.Destroy(col);
        }
        else
        {
            go.AddComponent<MeshCollider>();
        }

        return go;
    }

    // ═══════════════════════════════════════════════════════════════
    //  SHARED HELPERS
    // ═══════════════════════════════════════════════════════════════

    private static void AddLamppost(Transform parent, Vector3 position)
    {
        Color lampC = new Color(0.18f, 0.16f, 0.14f);
        MakeBlock("Lamppost", parent, new Vector3(0.16f, 2.8f, 0.16f), position + new Vector3(0f, 1.4f, 0f), lampC, true);
        MakeBlock("LampHead", parent, new Vector3(0.34f, 0.3f, 0.34f), position + new Vector3(0f, 2.85f, 0f), new Color(0.9f, 0.35f, 0.2f), true);
        MakeBlock("LampGlow", parent, new Vector3(0.2f, 0.18f, 0.2f), position + new Vector3(0f, 2.85f, 0f), new Color(1f, 0.85f, 0.45f), true);
        AddEntranceLight(parent, position + new Vector3(0f, 2.8f, 0f));
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
}

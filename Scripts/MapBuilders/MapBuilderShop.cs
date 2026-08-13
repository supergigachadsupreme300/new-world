using System.Collections.Generic;
using UnityEngine;

public static partial class MapBuilder
{
    // ═══════════════════════════════════════════════════════════════
    //  SHOP / BUFFALO SHOP  (10 x 4 x 10, counter, shelves, awning)
    // ═══════════════════════════════════════════════════════════════

    public static GameObject BuildShop(Transform parent, Vector3 position, float scale = 1f, Quaternion rotation = default)
    {
        var root = new GameObject("Shop");
        root.transform.SetParent(parent);
        root.transform.position = position;
        root.transform.rotation = (rotation == default) ? Quaternion.identity : rotation;
        root.transform.localScale = Vector3.one * scale;

        Color wallC    = new Color(0.404f, 0.361f, 0.302f);
        Color roofC    = new Color(0.871f, 0.161f, 0.11f);
        Color ridgeC   = new Color(0.537f, 0.067f, 0.118f);
        Color eaveC    = new Color(0.18f, 0.18f, 0.18f);
        Color stoneC   = new Color(0.439f, 0.4f, 0.361f);
        Color floorC   = new Color(0.357f, 0.275f, 0.18f);
        Color frameC   = new Color(0.2f, 0.125f, 0.078f);
        Color counterC = new Color(0.584f, 0.294f, 0.165f);
        Color shelfC   = new Color(0.455f, 0.275f, 0.157f);
        Color winC     = new Color(0.549f, 0.784f, 0.863f);
        Color signC    = new Color(0.886f, 0.753f, 0.098f);
        Color awningC  = new Color(0.843f, 0.184f, 0.161f);
        Color itemC    = new Color(0.949f, 0.584f, 0.094f);

        // ── Walls ──
        MakeBlock("Wall", root.transform, new Vector3(10f, 4f, 0.5f), new Vector3(0f, 2f, -5f), wallC);
        MakeBlock("Wall", root.transform, new Vector3(10f, 4f, 0.5f), new Vector3(0f, 2f, 5f), wallC);
        MakeBlock("Wall", root.transform, new Vector3(0.5f, 4f, 10f), new Vector3(-5f, 2f, 0f), wallC);
        MakeBlock("Wall", root.transform, new Vector3(0.5f, 4f, 3.5f), new Vector3(5f, 2f, -3.25f), wallC);
        MakeBlock("Wall", root.transform, new Vector3(0.5f, 4f, 3.5f), new Vector3(5f, 2f, 3.25f), wallC);
        MakeBlock("Transom", root.transform, new Vector3(0.5f, 1.2f, 3f), new Vector3(5f, 3.4f, 0f), wallC);
        MakeBlock("Floor", root.transform, new Vector3(10f, 0.5f, 10f), Vector3.zero, floorC);

        // ── Gabled roof ──
        float rise = 2.5f;
        float halfW = 5f;
        float panelLen = Mathf.Sqrt(halfW * halfW + rise * rise);
        float tilt = Mathf.Atan2(rise, halfW) * Mathf.Rad2Deg;
        float overhang = 1.2f;
        float roofZ = 10f + overhang * 2f;

        MakeBlock("RoofPanel", root.transform, new Vector3(panelLen, 0.5f, roofZ),
            new Vector3(halfW / 2f, 4f + rise / 2f, 0f), roofC).transform.rotation = Quaternion.Euler(0f, 0f, -tilt);
        MakeBlock("RoofPanel", root.transform, new Vector3(panelLen, 0.5f, roofZ),
            new Vector3(-halfW / 2f, 4f + rise / 2f, 0f), roofC).transform.rotation = Quaternion.Euler(0f, 0f, tilt);
        MakeBlock("Ridge", root.transform, new Vector3(0.55f, 0.3f, roofZ + 0.2f),
            new Vector3(0f, 4f + rise + 0.1f, 0f), ridgeC);
        MakeBlock("Eave", root.transform, new Vector3(0.5f, 0.25f, roofZ + 0.2f),
            new Vector3(halfW, 4.1f, 0f), eaveC);
        MakeBlock("Eave", root.transform, new Vector3(0.5f, 0.25f, roofZ + 0.2f),
            new Vector3(-halfW, 4.1f, 0f), eaveC);

        foreach (float gz in new[] { -5f, 5f })
        {
            float gzFace = gz + (gz > 0 ? 1f : -1f) * 0.04f;
            for (int i = 0; i < 5; i++)
            {
                float t = (i + 0.5f) / 5f;
                float sw = 10f * (1f - t) + 0.2f;
                float sy = 4f + (i + 0.5f) * rise / 5f;
                float sh = rise / 5f + 0.15f;
                MakeBlock("GableFill", root.transform, new Vector3(sw, sh, 0.55f),
                    new Vector3(0f, sy, gzFace), wallC);
            }
        }

        // ── Stone foundation ──
        MakeBlock("Foundation", root.transform, new Vector3(11.5f, 0.4f, 11.5f),
            new Vector3(0f, -0.2f, 0f), stoneC);

        // ── Sign ──
        MakeBlock("Sign", root.transform, new Vector3(0.2f, 0.8f, 3.5f),
            new Vector3(5.08f, 3.6f, 0f), signC, true);
        var shopSignLabel = new GameObject("ShopSignLabel");
        shopSignLabel.transform.SetParent(root.transform);
        shopSignLabel.transform.localPosition = new Vector3(5.3f, 3.6f, 0f);
        shopSignLabel.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);
        var shopSignTmp = shopSignLabel.AddComponent<TMPro.TextMeshPro>();
        shopSignTmp.text = "CỬA HÀNG";
        shopSignTmp.fontSize = 1.0f;
        shopSignTmp.alignment = TMPro.TextAlignmentOptions.Center;
        shopSignTmp.color = new Color(0.98f, 0.94f, 0.85f);
        shopSignTmp.outlineWidth = 0.18f;
        shopSignTmp.outlineColor = Color.black;
        shopSignTmp.rectTransform.sizeDelta = new Vector3(3.2f, 0.75f);

        // ── Entrance awning ──
        MakeBlock("Awning", root.transform, new Vector3(1.5f, 0.15f, 3.5f),
            new Vector3(5.8f, 3.8f, 0f), awningC, true);
        MakeBlock("AwningPost", root.transform, new Vector3(0.12f, 3.8f, 0.12f),
            new Vector3(6.6f, 1.9f, -1.5f), frameC, true);
        MakeBlock("AwningPost", root.transform, new Vector3(0.12f, 3.8f, 0.12f),
            new Vector3(6.6f, 1.9f, 1.5f), frameC, true);

        // ── Windows ──
        foreach (float wz in new[] { -3f, 3f })
        {
            MakeBlock("WinGlass", root.transform, new Vector3(0.14f, 1.2f, 1.2f),
                new Vector3(-5.03f, 2.2f, wz), winC, true);
            MakeBlock("WinFrame", root.transform, new Vector3(0.16f, 0.08f, 1.2f),
                new Vector3(-5.03f, 2.2f, wz), frameC, true);
            MakeBlock("WinFrame", root.transform, new Vector3(0.16f, 1.2f, 0.08f),
                new Vector3(-5.03f, 2.2f, wz), frameC, true);
        }
        foreach (float wx in new[] { -3f, 3f })
        {
            MakeBlock("WinGlass", root.transform, new Vector3(1.4f, 1.2f, 0.14f),
                new Vector3(wx, 2.2f, -5.03f), winC, true);
            MakeBlock("WinFrame", root.transform, new Vector3(0.1f, 1.2f, 0.16f),
                new Vector3(wx, 2.2f, -5.03f), frameC, true);
            MakeBlock("WinFrame", root.transform, new Vector3(1.4f, 0.08f, 0.16f),
                new Vector3(wx, 2.2f, -5.03f), frameC, true);
        }

        // ── Counter where buffalo stands ──
        MakeBlock("Counter", root.transform, new Vector3(1.8f, 1f, 4f),
            new Vector3(-2.4f, 0.5f, 0f), counterC);
        MakeBlock("CounterTop", root.transform, new Vector3(1.8f, 0.08f, 4.2f),
            new Vector3(-2.4f, 0.96f, 0f), new Color(0.757f, 0.62f, 0.404f), true);
        MakeBlock("CounterFront", root.transform, new Vector3(0.03f, 0.8f, 4f),
            new Vector3(-1.5f, 0.4f, 0f), new Color(0.624f, 0.369f, 0.192f), true);

        // ── Shelves behind counter ──
        MakeBlock("ShelfPost", root.transform, new Vector3(0.12f, 4f, 0.12f),
            new Vector3(-4.4f, 2f, -3.5f), frameC, true);
        MakeBlock("ShelfPost", root.transform, new Vector3(0.12f, 4f, 0.12f),
            new Vector3(-4.4f, 2f, 3.5f), frameC, true);
        for (int i = 0; i < 3; i++)
        {
            float sy = 0.5f + i * 1.4f;
            MakeBlock("ShelfBoard", root.transform, new Vector3(0.12f, 0.08f, 7f),
                new Vector3(-4.4f, sy, 0f), shelfC, true);
            MakeBlock("ShelfItem", root.transform, new Vector3(0.25f, 0.25f, 0.25f),
                new Vector3(-4.4f, sy + 0.2f, -1.5f + i * 1.5f), itemC, true);
        }

        // ── Door frame ──
        MakeBlock("DoorFrame", root.transform, new Vector3(0.25f, 3.5f, 0.25f),
            new Vector3(5.04f, 1.75f, -1.5f), frameC, true);
        MakeBlock("DoorFrame", root.transform, new Vector3(0.25f, 3.5f, 0.25f),
            new Vector3(5.04f, 1.75f, 1.5f), frameC, true);
        MakeBlock("DoorLintel", root.transform, new Vector3(0.25f, 0.3f, 3.25f),
            new Vector3(5.04f, 3.65f, 0f), frameC, true);

        return root;
    }

}

using System.Collections.Generic;
using UnityEngine;

public static partial class MapBuilder
{
    // ═══════════════════════════════════════════════════════════════
    //  CONVENIENCE STORE  (small market kiosk between Shop & Restaurant)
    // ═══════════════════════════════════════════════════════════════

    public static GameObject BuildConvenienceStore(Transform parent, Vector3 position, float scale = 1f, Quaternion rotation = default)
    {
        var root = new GameObject("ConvenienceStore");
        root.transform.SetParent(parent);
        root.transform.position = position;
        root.transform.rotation = (rotation == default) ? Quaternion.identity : rotation;
        root.transform.localScale = Vector3.one * scale;

        Color wallC    = new Color(0.9f, 0.88f, 0.84f);
        Color roofC    = new Color(0.85f, 0.24f, 0.18f);
        Color eaveC    = new Color(0.2f, 0.2f, 0.2f);
        Color floorC   = new Color(0.78f, 0.78f, 0.76f);
        Color frameC   = new Color(0.35f, 0.35f, 0.38f);
        Color counterC = new Color(0.45f, 0.55f, 0.6f);
        Color shelfC   = new Color(0.5f, 0.5f, 0.52f);
        Color signC    = new Color(0.85f, 0.24f, 0.18f);
        Color winC     = new Color(0.55f, 0.78f, 0.86f);

        float hw = 4f;
        float hd = 3.5f;
        float wallH = 4.5f;

        // ── Floor ──
        MakeBlock("Floor", root.transform, new Vector3(8f, 0.25f, 7f), new Vector3(0f, 0.125f, 0f), floorC);

        // ── Walls (entrance on +x, facing the road) ──
        MakeBlock("Wall", root.transform, new Vector3(0.5f, wallH, 7f), new Vector3(-hw, wallH / 2f, 0f), wallC);
        MakeBlock("Wall", root.transform, new Vector3(8f, wallH, 0.5f), new Vector3(0f, wallH / 2f, hd), wallC);
        MakeBlock("Wall", root.transform, new Vector3(8f, wallH, 0.5f), new Vector3(0f, wallH / 2f, -hd), wallC);
        MakeBlock("Wall", root.transform, new Vector3(0.5f, wallH, 2.7f), new Vector3(hw, wallH / 2f, -2.15f), wallC);
        MakeBlock("Wall", root.transform, new Vector3(0.5f, wallH, 2.7f), new Vector3(hw, wallH / 2f, 2.15f), wallC);
        MakeBlock("Transom", root.transform, new Vector3(0.5f, 1.3f, 1.6f), new Vector3(hw, wallH - 0.65f, 0f), wallC);
        MakeBlock("DoorFrame", root.transform, new Vector3(0.2f, 3.2f, 0.2f), new Vector3(hw, 1.6f, -0.8f), frameC);
        MakeBlock("DoorFrame", root.transform, new Vector3(0.2f, 3.2f, 0.2f), new Vector3(hw, 1.6f, 0.8f), frameC);

        // ── Flat roof with overhang ──
        MakeBlock("Roof", root.transform, new Vector3(9f, 0.5f, 8f), new Vector3(0f, wallH + 0.25f, 0f), roofC);
        MakeBlock("Eave", root.transform, new Vector3(9.4f, 0.2f, 8.4f), new Vector3(0f, wallH + 0.5f, 0f), eaveC, true);

        // ── Sign above the door ──
        MakeBlock("Sign", root.transform, new Vector3(0.25f, 0.9f, 5f), new Vector3(hw + 0.3f, 3.6f, 0f), signC, true);
        var storeSignLabel = new GameObject("StoreSignLabel");
        storeSignLabel.transform.SetParent(root.transform);
        storeSignLabel.transform.localPosition = new Vector3(hw + 0.5f, 3.6f, 0f);
        storeSignLabel.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);
        var storeSignTmp = storeSignLabel.AddComponent<TMPro.TextMeshPro>();
        storeSignTmp.text = "TIỆN LỢI";
        storeSignTmp.fontSize = 1.0f;
        storeSignTmp.alignment = TMPro.TextAlignmentOptions.Center;
        storeSignTmp.color = new Color(0.98f, 0.94f, 0.85f);
        storeSignTmp.outlineWidth = 0.18f;
        storeSignTmp.outlineColor = Color.black;
        storeSignTmp.rectTransform.sizeDelta = new Vector3(4.2f, 0.75f);

        // ── Window on the back wall ──
        MakeBlock("WinGlass", root.transform, new Vector3(0.12f, 1.4f, 2f), new Vector3(-hw - 0.02f, 2.3f, 0f), winC, true);

        // ── Counter inside ──
        MakeBlock("Counter", root.transform, new Vector3(2f, 1f, 4.5f), new Vector3(1.5f, 0.5f, 0f), counterC);
        MakeBlock("CounterTop", root.transform, new Vector3(2f, 0.08f, 4.7f), new Vector3(1.5f, 0.98f, 0f), new Color(0.7f, 0.75f, 0.8f), true);
        MakeBlock("Register", root.transform, new Vector3(0.5f, 0.4f, 0.5f), new Vector3(1.7f, 1.22f, 0f), frameC, true);

        // ── Shelf units flanking the back wall (4 tiers each) ──
        Color[] itemColors =
        {
            new Color(0.9f, 0.2f, 0.2f), new Color(0.2f, 0.6f, 0.3f),
            new Color(0.95f, 0.7f, 0.15f), new Color(0.5f, 0.4f, 0.8f)
        };
        foreach (float zc in new[] { -1.9f, 1.9f })
        {
            string tag = zc < 0 ? "L" : "R";
            MakeBlock("ShelfPost" + tag, root.transform, new Vector3(0.15f, 3.8f, 0.15f), new Vector3(-2.9f, 1.9f, zc - 1.5f), frameC);
            MakeBlock("ShelfPost" + tag, root.transform, new Vector3(0.15f, 3.8f, 0.15f), new Vector3(-2.9f, 1.9f, zc + 1.5f), frameC);
            for (int i = 0; i < 4; i++)
            {
                float sy = 0.5f + i * 0.9f;
                MakeBlock("ShelfBoard" + tag, root.transform, new Vector3(0.15f, 0.08f, 3.2f), new Vector3(-2.9f, sy, zc), shelfC);
                for (int k = 0; k < 3; k++)
                {
                    float iz = zc + (k - 1) * 0.9f;
                    MakeBlock("ShelfItem" + tag, root.transform, new Vector3(0.28f, 0.28f, 0.28f), new Vector3(-2.9f, sy + 0.2f, iz), itemColors[(i + k) % itemColors.Length]);
                }
            }
        }

        // ── Shopkeeper behind the counter ──
        BuildMarketNpc(root.transform, "ConvenienceNPC", new Vector3(1.5f, 1.13f, 0f), Quaternion.Euler(0f, 180f, 0f));

        return root;
    }

}

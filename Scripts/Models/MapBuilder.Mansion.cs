using UnityEngine;

public static partial class MapBuilder
{
    // ═══════════════════════════════════════════════════════════════
    //  RICH MAN MANSION
    // ═══════════════════════════════════════════════════════════════

    public static GameObject BuildRichManMansion(Transform parent, Vector3 position, float scale = 1f, Quaternion rotation = default)
    {
        var root = new GameObject("RichManMansion");
        root.transform.SetParent(parent);
        root.transform.position = position;
        root.transform.rotation = (rotation == default) ? Quaternion.identity : rotation;
        root.transform.localScale = Vector3.one * scale;

        Color wallC    = new Color(0.94f, 0.9f, 0.82f);
        Color trimC    = new Color(0.28f, 0.18f, 0.1f);
        Color roofC    = new Color(0.22f, 0.22f, 0.26f);
        Color goldC    = new Color(0.9f, 0.68f, 0.16f);
        Color stoneC   = new Color(0.65f, 0.62f, 0.58f);
        Color winC     = new Color(0.55f, 0.78f, 0.86f);
        Color leafC    = new Color(0.2f, 0.45f, 0.18f);
        Color waterC   = new Color(0.3f, 0.6f, 0.9f);
        Color sofaC    = new Color(0.45f, 0.28f, 0.18f);
        Color tableC   = new Color(0.55f, 0.35f, 0.15f);
        Color floorC   = new Color(0.72f, 0.58f, 0.38f);
        Color stairC   = new Color(0.6f, 0.48f, 0.32f);
        Color frameC   = new Color(0.4f, 0.28f, 0.14f);
        Color flowerDecorC = new Color(0.9f, 0.35f, 0.45f);
        Color amberC   = new Color(1f, 0.72f, 0.35f);
        Color rugC     = new Color(0.62f, 0.16f, 0.16f);
        Color bedC     = new Color(0.95f, 0.93f, 0.88f);
        Color stoveC   = new Color(0.12f, 0.12f, 0.12f);
        Color metalC   = new Color(0.55f, 0.55f, 0.57f);
        Color marbleC  = new Color(0.92f, 0.88f, 0.82f);
        Color wainC    = new Color(0.85f, 0.78f, 0.68f);

        float h1 = 5f, h2 = 4f;
        float halfW = 12f, halfD = 10f;
        float y1 = h1 / 2f;
        float y2 = h1 + 0.25f + h2 / 2f;
        float fullW = halfW * 2f;
        float fullD = halfD * 2f;

        // ── Foundation & floors ──
        MakeBlock("MansionFoundation", root.transform, new Vector3(halfW * 2f + 1.4f, 0.55f, halfD * 2f + 1.4f),
            new Vector3(0f, -0.28f, 0f), stoneC);
        MakeBlock("Floor1F", root.transform, new Vector3(halfW * 2f, 0.5f, halfD * 2f), Vector3.zero, stoneC);
        // 2F floor with stairwell opening above the staircase (+X side).
        // W/E main slabs + NS/SS landing strips flank the opening (x≈6.7-9.3, z≈-7.0..-0.7).
        // Strips are 0.46 thick and raised +0.01 so their faces clear the main-slabs on the 1-unit overlaps.
        MakeBlock("Floor2F_W", root.transform, new Vector3(18.8f, 0.5f, fullD),
            new Vector3(-2.6f, h1 + 0.25f, 0f), stoneC);
        MakeBlock("Floor2F_E", root.transform, new Vector3(2.8f, 0.5f, fullD),
            new Vector3(10.6f, h1 + 0.25f, 0f), stoneC);
        MakeBlock("Floor2F_NS", root.transform, new Vector3(2.6f, 0.46f, 3f),
            new Vector3(8f, h1 + 0.26f, -8.5f), stoneC);
        MakeBlock("Floor2F_SS", root.transform, new Vector3(2.6f, 0.46f, 10.7f),
            new Vector3(8f, h1 + 0.26f, 4.65f), stoneC);
        MakeBlock("Ceiling", root.transform, new Vector3(halfW * 2f, 0.3f, halfD * 2f),
            new Vector3(0f, h1 + h2 + 0.4f, 0f), stoneC);

        // ── 1F walls ──
        MakeBlock("Wall1F_Zneg", root.transform, new Vector3(halfW * 2f, h1, 0.5f), new Vector3(0f, y1, -halfD), wallC);
        MakeBlock("Wall1F_Zpos", root.transform, new Vector3(halfW * 2f, h1, 0.5f), new Vector3(0f, y1, halfD), wallC);
        MakeBlock("Wall1F_Xpos", root.transform, new Vector3(0.5f, h1, halfD * 2f), new Vector3(halfW, y1, 0f), wallC);
        MakeBlock("Wall1F_Xneg_N", root.transform, new Vector3(0.5f, h1, 7.8f), new Vector3(-halfW, y1, -6.1f), wallC);
        MakeBlock("Wall1F_Xneg_S", root.transform, new Vector3(0.5f, h1, 7.8f), new Vector3(-halfW, y1, 6.1f), wallC);

        // 1F windows (-Z, +Z, +X faces) — 3 per face for larger walls
        foreach (float wx in new[] { -7f, 0f, 7f })
        {
            MakeBlock("Win1F_Zneg", root.transform, new Vector3(1.8f, 1.8f, 0.14f), new Vector3(wx, 2f, -halfD - 0.03f), winC, true);
            MakeBlock("WinFrame1F_Zneg", root.transform, new Vector3(2f, 0.12f, 0.16f), new Vector3(wx, 1.12f, -halfD - 0.03f), trimC, true);
            MakeBlock("WinFrame1F_Zneg", root.transform, new Vector3(2f, 0.12f, 0.16f), new Vector3(wx, 2.94f, -halfD - 0.03f), trimC, true);
            MakeBlock("Win1F_Zpos", root.transform, new Vector3(1.8f, 1.8f, 0.14f), new Vector3(wx, 2f, halfD + 0.03f), winC, true);
            MakeBlock("WinFrame1F_Zpos", root.transform, new Vector3(2f, 0.12f, 0.16f), new Vector3(wx, 1.12f, halfD + 0.03f), trimC, true);
            MakeBlock("WinFrame1F_Zpos", root.transform, new Vector3(2f, 0.12f, 0.16f), new Vector3(wx, 2.94f, halfD + 0.03f), trimC, true);
            MakeBlock("Shutter1F_ZNL" + (wx < 0 ? "A" : wx == 0 ? "B" : "C"), root.transform, new Vector3(0.12f, 1.8f, 0.28f), new Vector3(wx - 1.06f, 2f, -halfD - 0.15f), trimC, true);
            MakeBlock("Shutter1F_ZNR" + (wx < 0 ? "A" : wx == 0 ? "B" : "C"), root.transform, new Vector3(0.12f, 1.8f, 0.28f), new Vector3(wx + 1.06f, 2f, -halfD - 0.15f), trimC, true);
            MakeBlock("Shutter1F_ZPL" + (wx < 0 ? "A" : wx == 0 ? "B" : "C"), root.transform, new Vector3(0.12f, 1.8f, 0.28f), new Vector3(wx - 1.06f, 2f, halfD + 0.15f), trimC, true);
            MakeBlock("Shutter1F_ZPR" + (wx < 0 ? "A" : wx == 0 ? "B" : "C"), root.transform, new Vector3(0.12f, 1.8f, 0.28f), new Vector3(wx + 1.06f, 2f, halfD + 0.15f), trimC, true);
        }
        foreach (float wz in new[] { -6f, 0f, 6f })
        {
            MakeBlock("Win1F_Xpos", root.transform, new Vector3(0.14f, 1.8f, 1.8f), new Vector3(halfW + 0.03f, 2f, wz), winC, true);
            MakeBlock("WinFrame1F_Xpos", root.transform, new Vector3(0.16f, 0.12f, 2f), new Vector3(halfW + 0.03f, 1.12f, wz), trimC, true);
            MakeBlock("WinFrame1F_Xpos", root.transform, new Vector3(0.16f, 0.12f, 2f), new Vector3(halfW + 0.03f, 2.94f, wz), trimC, true);
            if (wz != 0)
            {
                MakeBlock("Win1F_Xneg", root.transform, new Vector3(0.14f, 1.5f, 1.5f), new Vector3(-halfW - 0.03f, 2f, wz), winC, true);
                MakeBlock("WinFrame1F_Xneg", root.transform, new Vector3(0.16f, 0.12f, 1.7f), new Vector3(-halfW - 0.03f, 1.24f, wz), trimC, true);
                MakeBlock("WinFrame1F_Xneg", root.transform, new Vector3(0.16f, 0.12f, 1.7f), new Vector3(-halfW - 0.03f, 2.81f, wz), trimC, true);
            }
        }

        // ── Cornice band at the 1F/2F junction (all four sides) ──
        MakeBlock("CorniceZN", root.transform, new Vector3(fullW + 1.2f, 0.3f, 0.55f), new Vector3(0f, 5.55f, -halfD - 0.18f), stoneC);
        MakeBlock("CorniceZNGold", root.transform, new Vector3(fullW + 1.2f, 0.1f, 0.6f), new Vector3(0f, 5.72f, -halfD - 0.18f), goldC, true);
        MakeBlock("CorniceZP", root.transform, new Vector3(fullW + 1.2f, 0.3f, 0.55f), new Vector3(0f, 5.55f, halfD + 0.18f), stoneC);
        MakeBlock("CorniceZPGold", root.transform, new Vector3(fullW + 1.2f, 0.1f, 0.6f), new Vector3(0f, 5.72f, halfD + 0.18f), goldC, true);
        MakeBlock("CorniceXN", root.transform, new Vector3(0.55f, 0.3f, fullD + 0.4f), new Vector3(-halfW + 0.05f, 5.55f, 0f), stoneC);
        MakeBlock("CorniceXNGold", root.transform, new Vector3(0.6f, 0.1f, fullD + 0.4f), new Vector3(-halfW + 0.05f, 5.72f, 0f), goldC, true);
        MakeBlock("CorniceXP", root.transform, new Vector3(0.55f, 0.3f, fullD + 0.4f), new Vector3(halfW - 0.05f, 5.55f, 0f), stoneC);
        MakeBlock("CorniceXPGold", root.transform, new Vector3(0.6f, 0.1f, fullD + 0.4f), new Vector3(halfW - 0.05f, 5.72f, 0f), goldC, true);

        // ── Entrance on -X side (double door + gold frame, interactive) ──
        MakeBlock("DoorFrameL", root.transform, new Vector3(0.4f, 4.5f, 0.4f), new Vector3(-halfW - 0.1f, 2.25f, -1.9f), goldC);
        MakeBlock("DoorFrameR", root.transform, new Vector3(0.4f, 4.5f, 0.4f), new Vector3(-halfW - 0.1f, 2.25f, 1.9f), goldC);
        MakeBlock("DoorLintel", root.transform, new Vector3(0.4f, 0.5f, 4.4f), new Vector3(-halfW - 0.1f, 4.75f, 0f), goldC);
        // Left door — pivot at left jamb, swings open
        {
            var doorL = new GameObject("Door");
            doorL.transform.SetParent(root.transform);
            doorL.transform.localPosition = new Vector3(-halfW - 0.2f, 0f, -0.95f);
            doorL.transform.localRotation = Quaternion.identity;
            MakeBlock("DoorVisual", doorL.transform, new Vector3(0.14f, 3.6f, 1.7f),
                new Vector3(0f, 2.25f, 0.09f), goldC, true);
            var doorPanelL = MakeBlock("DoorPanel", doorL.transform, new Vector3(0.14f, 3.6f, 1.7f),
                new Vector3(0f, 2.25f, 0.09f), goldC);
            doorPanelL.AddComponent<BoxCollider>();
        }
        // Right door — pivot at right jamb, swings open
        {
            var doorR = new GameObject("Door");
            doorR.transform.SetParent(root.transform);
            doorR.transform.localPosition = new Vector3(-halfW - 0.2f, 0f, 0.95f);
            doorR.transform.localRotation = Quaternion.identity;
            MakeBlock("DoorVisual", doorR.transform, new Vector3(0.14f, 3.6f, 1.7f),
                new Vector3(0f, 2.25f, -0.09f), goldC, true);
            var doorPanelR = MakeBlock("DoorPanel", doorR.transform, new Vector3(0.14f, 3.6f, 1.7f),
                new Vector3(0f, 2.25f, -0.09f), goldC);
            doorPanelR.AddComponent<BoxCollider>();
        }
        MakeBlock("DoorKnobL", root.transform, new Vector3(0.16f, 0.16f, 0.16f), new Vector3(-halfW - 0.28f, 2.2f, -0.55f), trimC, true);
        MakeBlock("DoorKnobR", root.transform, new Vector3(0.16f, 0.16f, 0.16f), new Vector3(-halfW - 0.28f, 2.2f, 0.55f), trimC, true);
        MakeBlock("DoorStep", root.transform, new Vector3(7f, 0.3f, 1f), new Vector3(-halfW - 0.5f, 0.15f, 0f), stoneC);
        MakeBlock("DoorStepGold", root.transform, new Vector3(7f, 0.08f, 0.14f), new Vector3(-halfW - 0.52f, 0.42f, -0.5f), goldC, true);
        MakeBlock("DoorStep2", root.transform, new Vector3(5f, 0.25f, 0.65f), new Vector3(-halfW - 1f, 0.125f, 0f), stoneC);
        MakeBlock("DoorStep3", root.transform, new Vector3(3.4f, 0.2f, 0.45f), new Vector3(-halfW - 1.45f, 0.1f, 0f), stoneC);

        // ── 2F walls ──
        MakeBlock("Wall2F_Zneg", root.transform, new Vector3(halfW * 2f, h2, 0.5f), new Vector3(0f, y2, -halfD), wallC);
        MakeBlock("Wall2F_Zpos", root.transform, new Vector3(halfW * 2f, h2, 0.5f), new Vector3(0f, y2, halfD), wallC);
        MakeBlock("Wall2F_Xpos", root.transform, new Vector3(0.5f, h2, halfD * 2f), new Vector3(halfW, y2, 0f), wallC);
        MakeBlock("Wall2F_Xneg", root.transform, new Vector3(0.5f, h2, halfD * 2f), new Vector3(-halfW, y2, 0f), wallC);

        // 2F windows — 3 per face for larger walls
        foreach (float wx in new[] { -7f, 0f, 7f })
        {
            MakeBlock("Win2F_Zneg", root.transform, new Vector3(1.8f, 1.4f, 0.14f), new Vector3(wx, y2, -halfD - 0.03f), winC, true);
            MakeBlock("Win2F_Zpos", root.transform, new Vector3(1.8f, 1.4f, 0.14f), new Vector3(wx, y2, halfD + 0.03f), winC, true);
        }
        foreach (float wz in new[] { -6f, 0f, 6f })
        {
            MakeBlock("Win2F_Xpos", root.transform, new Vector3(0.14f, 1.4f, 1.8f), new Vector3(halfW + 0.03f, y2, wz), winC, true);
            MakeBlock("Win2F_Xneg", root.transform, new Vector3(0.14f, 1.4f, 1.8f), new Vector3(-halfW - 0.03f, y2, wz), winC, true);
        }

        // 2F window trims
        foreach (float wx in new[] { -7f, 0f, 7f })
        {
            AddWindowTrim(root.transform, new Vector3(wx, y2, -halfD - 0.05f), 1.8f, 1.4f, -Vector3.forward, trimC, stoneC, "2F_ZN_" + wx);
            AddWindowTrim(root.transform, new Vector3(wx, y2, halfD + 0.05f), 1.8f, 1.4f, Vector3.forward, trimC, stoneC, "2F_ZP_" + wx);
        }
        foreach (float wz in new[] { -6f, 0f, 6f })
        {
            string tag = wz < -2 ? "A" : wz > 2 ? "C" : "B";
            MakeBlock("WinSill2F_XP" + tag, root.transform, new Vector3(0.45f, 0.12f, 2f), new Vector3(halfW + 0.02f, y2 - 0.76f, wz), stoneC, true);
            MakeBlock("WinHead2F_XP" + tag, root.transform, new Vector3(0.45f, 0.16f, 2f), new Vector3(halfW + 0.02f, y2 + 0.78f, wz), stoneC, true);
            MakeBlock("Shutter2F_XPL" + tag, root.transform, new Vector3(0.12f, 1.4f, 0.28f), new Vector3(halfW + 0.15f, y2, wz - 1.06f), trimC, true);
            MakeBlock("Shutter2F_XPR" + tag, root.transform, new Vector3(0.12f, 1.4f, 0.28f), new Vector3(halfW + 0.15f, y2, wz + 1.06f), trimC, true);
            MakeBlock("WinSill2F_XN" + tag, root.transform, new Vector3(0.45f, 0.12f, 2f), new Vector3(-halfW - 0.02f, y2 - 0.76f, wz), stoneC, true);
            MakeBlock("WinHead2F_XN" + tag, root.transform, new Vector3(0.45f, 0.16f, 2f), new Vector3(-halfW - 0.02f, y2 + 0.78f, wz), stoneC, true);
            MakeBlock("Shutter2F_XNL" + tag, root.transform, new Vector3(0.12f, 1.4f, 0.28f), new Vector3(-halfW - 0.15f, y2, wz - 1.06f), trimC, true);
            MakeBlock("Shutter2F_XNR" + tag, root.transform, new Vector3(0.12f, 1.4f, 0.28f), new Vector3(-halfW - 0.15f, y2, wz + 1.06f), trimC, true);
        }

        // ── Entrance-face 2F window (above the double door, gold frame + pediment) ──
        MakeBlock("Win2F_Entr", root.transform, new Vector3(0.14f, 2.6f, 1.9f), new Vector3(-halfW - 0.27f, 7.2f, 0f), winC, true);
        MakeBlock("Win2F_EntrJambL", root.transform, new Vector3(0.18f, 3f, 0.28f), new Vector3(-halfW - 0.15f, 7.2f, -1f), goldC);
        MakeBlock("Win2F_EntrJambR", root.transform, new Vector3(0.18f, 3f, 0.28f), new Vector3(-halfW - 0.15f, 7.2f, 1f), goldC);
        MakeBlock("Win2F_EntrHead", root.transform, new Vector3(0.18f, 0.28f, 2.4f), new Vector3(-halfW - 0.15f, 8.72f, 0f), goldC);
        MakeBlock("Win2F_EntrSill", root.transform, new Vector3(0.34f, 0.22f, 2.3f), new Vector3(-halfW - 0.3f, 5.6f, 0f), goldC);
        MakeBlock("Win2F_EntrPedBase", root.transform, new Vector3(0.2f, 0.22f, 2.7f), new Vector3(-halfW - 0.12f, 8.95f, 0f), goldC);
        MakeBlock("Win2F_EntrPed1", root.transform, new Vector3(0.2f, 0.3f, 2f), new Vector3(-halfW - 0.14f, 9.15f, 0f), goldC);
        MakeBlock("Win2F_EntrPed2", root.transform, new Vector3(0.2f, 0.3f, 1.3f), new Vector3(-halfW - 0.14f, 9.5f, 0f), goldC);
        MakeBlock("Win2F_EntrPed3", root.transform, new Vector3(0.2f, 0.3f, 0.6f), new Vector3(-halfW - 0.14f, 9.85f, 0f), goldC);
        MakeBlock("Win2F_EntrFinial", root.transform, new Vector3(0.22f, 0.25f, 0.22f), new Vector3(-halfW - 0.14f, 10.12f, 0f), goldC, true);

        // ── Balcony balustrade on the entrance face (wider for larger building) ──
        MakeBlock("BalconySlab", root.transform, new Vector3(0.6f, 0.2f, 8f), new Vector3(-halfW - 0.32f, 5.85f, 0f), stoneC);
        MakeBlock("BalconyBase", root.transform, new Vector3(0.34f, 0.1f, 8f), new Vector3(-halfW - 0.4f, 5.72f, 0f), stoneC);
        MakeBlock("BalconyRailBot", root.transform, new Vector3(0.16f, 0.12f, 7.8f), new Vector3(-halfW - 0.48f, 6.02f, 0f), trimC, true);
        MakeBlock("BalconyRailTop", root.transform, new Vector3(0.16f, 0.14f, 7.8f), new Vector3(-halfW - 0.5f, 6.66f, 0f), goldC, true);
        MakeBlock("BalconyRailGold", root.transform, new Vector3(0.2f, 0.08f, 7.8f), new Vector3(-halfW - 0.52f, 6.76f, 0f), goldC, true);
        for (int b = 0; b < 11; b++)
        {
            float bz = -3.5f + b * 0.7f;
            MakeBlock("Baluster" + b, root.transform, new Vector3(0.12f, 0.72f, 0.12f), new Vector3(-halfW - 0.5f, 6.36f, bz), trimC, true);
        }

        MakeBlock("GoldTrimLine", root.transform, new Vector3(halfW * 2f + 0.1f, 0.18f, 0.3f), new Vector3(0f, h1 + 0.25f, -halfD - 0.28f), goldC, true);
        MakeBlock("GoldTrimLine", root.transform, new Vector3(halfW * 2f + 0.1f, 0.18f, 0.3f), new Vector3(0f, h1 + 0.25f, halfD + 0.28f), goldC, true);

        // ── Portico (columns + pediment) on -X side (wider for 24-wide building) ──
        MakeBlock("PorchSlab", root.transform, new Vector3(9f, 0.3f, 12f), new Vector3(-halfW - 1.2f, 0.15f, 0f), stoneC);
        MakeBlock("PorchColumnL", root.transform, new Vector3(0.55f, h1 + h2 + 0.9f, 0.55f), new Vector3(-halfW - 1.6f, (h1 + h2 + 0.9f) / 2f, -4.5f), wallC);
        MakeBlock("PorchColumnR", root.transform, new Vector3(0.55f, h1 + h2 + 0.9f, 0.55f), new Vector3(-halfW - 1.6f, (h1 + h2 + 0.9f) / 2f, 4.5f), wallC);
        MakeBlock("ColumnCapL", root.transform, new Vector3(0.85f, 0.25f, 0.85f), new Vector3(-halfW - 1.6f, h1 + h2 + 0.9f, -4.5f), goldC, true);
        MakeBlock("ColumnCapR", root.transform, new Vector3(0.85f, 0.25f, 0.85f), new Vector3(-halfW - 1.6f, h1 + h2 + 0.9f, 4.5f), goldC, true);
        MakeBlock("PorchRoof", root.transform, new Vector3(8f, 0.35f, 11.5f), new Vector3(-halfW - 1.6f, h1 + h2 + 1.1f, 0f), roofC);
        MakeBlock("PorchRoofGold", root.transform, new Vector3(8.4f, 0.2f, 11.9f), new Vector3(-halfW - 1.6f, h1 + h2 + 1.28f, 0f), goldC, true);

        // ══════════════════════════════════════════════════════════
        // ── INTERIOR WALLS — Multiple rooms per floor ──
        // ══════════════════════════════════════════════════════════

        // ── 1F Interior walls ──
        // Foyer/Master-Hall divider (x = -8, Z-span with door gap at z = -1..1)
        MakeBlock("Wall1F_FoyerN", root.transform, new Vector3(0.35f, h1, 4f), new Vector3(-8f, y1, -7f), wallC);
        MakeBlock("Wall1F_FoyerS", root.transform, new Vector3(0.35f, h1, 4f), new Vector3(-8f, y1, 7f), wallC);
        // Library wall (z = -3, X-span from x = -8 to +9 with door gap at x = -1..1)
        MakeBlock("Wall1F_LibW", root.transform, new Vector3(7f, h1, 0.35f), new Vector3(-4.5f, y1, -3f), wallC);
        MakeBlock("Wall1F_LibE", root.transform, new Vector3(7f, h1, 0.35f), new Vector3(5.5f, y1, -3f), wallC);
        // Kitchen/Dining wall (z = 3, X-span from x = -8 to +9 with door gap)
        MakeBlock("Wall1F_KitW", root.transform, new Vector3(7f, h1, 0.35f), new Vector3(-4.5f, y1, 3f), wallC);
        MakeBlock("Wall1F_KitE", root.transform, new Vector3(7f, h1, 0.35f), new Vector3(5.5f, y1, 3f), wallC);
        // Staircase wall (x = 9.5, Z-span from z = -10 to -5 with door gap)
        MakeBlock("Wall1F_StairS", root.transform, new Vector3(0.35f, h1, 3.5f), new Vector3(9.5f, y1, -8.25f), wallC);
        MakeBlock("Wall1F_StairN", root.transform, new Vector3(0.35f, h1, 3.5f), new Vector3(9.5f, y1, 8.25f), wallC);

        // ── 1F Wainscoting panels (decorative trim on lower walls) ──
        foreach (float wx in new[] { -7f, 0f, 7f })
        {
            MakeBlock("Wainscot_ZN", root.transform, new Vector3(2f, 1.2f, 0.08f), new Vector3(wx, 0.65f, -halfD + 0.25f), wainC, true);
            MakeBlock("Wainscot_ZP", root.transform, new Vector3(2f, 1.2f, 0.08f), new Vector3(wx, 0.65f, halfD - 0.25f), wainC, true);
        }
        foreach (float wz in new[] { -6f, 0f, 6f })
        {
            MakeBlock("Wainscot_XP", root.transform, new Vector3(0.08f, 1.2f, 2f), new Vector3(halfW - 0.25f, 0.65f, wz), wainC, true);
            if (wz != 0)
            {
                MakeBlock("Wainscot_XN", root.transform, new Vector3(0.08f, 1.2f, 2f), new Vector3(-halfW + 0.25f, 0.65f, wz), wainC, true);
            }
        }
        // ── 1F Crown molding (decorative trim at ceiling) ──
        MakeBlock("CrownMold_ZN", root.transform, new Vector3(fullW, 0.15f, 0.2f), new Vector3(0f, h1 - 0.1f, -halfD + 0.2f), goldC, true);
        MakeBlock("CrownMold_ZP", root.transform, new Vector3(fullW, 0.15f, 0.2f), new Vector3(0f, h1 - 0.1f, halfD - 0.2f), goldC, true);
        MakeBlock("CrownMold_XN", root.transform, new Vector3(0.2f, 0.15f, fullD), new Vector3(-halfW + 0.2f, h1 - 0.1f, 0f), goldC, true);
        MakeBlock("CrownMold_XP", root.transform, new Vector3(0.2f, 0.15f, fullD), new Vector3(halfW - 0.2f, h1 - 0.1f, 0f), goldC, true);

        // ── 1F Staircase (against +X back wall, within staircase hall) ──
        for (int i = 0; i < 17; i++)
        {
            float sy = 0.25f + (i + 1) * 0.3f - 0.05f;
            float sz = -1.4f - i * 0.3f;
            float th = 0.1f;
            if (i == 0) { th = 0.3f; sy = 0.4f; }
            MakeBlock("Stair", root.transform, new Vector3(2.5f, th, 0.55f),
                new Vector3(8f, sy, sz), stairC, false);
        }
        {
            var ramp = new GameObject("StairRamp");
            ramp.transform.SetParent(root.transform);
            ramp.transform.localPosition = Vector3.zero;
            ramp.SetActive(true);
            var mf = ramp.AddComponent<MeshFilter>();
            var mc = ramp.AddComponent<MeshCollider>();
            var mesh = new Mesh { name = "StairRampMesh" };
            float rampW = 1.2f;
            float rampX = 8f;
            float rampFrontZ = -0.9f;
            float rampBackZ = -6.7f;
            float rampBottomY = 0.05f;
            float rampTopY = 5.5f;
            Vector3[] verts = new Vector3[]
            {
                new Vector3(-rampW, 0, 0),
                new Vector3( rampW, 0, 0),
                new Vector3(-rampW, 0, rampBackZ - rampFrontZ),
                new Vector3( rampW, 0, rampBackZ - rampFrontZ),
                new Vector3(-rampW, rampTopY - rampBottomY, rampBackZ - rampFrontZ),
                new Vector3( rampW, rampTopY - rampBottomY, rampBackZ - rampFrontZ),
            };
            int[] tris = new int[]
            {
                0,3,1, 0,2,3,
                2,5,4, 2,3,5,
                0,4,5, 0,5,1,
                0,2,4, 1,5,3,
            };
            mesh.vertices = verts;
            mesh.triangles = tris;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            mf.mesh = mesh;
            mc.sharedMesh = mesh;
            mc.convex = true;
            ramp.transform.localPosition = new Vector3(rampX, rampBottomY, rampFrontZ);
            var mr = ramp.GetComponent<MeshRenderer>();
            if (mr != null) Object.Destroy(mr);
        }
        // Staircase railing
        MakeBlock("StairRail", root.transform, new Vector3(0.08f, 1.2f, 5.5f),
            new Vector3(6.6f, 3f, -3.5f), frameC, true);
        MakeBlock("StairRailCap", root.transform, new Vector3(0.12f, 0.08f, 5.5f),
            new Vector3(6.6f, 3.65f, -3.5f), goldC, true);

        // ── Foyer columns (interior decorative pillars flanking the entrance hall) ──
        foreach (float cz in new[] { -3f, 3f })
        {
            MakeBlock("FoyerColumn", root.transform, new Vector3(0.5f, h1, 0.5f),
                new Vector3(-8f, y1, cz), wallC);
            MakeBlock("FoyerColBase", root.transform, new Vector3(0.7f, 0.2f, 0.7f),
                new Vector3(-8f, 0.1f, cz), stoneC, true);
            MakeBlock("FoyerColCap", root.transform, new Vector3(0.7f, 0.2f, 0.7f),
                new Vector3(-8f, h1 - 0.1f, cz), goldC, true);
        }

        // ── 1F Foyer (x=-12 to -8, grand entrance hall) ──
        MakeBlock("FoyerFloor", root.transform, new Vector3(fullW * 0.35f, 0.06f, 4f),
            new Vector3(-10f, 0.03f, 0f), marbleC, true);
        MakeBlock("FoyerRug", root.transform, new Vector3(3f, 0.04f, 3f),
            new Vector3(-10f, 0.04f, 0f), rugC, true);
        MakeBlock("FoyerChandelierCord", root.transform, new Vector3(0.06f, 0.5f, 0.06f),
            new Vector3(-10f, 4.95f, 0f), goldC, true);
        var foyerChand = MakeBlock("FoyerChandelier", root.transform, new Vector3(1.8f, 0.25f, 1.8f),
            new Vector3(-10f, 4.6f, 0f), goldC, true);
        foyerChand.GetComponent<MeshRenderer>().enabled = false;
        var foyerGlow = MakeBlock("FoyerGlow", root.transform, new Vector3(1f, 0.3f, 1f),
            new Vector3(-10f, 4.4f, 0f), amberC, true);
        DisableShadowCasting(foyerGlow);
        AddGlowLight(root.transform, new Vector3(-10f, 4.15f, 0f), 10f, 2f, new Color(1f, 0.9f, 0.7f));

        // ── 1F Library (south room: x=-8 to +9, z=-10 to -3) ──
        MakeBlock("LibRug", root.transform, new Vector3(5f, 0.05f, 4f),
            new Vector3(-2f, 0.02f, -7f), rugC, true);
        // Sofa against -Z wall
        MakeBlock("Sofa", root.transform, new Vector3(3.5f, 0.35f, 1.2f),
            new Vector3(-2f, 0.425f, -8.5f), sofaC);
        MakeBlock("SofaBack", root.transform, new Vector3(3.5f, 0.75f, 0.2f),
            new Vector3(-2f, 0.85f, -9.1f), sofaC);
        MakeBlock("SofaArmL", root.transform, new Vector3(0.25f, 0.55f, 1.2f),
            new Vector3(-3.625f, 0.625f, -8.5f), sofaC);
        MakeBlock("SofaArmR", root.transform, new Vector3(0.25f, 0.55f, 1.2f),
            new Vector3(-0.375f, 0.625f, -8.5f), sofaC);
        MakeBlock("SofaCushion", root.transform, new Vector3(1.3f, 0.12f, 0.9f),
            new Vector3(-2.5f, 0.66f, -8.5f), new Color(0.55f, 0.7f, 0.78f), true);
        MakeBlock("SofaCushion", root.transform, new Vector3(1.3f, 0.12f, 0.9f),
            new Vector3(-1.5f, 0.66f, -8.5f), new Color(0.5f, 0.66f, 0.75f), true);
        // Coffee table
        MakeBlock("TableTop", root.transform, new Vector3(1.5f, 0.08f, 0.85f),
            new Vector3(-2f, 0.58f, -7f), tableC);
        MakeBlock("TableLeg", root.transform, new Vector3(0.08f, 0.45f, 0.08f),
            new Vector3(-2.7f, 0.28f, -7.35f), frameC, true);
        MakeBlock("TableLeg", root.transform, new Vector3(0.08f, 0.45f, 0.08f),
            new Vector3(-1.3f, 0.28f, -7.35f), frameC, true);
        MakeBlock("TableLeg", root.transform, new Vector3(0.08f, 0.45f, 0.08f),
            new Vector3(-2.7f, 0.28f, -6.65f), frameC, true);
        MakeBlock("TableLeg", root.transform, new Vector3(0.08f, 0.45f, 0.08f),
            new Vector3(-1.3f, 0.28f, -6.65f), frameC, true);
        // Armchair
        MakeBlock("Armchair", root.transform, new Vector3(1f, 0.35f, 1f),
            new Vector3(2f, 0.425f, -7.5f), wallC);
        MakeBlock("ArmchairBack", root.transform, new Vector3(1f, 0.7f, 0.16f),
            new Vector3(2f, 0.8f, -8.35f), wallC);
        // Floor lamp
        MakeBlock("LampPole", root.transform, new Vector3(0.06f, 1.5f, 0.06f),
            new Vector3(-7.2f, 0.8f, -7f), frameC, true);
        MakeBlock("LampBase", root.transform, new Vector3(0.3f, 0.08f, 0.3f),
            new Vector3(-7.2f, 0.29f, -7f), frameC, true);
        var lampShade1F = MakeBlock("LampShade", root.transform, new Vector3(0.4f, 0.28f, 0.4f),
            new Vector3(-7.2f, 1.7f, -7f), goldC, true);
        lampShade1F.GetComponent<MeshRenderer>().enabled = false;
        var lampGlow1F = MakeBlock("LampGlow", root.transform, new Vector3(0.22f, 0.16f, 0.22f),
            new Vector3(-7.2f, 1.5f, -7f), amberC, true);
        DisableShadowCasting(lampGlow1F);
        AddGlowLight(root.transform, new Vector3(-7.2f, 1.32f, -7f), 6f, 1.2f, new Color(1f, 0.85f, 0.6f));
        // Bookshelf on -Z wall (library)
        MakeBlock("Bookshelf", root.transform, new Vector3(3f, 2.8f, 0.55f),
            new Vector3(6f, 1.9f, -9.45f), frameC);
        MakeBlock("BookRow1", root.transform, new Vector3(2.6f, 0.12f, 0.4f),
            new Vector3(6f, 0.9f, -9.45f), rugC, true);
        MakeBlock("BookRow2", root.transform, new Vector3(2.6f, 0.12f, 0.4f),
            new Vector3(6f, 1.8f, -9.45f), new Color(0.2f, 0.4f, 0.6f), true);
        MakeBlock("BookRow3", root.transform, new Vector3(2.6f, 0.12f, 0.4f),
            new Vector3(6f, 2.7f, -9.45f), new Color(0.6f, 0.3f, 0.2f), true);
        // Wall shelf
        MakeBlock("WallShelf", root.transform, new Vector3(2.5f, 0.12f, 0.6f),
            new Vector3(-5f, 3.4f, -9.45f), frameC, true);
        // Plant
        MakeBlock("PlantPot", root.transform, new Vector3(0.55f, 0.55f, 0.55f),
            new Vector3(8f, 0.52f, -8.5f), frameC);
        MakeBlock("PlantLeaf", root.transform, new Vector3(1f, 0.9f, 1f),
            new Vector3(8f, 1.15f, -8.5f), leafC, true);
        MakeBlock("PlantFlower", root.transform, new Vector3(0.3f, 0.3f, 0.3f),
            new Vector3(8f, 1.6f, -8.3f), flowerDecorC, true);

        // ── 1F Fireplace in library (south wall center) ──
        MakeBlock("FireplaceMantle", root.transform, new Vector3(2.5f, 0.35f, 0.8f),
            new Vector3(-2f, 1.8f, -9.5f), stoneC);
        MakeBlock("FireplaceOpening", root.transform, new Vector3(1.5f, 1.2f, 0.3f),
            new Vector3(-2f, 0.8f, -9.7f), new Color(0.08f, 0.08f, 0.08f));
        MakeBlock("FireplaceHearth", root.transform, new Vector3(2f, 0.1f, 0.6f),
            new Vector3(-2f, 0.05f, -9.8f), stoneC);
        MakeBlock("FireplaceMantelDecor", root.transform, new Vector3(1.8f, 0.08f, 0.25f),
            new Vector3(-2f, 2.02f, -9.6f), goldC, true);

        // ── 1F Main Hall / Living-Dining (center: x=-8 to +9, z=-3 to +3) ──
        MakeBlock("MainHallRug", root.transform, new Vector3(6f, 0.05f, 4f),
            new Vector3(0f, 0.02f, 0f), rugC, true);
        // Dining table (center of main hall)
        MakeBlock("DiningTable", root.transform, new Vector3(2.2f, 0.1f, 1.3f),
            new Vector3(0f, 0.8f, 0f), tableC);
        MakeBlock("DiningTableLeg", root.transform, new Vector3(0.1f, 0.45f, 0.1f),
            new Vector3(-0.95f, 0.48f, -0.62f), frameC, true);
        MakeBlock("DiningTableLeg", root.transform, new Vector3(0.1f, 0.45f, 0.1f),
            new Vector3(0.95f, 0.48f, -0.62f), frameC, true);
        MakeBlock("DiningTableLeg", root.transform, new Vector3(0.1f, 0.45f, 0.1f),
            new Vector3(-0.95f, 0.48f, 0.62f), frameC, true);
        MakeBlock("DiningTableLeg", root.transform, new Vector3(0.1f, 0.45f, 0.1f),
            new Vector3(0.95f, 0.48f, 0.62f), frameC, true);
        // 6 chairs around table
        MakeBlock("DiningChair1", root.transform, new Vector3(0.6f, 0.6f, 0.6f),
            new Vector3(-2.2f, 0.4f, 0f), tableC);
        MakeBlock("DiningChairBack1", root.transform, new Vector3(0.08f, 0.55f, 0.6f),
            new Vector3(-2.52f, 0.73f, 0f), tableC, true);
        MakeBlock("DiningChair2", root.transform, new Vector3(0.6f, 0.6f, 0.6f),
            new Vector3(2.2f, 0.4f, 0f), tableC);
        MakeBlock("DiningChairBack2", root.transform, new Vector3(0.08f, 0.55f, 0.6f),
            new Vector3(2.52f, 0.73f, 0f), tableC, true);
        MakeBlock("DiningChair3", root.transform, new Vector3(0.6f, 0.6f, 0.6f),
            new Vector3(0f, 0.4f, -1.3f), tableC);
        MakeBlock("DiningChairBack3", root.transform, new Vector3(0.6f, 0.55f, 0.08f),
            new Vector3(0f, 0.73f, -1.62f), tableC, true);
        MakeBlock("DiningChair4", root.transform, new Vector3(0.6f, 0.6f, 0.6f),
            new Vector3(0f, 0.4f, 1.3f), tableC);
        MakeBlock("DiningChairBack4", root.transform, new Vector3(0.6f, 0.55f, 0.08f),
            new Vector3(0f, 0.73f, 1.62f), tableC, true);
        MakeBlock("DiningChair5", root.transform, new Vector3(0.6f, 0.6f, 0.6f),
            new Vector3(-1.5f, 0.4f, 1.1f), tableC);
        MakeBlock("DiningChairBack5", root.transform, new Vector3(0.6f, 0.55f, 0.08f),
            new Vector3(-1.5f, 0.73f, 1.42f), tableC, true);
        MakeBlock("DiningChair6", root.transform, new Vector3(0.6f, 0.6f, 0.6f),
            new Vector3(1.5f, 0.4f, -1.1f), tableC);
        MakeBlock("DiningChairBack6", root.transform, new Vector3(0.6f, 0.55f, 0.08f),
            new Vector3(1.5f, 0.73f, -1.42f), tableC, true);
        // Dining chandelier
        MakeBlock("DiningChandCord", root.transform, new Vector3(0.06f, 0.6f, 0.06f),
            new Vector3(0f, 4.9f, 0f), frameC, true);
        var diningChand = MakeBlock("DiningChandelier", root.transform, new Vector3(1.4f, 0.25f, 1.4f),
            new Vector3(0f, 4.55f, 0f), goldC, true);
        diningChand.GetComponent<MeshRenderer>().enabled = false;
        var diningGlow = MakeBlock("DiningGlow", root.transform, new Vector3(0.8f, 0.2f, 0.8f),
            new Vector3(0f, 4.38f, 0f), amberC, true);
        DisableShadowCasting(diningGlow);
        AddGlowLight(root.transform, new Vector3(0f, 4.2f, 0f), 8f, 1.8f, new Color(1f, 0.85f, 0.6f));

        // ── 1F Kitchen (north room: x=-8 to +9, z=+3 to +10) ──
        MakeBlock("KitchenRug", root.transform, new Vector3(4f, 0.04f, 2f),
            new Vector3(0f, 0.02f, 7f), rugC, true);
        MakeBlock("KitchenCounter", root.transform, new Vector3(5f, 0.9f, 1f),
            new Vector3(0f, 0.7f, 8.5f), frameC);
        MakeBlock("CounterTop", root.transform, new Vector3(5.1f, 0.08f, 1.1f),
            new Vector3(0f, 1.2f, 8.5f), new Color(0.75f, 0.62f, 0.45f), true);
        MakeBlock("Sink", root.transform, new Vector3(0.9f, 0.12f, 0.6f),
            new Vector3(-1.5f, 1.28f, 8.5f), metalC, true);
        MakeBlock("SinkFaucet", root.transform, new Vector3(0.06f, 0.28f, 0.06f),
            new Vector3(-1.5f, 1.43f, 8.25f), metalC, true);
        MakeBlock("SinkFaucetArc", root.transform, new Vector3(0.06f, 0.06f, 0.22f),
            new Vector3(-1.5f, 1.56f, 8.36f), metalC, true);
        MakeBlock("Stove", root.transform, new Vector3(1.8f, 0.9f, 1f),
            new Vector3(2f, 0.7f, 8.5f), stoveC);
        MakeBlock("StoveBurner", root.transform, new Vector3(0.5f, 0.06f, 0.5f),
            new Vector3(1.5f, 1.2f, 8.2f), metalC, true);
        MakeBlock("StoveBurner", root.transform, new Vector3(0.5f, 0.06f, 0.5f),
            new Vector3(1.5f, 1.2f, 8.8f), metalC, true);
        MakeBlock("StoveDoor", root.transform, new Vector3(1.3f, 0.4f, 0.06f),
            new Vector3(2f, 0.55f, 9.02f), new Color(0.18f, 0.18f, 0.2f), true);
        MakeBlock("Fridge", root.transform, new Vector3(1.2f, 2f, 1f),
            new Vector3(5f, 1.25f, 8.5f), new Color(0.85f, 0.85f, 0.88f));
        MakeBlock("FridgeHandle", root.transform, new Vector3(0.06f, 0.55f, 0.08f),
            new Vector3(4.4f, 1.55f, 8f), metalC, true);
        MakeBlock("Microwave", root.transform, new Vector3(0.8f, 0.4f, 0.5f),
            new Vector3(-3f, 1.5f, 8.5f), new Color(0.25f, 0.25f, 0.28f), true);
        MakeBlock("MicrowaveWindow", root.transform, new Vector3(0.5f, 0.22f, 0.06f),
            new Vector3(-3f, 1.5f, 8.24f), new Color(0.15f, 0.18f, 0.25f), true);
        MakeBlock("Kettle", root.transform, new Vector3(0.3f, 0.35f, 0.3f),
            new Vector3(2.5f, 1.38f, 8.2f), metalC, true);
        MakeBlock("KettleHandle", root.transform, new Vector3(0.06f, 0.2f, 0.06f),
            new Vector3(2.5f, 1.6f, 8.1f), new Color(0.18f, 0.18f, 0.18f), true);
        // Upper cabinets
        for (int c = 0; c < 4; c++)
        {
            float cx = -4f + c * 2f;
            MakeBlock("UpperCab" + c, root.transform, new Vector3(1.6f, 0.9f, 0.55f),
                new Vector3(cx, 3f, 9.45f), frameC, true);
            MakeBlock("UpperCabDoor" + c + "L", root.transform, new Vector3(0.7f, 0.8f, 0.06f),
                new Vector3(cx - 0.35f, 3f, 9.15f), new Color(0.45f, 0.3f, 0.15f), true);
            MakeBlock("UpperCabDoor" + c + "R", root.transform, new Vector3(0.7f, 0.8f, 0.06f),
                new Vector3(cx + 0.35f, 3f, 9.15f), new Color(0.45f, 0.3f, 0.15f), true);
        }
        MakeBlock("Backsplash", root.transform, new Vector3(6.5f, 0.65f, 0.08f),
            new Vector3(0f, 1.65f, 9.02f), new Color(0.82f, 0.78f, 0.7f), true);
        MakeBlock("KitchenShelf", root.transform, new Vector3(3.5f, 0.12f, 0.6f),
            new Vector3(0f, 3.4f, 9.45f), frameC, true);
        // Kitchen window on +Z wall
        MakeBlock("KitchenWindow", root.transform, new Vector3(2f, 1.4f, 0.08f),
            new Vector3(-1.5f, 2.4f, 9.96f), winC, true);
        MakeBlock("KitchenWindowFrame", root.transform, new Vector3(2.1f, 1.5f, 0.06f),
            new Vector3(-1.5f, 2.4f, 9.94f), frameC, true);
        // Kitchen ceiling lamp
        MakeBlock("KitchenLampCord", root.transform, new Vector3(0.06f, 0.5f, 0.06f),
            new Vector3(0f, 4.95f, 6.5f), frameC, true);
        var kitchenLampShade = MakeBlock("KitchenLampShade", root.transform, new Vector3(0.35f, 0.25f, 0.35f),
            new Vector3(0f, 4.7f, 6.5f), goldC, true);
        kitchenLampShade.GetComponent<MeshRenderer>().enabled = false;
        var kitchenLampGlow = MakeBlock("KitchenLampGlow", root.transform, new Vector3(0.2f, 0.15f, 0.2f),
            new Vector3(0f, 4.52f, 6.5f), amberC, true);
        DisableShadowCasting(kitchenLampGlow);
        AddGlowLight(root.transform, new Vector3(0f, 4.35f, 6.5f), 5f, 1f, new Color(1f, 0.85f, 0.6f));

        // ── 1F Entrance lantern (near front door) ──
        var lanternGlow = MakeBlock("EntranceLantern", root.transform, new Vector3(0.3f, 0.3f, 0.3f),
            new Vector3(-halfW + 1f, 3.8f, 0f), amberC, true);
        lanternGlow.GetComponent<MeshRenderer>().enabled = false;
        DisableShadowCasting(lanternGlow);
        AddGlowLight(root.transform, new Vector3(-halfW + 1f, 3.65f, 0f), 5f, 1f, new Color(1f, 0.85f, 0.6f));

        // ══════════════════════════════════════════════════════════
        // ── 2F Interior Walls ──
        // ══════════════════════════════════════════════════════════
        float bedY = h1 + 0.25f + 0.15f;
        // Center divider (x=0, z=-10 to z=10 with door gaps)
        MakeBlock("Wall2F_CtrS", root.transform, new Vector3(0.35f, h2, 3.5f),
            new Vector3(0f, y2, -8.25f), wallC);
        MakeBlock("Wall2F_CtrN", root.transform, new Vector3(0.35f, h2, 3.5f),
            new Vector3(0f, y2, 8.25f), wallC);
        // Cross divider (z=0, x=-12 to x=9.5 with door gaps)
        MakeBlock("Wall2F_CrossW", root.transform, new Vector3(4.5f, h2, 0.35f),
            new Vector3(-9.75f, y2, 0f), wallC);
        MakeBlock("Wall2F_CrossE", root.transform, new Vector3(4.5f, h2, 0.35f),
            new Vector3(5.25f, y2, 0f), wallC);

        // ── 2F Master Bedroom (southwest: x=-12 to 0, z=-10 to 0) ──
        MakeBlock("MBRug", root.transform, new Vector3(5f, 0.06f, 3.5f),
            new Vector3(-5f, bedY - 0.04f, -5f), rugC);
        // Bed (against -Z wall)
        MakeBlock("BedFrame", root.transform, new Vector3(3.5f, 0.35f, 2.5f),
            new Vector3(-4f, bedY + 0.17f, -6f), frameC);
        MakeBlock("BedMattress", root.transform, new Vector3(3.3f, 0.3f, 2.3f),
            new Vector3(-4f, bedY + 0.45f, -6f), bedC);
        MakeBlock("BedBlanket", root.transform, new Vector3(3.3f, 0.1f, 1.8f),
            new Vector3(-4f, bedY + 0.65f, -6.4f), new Color(0.65f, 0.3f, 0.45f), true);
        MakeBlock("BedPillow", root.transform, new Vector3(1.2f, 0.2f, 0.5f),
            new Vector3(-4.6f, bedY + 0.7f, -7.2f), bedC, true);
        MakeBlock("BedPillow", root.transform, new Vector3(1.2f, 0.2f, 0.5f),
            new Vector3(-3.4f, bedY + 0.7f, -7.2f), bedC, true);
        MakeBlock("BedHeadboard", root.transform, new Vector3(3.7f, 1.3f, 0.18f),
            new Vector3(-4f, bedY + 0.95f, -7.24f), frameC);
        MakeBlock("BedFootboard", root.transform, new Vector3(3.5f, 0.65f, 0.14f),
            new Vector3(-4f, bedY + 0.5f, -4.73f), frameC, true);
        // Nightstands
        MakeBlock("Nightstand", root.transform, new Vector3(0.8f, 0.75f, 0.6f),
            new Vector3(-6f, bedY + 0.37f, -6.5f), tableC);
        MakeBlock("Nightstand", root.transform, new Vector3(0.8f, 0.75f, 0.6f),
            new Vector3(-2f, bedY + 0.37f, -6.5f), tableC);
        // Night lamps
        var nightLampL = MakeBlock("NightLamp", root.transform, new Vector3(0.3f, 0.45f, 0.3f),
            new Vector3(-6f, bedY + 0.92f, -6.5f), goldC, true);
        nightLampL.GetComponent<MeshRenderer>().enabled = false;
        var nightGlowL = MakeBlock("NightLampGlow", root.transform, new Vector3(0.2f, 0.22f, 0.2f),
            new Vector3(-6f, bedY + 0.98f, -6.5f), amberC, true);
        DisableShadowCasting(nightGlowL);
        AddGlowLight(root.transform, new Vector3(-6f, bedY + 0.78f, -6.5f), 4f, 1f, new Color(1f, 0.85f, 0.6f));
        var nightLampR = MakeBlock("NightLamp", root.transform, new Vector3(0.3f, 0.45f, 0.3f),
            new Vector3(-2f, bedY + 0.92f, -6.5f), goldC, true);
        nightLampR.GetComponent<MeshRenderer>().enabled = false;
        var nightGlowR = MakeBlock("NightLampGlow", root.transform, new Vector3(0.2f, 0.22f, 0.2f),
            new Vector3(-2f, bedY + 0.98f, -6.5f), amberC, true);
        DisableShadowCasting(nightGlowR);
        AddGlowLight(root.transform, new Vector3(-2f, bedY + 0.78f, -6.5f), 4f, 1f, new Color(1f, 0.85f, 0.6f));
        // Wardrobe (against -X wall)
        MakeBlock("Wardrobe", root.transform, new Vector3(2.5f, 2.6f, 0.8f),
            new Vector3(-11f, bedY + 1.55f, -5f), frameC);
        MakeBlock("WardrobeDoor1", root.transform, new Vector3(1f, 2.4f, 0.06f),
            new Vector3(-11.35f, bedY + 1.55f, -4.6f), new Color(0.45f, 0.3f, 0.15f), true);
        MakeBlock("WardrobeDoor2", root.transform, new Vector3(1f, 2.4f, 0.06f),
            new Vector3(-10.65f, bedY + 1.55f, -4.6f), new Color(0.45f, 0.3f, 0.15f), true);

        // ── 2F Master Bathroom (southeast: x=0 to +9.5, z=-10 to 0) ──
        MakeBlock("BathTile", root.transform, new Vector3(4f, 0.04f, 4f),
            new Vector3(5f, bedY - 0.03f, -5f), marbleC, true);
        MakeBlock("Bathtub", root.transform, new Vector3(2f, 0.6f, 1.2f),
            new Vector3(7f, bedY + 0.3f, -8f), new Color(0.92f, 0.92f, 0.94f));
        MakeBlock("BathtubRim", root.transform, new Vector3(2.1f, 0.08f, 1.3f),
            new Vector3(7f, bedY + 0.64f, -8f), new Color(0.88f, 0.88f, 0.9f), true);
        MakeBlock("BathroomSink", root.transform, new Vector3(0.8f, 0.15f, 0.6f),
            new Vector3(3f, bedY + 0.8f, -9f), new Color(0.9f, 0.9f, 0.92f), true);
        MakeBlock("BathroomMirror", root.transform, new Vector3(0.9f, 1.2f, 0.06f),
            new Vector3(3f, bedY + 1.7f, -9.7f), winC, true);
        MakeBlock("BathroomMirrorFrame", root.transform, new Vector3(1f, 1.3f, 0.06f),
            new Vector3(3f, bedY + 1.7f, -9.72f), frameC, true);

        // ── 2F Guest Bedroom (northwest: x=-12 to 0, z=0 to +10) ──
        MakeBlock("GuestRug", root.transform, new Vector3(4f, 0.06f, 3f),
            new Vector3(-5f, bedY - 0.04f, 5f), rugC);
        MakeBlock("GuestBed", root.transform, new Vector3(3f, 0.3f, 2f),
            new Vector3(-5f, bedY + 0.15f, 7.5f), frameC);
        MakeBlock("GuestMattress", root.transform, new Vector3(2.8f, 0.25f, 1.8f),
            new Vector3(-5f, bedY + 0.38f, 7.5f), bedC);
        MakeBlock("GuestBlanket", root.transform, new Vector3(2.8f, 0.08f, 1.4f),
            new Vector3(-5f, bedY + 0.55f, 7.2f), new Color(0.5f, 0.6f, 0.75f), true);
        MakeBlock("GuestPillow", root.transform, new Vector3(1f, 0.18f, 0.4f),
            new Vector3(-5.5f, bedY + 0.58f, 8.3f), bedC, true);
        MakeBlock("GuestPillow", root.transform, new Vector3(1f, 0.18f, 0.4f),
            new Vector3(-4.5f, bedY + 0.58f, 8.3f), bedC, true);
        MakeBlock("GuestHeadboard", root.transform, new Vector3(3.2f, 1.1f, 0.15f),
            new Vector3(-5f, bedY + 0.8f, 8.54f), frameC);
        MakeBlock("GuestNightstand", root.transform, new Vector3(0.7f, 0.65f, 0.5f),
            new Vector3(-7.5f, bedY + 0.32f, 8f), tableC);
        MakeBlock("GuestSideTable", root.transform, new Vector3(0.7f, 0.65f, 0.5f),
            new Vector3(-2.5f, bedY + 0.32f, 8f), tableC);

        // ── 2F Study / Office (northeast: x=0 to +9.5, z=0 to +10) ──
        MakeBlock("StudyDesk", root.transform, new Vector3(2f, 0.08f, 1f),
            new Vector3(5f, bedY + 0.7f, 5f), tableC);
        MakeBlock("StudyDeskLeg1", root.transform, new Vector3(0.08f, 0.65f, 0.08f),
            new Vector3(4.1f, bedY + 0.35f, 4.58f), frameC, true);
        MakeBlock("StudyDeskLeg2", root.transform, new Vector3(0.08f, 0.65f, 0.08f),
            new Vector3(5.9f, bedY + 0.35f, 4.58f), frameC, true);
        MakeBlock("StudyDeskLeg3", root.transform, new Vector3(0.08f, 0.65f, 0.08f),
            new Vector3(4.1f, bedY + 0.35f, 5.42f), frameC, true);
        MakeBlock("StudyDeskLeg4", root.transform, new Vector3(0.08f, 0.65f, 0.08f),
            new Vector3(5.9f, bedY + 0.35f, 5.42f), frameC, true);
        MakeBlock("StudyChair", root.transform, new Vector3(0.7f, 0.4f, 0.7f),
            new Vector3(5f, bedY + 0.3f, 3.6f), sofaC);
        MakeBlock("StudyChairBack", root.transform, new Vector3(0.7f, 0.6f, 0.14f),
            new Vector3(5f, bedY + 0.65f, 3.25f), sofaC, true);
        MakeBlock("StudyBookshelf", root.transform, new Vector3(2.5f, 2.8f, 0.55f),
            new Vector3(7f, bedY + 1.55f, 9.45f), frameC);
        MakeBlock("StudyShelfBooks1", root.transform, new Vector3(2.1f, 0.12f, 0.4f),
            new Vector3(7f, bedY + 0.6f, 9.45f), rugC, true);
        MakeBlock("StudyShelfBooks2", root.transform, new Vector3(2.1f, 0.12f, 0.4f),
            new Vector3(7f, bedY + 1.5f, 9.45f), new Color(0.2f, 0.4f, 0.6f), true);
        // Dresser against +Z wall
        MakeBlock("Dresser", root.transform, new Vector3(2f, 1.1f, 0.7f),
            new Vector3(2f, bedY + 0.7f, 9.3f), frameC);
        MakeBlock("DresserMirror", root.transform, new Vector3(1.2f, 1.4f, 0.06f),
            new Vector3(2f, bedY + 2.1f, 9.62f), winC, true);
        MakeBlock("DresserMirrorFrame", root.transform, new Vector3(1.3f, 1.5f, 0.06f),
            new Vector3(2f, bedY + 2.1f, 9.65f), frameC, true);

        // ── 2F Hallway chandelier ──
        MakeBlock("HallChandCord", root.transform, new Vector3(0.06f, 0.6f, 0.06f),
            new Vector3(0f, 8.95f, 0f), frameC, true);
        var hallChand = MakeBlock("HallChandelier", root.transform, new Vector3(1.2f, 0.2f, 1.2f),
            new Vector3(0f, 8.55f, 0f), goldC, true);
        hallChand.GetComponent<MeshRenderer>().enabled = false;
        var hallGlow = MakeBlock("HallGlow", root.transform, new Vector3(0.6f, 0.18f, 0.6f),
            new Vector3(0f, 8.38f, 0f), amberC, true);
        DisableShadowCasting(hallGlow);
        AddGlowLight(root.transform, new Vector3(0f, 8.22f, 0f), 8f, 1.5f, new Color(1f, 0.85f, 0.6f));

        // ── Gabled roof (ridge along X = front-to-back after -90° rotation, gold ridge) ──
        float rise = 3.5f;
        float panelLen = Mathf.Sqrt(halfD * halfD + rise * rise);
        float tilt = Mathf.Atan2(rise, halfD) * Mathf.Rad2Deg;
        float roofX = fullW + 3.6f;
        float roofY = h1 + h2 + 0.55f;

        MakeBlock("MansionRoofPanel", root.transform, new Vector3(roofX, 0.6f, panelLen),
            new Vector3(0f, roofY + rise / 2f, halfD / 2f), roofC).transform.localRotation = Quaternion.Euler(tilt, 0f, 0f);
        MakeBlock("MansionRoofPanel", root.transform, new Vector3(roofX, 0.6f, panelLen),
            new Vector3(0f, roofY + rise / 2f, -halfD / 2f), roofC).transform.localRotation = Quaternion.Euler(-tilt, 0f, 0f);
        MakeBlock("MansionRidge", root.transform, new Vector3(roofX + 0.2f, 0.35f, 0.7f),
            new Vector3(0f, roofY + rise + 0.1f, 0f), goldC);
        MakeBlock("RidgeFinialE", root.transform, new Vector3(0.45f, 0.45f, 0.45f),
            new Vector3(halfW + 1.8f, roofY + rise + 0.55f, 0f), goldC, true);
        MakeBlock("RidgeFinialW", root.transform, new Vector3(0.45f, 0.45f, 0.45f),
            new Vector3(-halfW - 1.8f, roofY + rise + 0.55f, 0f), goldC, true);
        MakeBlock("MansionRidgeTrim", root.transform, new Vector3(roofX + 0.4f, 0.3f, 0.55f),
            new Vector3(0f, roofY + rise + 0.35f, 0f), goldC, true);
        MakeBlock("FasciaZP", root.transform, new Vector3(roofX + 0.4f, 0.3f, 0.35f),
            new Vector3(0f, roofY + 0.07f, halfD + 0.03f), goldC, true);
        MakeBlock("FasciaZN", root.transform, new Vector3(roofX + 0.4f, 0.3f, 0.35f),
            new Vector3(0f, roofY + 0.07f, -halfD - 0.03f), goldC, true);

        foreach (float gx in new[] { -halfW, halfW })
        {
            float gxFace = gx + (gx > 0 ? 1f : -1f) * 0.04f;
            for (int i = 0; i < 7; i++)
            {
                float t = (i + 0.5f) / 7f;
                float sz = fullD * (1f - t) + 0.2f;
                float sy = roofY + (i + 0.5f) * rise / 7f;
                float sh = rise / 7f + 0.15f;
                MakeBlock("GableFill", root.transform, new Vector3(0.55f, sh, sz),
                    new Vector3(gxFace, sy, 0f), wallC);
            }
        }

        // ── Chimney ──
        MakeBlock("ChimneyCollar", root.transform, new Vector3(2.1f, 0.25f, 2.1f), new Vector3(halfW - 5f, 11.7f, 4f), roofC);
        MakeBlock("Chimney", root.transform, new Vector3(1.4f, 1.9f, 1.4f), new Vector3(halfW - 5f, 12.15f, 4f), trimC);
        MakeBlock("ChimneyCap", root.transform, new Vector3(1.7f, 0.15f, 1.7f), new Vector3(halfW - 5f, 13.3f, 4f), stoneC);

        // ── Front yard: hedges, statues, straight walk ──
        MakeBlock("HedgeL", root.transform, new Vector3(2.4f, 1f, 1.3f), new Vector3(-halfW - 2.6f, 0.5f, -5.2f), leafC);
        MakeBlock("HedgeR", root.transform, new Vector3(2.4f, 1f, 1.3f), new Vector3(-halfW - 2.6f, 0.5f, 5.2f), leafC);
        MakeBlock("GoldStatueL", root.transform, new Vector3(0.5f, 1.3f, 0.5f), new Vector3(-halfW - 1.7f, 0.65f, -4.6f), goldC, true);
        MakeBlock("GoldStatueR", root.transform, new Vector3(0.5f, 1.3f, 0.5f), new Vector3(-halfW - 1.7f, 0.65f, 4.6f), goldC, true);
        MakeBlock("MansionWalk", root.transform, new Vector3(13.8f, 0.12f, 3.2f), new Vector3(-halfW - 11.1f, 0.06f, 0f), stoneC);

        // ── Fountain centered on the walkway ──
        MakeBlock("FountainPad", root.transform, new Vector3(6.4f, 0.15f, 6.4f), new Vector3(-halfW - 11f, 0.075f, 0f), stoneC);
        MakeBlock("FountainBase", root.transform, new Vector3(3.8f, 0.45f, 3.8f), new Vector3(-halfW - 11f, 0.225f, 0f), stoneC);
        MakeBlock("FountainRim", root.transform, new Vector3(4.4f, 0.3f, 4.4f), new Vector3(-halfW - 11f, 0.6f, 0f), stoneC, true);
        MakeBlock("FountainRimTop", root.transform, new Vector3(4.4f, 0.12f, 4.4f), new Vector3(-halfW - 11f, 0.78f, 0f), stoneC, true);
        MakeBlock("FountainWater", root.transform, new Vector3(3.7f, 0.1f, 3.7f), new Vector3(-halfW - 11f, 0.76f, 0f), waterC, true);
        MakeBlock("FountainPedestal", root.transform, new Vector3(1f, 1.3f, 1f), new Vector3(-halfW - 11f, 1.35f, 0f), stoneC, true);
        MakeBlock("FountainJet", root.transform, new Vector3(0.3f, 0.7f, 0.3f), new Vector3(-halfW - 11f, 2.05f, 0f), waterC, true);
        MakeBlock("FountainStatue", root.transform, new Vector3(0.5f, 0.7f, 0.5f), new Vector3(-halfW - 11f, 2.35f, 0f), goldC, true);
        foreach (int cs in new[] { -1, 1 })
        {
            foreach (int cz in new[] { -1, 1 })
            {
                MakeBlock("FountainFinial" + (cs < 0 ? "A" : "B") + (cz < 0 ? "A" : "B"),
                    root.transform, new Vector3(0.16f, 0.16f, 0.16f),
                    new Vector3(-halfW - 11f + cs * 1.9f, 0.86f, cz * 1.9f), goldC, true);
            }
        }

        // ── Lampposts flanking the walk near the porch ──
        foreach (int side in new[] { -1, 1 })
        {
            string tag = side < 0 ? "L" : "R";
            MakeBlock("LampPost" + tag, root.transform, new Vector3(0.16f, 3.4f, 0.16f), new Vector3(-halfW - 4.5f, 1.7f, side * 3.1f), trimC, true);
            MakeBlock("LampHead" + tag, root.transform, new Vector3(0.55f, 0.35f, 0.55f), new Vector3(-halfW - 4.5f, 3.4f, side * 3.1f), goldC, true);
            MakeBlock("LampGlow" + tag, root.transform, new Vector3(0.4f, 0.22f, 0.4f), new Vector3(-halfW - 4.5f, 3.45f, side * 3.1f), new Color(1f, 0.95f, 0.7f), true);
        }

        // ── Cypress trees: gate flankers, fence corners, behind-house pair ──
        BuildCypress(root.transform, new Vector3(-halfW - 17f, 0f, -3.6f), 0.9f, "NW2", leafC, trimC);
        BuildCypress(root.transform, new Vector3(-halfW - 17f, 0f, 3.6f), 0.9f, "SW2", leafC, trimC);
        BuildCypress(root.transform, new Vector3(-halfW - 17.4f, 0f, -9f), 1f, "NW", leafC, trimC);
        BuildCypress(root.transform, new Vector3(-halfW - 17.4f, 0f, 9f), 1f, "SW", leafC, trimC);
        BuildCypress(root.transform, new Vector3(halfW + 2.6f, 0f, -5.5f), 1f, "NE", leafC, trimC);
        BuildCypress(root.transform, new Vector3(halfW + 2.6f, 0f, 5.5f), 1f, "SE", leafC, trimC);

        // ── Flower beds flanking the walk ──
        Color[] flowerC = { new Color(0.85f, 0.2f, 0.2f), new Color(0.95f, 0.85f, 0.15f), new Color(0.6f, 0.3f, 0.8f) };
        int fi = 0;
        foreach (float fz in new[] { -2.4f, 2.4f })
        {
            foreach (float fx in new[] { -halfW - 5f, -halfW - 9f, -halfW - 13f })
            {
                MakeBlock("FlowerBed", root.transform, new Vector3(0.8f, 0.25f, 0.8f), new Vector3(fx, 0.125f, fz), leafC);
                MakeBlock("Flower", root.transform, new Vector3(0.35f, 0.28f, 0.35f), new Vector3(fx, 0.28f, fz), flowerC[fi++ % flowerC.Length], true);
            }
        }

        // ── Garden fence along the west edge, split for the gate ──
        Color fenceC = new Color(0.18f, 0.32f, 0.15f);
        foreach (int side in new[] { -1, 1 })
        {
            string tag = side < 0 ? "S" : "N";
            float zc = side * 5.65f;
            MakeBlock("FenceTopRail" + tag, root.transform, new Vector3(0.18f, 0.12f, 7.7f), new Vector3(-halfW - 18f, 1.15f, zc), fenceC, true);
            MakeBlock("FenceBottomRail" + tag, root.transform, new Vector3(0.18f, 0.1f, 7.7f), new Vector3(-halfW - 18f, 0.5f, zc), fenceC, true);
            for (int p = 0; p < 5; p++)
            {
                float pz = side * (2.6f + p * 1.8f);
                MakeBlock("FencePost" + tag + p, root.transform, new Vector3(0.22f, 1.5f, 0.22f), new Vector3(-halfW - 18.05f, 0.75f, pz), fenceC, true);
            }
        }
        MakeBlock("GatePostL", root.transform, new Vector3(0.3f, 2.2f, 0.3f), new Vector3(-halfW - 18f, 1.1f, -1.9f), fenceC, true);
        MakeBlock("GatePostR", root.transform, new Vector3(0.3f, 2.2f, 0.3f), new Vector3(-halfW - 18f, 1.1f, 1.9f), fenceC, true);
        MakeBlock("GateCapL", root.transform, new Vector3(0.38f, 0.14f, 0.38f), new Vector3(-halfW - 18f, 2.22f, -1.9f), goldC, true);
        MakeBlock("GateCapR", root.transform, new Vector3(0.38f, 0.14f, 0.38f), new Vector3(-halfW - 18f, 2.22f, 1.9f), goldC, true);
        MakeBlock("GateLintel", root.transform, new Vector3(0.18f, 0.2f, 3.8f), new Vector3(-halfW - 18.1f, 2.05f, 0f), fenceC, true);
        MakeBlock("GateApron", root.transform, new Vector3(4.4f, 0.1f, 4.6f), new Vector3(-halfW - 17.7f, 0.05f, 0f), stoneC);

        return root;
    }

}

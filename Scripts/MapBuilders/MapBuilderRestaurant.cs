using System.Collections.Generic;
using UnityEngine;

public static partial class MapBuilder
{
    // ═══════════════════════════════════════════════════════════════
    //  RICE RESTAURANT  (12 x 10, gabled roof, counter, chef NPC)
    // ═══════════════════════════════════════════════════════════════

    public static GameObject BuildRiceRestaurant(Transform parent, Vector3 position, float scale = 1f, Quaternion rotation = default)
    {
        var root = new GameObject("RiceRestaurant");
        root.transform.SetParent(parent);
        root.transform.position = position;
        root.transform.rotation = (rotation == default) ? Quaternion.identity : rotation;
        root.transform.localScale = Vector3.one * scale;

        Color wallC   = new Color(0.93f, 0.87f, 0.72f);
        Color trimC   = new Color(0.42f, 0.27f, 0.14f);
        Color roofC   = new Color(0.78f, 0.3f, 0.12f);
        Color ridgeC  = new Color(0.55f, 0.16f, 0.06f);
        Color eaveC   = new Color(0.2f, 0.18f, 0.16f);
        Color floorC  = new Color(0.45f, 0.32f, 0.2f);
        Color stoneC  = new Color(0.439f, 0.4f, 0.361f);
        Color winC    = new Color(0.549f, 0.784f, 0.863f);
        Color signC   = new Color(0.886f, 0.753f, 0.098f);
        Color awningC = new Color(0.85f, 0.25f, 0.2f);
        Color counterC = new Color(0.584f, 0.294f, 0.165f);
        Color riceC   = new Color(0.98f, 0.95f, 0.85f);

        float halfW = 6f;
        float depth = 10f;

        // ── Walls (door on +X) ──
        MakeBlock("Wall", root.transform, new Vector3(0.5f, 4f, depth), new Vector3(-halfW, 2f, 0f), wallC);
        MakeBlock("Wall", root.transform, new Vector3(halfW * 2f, 4f, 0.5f), new Vector3(0f, 2f, -depth / 2f), wallC);
        MakeBlock("Wall", root.transform, new Vector3(halfW * 2f, 4f, 0.5f), new Vector3(0f, 2f, depth / 2f), wallC);
        MakeBlock("Wall", root.transform, new Vector3(0.5f, 4f, 3.5f), new Vector3(halfW, 2f, -3.25f), wallC);
        MakeBlock("Wall", root.transform, new Vector3(0.5f, 4f, 3.5f), new Vector3(halfW, 2f, 3.25f), wallC);
        MakeBlock("Transom", root.transform, new Vector3(0.5f, 1.2f, 3f), new Vector3(halfW, 3.4f, 0f), wallC);
        MakeBlock("Floor", root.transform, new Vector3(halfW * 2f, 0.5f, depth), Vector3.zero, floorC);

        // ── Gabled roof ──
        float rise = 2.5f;
        float panelLen = Mathf.Sqrt(halfW * halfW + rise * rise);
        float tilt = Mathf.Atan2(rise, halfW) * Mathf.Rad2Deg;
        float overhang = 1.2f;
        float roofZ = depth + overhang * 2f;

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

        foreach (float gz in new[] { -depth / 2f, depth / 2f })
        {
            float gzFace = gz + (gz > 0 ? 1f : -1f) * 0.04f;
            for (int i = 0; i < 5; i++)
            {
                float t = (i + 0.5f) / 5f;
                float sw = (halfW * 2f) * (1f - t) + 0.2f;
                float sy = 4f + (i + 0.5f) * rise / 5f;
                float sh = rise / 5f + 0.15f;
                MakeBlock("GableFill", root.transform, new Vector3(sw, sh, 0.55f),
                    new Vector3(0f, sy, gzFace), wallC);
            }
        }

        // ── Stone foundation ──
        MakeBlock("Foundation", root.transform, new Vector3(halfW * 2f + 1.5f, 0.4f, depth + 1.5f),
            new Vector3(0f, -0.2f, 0f), stoneC);

        // ── Sign (faces the +X road) ──
        MakeBlock("Sign", root.transform, new Vector3(0.2f, 0.8f, 3.5f),
            new Vector3(halfW + 0.08f, 3.6f, 0f), signC, true);
        var signLabel = new GameObject("RestaurantSignLabel");
        signLabel.transform.SetParent(root.transform);
        signLabel.transform.localPosition = new Vector3(halfW + 0.3f, 3.6f, 0f);
        signLabel.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);
        var signTmp = signLabel.AddComponent<TMPro.TextMeshPro>();
        signTmp.text = "NHÀ HÀNG";
        signTmp.fontSize = 1.0f;
        signTmp.alignment = TMPro.TextAlignmentOptions.Center;
        signTmp.color = new Color(0.98f, 0.94f, 0.85f);
        signTmp.outlineWidth = 0.18f;
        signTmp.outlineColor = Color.black;
        signTmp.rectTransform.sizeDelta = new Vector3(3.2f, 0.75f);

        // ── Roof trim + ridge finials ──
        MakeBlock("RoofTrimN", root.transform, new Vector3(halfW * 2f + 0.2f, 0.12f, 0.55f),
            new Vector3(0f, 4.18f, -depth / 2f - 1.1f), trimC, true);
        MakeBlock("RoofTrimS", root.transform, new Vector3(halfW * 2f + 0.2f, 0.12f, 0.55f),
            new Vector3(0f, 4.18f, depth / 2f + 1.1f), trimC, true);
        MakeBlock("FinialN", root.transform, new Vector3(0.5f, 0.7f, 0.5f),
            new Vector3(0f, 4f + rise + 0.35f, -roofZ / 2f + 0.2f), ridgeC, true);
        MakeBlock("FinialS", root.transform, new Vector3(0.5f, 0.7f, 0.5f),
            new Vector3(0f, 4f + rise + 0.35f, roofZ / 2f - 0.2f), ridgeC, true);

        // ── Entrance awning (east) ──
        MakeBlock("Awning", root.transform, new Vector3(1.5f, 0.15f, 4f),
            new Vector3(halfW + 0.8f, 3.8f, 0f), awningC, true);
        MakeBlock("AwningPost", root.transform, new Vector3(0.12f, 3.8f, 0.12f),
            new Vector3(halfW + 1.6f, 1.9f, -1.6f), trimC, true);
        MakeBlock("AwningPost", root.transform, new Vector3(0.12f, 3.8f, 0.12f),
            new Vector3(halfW + 1.6f, 1.9f, 1.6f), trimC, true);

        // ── Hanging lanterns ──
        foreach (float lz in new[] { -1.6f, 1.6f })
        {
            MakeBlock("LanternCord", root.transform, new Vector3(0.04f, 0.5f, 0.04f),
                new Vector3(halfW + 0.8f, 3.55f, lz), trimC, true);
            MakeBlock("Lantern", root.transform, new Vector3(0.28f, 0.4f, 0.28f),
                new Vector3(halfW + 0.8f, 3.1f, lz), new Color(0.9f, 0.35f, 0.2f), true);
            MakeBlock("LanternGlow", root.transform, new Vector3(0.18f, 0.22f, 0.18f),
                new Vector3(halfW + 0.8f, 3.1f, lz), new Color(1f, 0.85f, 0.45f), true);
            AddEntranceLight(root.transform, new Vector3(halfW + 0.8f, 3.0f, lz));
        }

        // ── Freestanding signpost (front, beside the path) ──
        MakeBlock("SignPost", root.transform, new Vector3(0.18f, 2.6f, 0.18f),
            new Vector3(halfW + 2.2f, 1.3f, 2.6f), trimC, true);
        MakeBlock("SignBoard", root.transform, new Vector3(0.14f, 0.9f, 2.4f),
            new Vector3(halfW + 2.2f, 2.6f, 2.6f), signC, true);
        MakeBlock("SignBoardText", root.transform, new Vector3(0.05f, 0.55f, 2.0f),
            new Vector3(halfW + 2.26f, 2.6f, 2.6f), new Color(0.98f, 0.94f, 0.85f), true);

        // ── Lampposts at the entrance and patio ──
        AddLamppost(root.transform, new Vector3(halfW + 1.3f, 0.05f, -3.1f));
        AddLamppost(root.transform, new Vector3(halfW + 1.3f, 0.05f, 3.1f));
        AddLamppost(root.transform, new Vector3(halfW + 2.3f, 0.05f, -4.0f));
        AddLamppost(root.transform, new Vector3(halfW + 2.3f, 0.05f, 4.0f));

        // ── Windows (west, north, south) ──
        foreach (float wz in new[] { -3f, 3f })
        {
            MakeBlock("WinGlass", root.transform, new Vector3(0.14f, 1.2f, 1.2f),
                new Vector3(-halfW - 0.03f, 2.2f, wz), winC, true);
            MakeBlock("WinFrame", root.transform, new Vector3(0.16f, 0.08f, 1.2f),
                new Vector3(-halfW - 0.03f, 2.2f, wz), trimC, true);
            MakeBlock("WinFrame", root.transform, new Vector3(0.16f, 1.2f, 0.08f),
                new Vector3(-halfW - 0.03f, 2.2f, wz), trimC, true);
        }
        foreach (float wx in new[] { -3f, 3f })
        {
            MakeBlock("WinGlass", root.transform, new Vector3(1.4f, 1.2f, 0.14f),
                new Vector3(wx, 2.2f, depth / 2f + 0.03f), winC, true);
            MakeBlock("WinFrame", root.transform, new Vector3(0.1f, 1.2f, 0.16f),
                new Vector3(wx, 2.2f, depth / 2f + 0.03f), trimC, true);
            MakeBlock("WinFrame", root.transform, new Vector3(1.4f, 0.08f, 0.16f),
                new Vector3(wx, 2.2f, depth / 2f + 0.03f), trimC, true);
        }

        // ── Door frame (east) ──
        MakeBlock("DoorFrame", root.transform, new Vector3(0.25f, 3.5f, 0.25f),
            new Vector3(halfW + 0.04f, 1.75f, -1.5f), trimC, true);
        MakeBlock("DoorFrame", root.transform, new Vector3(0.25f, 3.5f, 0.25f),
            new Vector3(halfW + 0.04f, 1.75f, 1.5f), trimC, true);
        MakeBlock("DoorLintel", root.transform, new Vector3(0.25f, 0.3f, 3.25f),
            new Vector3(halfW + 0.04f, 3.65f, 0f), trimC, true);

        // ── Menu board (interior, beside door) ──
        MakeBlock("MenuBoardFrame", root.transform, new Vector3(0.14f, 1.4f, 1.2f),
            new Vector3(halfW - 0.05f, 1.9f, 2.6f), trimC, true);
        MakeBlock("MenuBoard", root.transform, new Vector3(0.12f, 1.3f, 1.1f),
            new Vector3(halfW - 0.05f, 1.9f, 2.6f), new Color(0.12f, 0.1f, 0.08f), true);
        for (int mi = 0; mi < 3; mi++)
        {
            MakeBlock("MenuChip", root.transform, new Vector3(0.06f, 0.12f, 0.8f),
                new Vector3(halfW - 0.08f, 1.85f - mi * 0.4f, 2.6f),
                mi == 1 ? new Color(1f, 0.6f, 0.35f) : new Color(0.98f, 0.95f, 0.85f), true);
        }

        // ── Counter with rice pots (west wall) ──
        MakeBlock("Counter", root.transform, new Vector3(2f, 1f, 6f),
            new Vector3(-halfW + 1f, 0.5f, 0f), counterC);
        MakeBlock("CounterTop", root.transform, new Vector3(2f, 0.08f, 6.2f),
            new Vector3(-halfW + 1f, 0.96f, 0f), new Color(0.757f, 0.62f, 0.404f), true);
        for (int i = -2; i <= 2; i++)
        {
            MakeBlock("RicePot", root.transform, new Vector3(0.32f, 0.5f, 0.32f),
                new Vector3(-halfW + 0.45f, 1.3f, i * 1.2f), new Color(0.35f, 0.35f, 0.36f), true);
            MakeBlock("RicePotTop", root.transform, new Vector3(0.36f, 0.08f, 0.36f),
                new Vector3(-halfW + 0.45f, 1.58f, i * 1.2f), riceC, true);
        }

        // ── Kitchen detail: stir pot, chopping board, stacked bowls (on countertop) ──
        MakeBlock("StirPot", root.transform, new Vector3(0.28f, 0.32f, 0.28f),
            new Vector3(-5.15f, 1.16f, 1.6f), new Color(0.35f, 0.35f, 0.36f), true);
        MakeBlock("StirPotTop", root.transform, new Vector3(0.32f, 0.07f, 0.32f),
            new Vector3(-5.15f, 1.34f, 1.6f), new Color(0.5f, 0.35f, 0.15f), true);
        MakeBlock("ChoppingBoard", root.transform, new Vector3(0.14f, 0.05f, 0.7f),
            new Vector3(-5.25f, 1.03f, 2.55f), new Color(0.72f, 0.55f, 0.3f), true);
        MakeBlock("ChoppedItem", root.transform, new Vector3(0.05f, 0.04f, 0.05f),
            new Vector3(-5.25f, 1.09f, 2.35f), new Color(1f, 0.85f, 0.3f), true);
        MakeBlock("StackedBowl", root.transform, new Vector3(0.32f, 0.14f, 0.32f),
            new Vector3(-5.15f, 1.07f, -2.45f), riceC, true);
        MakeBlock("StackedBowl", root.transform, new Vector3(0.34f, 0.1f, 0.34f),
            new Vector3(-5.15f, 1.21f, -2.45f), riceC, true);
        MakeBlock("SmallPot", root.transform, new Vector3(0.22f, 0.24f, 0.22f),
            new Vector3(-5.1f, 1.12f, 0.4f), new Color(0.3f, 0.3f, 0.32f), true);
        MakeBlock("Ladle", root.transform, new Vector3(0.05f, 0.3f, 0.05f),
            new Vector3(-5.3f, 1.15f, -1.15f), new Color(0.55f, 0.55f, 0.58f), true);

        // ── Stove on the countertop ──
        MakeBlock("Stove", root.transform, new Vector3(0.9f, 0.45f, 0.9f),
            new Vector3(-5.2f, 1.2f, -0.6f), new Color(0.28f, 0.28f, 0.3f), true);
        MakeBlock("StoveTop", root.transform, new Vector3(0.9f, 0.05f, 0.9f),
            new Vector3(-5.2f, 1.45f, -0.6f), new Color(0.12f, 0.12f, 0.13f), true);
        MakeBlock("Burner", root.transform, new Vector3(0.34f, 0.03f, 0.34f),
            new Vector3(-5.2f, 1.48f, -0.85f), new Color(0.08f, 0.08f, 0.08f), true);
        MakeBlock("Burner", root.transform, new Vector3(0.34f, 0.03f, 0.34f),
            new Vector3(-5.2f, 1.48f, -0.35f), new Color(0.08f, 0.08f, 0.08f), true);

        // ── Wall shelf + jars behind the counter ──
        MakeBlock("ShelfBoard", root.transform, new Vector3(0.12f, 0.08f, 4.2f),
            new Vector3(-5.78f, 2.6f, 0f), trimC, true);
        for (int j = 0; j < 4; j++)
        {
            MakeBlock("Jar", root.transform, new Vector3(0.16f, 0.3f, 0.16f),
                new Vector3(-5.82f, 2.78f, -1.6f + j * 1.1f),
                j % 2 == 0 ? new Color(0.55f, 0.78f, 0.86f) : new Color(0.9f, 0.35f, 0.2f), true);
        }

        // ── Rice sacks in the corner ──
        MakeBlock("RiceSack", root.transform, new Vector3(0.5f, 0.7f, 0.5f),
            new Vector3(-5.7f, 0.35f, -3.5f), new Color(0.93f, 0.9f, 0.82f), true);
        MakeBlock("RiceSack", root.transform, new Vector3(0.45f, 0.6f, 0.45f),
            new Vector3(-5.35f, 0.3f, -3.6f), new Color(0.86f, 0.81f, 0.72f), true);

        // ── Interior dining tables (full height) ──
        foreach (float tz in new[] { -3.5f, 3.5f })
        {
            MakeBlock("DiningTable", root.transform, new Vector3(1.6f, 0.1f, 0.9f),
                new Vector3(0.5f, 0.75f, tz), new Color(0.6f, 0.42f, 0.24f), true);
            MakeBlock("TableLeg", root.transform, new Vector3(0.1f, 0.7f, 0.1f),
                new Vector3(0.1f, 0.35f, tz - 0.35f), trimC, true);
            MakeBlock("TableLeg", root.transform, new Vector3(0.1f, 0.7f, 0.1f),
                new Vector3(0.9f, 0.35f, tz - 0.35f), trimC, true);
            MakeBlock("TableLeg", root.transform, new Vector3(0.1f, 0.7f, 0.1f),
                new Vector3(0.1f, 0.35f, tz + 0.35f), trimC, true);
            MakeBlock("TableLeg", root.transform, new Vector3(0.1f, 0.7f, 0.1f),
                new Vector3(0.9f, 0.35f, tz + 0.35f), trimC, true);
            MakeBlock("Bowl", root.transform, new Vector3(0.3f, 0.08f, 0.3f),
                new Vector3(0.5f, 0.82f, tz - 0.15f), riceC, true);
            MakeBlock("Plate", root.transform, new Vector3(0.28f, 0.04f, 0.28f),
                new Vector3(0.5f, 0.81f, tz + 0.2f), new Color(0.95f, 0.93f, 0.88f), true);
        }

        // ── Interior benches + stools ──
        foreach (float bz in new[] { -3.5f, 3.5f })
        {
            MakeBlock("DiningBench", root.transform, new Vector3(1.4f, 0.12f, 0.4f),
                new Vector3(1.55f, 0.44f, bz), trimC, true);
            MakeBlock("BenchLeg", root.transform, new Vector3(0.12f, 0.4f, 0.12f),
                new Vector3(1.15f, 0.2f, bz - 0.16f), trimC, true);
            MakeBlock("BenchLeg", root.transform, new Vector3(0.12f, 0.4f, 0.12f),
                new Vector3(1.95f, 0.2f, bz - 0.16f), trimC, true);
            MakeBlock("BenchLeg", root.transform, new Vector3(0.12f, 0.4f, 0.12f),
                new Vector3(1.15f, 0.2f, bz + 0.16f), trimC, true);
            MakeBlock("BenchLeg", root.transform, new Vector3(0.12f, 0.4f, 0.12f),
                new Vector3(1.95f, 0.2f, bz + 0.16f), trimC, true);
            MakeBlock("DiningStool", root.transform, new Vector3(0.3f, 0.5f, 0.3f),
                new Vector3(-0.55f, 0.25f, bz), trimC, true);
        }

        // ── Interior seated diners (one per stool + one per bench) ──
        BuildSeatedCustomer(root.transform, new Vector3(-0.55f, 0.5f, -3.5f),
            Quaternion.Euler(0f, -90f, 0f), new Color(0.45f, 0.6f, 0.75f), new Color(0.35f, 0.4f, 0.5f),
            new Color(0.9f, 0.78f, 0.68f));
        BuildSeatedCustomer(root.transform, new Vector3(1.55f, 0.5f, -3.5f),
            Quaternion.Euler(0f, 90f, 0f), new Color(0.85f, 0.55f, 0.4f), new Color(0.4f, 0.32f, 0.25f),
            new Color(0.9f, 0.78f, 0.68f));
        BuildSeatedCustomer(root.transform, new Vector3(-0.55f, 0.5f, 3.5f),
            Quaternion.Euler(0f, -90f, 0f), new Color(0.5f, 0.65f, 0.4f), new Color(0.35f, 0.45f, 0.3f),
            new Color(0.9f, 0.78f, 0.68f));
        BuildSeatedCustomer(root.transform, new Vector3(1.55f, 0.5f, 3.5f),
            Quaternion.Euler(0f, 90f, 0f), new Color(0.75f, 0.55f, 0.85f), new Color(0.35f, 0.3f, 0.45f),
            new Color(0.9f, 0.78f, 0.68f));

        // ── Kitchen staff: cook helper + waitress ──
        BuildStandingStaff(root.transform, new Vector3(-3.6f, 1.13f, -2.2f),
            Quaternion.Euler(0f, 90f, 0f), new Color(0.95f, 0.95f, 0.92f), new Color(0.2f, 0.2f, 0.2f),
            new Color(0.9f, 0.78f, 0.68f));
        BuildStandingStaff(root.transform, new Vector3(4.5f, 1.13f, 1.5f),
            Quaternion.Euler(0f, -90f, 0f), new Color(0.9f, 0.55f, 0.35f), new Color(0.3f, 0.35f, 0.55f),
            new Color(0.9f, 0.78f, 0.68f));

        // ── Hanging lamps over the dining tables ──
        foreach (float lz in new[] { -3.5f, 3.5f })
        {
            MakeBlock("LampCord", root.transform, new Vector3(0.04f, 0.5f, 0.04f),
                new Vector3(0.5f, 2.95f, lz), trimC, true);
            MakeBlock("Lamp", root.transform, new Vector3(0.4f, 0.35f, 0.4f),
                new Vector3(0.5f, 2.55f, lz), new Color(0.9f, 0.35f, 0.2f), true);
            MakeBlock("LampGlow", root.transform, new Vector3(0.24f, 0.2f, 0.24f),
                new Vector3(0.5f, 2.55f, lz), new Color(1f, 0.85f, 0.45f), true);
        }

        // ── Interior planters ──
        MakeBlock("Planter", root.transform, new Vector3(0.45f, 0.55f, 0.45f),
            new Vector3(4.6f, 0.28f, -2.7f), new Color(0.62f, 0.38f, 0.2f), true);
        MakeBlock("PlantLeaves", root.transform, new Vector3(0.55f, 0.55f, 0.55f),
            new Vector3(4.6f, 0.78f, -2.7f), new Color(0.25f, 0.55f, 0.25f), true);
        MakeBlock("Planter", root.transform, new Vector3(0.4f, 0.5f, 0.4f),
            new Vector3(2.6f, 0.25f, 4.2f), new Color(0.62f, 0.38f, 0.2f), true);
        MakeBlock("PlantLeaves", root.transform, new Vector3(0.5f, 0.5f, 0.5f),
            new Vector3(2.6f, 0.72f, 4.2f), new Color(0.25f, 0.55f, 0.25f), true);

        // ── Outdoor patio tables (east of building) ──
        foreach (float tz in new[] { -2.6f, 2.6f })
        {
            MakeBlock("PatioTable", root.transform, new Vector3(1.5f, 0.08f, 0.85f),
                new Vector3(halfW + 2.3f, 0.04f, tz), new Color(0.6f, 0.42f, 0.24f), true);
            MakeBlock("PatioLeg", root.transform, new Vector3(0.1f, 0.35f, 0.1f),
                new Vector3(halfW + 2.3f, -0.1f, tz), trimC, true);
            MakeBlock("UmbrellaPole", root.transform, new Vector3(0.08f, 1.5f, 0.08f),
                new Vector3(halfW + 2.3f, 0.75f, tz), trimC, true);
            MakeBlock("UmbrellaCanopy", root.transform, new Vector3(1.4f, 0.1f, 1.4f),
                new Vector3(halfW + 2.3f, 1.55f, tz), awningC, true);
            MakeBlock("Stool", root.transform, new Vector3(0.3f, 0.5f, 0.3f),
                new Vector3(halfW + 1.3f, 0.25f, tz - 0.65f), trimC, true);
        }

        // ── Seated customer at north patio table ──
        MakeBlock("StoolN", root.transform, new Vector3(0.3f, 0.5f, 0.3f),
            new Vector3(halfW + 1.3f, 0.25f, 3.25f), trimC, true);
        BuildSeatedCustomer(root.transform, new Vector3(halfW + 1.3f, 0.5f, 3.25f),
            Quaternion.Euler(0f, -90f, 0f), new Color(0.85f, 0.55f, 0.4f), new Color(0.4f, 0.32f, 0.25f),
            new Color(0.9f, 0.78f, 0.68f));

        // ── Wooden fence + gate (east front, clear of the road) ──
        float fenceX = halfW + 3.6f;
        foreach (float fz in new[] { -5f, -3.2f, -1.9f, 1.9f, 3.2f, 5f })
        {
            MakeBlock("FencePost", root.transform, new Vector3(0.16f, 1.1f, 0.16f),
                new Vector3(fenceX, 0.55f, fz), trimC, true);
        }
        foreach (float secZ in new[] { -2.95f, 2.95f })
        {
            MakeBlock("FenceRailTop", root.transform, new Vector3(0.08f, 0.12f, 4.1f),
                new Vector3(fenceX + 0.16f, 0.98f, secZ), trimC, true);
            MakeBlock("FenceRailBot", root.transform, new Vector3(0.08f, 0.12f, 4.1f),
                new Vector3(fenceX + 0.16f, 0.5f, secZ), trimC, true);
        }
        MakeBlock("GatePostL", root.transform, new Vector3(0.2f, 1.2f, 0.2f),
            new Vector3(fenceX, 0.6f, -0.9f), trimC, true);
        MakeBlock("GatePostR", root.transform, new Vector3(0.2f, 1.2f, 0.2f),
            new Vector3(fenceX, 0.6f, 0.9f), trimC, true);
        MakeBlock("GateLintel", root.transform, new Vector3(0.12f, 0.14f, 1.9f),
            new Vector3(fenceX + 0.18f, 1.25f, 0f), trimC, true);

        // ── Plants, planters, shrubs ──
        MakeBlock("PlanterN", root.transform, new Vector3(0.7f, 0.32f, 0.45f),
            new Vector3(halfW + 1.3f, 0.16f, -4.9f), stoneC, true);
        MakeBlock("PlanterNFlower", root.transform, new Vector3(0.22f, 0.3f, 0.22f),
            new Vector3(halfW + 1.25f, 0.45f, -4.9f), new Color(0.9f, 0.25f, 0.35f), true);
        MakeBlock("PlanterS", root.transform, new Vector3(0.7f, 0.32f, 0.45f),
            new Vector3(halfW + 1.3f, 0.16f, 4.9f), stoneC, true);
        MakeBlock("PlanterSFlower", root.transform, new Vector3(0.22f, 0.3f, 0.22f),
            new Vector3(halfW + 1.25f, 0.45f, 4.9f), new Color(1f, 0.7f, 0.2f), true);
        MakeBlock("PlantPot", root.transform, new Vector3(0.4f, 0.35f, 0.4f),
            new Vector3(halfW + 0.9f, 0.18f, -2.6f), new Color(0.55f, 0.35f, 0.2f), true);
        MakeBlock("PlantLeaf", root.transform, new Vector3(0.65f, 0.75f, 0.65f),
            new Vector3(halfW + 0.9f, 0.65f, -2.6f), new Color(0.25f, 0.55f, 0.2f), true);
        MakeBlock("PlantPot2", root.transform, new Vector3(0.4f, 0.35f, 0.4f),
            new Vector3(halfW + 0.9f, 0.18f, 2.6f), new Color(0.55f, 0.35f, 0.2f), true);
        MakeBlock("PlantLeaf2", root.transform, new Vector3(0.65f, 0.75f, 0.65f),
            new Vector3(halfW + 0.9f, 0.65f, 2.6f), new Color(0.25f, 0.55f, 0.2f), true);
        MakeBlock("ShrubN", root.transform, new Vector3(1f, 0.55f, 0.75f),
            new Vector3(-halfW - 1.2f, 0.28f, -4.3f), new Color(0.2f, 0.45f, 0.2f), true);
        MakeBlock("ShrubS", root.transform, new Vector3(0.9f, 0.5f, 0.7f),
            new Vector3(-halfW - 1.2f, 0.25f, 4.3f), new Color(0.2f, 0.45f, 0.2f), true);

        // ── Chef behind counter ──
        BuildChefNpc(root.transform, new Vector3(-3.6f, 1.13f, 0f), Quaternion.Euler(0f, 90f, 0f));

        root.AddComponent<SteamEmitter>();

        return root;
    }

    private static void BuildChefNpc(Transform parent, Vector3 position, Quaternion rotation)
    {
        var root = new GameObject("RestaurantNPC");
        root.transform.SetParent(parent);
        root.transform.localPosition = position;
        root.transform.localRotation = rotation;

        Color coatC = new Color(0.95f, 0.95f, 0.92f);
        Color skinC = new Color(230f / 255f, 200f / 255f, 175f / 255f);
        Color pantsC = new Color(0.16f, 0.16f, 0.16f);
        Color bootC = new Color(0.1f, 0.1f, 0.1f);

        MakeBlock("LegL", root.transform, new Vector3(0.17f, 0.5f, 0.17f), new Vector3(-0.15f, -0.6f, 0f), pantsC, true);
        MakeBlock("LegR", root.transform, new Vector3(0.17f, 0.5f, 0.17f), new Vector3(0.15f, -0.6f, 0f), pantsC, true);
        MakeBlock("BootL", root.transform, new Vector3(0.2f, 0.09f, 0.3f), new Vector3(-0.15f, -0.88f, 0f), bootC, true);
        MakeBlock("BootR", root.transform, new Vector3(0.2f, 0.09f, 0.3f), new Vector3(0.15f, -0.88f, 0f), bootC, true);

        MakeBlock("Body", root.transform, new Vector3(0.52f, 0.55f, 0.3f), new Vector3(0f, 0f, 0f), coatC, true);
        MakeBlock("Apron", root.transform, new Vector3(0.5f, 0.42f, 0.06f), new Vector3(0f, -0.05f, 0.11f), new Color(0.85f, 0.86f, 0.88f), true);
        MakeBlock("Belt", root.transform, new Vector3(0.54f, 0.07f, 0.06f), new Vector3(0f, 0.12f, -0.16f), pantsC, true);
        MakeBlock("Collar", root.transform, new Vector3(0.2f, 0.08f, 0.06f), new Vector3(0f, 0.34f, -0.15f), coatC, true);

        MakeBlock("Neck", root.transform, new Vector3(0.14f, 0.12f, 0.14f), new Vector3(0f, 0.36f, 0f), skinC, true);
        MakeBlock("Head", root.transform, new Vector3(0.32f, 0.3f, 0.32f), new Vector3(0f, 0.52f, 0f), skinC, true);
        MakeBlock("EyeWhiteL", root.transform, new Vector3(0.08f, 0.06f, 0.03f), new Vector3(-0.09f, 0.55f, -0.16f), new Color(0.95f, 0.95f, 0.97f), true);
        MakeBlock("EyeWhiteR", root.transform, new Vector3(0.08f, 0.06f, 0.03f), new Vector3(0.09f, 0.55f, -0.16f), new Color(0.95f, 0.95f, 0.97f), true);
        MakeBlock("Nose", root.transform, new Vector3(0.07f, 0.06f, 0.05f), new Vector3(0f, 0.51f, -0.17f), skinC, true);

        MakeBlock("HatBase", root.transform, new Vector3(0.36f, 0.06f, 0.36f), new Vector3(0f, 0.65f, 0f), coatC, true);
        MakeBlock("HatCrown", root.transform, new Vector3(0.3f, 0.18f, 0.3f), new Vector3(0f, 0.75f, 0f), coatC, true);

        MakeBlock("ArmL", root.transform, new Vector3(0.14f, 0.45f, 0.14f), new Vector3(-0.36f, 0.1f, 0f), coatC, true);
        MakeBlock("ArmR", root.transform, new Vector3(0.14f, 0.45f, 0.14f), new Vector3(0.36f, 0.1f, 0f), coatC, true);
        MakeBlock("HandL", root.transform, new Vector3(0.12f, 0.1f, 0.12f), new Vector3(-0.36f, -0.14f, 0f), skinC, true);
        MakeBlock("HandR", root.transform, new Vector3(0.12f, 0.1f, 0.12f), new Vector3(0.36f, -0.14f, 0f), skinC, true);

        var col = root.AddComponent<BoxCollider>();
        col.size = new Vector3(0.9f, 1.7f, 0.7f);
        col.center = new Vector3(0f, 0.45f, 0f);
        col.isTrigger = true;

        root.AddComponent<ChefNPC>();
    }

    private static void BuildSeatedCustomer(Transform parent, Vector3 position, Quaternion rotation, Color shirtC, Color pantsC, Color skinC)
    {
        var root = new GameObject("RestaurantCustomer");
        root.transform.SetParent(parent);
        root.transform.localPosition = position;
        root.transform.localRotation = rotation;

        MakeBlock("LegL", root.transform, new Vector3(0.12f, 0.12f, 0.3f), new Vector3(-0.1f, -0.18f, 0.18f), pantsC, true);
        MakeBlock("LegR", root.transform, new Vector3(0.12f, 0.12f, 0.3f), new Vector3(0.1f, -0.18f, 0.18f), pantsC, true);
        MakeBlock("Body", root.transform, new Vector3(0.42f, 0.5f, 0.3f), new Vector3(0f, 0.02f, 0f), shirtC, true);
        MakeBlock("Neck", root.transform, new Vector3(0.12f, 0.1f, 0.12f), new Vector3(0f, 0.32f, 0f), skinC, true);
        MakeBlock("Head", root.transform, new Vector3(0.28f, 0.26f, 0.28f), new Vector3(0f, 0.46f, 0f), skinC, true);
        MakeBlock("Hair", root.transform, new Vector3(0.29f, 0.09f, 0.29f), new Vector3(0f, 0.6f, 0f), new Color(0.15f, 0.1f, 0.07f), true);
        MakeBlock("ArmL", root.transform, new Vector3(0.11f, 0.35f, 0.11f), new Vector3(-0.29f, 0.08f, 0.05f), shirtC, true);
        MakeBlock("ArmR", root.transform, new Vector3(0.11f, 0.35f, 0.11f), new Vector3(0.29f, 0.08f, 0.05f), shirtC, true);
        MakeBlock("HandL", root.transform, new Vector3(0.1f, 0.08f, 0.1f), new Vector3(-0.29f, -0.1f, 0.05f), skinC, true);
        MakeBlock("HandR", root.transform, new Vector3(0.1f, 0.08f, 0.1f), new Vector3(0.29f, -0.1f, 0.05f), skinC, true);
    }

    private static void BuildStandingStaff(Transform parent, Vector3 position, Quaternion rotation, Color shirtC, Color pantsC, Color skinC)
    {
        var root = new GameObject("RestaurantStaff");
        root.transform.SetParent(parent);
        root.transform.localPosition = position;
        root.transform.localRotation = rotation;

        MakeBlock("LegL", root.transform, new Vector3(0.15f, 0.5f, 0.15f), new Vector3(-0.13f, -0.55f, 0f), pantsC, true);
        MakeBlock("LegR", root.transform, new Vector3(0.15f, 0.5f, 0.15f), new Vector3(0.13f, -0.55f, 0f), pantsC, true);
        MakeBlock("BootL", root.transform, new Vector3(0.18f, 0.09f, 0.28f), new Vector3(-0.13f, -0.85f, 0f), new Color(0.1f, 0.1f, 0.1f), true);
        MakeBlock("BootR", root.transform, new Vector3(0.18f, 0.09f, 0.28f), new Vector3(0.13f, -0.85f, 0f), new Color(0.1f, 0.1f, 0.1f), true);
        MakeBlock("Body", root.transform, new Vector3(0.44f, 0.5f, 0.28f), new Vector3(0f, 0f, 0f), shirtC, true);
        MakeBlock("Apron", root.transform, new Vector3(0.42f, 0.4f, 0.05f), new Vector3(0f, -0.04f, 0.1f), new Color(0.95f, 0.95f, 0.92f), true);
        MakeBlock("Neck", root.transform, new Vector3(0.12f, 0.1f, 0.12f), new Vector3(0f, 0.32f, 0f), skinC, true);
        MakeBlock("Head", root.transform, new Vector3(0.28f, 0.26f, 0.28f), new Vector3(0f, 0.47f, 0f), skinC, true);
        MakeBlock("Hair", root.transform, new Vector3(0.29f, 0.09f, 0.29f), new Vector3(0f, 0.61f, 0f), new Color(0.2f, 0.13f, 0.08f), true);
        MakeBlock("ArmL", root.transform, new Vector3(0.12f, 0.4f, 0.12f), new Vector3(-0.31f, 0.08f, 0f), shirtC, true);
        MakeBlock("ArmR", root.transform, new Vector3(0.12f, 0.4f, 0.12f), new Vector3(0.31f, 0.08f, 0f), shirtC, true);
        MakeBlock("HandL", root.transform, new Vector3(0.1f, 0.08f, 0.1f), new Vector3(-0.31f, -0.14f, 0f), skinC, true);
        MakeBlock("HandR", root.transform, new Vector3(0.1f, 0.08f, 0.1f), new Vector3(0.31f, -0.14f, 0f), skinC, true);
    }

}

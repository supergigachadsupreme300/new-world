/// <summary>
/// MapBuilder partial: STORES (convenience store, library, shop, fishing shop).
/// </summary>
using System.Collections.Generic;
using UnityEngine;
using CountryLife.Helpers;

public static partial class MapBuilder
{
    // ==================== MapBuilderConvenienceStore.cs ====================
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
        storeSignLabel.transform.localScale = new Vector3(-1f, 1f, 1f);
        var storeSignTmp = storeSignLabel.AddComponent<TMPro.TextMeshPro>();
        storeSignTmp.text = Localization.T("TIỆN LỢI");
        storeSignTmp.fontSize = 2.0f;
        storeSignTmp.alignment = TMPro.TextAlignmentOptions.Center;
        storeSignTmp.color = new Color(0.98f, 0.94f, 0.85f);
        storeSignTmp.outlineWidth = 0.18f;
        storeSignTmp.outlineColor = Color.black;
        storeSignTmp.rectTransform.sizeDelta = new Vector3(8.4f, 1.5f);

        // ── Window on the back wall ──
        MakeBlock("WinGlass", root.transform, new Vector3(0.12f, 1.4f, 2f), new Vector3(-hw - 0.02f, 2.3f, 0f), winC, true);

        // ── Counter inside (moved in from the door) ──
        MakeBlock("Counter", root.transform, new Vector3(2f, 1f, 4.5f), new Vector3(1.0f, 0.5f, 0f), counterC);
        MakeBlock("CounterTop", root.transform, new Vector3(2f, 0.08f, 4.7f), new Vector3(1.0f, 0.98f, 0f), new Color(0.7f, 0.75f, 0.8f), true);
        MakeBlock("Register", root.transform, new Vector3(0.5f, 0.4f, 0.5f), new Vector3(1.2f, 1.22f, 0f), frameC, true);

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

        // ── Shopkeeper behind the counter, facing the door ──
        BuildMarketNpc(root.transform, "ConvenienceNPC", new Vector3(-0.3f, 1.13f, 0f), Quaternion.Euler(0f, -90f, 0f));

        return root;
    }

    // ==================== MapBuilderLibrary.cs ====================
    // ═══════════════════════════════════════════════════════════════
    //  LIBRARY  (10 x 5 x 8, bookshelves, reading table, entrance on east)
    //  + LIBRARIAN NPC (knowledge vendor / blueprint research)
    // ═══════════════════════════════════════════════════════════════

    public static GameObject BuildLibrary(Transform parent, Vector3 position, float scale = 1f, Quaternion rotation = default)
    {
        var root = new GameObject("Library");
        root.transform.SetParent(parent);
        root.transform.position = position;
        root.transform.rotation = (rotation == default) ? Quaternion.identity : rotation;
        root.transform.localScale = Vector3.one * scale;

        Color wallC    = new Color(0.93f, 0.88f, 0.79f);
        Color trimC    = new Color(0.4f, 0.27f, 0.15f);
        Color roofC    = new Color(0.24f, 0.33f, 0.45f);
        Color ridgeC   = new Color(0.16f, 0.24f, 0.35f);
        Color eaveC    = new Color(0.2f, 0.2f, 0.2f);
        Color stoneC   = new Color(0.44f, 0.4f, 0.36f);
        Color floorC   = new Color(0.62f, 0.45f, 0.28f);
        Color frameC   = new Color(0.3f, 0.18f, 0.1f);
        Color shelfC   = new Color(0.45f, 0.28f, 0.16f);
        Color signC    = new Color(0.89f, 0.75f, 0.1f);
        Color awningC  = new Color(0.2f, 0.55f, 0.55f);
        Color bookRed  = new Color(0.8f, 0.25f, 0.2f);
        Color bookGrn  = new Color(0.25f, 0.6f, 0.3f);
        Color bookBlu  = new Color(0.25f, 0.4f, 0.75f);
        Color bookGld  = new Color(0.9f, 0.7f, 0.2f);

        // ── Walls (entrance gap on the +x side) ──
        MakeBlock("Wall", root.transform, new Vector3(10f, 5f, 0.4f), new Vector3(0f, 2.5f, -4f), wallC);
        MakeBlock("Wall", root.transform, new Vector3(10f, 5f, 0.4f), new Vector3(0f, 2.5f, 4f), wallC);
        MakeBlock("Wall", root.transform, new Vector3(0.4f, 5f, 8f), new Vector3(-5f, 2.5f, 0f), wallC);
        MakeBlock("Wall", root.transform, new Vector3(0.4f, 5f, 2.5f), new Vector3(5f, 2.5f, -3.25f), wallC);
        MakeBlock("Wall", root.transform, new Vector3(0.4f, 5f, 2.5f), new Vector3(5f, 2.5f, 3.25f), wallC);
        MakeBlock("Transom", root.transform, new Vector3(0.4f, 1.2f, 3f), new Vector3(5f, 4.4f, 0f), wallC);
        MakeBlock("WallTrim", root.transform, new Vector3(10.2f, 0.3f, 0.45f), new Vector3(0f, 0.4f, 4f), trimC);
        MakeBlock("WallTrim", root.transform, new Vector3(10.2f, 0.3f, 0.45f), new Vector3(0f, 0.4f, -4f), trimC);

        // ── Floor + stone foundation ──
        MakeBlock("Floor", root.transform, new Vector3(10f, 0.4f, 8f), new Vector3(0f, 0.05f, 0f), floorC);
        MakeBlock("Foundation", root.transform, new Vector3(11.5f, 0.4f, 9.5f), new Vector3(0f, -0.15f, 0f), stoneC);

        // ── Gabled roof ──
        float rise = 2.5f;
        float halfW = 5f;
        float panelLen = Mathf.Sqrt(halfW * halfW + rise * rise);
        float tilt = Mathf.Atan2(rise, halfW) * Mathf.Rad2Deg;
        float overhang = 1.2f;
        float roofZ = 8f + overhang * 2f;

        MakeBlock("RoofPanel", root.transform, new Vector3(panelLen, 0.5f, roofZ),
            new Vector3(halfW / 2f, 5f + rise / 2f, 0f), roofC).transform.rotation = Quaternion.Euler(0f, 0f, -tilt);
        MakeBlock("RoofPanel", root.transform, new Vector3(panelLen, 0.5f, roofZ),
            new Vector3(-halfW / 2f, 5f + rise / 2f, 0f), roofC).transform.rotation = Quaternion.Euler(0f, 0f, tilt);
        MakeBlock("Ridge", root.transform, new Vector3(0.55f, 0.3f, roofZ + 0.2f),
            new Vector3(0f, 5f + rise + 0.1f, 0f), ridgeC);
        MakeBlock("Eave", root.transform, new Vector3(0.5f, 0.25f, roofZ + 0.2f),
            new Vector3(halfW, 5.1f, 0f), eaveC);
        MakeBlock("Eave", root.transform, new Vector3(0.5f, 0.25f, roofZ + 0.2f),
            new Vector3(-halfW, 5.1f, 0f), eaveC);

        // ── Gable end fillers (triangular approximation) ──
        for (int ge = 0; ge < 2; ge++)
        {
            float gz = ge == 0 ? -4f : 4f;
            for (int i = 0; i < 5; i++)
            {
                float gy = 5f + i * 0.5f;
                float gw = halfW * 2f * (1f - (float)i / 5f);
                MakeBlock("GableEnd", root.transform, new Vector3(gw, 0.5f, 0.35f),
                    new Vector3(0f, gy + 0.25f, gz), wallC);
            }
        }

        // ── Sign above entrance ──
        MakeBlock("Sign", root.transform, new Vector3(0.2f, 0.8f, 3.5f),
            new Vector3(5.08f, 4.4f, 0f), signC, true);
        var librarySignLabel = new GameObject("LibrarySignLabel");
        librarySignLabel.transform.SetParent(root.transform);
        librarySignLabel.transform.localPosition = new Vector3(5.3f, 4.4f, 0f);
        librarySignLabel.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);
        librarySignLabel.transform.localScale = new Vector3(-1f, 1f, 1f);
        var librarySignTmp = librarySignLabel.AddComponent<TMPro.TextMeshPro>();
        librarySignTmp.text = Localization.T("THƯ VIỆN");
        librarySignTmp.fontSize = 2.0f;
        librarySignTmp.alignment = TMPro.TextAlignmentOptions.Center;
        librarySignTmp.color = new Color(0.98f, 0.94f, 0.85f);
        librarySignTmp.outlineWidth = 0.18f;
        librarySignTmp.outlineColor = Color.black;
        librarySignTmp.rectTransform.sizeDelta = new Vector3(6.4f, 1.5f);

        // ── Entrance awning ──
        MakeBlock("Awning", root.transform, new Vector3(1.5f, 0.15f, 3.5f),
            new Vector3(5.8f, 5f, 0f), awningC, true);
        MakeBlock("AwningPost", root.transform, new Vector3(0.12f, 5f, 0.12f),
            new Vector3(6.6f, 2.5f, -1.5f), frameC, true);
        MakeBlock("AwningPost", root.transform, new Vector3(0.12f, 5f, 0.12f),
            new Vector3(6.6f, 2.5f, 1.5f), frameC, true);

        // ── Door frame ──
        MakeBlock("DoorFrame", root.transform, new Vector3(0.25f, 3.5f, 0.25f),
            new Vector3(5.04f, 1.75f, -1.5f), frameC, true);
        MakeBlock("DoorFrame", root.transform, new Vector3(0.25f, 3.5f, 0.25f),
            new Vector3(5.04f, 1.75f, 1.5f), frameC, true);
        MakeBlock("DoorLintel", root.transform, new Vector3(0.25f, 0.3f, 3.25f),
            new Vector3(5.04f, 3.65f, 0f), frameC, true);

        // ── Windows on front (+z) and west (-x) walls ──
        foreach (float wx in new[] { -3f, 3f })
        {
            MakeBlock("WinGlass", root.transform, new Vector3(1.4f, 1.2f, 0.14f),
                new Vector3(wx, 2.4f, -4.25f), new Color(0.65f, 0.8f, 0.9f), true);
            MakeBlock("WinFrame", root.transform, new Vector3(0.1f, 1.2f, 0.16f),
                new Vector3(wx, 2.4f, -4.25f), frameC, true);
            MakeBlock("WinFrame", root.transform, new Vector3(1.4f, 0.08f, 0.16f),
                new Vector3(wx, 2.4f, -4.25f), frameC, true);
        }
        MakeBlock("WinGlass", root.transform, new Vector3(0.14f, 1.2f, 1.4f),
            new Vector3(-5.25f, 2.4f, 2f), new Color(0.65f, 0.8f, 0.9f), true);
        MakeBlock("WinFrame", root.transform, new Vector3(0.16f, 1.2f, 0.1f),
            new Vector3(-5.25f, 2.4f, 2f), frameC, true);
        MakeBlock("WinFrame", root.transform, new Vector3(0.16f, 0.08f, 1.4f),
            new Vector3(-5.25f, 2.4f, 2f), frameC, true);

        // ── Bookshelves along the back (-z) wall ──
        foreach (float sx in new[] { -2.6f, 2.6f })
        {
            MakeBlock("ShelfUnit", root.transform, new Vector3(2.2f, 3.2f, 0.4f),
                new Vector3(sx, 1.85f, -3.45f), shelfC);
            for (int row = 0; row < 3; row++)
            {
                float by = 0.7f + row * 0.95f;
                MakeBlock("BookShelfBoard", root.transform, new Vector3(1.9f, 0.07f, 0.32f),
                    new Vector3(sx, by - 0.31f, -3.2f), shelfC);
                for (int i = 0; i < 5; i++)
                {
                    Color bc = i % 4 == 0 ? bookRed : (i % 4 == 1 ? bookGrn : (i % 4 == 2 ? bookBlu : bookGld));
                    MakeBlock("Book", root.transform, new Vector3(0.2f, 0.55f, 0.2f),
                        new Vector3(sx - 0.8f + i * 0.4f, by, -3.2f), bc, true);
                }
            }
            MakeBlock("BookShelfBoard", root.transform, new Vector3(1.9f, 0.07f, 0.32f),
                new Vector3(sx, 2.86f, -3.2f), shelfC);
            for (int i = 0; i < 4; i++)
            {
                Color bc = i % 4 == 0 ? bookGld : (i % 4 == 1 ? bookRed : (i % 4 == 2 ? bookGrn : bookBlu));
                MakeBlock("BookTop", root.transform, new Vector3(0.18f, 0.45f, 0.18f),
                    new Vector3(sx - 0.68f + i * 0.44f, 3.15f, -3.2f), bc, true);
            }
        }
        MakeBlock("BookShelfBoard", root.transform, new Vector3(2.0f, 0.07f, 0.32f),
            new Vector3(0f, 2.53f, -3.2f), shelfC);
        MakeBlock("BookRowGld", root.transform, new Vector3(1.9f, 0.5f, 0.22f),
            new Vector3(0f, 2.85f, -3.2f), bookGld, true);

        // ── Bookshelves along the front (+z) wall ──
        foreach (float sx in new[] { -2.6f, 2.6f })
        {
            MakeBlock("ShelfUnitFront", root.transform, new Vector3(2.2f, 2.8f, 0.4f),
                new Vector3(sx, 1.65f, 3.45f), shelfC);
            for (int row = 0; row < 3; row++)
            {
                float by = 0.7f + row * 0.85f;
                MakeBlock("BookShelfBoardFront", root.transform, new Vector3(1.9f, 0.07f, 0.32f),
                    new Vector3(sx, by - 0.29f, 3.2f), shelfC);
                for (int i = 0; i < 5; i++)
                {
                    Color bc = i % 4 == 0 ? bookRed : (i % 4 == 1 ? bookGrn : (i % 4 == 2 ? bookBlu : bookGld));
                    MakeBlock("BookFront", root.transform, new Vector3(0.2f, 0.5f, 0.2f),
                        new Vector3(sx - 0.8f + i * 0.4f, by, 3.2f), bc, true);
                }
            }
        }

        // ── Shelf unit on the east wall (near the entrance) ──
        MakeBlock("ShelfEast", root.transform, new Vector3(0.5f, 2.4f, 1.6f),
            new Vector3(4.55f, 1.45f, 2.8f), shelfC);
        for (int row = 0; row < 2; row++)
        {
            float by = 0.75f + row * 0.9f;
            MakeBlock("BookShelfBoardEast", root.transform, new Vector3(0.32f, 0.07f, 1.0f),
                new Vector3(4.25f, by - 0.29f, 2.6f), shelfC);
            for (int i = 0; i < 3; i++)
            {
                Color bc = i % 3 == 0 ? bookGrn : (i % 3 == 1 ? bookBlu : bookRed);
                MakeBlock("BookEast", root.transform, new Vector3(0.2f, 0.5f, 0.2f),
                    new Vector3(4.25f, by, 2.2f + i * 0.4f), bc, true);
            }
        }

        // ── Librarian desk against the back wall (long side along X) ──
        MakeBlock("Desk", root.transform, new Vector3(2.4f, 1f, 1.6f),
            new Vector3(0f, 0.75f, -2.2f), trimC);
        MakeBlock("DeskTop", root.transform, new Vector3(2.6f, 0.08f, 1.6f),
            new Vector3(0f, 1.29f, -2.2f), floorC, true);
        MakeBlock("OpenBook", root.transform, new Vector3(0.9f, 0.06f, 1.2f),
            new Vector3(0f, 1.38f, -2.2f), new Color(0.95f, 0.93f, 0.85f), true);

        // ── Desk props ──
        MakeBlock("GlobeBase", root.transform, new Vector3(0.16f, 0.05f, 0.16f), new Vector3(-0.85f, 1.38f, -1.65f), trimC, true);
        MakeBlock("GlobeStand", root.transform, new Vector3(0.05f, 0.22f, 0.05f), new Vector3(-0.85f, 1.45f, -1.65f), ridgeC, true);
        MakeBlock("Globe", root.transform, new Vector3(0.2f, 0.2f, 0.2f), new Vector3(-0.85f, 1.65f, -1.65f), bookBlu, true);
        MakeBlock("Papers", root.transform, new Vector3(0.35f, 0.04f, 0.5f), new Vector3(0.85f, 1.39f, -2.75f), new Color(0.95f, 0.93f, 0.85f), true);
        MakeBlock("PaperTop", root.transform, new Vector3(0.3f, 0.04f, 0.45f), new Vector3(0.85f, 1.43f, -2.77f), new Color(0.9f, 0.87f, 0.78f), true);
        MakeBlock("InkPot", root.transform, new Vector3(0.12f, 0.2f, 0.12f), new Vector3(0.95f, 1.4f, -1.75f), ridgeC, true);
        MakeBlock("Quill", root.transform, new Vector3(0.04f, 0.55f, 0.04f), new Vector3(1.02f, 1.44f, -1.67f), trimC, true);
        MakeBlock("DeskCandle", root.transform, new Vector3(0.08f, 0.22f, 0.08f), new Vector3(-0.85f, 1.4f, -2.75f), new Color(0.96f, 0.92f, 0.8f), true);
        MakeBlock("DeskCandleFlame", root.transform, new Vector3(0.06f, 0.09f, 0.06f), new Vector3(-0.85f, 1.56f, -2.75f), bookGld, true);

        // ── Librarian chair behind the desk ──
        MakeBlock("LibrarianChair", root.transform, new Vector3(0.8f, 0.7f, 0.8f),
            new Vector3(0f, 0.6f, -3.38f), shelfC, true);
        MakeBlock("LibrarianChairBack", root.transform, new Vector3(0.1f, 0.6f, 0.8f),
            new Vector3(0f, 1.0f, -3.7f), trimC, true);

        // ── Wall clock on the back wall ──
        MakeBlock("Clock", root.transform, new Vector3(0.06f, 0.5f, 0.5f), new Vector3(0f, 4.15f, -3.97f), new Color(0.95f, 0.93f, 0.85f), true);
        MakeBlock("ClockRim", root.transform, new Vector3(0.07f, 0.55f, 0.55f), new Vector3(0f, 4.15f, -3.96f), trimC, true);

        // ── Window bench along the west wall ──
        MakeBlock("Bench", root.transform, new Vector3(0.45f, 0.5f, 2.6f), new Vector3(-4.6f, 0.5f, 2f), trimC, true);
        MakeBlock("BenchCushion", root.transform, new Vector3(0.47f, 0.12f, 2.62f), new Vector3(-4.6f, 0.81f, 2f), bookRed, true).AddComponent<SittableSeat>();

        // ── Potted plants in the corners ──
        MakeBlock("PlantPotSW", root.transform, new Vector3(0.34f, 0.38f, 0.34f), new Vector3(-4.55f, 0.44f, -3.35f), new Color(0.55f, 0.35f, 0.2f), true);
        MakeBlock("PlantLeavesSW", root.transform, new Vector3(0.62f, 0.5f, 0.62f), new Vector3(-4.55f, 0.85f, -3.35f), bookGrn, true);
        MakeBlock("PlantPotNE", root.transform, new Vector3(0.3f, 0.42f, 0.3f), new Vector3(4.5f, 0.46f, 3.35f), new Color(0.55f, 0.35f, 0.2f), true);
        MakeBlock("PlantLeavesNE", root.transform, new Vector3(0.55f, 0.55f, 0.55f), new Vector3(4.5f, 0.93f, 3.35f), bookGrn, true);

        // ── Lantern hanging from the ridge over the reading table ──
        MakeBlock("LanternChain", root.transform, new Vector3(0.03f, 1.6f, 0.03f), new Vector3(0f, 5.75f, 0f), ridgeC, true);
        MakeBlock("Lantern", root.transform, new Vector3(0.28f, 0.32f, 0.28f), new Vector3(0f, 4.92f, 0f), eaveC, true);
        MakeBlock("LanternGlow", root.transform, new Vector3(0.16f, 0.2f, 0.16f), new Vector3(0f, 4.92f, 0f), bookGld, true);

        // ── Exterior: welcome mat + entrance bushes ──
        MakeBlock("Mat", root.transform, new Vector3(1.4f, 0.06f, 2.2f), new Vector3(6.2f, 0.03f, 0f), bookRed, true);
        MakeBlock("BushL", root.transform, new Vector3(1.0f, 0.65f, 0.8f), new Vector3(6.9f, 0.33f, -2.8f), new Color(0.22f, 0.5f, 0.16f), true);
        MakeBlock("BushR", root.transform, new Vector3(1.0f, 0.65f, 0.8f), new Vector3(6.9f, 0.33f, 2.8f), new Color(0.22f, 0.5f, 0.16f), true);

        // ── Exterior: stepping-stone path toward the road ──
        for (int i = 0; i < 3; i++)
        {
            MakeBlock("PathStone", root.transform, new Vector3(1.0f, 0.12f, 1.4f), new Vector3(7.4f + i * 1.6f, 0.06f, 0f), stoneC, true);
        }

        return root;
    }

    public static GameObject BuildLibrarianNpc(Transform parent, Vector3 position = default, Quaternion rotation = default)
    {
        var root = new GameObject("LibrarianNPC");
        root.transform.SetParent(parent);
        root.transform.position = position;
        root.transform.rotation = (rotation == default) ? Quaternion.identity : rotation;

        Color robeC   = new Color(0.35f, 0.22f, 0.42f);
        Color robeDark= new Color(0.27f, 0.16f, 0.33f);
        Color skinC   = new Color(230f / 255f, 200f / 255f, 175f / 255f);
        Color shoeC   = new Color(0.2f, 0.14f, 0.1f);
        Color hairC   = new Color(0.85f, 0.85f, 0.85f);
        Color glassC  = new Color(0.12f, 0.12f, 0.12f);
        Color bookC   = new Color(0.8f, 0.25f, 0.2f);

        MakeBlock("LegL", root.transform, new Vector3(0.2f, 0.5f, 0.2f), new Vector3(-0.15f, -0.6f, 0f), robeDark, true);
        MakeBlock("LegR", root.transform, new Vector3(0.2f, 0.5f, 0.2f), new Vector3(0.15f, -0.6f, 0f), robeDark, true);
        MakeBlock("ShoeL", root.transform, new Vector3(0.24f, 0.1f, 0.34f), new Vector3(-0.15f, -0.86f, 0f), shoeC, true);
        MakeBlock("ShoeR", root.transform, new Vector3(0.24f, 0.1f, 0.34f), new Vector3(0.15f, -0.86f, 0f), shoeC, true);

        MakeBlock("Robe", root.transform, new Vector3(0.58f, 0.6f, 0.36f), new Vector3(0f, 0f, 0f), robeC, true);
        MakeBlock("Sash", root.transform, new Vector3(0.6f, 0.09f, 0.08f), new Vector3(0f, 0.1f, -0.17f), robeDark, true);
        MakeBlock("Collar", root.transform, new Vector3(0.34f, 0.06f, 0.16f), new Vector3(0f, 0.3f, -0.16f), new Color(0.95f, 0.93f, 0.85f), true);

        MakeBlock("Neck", root.transform, new Vector3(0.14f, 0.12f, 0.14f), new Vector3(0f, 0.36f, 0f), skinC, true);
        MakeBlock("Head", root.transform, new Vector3(0.3f, 0.28f, 0.3f), new Vector3(0f, 0.52f, 0f), skinC, true);
        MakeBlock("Hair", root.transform, new Vector3(0.32f, 0.1f, 0.32f), new Vector3(0f, 0.67f, 0f), hairC, true);
        MakeBlock("EyeWhiteL", root.transform, new Vector3(0.08f, 0.06f, 0.03f), new Vector3(-0.09f, 0.55f, -0.16f), new Color(0.95f, 0.95f, 0.97f), true);
        MakeBlock("EyeWhiteR", root.transform, new Vector3(0.08f, 0.06f, 0.03f), new Vector3(0.09f, 0.55f, -0.16f), new Color(0.95f, 0.95f, 0.97f), true);
        MakeBlock("GlassesL", root.transform, new Vector3(0.12f, 0.09f, 0.04f), new Vector3(-0.09f, 0.55f, -0.175f), glassC, true);
        MakeBlock("GlassesR", root.transform, new Vector3(0.12f, 0.09f, 0.04f), new Vector3(0.09f, 0.55f, -0.175f), glassC, true);
        MakeBlock("GlassesBridge", root.transform, new Vector3(0.1f, 0.03f, 0.03f), new Vector3(0f, 0.55f, -0.175f), glassC, true);
        MakeBlock("Nose", root.transform, new Vector3(0.07f, 0.06f, 0.05f), new Vector3(0f, 0.51f, -0.17f), skinC, true);

        MakeBlock("ArmL", root.transform, new Vector3(0.15f, 0.45f, 0.15f), new Vector3(-0.37f, 0.1f, 0f), robeC, true);
        MakeBlock("ArmR", root.transform, new Vector3(0.15f, 0.45f, 0.15f), new Vector3(0.37f, 0.1f, 0f), robeC, true);
        MakeBlock("HandL", root.transform, new Vector3(0.12f, 0.1f, 0.12f), new Vector3(-0.37f, -0.14f, 0f), skinC, true);
        MakeBlock("HandR", root.transform, new Vector3(0.12f, 0.1f, 0.12f), new Vector3(0.37f, -0.14f, 0f), skinC, true);
        MakeBlock("Book", root.transform, new Vector3(0.24f, 0.14f, 0.3f), new Vector3(0.37f, -0.05f, 0.1f), bookC, true);
        MakeBlock("BookPage", root.transform, new Vector3(0.24f, 0.02f, 0.3f), new Vector3(0.37f, -0.04f, 0.1f), new Color(0.95f, 0.93f, 0.85f), true);

        var col = root.AddComponent<BoxCollider>();
        col.size = new Vector3(0.9f, 1.7f, 0.7f);
        col.center = new Vector3(0f, 0.45f, 0f);
        col.isTrigger = true;

        return root;
    }

    // ==================== MapBuilderShop.cs ====================
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
            new Vector3(0f, -0.15f, 0f), stoneC);

        // ── Sign ──
        MakeBlock("Sign", root.transform, new Vector3(0.2f, 0.8f, 3.5f),
            new Vector3(5.08f, 3.6f, 0f), signC, true);
        var shopSignLabel = new GameObject("ShopSignLabel");
        shopSignLabel.transform.SetParent(root.transform);
        shopSignLabel.transform.localPosition = new Vector3(5.3f, 3.6f, 0f);
        shopSignLabel.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);
        shopSignLabel.transform.localScale = new Vector3(-1f, 1f, 1f);
        var shopSignTmp = shopSignLabel.AddComponent<TMPro.TextMeshPro>();
        shopSignTmp.text = Localization.T("CỬA HÀNG");
        shopSignTmp.fontSize = 2.0f;
        shopSignTmp.alignment = TMPro.TextAlignmentOptions.Center;
        shopSignTmp.color = new Color(0.98f, 0.94f, 0.85f);
        shopSignTmp.outlineWidth = 0.18f;
        shopSignTmp.outlineColor = Color.black;
        shopSignTmp.rectTransform.sizeDelta = new Vector3(6.4f, 1.5f);

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
            new Vector3(-0.5f, 0.5f, 0f), counterC);
        MakeBlock("CounterTop", root.transform, new Vector3(1.8f, 0.08f, 4.2f),
            new Vector3(-0.5f, 1.04f, 0f), new Color(0.757f, 0.62f, 0.404f), true);
        MakeBlock("CounterFront", root.transform, new Vector3(0.03f, 0.8f, 4f),
            new Vector3(0.4f, 0.4f, 0f), new Color(0.624f, 0.369f, 0.192f), true);

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

    // ==================== MapBuilderFishingShop.cs ====================
    // ═══════════════════════════════════════════════════════════════
    //  FISHING TOOLS SHOP  (open-air stilted hut)
    // ═══════════════════════════════════════════════════════════════

    public static GameObject BuildFishingShop(Transform parent, Vector3 position, float scale = 1f, Quaternion rotation = default)
    {
        var root = new GameObject("FishingShop");
        root.transform.SetParent(parent);
        root.transform.position = position;
        root.transform.rotation = (rotation == default) ? Quaternion.identity : rotation;
        root.transform.localScale = Vector3.one * scale;

        Color woodC     = new Color(0.45f, 0.30f, 0.15f);
        Color woodDarkC = new Color(0.30f, 0.20f, 0.10f);
        Color thatchC   = new Color(0.72f, 0.60f, 0.35f);
        Color ropeC     = new Color(0.55f, 0.45f, 0.28f);
        Color netC      = new Color(0.80f, 0.80f, 0.75f);
        Color waterC    = new Color(0.30f, 0.55f, 0.85f);
        Color bucketC   = new Color(0.35f, 0.35f, 0.38f);
        Color iceC      = new Color(0.85f, 0.92f, 0.97f);
        Color fishOrange = new Color(1.0f, 0.65f, 0.2f);
        Color fishPink  = new Color(1.0f, 0.50f, 0.40f);
        Color signC     = new Color(0.90f, 0.68f, 0.16f);
        Color frameC    = new Color(0.28f, 0.18f, 0.10f);
        Color winC      = new Color(0.55f, 0.78f, 0.86f);
        Color awningC   = new Color(0.25f, 0.45f, 0.30f);

        float stiltH = 0.8f;
        float halfW = 5f;
        float halfD = 4.5f;

        // ── Stilts (wooden pillars raising the floor) ──
        float[][] stiltPos = new float[][] {
            new float[] { -halfW, -halfD }, new float[] { -halfW, halfD },
            new float[] { halfW, -halfD }, new float[] { halfW, halfD },
            new float[] { 0f, -halfD }, new float[] { 0f, halfD },
            new float[] { -halfW, 0f }, new float[] { halfW, 0f }
        };
        foreach (var sp in stiltPos)
        {
            MakeBlock("Stilt", root.transform, new Vector3(0.18f, stiltH, 0.18f),
                new Vector3(sp[0], stiltH / 2f, sp[1]), woodDarkC, true);
        }

        // ── Platform floor (elevated) ──
        MakeBlock("PlatformFloor", root.transform, new Vector3(halfW * 2f, 0.15f, halfD * 2f),
            new Vector3(0f, stiltH, 0f), woodC);

        // ── Platform deck planks ──
        for (float fx = -halfW + 0.5f; fx <= halfW; fx += 1f)
        {
            MakeBlock("Plank", root.transform, new Vector3(0.08f, 0.16f, halfD * 2f),
                new Vector3(fx, stiltH, 0f), woodDarkC, true);
        }

        // ── Railing around platform ──
        float railY = stiltH + 0.55f;
        // Front railing (+Z side)
        for (float rx = -halfW + 1f; rx <= halfW - 0.5f; rx += 1.5f)
        {
            MakeBlock("RailF", root.transform, new Vector3(0.08f, 1.2f, 0.08f),
                new Vector3(rx, railY, halfD), woodC, true);
        }
        MakeBlock("RailBarF", root.transform, new Vector3(halfW * 2f, 0.08f, 0.08f),
            new Vector3(0f, railY, halfD), woodC, true);
        MakeBlock("RailBarF2", root.transform, new Vector3(halfW * 2f, 0.08f, 0.08f),
            new Vector3(0f, railY - 0.4f, halfD), woodC, true);
        // Back railing (-Z side)
        for (float rx = -halfW + 1f; rx <= halfW - 0.5f; rx += 1.5f)
        {
            MakeBlock("RailB", root.transform, new Vector3(0.08f, 1.2f, 0.08f),
                new Vector3(rx, railY, -halfD), woodC, true);
        }
        MakeBlock("RailBarB", root.transform, new Vector3(halfW * 2f, 0.08f, 0.08f),
            new Vector3(0f, railY, -halfD), woodC, true);
        MakeBlock("RailBarB2", root.transform, new Vector3(halfW * 2f, 0.08f, 0.08f),
            new Vector3(0f, railY - 0.4f, -halfD), woodC, true);
        // Left railing (-X side)
        for (float rz = -halfD + 1f; rz <= halfD - 0.5f; rz += 1.5f)
        {
            MakeBlock("RailL", root.transform, new Vector3(0.08f, 1.2f, 0.08f),
                new Vector3(-halfW, railY, rz), woodC, true);
        }
        MakeBlock("RailBarL", root.transform, new Vector3(0.08f, 0.08f, halfD * 2f),
            new Vector3(-halfW, railY, 0f), woodC, true);
        MakeBlock("RailBarL2", root.transform, new Vector3(0.08f, 0.08f, halfD * 2f),
            new Vector3(-halfW, railY - 0.4f, 0f), woodC, true);
        // Right railing (+X side)
        for (float rz = -halfD + 1f; rz <= halfD - 0.5f; rz += 1.5f)
        {
            MakeBlock("RailR", root.transform, new Vector3(0.08f, 1.2f, 0.08f),
                new Vector3(halfW, railY, rz), woodC, true);
        }
        MakeBlock("RailBarR", root.transform, new Vector3(0.08f, 0.08f, halfD * 2f),
            new Vector3(halfW, railY, 0f), woodC, true);
        MakeBlock("RailBarR2", root.transform, new Vector3(0.08f, 0.08f, halfD * 2f),
            new Vector3(halfW, railY - 0.4f, 0f), woodC, true);

        // Corner posts (connect railing to platform at each corner)
        foreach (var cp in new float[][] { new float[] { -halfW, -halfD }, new float[] { -halfW, halfD },
            new float[] { halfW, -halfD }, new float[] { halfW, halfD } })
        {
            MakeBlock("CornerPost", root.transform, new Vector3(0.14f, railY - stiltH + 0.1f, 0.14f),
                new Vector3(cp[0], (railY + stiltH) / 2f, cp[1]), woodDarkC, true);
        }

        // ── Roof posts (4 corner + 2 center) ──
        float roofBase = stiltH + 0.15f;
        float postH = 3.2f;
        float[][] roofPosts = new float[][] {
            new float[] { -halfW, -halfD }, new float[] { -halfW, halfD },
            new float[] { halfW, -halfD }, new float[] { halfW, halfD },
            new float[] { 0f, -halfD }, new float[] { 0f, halfD }
        };
        foreach (var rp in roofPosts)
        {
            MakeBlock("RoofPost", root.transform, new Vector3(0.18f, postH, 0.18f),
                new Vector3(rp[0], roofBase + postH / 2f, rp[1]), woodDarkC, true);
        }

        // ── Thatched roof (two sloped panels) ──
        float roofH = roofBase + postH;
        float roofRise = 1.5f;
        float roofOverhang = 1.2f;
        float roofZLen = halfD * 2f + roofOverhang * 2f;
        float panelLen = Mathf.Sqrt(halfW * halfW + roofRise * roofRise) + roofOverhang * 0.5f;
        float tilt = Mathf.Atan2(roofRise, halfW) * Mathf.Rad2Deg;

        MakeBlock("RoofPanel", root.transform, new Vector3(panelLen, 0.4f, roofZLen),
            new Vector3(halfW / 2f, roofH + roofRise / 2f, 0f), thatchC).transform.rotation = Quaternion.Euler(0f, 0f, tilt);
        MakeBlock("RoofPanel", root.transform, new Vector3(panelLen, 0.4f, roofZLen),
            new Vector3(-halfW / 2f, roofH + roofRise / 2f, 0f), thatchC).transform.rotation = Quaternion.Euler(0f, 0f, -tilt);
        MakeBlock("Ridge", root.transform, new Vector3(0.4f, 0.25f, roofZLen + 0.3f),
            new Vector3(0f, roofH + roofRise + 0.05f, 0f), woodDarkC);

        // ── Fishing nets draped from roof edges ──
        MakeBlock("Net1", root.transform, new Vector3(0.08f, 1.5f, 3f),
            new Vector3(halfW + 0.3f, roofH + 0.5f, 1.5f), netC, true);
        MakeBlock("Net2", root.transform, new Vector3(0.08f, 1.2f, 2.5f),
            new Vector3(halfW + 0.3f, roofH + 0.6f, -1.5f), netC, true);
        MakeBlock("Net3", root.transform, new Vector3(2f, 1.5f, 0.08f),
            new Vector3(-2f, roofH + 0.5f, -halfD - 0.3f), netC, true);

        // ── Sign ──
        MakeBlock("Sign", root.transform, new Vector3(0.2f, 0.8f, 3f),
            new Vector3(halfW + 0.25f, roofH - 0.5f, 0f), signC, true);
        var fishingSignLabel = new GameObject("FishingShopSignLabel");
        fishingSignLabel.transform.SetParent(root.transform);
        fishingSignLabel.transform.localPosition = new Vector3(halfW + 0.45f, roofH - 0.5f, 0f);
        fishingSignLabel.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);
        fishingSignLabel.transform.localScale = new Vector3(-1f, 1f, 1f);
        var fishingSignTmp = fishingSignLabel.AddComponent<TMPro.TextMeshPro>();
        fishingSignTmp.text = Localization.T("CỬA HÀNG CÂU CÁ");
        fishingSignTmp.fontSize = 1.8f;
        fishingSignTmp.alignment = TMPro.TextAlignmentOptions.Center;
        fishingSignTmp.color = new Color(0.98f, 0.94f, 0.85f);
        fishingSignTmp.outlineWidth = 0.18f;
        fishingSignTmp.outlineColor = Color.black;
        fishingSignTmp.rectTransform.sizeDelta = new Vector3(7.0f, 1.5f);

        // ── Counter (vendor area, +X side near entrance) ──
        MakeBlock("Counter", root.transform, new Vector3(2.2f, 0.9f, 3.5f),
            new Vector3(halfW - 1.2f, stiltH + 0.45f, 0f), woodC);
        MakeBlock("CounterTop", root.transform, new Vector3(2.2f, 0.06f, 3.7f),
            new Vector3(halfW - 1.2f, stiltH + 0.93f, 0f), new Color(0.65f, 0.52f, 0.32f), true);

        // ── Ice display on counter ──
        MakeBlock("IceBlock", root.transform, new Vector3(1.4f, 0.2f, 2.5f),
            new Vector3(halfW - 1.2f, stiltH + 1.03f, 0f), iceC, true);
        // Fish on ice
        MakeBlock("Fish1", root.transform, new Vector3(0.3f, 0.1f, 0.5f),
            new Vector3(halfW - 1.5f, stiltH + 1.18f, -0.6f), fishOrange, true);
        MakeBlock("Fish2", root.transform, new Vector3(0.3f, 0.1f, 0.5f),
            new Vector3(halfW - 1.0f, stiltH + 1.18f, 0.3f), fishPink, true);

        // ── Fishing rod display (on back wall / rack) ──
        MakeBlock("RodRack", root.transform, new Vector3(0.1f, 1.8f, 3f),
            new Vector3(-halfW + 0.2f, stiltH + 1.5f, 0f), woodDarkC, true);
        for (int i = 0; i < 3; i++)
        {
            float rz = -1f + i * 1f;
            MakeBlock("DisplayRod" + i, root.transform, new Vector3(0.04f, 1.5f, 0.04f),
                new Vector3(-halfW + 0.3f, stiltH + 1.6f, rz), new Color(0.55f, 0.30f, 0.08f), true);
        }

        // ── Buckets + barrels ──
        MakeBlock("Bucket1", root.transform, new Vector3(0.4f, 0.45f, 0.4f),
            new Vector3(-2f, stiltH + 0.23f, halfD - 0.5f), bucketC, true);
        MakeBlock("Bucket2", root.transform, new Vector3(0.35f, 0.4f, 0.35f),
            new Vector3(-3f, stiltH + 0.2f, halfD - 0.5f), new Color(0.40f, 0.30f, 0.15f), true);
        MakeBlock("Barrel", root.transform, new Vector3(0.6f, 0.8f, 0.6f),
            new Vector3(-halfW + 0.5f, stiltH + 0.4f, -halfD + 0.5f), woodDarkC, true);

        // ── Awning over entrance (+X side) ──
        MakeBlock("Awning", root.transform, new Vector3(1.5f, 0.12f, 3f),
            new Vector3(halfW + 0.75f, roofH - 0.3f, 0f), awningC, true);
        float awningPostH = roofH - 0.3f;
        MakeBlock("AwningPost", root.transform, new Vector3(0.1f, awningPostH, 0.1f),
            new Vector3(halfW + 1.4f, awningPostH / 2f, -1.2f), woodC, true);
        MakeBlock("AwningPost", root.transform, new Vector3(0.1f, awningPostH, 0.1f),
            new Vector3(halfW + 1.4f, awningPostH / 2f, 1.2f), woodC, true);

        // ── Water trough under the platform ──
        MakeBlock("WaterTrough", root.transform, new Vector3(2.5f, 0.3f, 1.5f),
            new Vector3(0f, 0.15f, 0f), waterC, true);

        // ── Small stepping-stone path to entrance ──
        for (int i = 0; i < 3; i++)
        {
            MakeBlock("SteppingStone", root.transform, new Vector3(0.6f, 0.08f, 0.6f),
                new Vector3(halfW + 1.5f + i * 0.8f, 0.04f, 0f), new Color(0.55f, 0.52f, 0.48f), true);
        }

        return root;
    }

    public static void BuildFishingShopkeeper(Transform parent, Vector3 position, Quaternion rotation)
    {
        var root = new GameObject("FishingShopNPC");
        root.transform.SetParent(parent);
        root.transform.localPosition = position;
        root.transform.localRotation = rotation;

        Color furC = new Color(0.85f, 0.55f, 0.2f);
        Color furDark = new Color(0.7f, 0.42f, 0.15f);
        Color bellyC = new Color(0.95f, 0.85f, 0.7f);
        Color noseC = new Color(0.9f, 0.5f, 0.5f);
        Color eyeC = new Color(0.2f, 0.7f, 0.3f);

        // ── Body ──
        MakeBlock("Body", root.transform, new Vector3(0.35f, 0.28f, 0.55f), new Vector3(0f, 0.2f, 0f), furC, true);
        MakeBlock("Belly", root.transform, new Vector3(0.25f, 0.2f, 0.4f), new Vector3(0f, 0.15f, 0f), bellyC, true);

        // ── Head ──
        MakeBlock("Head", root.transform, new Vector3(0.28f, 0.26f, 0.26f), new Vector3(0f, 0.38f, 0.22f), furC, true);
        MakeBlock("Snout", root.transform, new Vector3(0.12f, 0.1f, 0.1f), new Vector3(0f, 0.34f, 0.35f), bellyC, true);
        MakeBlock("Nose", root.transform, new Vector3(0.06f, 0.04f, 0.03f), new Vector3(0f, 0.36f, 0.4f), noseC, true);

        // ── Ears ──
        MakeBlock("EarL", root.transform, new Vector3(0.06f, 0.12f, 0.05f), new Vector3(-0.1f, 0.55f, 0.18f), furC, true);
        MakeBlock("EarR", root.transform, new Vector3(0.06f, 0.12f, 0.05f), new Vector3(0.1f, 0.55f, 0.18f), furC, true);
        MakeBlock("EarInnerL", root.transform, new Vector3(0.03f, 0.07f, 0.03f), new Vector3(-0.1f, 0.55f, 0.19f), noseC, true);
        MakeBlock("EarInnerR", root.transform, new Vector3(0.03f, 0.07f, 0.03f), new Vector3(0.1f, 0.55f, 0.19f), noseC, true);

        // ── Eyes ──
        MakeBlock("EyeWhiteL", root.transform, new Vector3(0.06f, 0.06f, 0.03f), new Vector3(-0.09f, 0.41f, 0.33f), new Color(0.95f, 0.95f, 0.97f), true);
        MakeBlock("EyeWhiteR", root.transform, new Vector3(0.06f, 0.06f, 0.03f), new Vector3(0.09f, 0.41f, 0.33f), new Color(0.95f, 0.95f, 0.97f), true);
        MakeBlock("EyeIrisL", root.transform, new Vector3(0.04f, 0.04f, 0.03f), new Vector3(-0.09f, 0.41f, 0.34f), eyeC, true);
        MakeBlock("EyeIrisR", root.transform, new Vector3(0.04f, 0.04f, 0.03f), new Vector3(0.09f, 0.41f, 0.34f), eyeC, true);

        // ── Whiskers ──
        MakeBlock("WhiskerL1", root.transform, new Vector3(0.2f, 0.01f, 0.01f), new Vector3(-0.15f, 0.34f, 0.36f), bellyC, true);
        MakeBlock("WhiskerL2", root.transform, new Vector3(0.18f, 0.01f, 0.01f), new Vector3(-0.14f, 0.32f, 0.36f), bellyC, true);
        MakeBlock("WhiskerR1", root.transform, new Vector3(0.2f, 0.01f, 0.01f), new Vector3(0.15f, 0.34f, 0.36f), bellyC, true);
        MakeBlock("WhiskerR2", root.transform, new Vector3(0.18f, 0.01f, 0.01f), new Vector3(0.14f, 0.32f, 0.36f), bellyC, true);

        // ── Legs ──
        MakeBlock("LegFL", root.transform, new Vector3(0.08f, 0.2f, 0.08f), new Vector3(-0.12f, 0.0f, 0.16f), furDark, true);
        MakeBlock("LegFR", root.transform, new Vector3(0.08f, 0.2f, 0.08f), new Vector3(0.12f, 0.0f, 0.16f), furDark, true);
        MakeBlock("LegBL", root.transform, new Vector3(0.08f, 0.2f, 0.08f), new Vector3(-0.12f, 0.0f, -0.16f), furDark, true);
        MakeBlock("LegBR", root.transform, new Vector3(0.08f, 0.2f, 0.08f), new Vector3(0.12f, 0.0f, -0.16f), furDark, true);
        MakeBlock("PawFL", root.transform, new Vector3(0.09f, 0.04f, 0.1f), new Vector3(-0.12f, -0.1f, 0.17f), bellyC, true);
        MakeBlock("PawFR", root.transform, new Vector3(0.09f, 0.04f, 0.1f), new Vector3(0.12f, -0.1f, 0.17f), bellyC, true);
        MakeBlock("PawBL", root.transform, new Vector3(0.09f, 0.04f, 0.1f), new Vector3(-0.12f, -0.1f, -0.17f), bellyC, true);
        MakeBlock("PawBR", root.transform, new Vector3(0.09f, 0.04f, 0.1f), new Vector3(0.12f, -0.1f, -0.17f), bellyC, true);

        // ── Tail ──
        MakeBlock("TailBase", root.transform, new Vector3(0.06f, 0.06f, 0.2f), new Vector3(0f, 0.28f, -0.35f), furC, true);
        MakeBlock("TailMid", root.transform, new Vector3(0.05f, 0.05f, 0.15f), new Vector3(0f, 0.38f, -0.48f), furC, true);
        MakeBlock("TailTip", root.transform, new Vector3(0.04f, 0.04f, 0.1f), new Vector3(0f, 0.45f, -0.55f), furDark, true);

        var col = root.AddComponent<BoxCollider>();
        col.size = new Vector3(0.4f, 0.5f, 0.7f);
        col.center = new Vector3(0f, 0.2f, 0f);
        col.isTrigger = true;
    }
}

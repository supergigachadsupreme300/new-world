using UnityEngine;

public static class MapBuilder
{
    // ═══════════════════════════════════════════════════════════════
    //  LOW-LEVEL BLOCK
    // ═══════════════════════════════════════════════════════════════

    public static GameObject MakeBlock(string name, Transform parent, Vector3 scale, Vector3 position, Color color, bool removeCollider = false)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.SetParent(parent);
        go.transform.localScale = scale;
        go.transform.localPosition = position;
        var r = go.GetComponent<Renderer>();
        if (r != null) r.material.color = color;
        if (removeCollider)
            Object.Destroy(go.GetComponent<Collider>());
        return go;
    }

    // ═══════════════════════════════════════════════════════════════
    //  TREES  (recursive branching)
    // ═══════════════════════════════════════════════════════════════

    public static GameObject BuildTree(Transform parent, Vector3 position, float scale = 1f, Quaternion rotation = default)
    {
        var root = new GameObject("Tree");
        root.transform.SetParent(parent);
        root.transform.position = position;
        root.transform.rotation = (rotation == default) ? Quaternion.identity : rotation;
        root.transform.localScale = Vector3.one * scale;

        Color wood = new Color(0.36f, 0.23f, 0.12f);
        Color leaf = new Color(0.17f, 0.55f, 0.12f);

        float trunkH = Random.Range(2.5f, 5f);
        float trunkW = Random.Range(0.4f, 0.9f);
        int maxBranches = Random.Range(10, 28);

        Vector3 tip = AddTrunk(root.transform, trunkH, trunkW, wood);
        int count = 0;
        int numInitial = Random.Range(3, 6);
        for (int i = 0; i < numInitial; i++)
        {
            float angle = Random.Range(20f, 50f) * Mathf.Deg2Rad;
            float azimuth = Random.Range(0f, Mathf.PI * 2f);
            Vector3 perp = GetPerpendicular(root.transform.up);
            Vector3 horz = Quaternion.AngleAxis(azimuth * Mathf.Rad2Deg, root.transform.up) * perp;
            Vector3 branchDir = (root.transform.up * Mathf.Cos(angle) + horz * Mathf.Sin(angle)).normalized;
            float branchLen = trunkH * Random.Range(0.5f, 0.75f);
            float branchW = trunkW * Random.Range(0.3f, 0.5f);
            GrowBranches(root.transform, tip, branchDir, branchLen, branchW, ref count, maxBranches, 0, wood, leaf);
        }

        return root;
    }

    private static Vector3 AddTrunk(Transform root, float height, float width, Color color)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = "Trunk";
        go.transform.SetParent(root);
        go.transform.localPosition = new Vector3(0, height * 0.5f, 0);
        go.transform.localScale = new Vector3(width, height, width);
        Quaternion rot = Quaternion.Euler(Random.Range(0, 10), 0, Random.Range(0, 10));
        go.transform.localRotation = rot;
        var r = go.GetComponent<Renderer>();
        if (r != null) r.material.color = color;
        return go.transform.localPosition + rot * new Vector3(0, height * 0.5f, 0);
    }

    private static void GrowBranches(Transform root, Vector3 startPos, Vector3 dir, float length, float width, ref int count, int maxCount, int depth, Color wood, Color leaf)
    {
        if (count >= maxCount || depth > 5 || length < 0.3f || width < 0.06f)
        {
            if (depth > 0) SpawnLeaves(root, startPos, leaf);
            return;
        }

        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = "Branch";
        go.transform.SetParent(root);
        Vector3 center = startPos + dir.normalized * (length * 0.5f);
        go.transform.localPosition = center;
        go.transform.localRotation = Quaternion.FromToRotation(Vector3.up, dir.normalized);
        go.transform.localScale = new Vector3(width, length, width);
        var r = go.GetComponent<Renderer>();
        if (r != null) r.material.color = wood;
        count++;

        Vector3 tip = startPos + dir.normalized * length;
        int numSub = Random.Range(1, 4);
        for (int i = 0; i < numSub; i++)
        {
            float angle = Random.Range(20f, 50f) * Mathf.Deg2Rad;
            float azimuth = Random.Range(0f, Mathf.PI * 2f);

            Vector3 perp = GetPerpendicular(dir);
            Vector3 horz = Quaternion.AngleAxis(azimuth * Mathf.Rad2Deg, dir) * perp;

            Vector3 subDir = (dir.normalized * Mathf.Cos(angle) + horz * Mathf.Sin(angle)).normalized;

            float subLen = length * Random.Range(0.45f, 0.7f);
            float subW = width * Random.Range(0.35f, 0.6f);

            if (subW < 0.06f)
            {
                SpawnLeaves(root, tip + subDir * subLen * 0.5f, leaf);
                continue;
            }

            GrowBranches(root, tip, subDir, subLen, subW, ref count, maxCount, depth + 1, wood, leaf);
        }
    }

    private static void SpawnLeaves(Transform root, Vector3 position, Color color)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = "Leaf";
        go.transform.SetParent(root);
        go.transform.localPosition = position;
        float s = Random.Range(0.8f, 1.4f);
        go.transform.localScale = new Vector3(s, s, s);
        var r = go.GetComponent<Renderer>();
        if (r != null) r.material.color = color;
        Object.Destroy(go.GetComponent<Collider>());
    }

    public static GameObject BuildCoconutTree(Transform parent, Vector3 position, float scale = 1f)
    {
        var root = new GameObject("CoconutTree");
        root.transform.SetParent(parent);
        root.transform.position = position;
        root.transform.localScale = Vector3.one * scale;

        Color wood = new Color(0.55f, 0.4f, 0.2f);
        Color leaf = new Color(0.25f, 0.65f, 0.15f);

        float trunkH = Random.Range(3f, 5f);
        float trunkW = Random.Range(0.5f, 0.8f);

        AddSegmentedTrunk(root.transform, trunkH, trunkW, wood);
        Vector3 tip = new Vector3(0f, trunkH, 0f);

        float angle = Random.Range(20f, 40f) * Mathf.Deg2Rad;
        float azimuth = Random.Range(0f, Mathf.PI * 2f);
        Vector3 perp = GetPerpendicular(root.transform.up);
        Vector3 horz = Quaternion.AngleAxis(azimuth * Mathf.Rad2Deg, root.transform.up) * perp;
        Vector3 branchDir = (root.transform.up * Mathf.Cos(angle) + horz * Mathf.Sin(angle)).normalized;
        float branchLen = trunkH * Random.Range(0.25f, 0.4f);
        float branchW = trunkW * Random.Range(0.5f, 0.7f);

        var branchGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
        branchGo.name = "Branch";
        branchGo.transform.SetParent(root.transform);
        Vector3 branchCenter = tip + branchDir.normalized * (branchLen * 0.5f);
        branchGo.transform.localPosition = branchCenter;
        branchGo.transform.localRotation = Quaternion.FromToRotation(Vector3.up, branchDir.normalized);
        branchGo.transform.localScale = new Vector3(branchW, branchLen, branchW);
        var br = branchGo.GetComponent<Renderer>();
        if (br != null) br.material.color = wood;
        Object.Destroy(branchGo.GetComponent<Collider>());

        Vector3 branchTip = tip + branchDir.normalized * branchLen;
        Vector3 perpBranch = GetPerpendicular(branchDir);
        for (int j = 0; j < 3; j++)
        {
            Vector3 leafDir = Quaternion.AngleAxis(j * 120f, branchDir) * perpBranch;
            GrowLeafChain(root.transform, branchTip, leafDir.normalized, branchDir, Random.Range(0.9f, 1.3f), 3, leaf);
        }

        return root;
    }

    private static void AddSegmentedTrunk(Transform root, float height, float width, Color color)
    {
        int segments = Mathf.Max(3, Mathf.RoundToInt(height));
        float segH = height / segments;
        for (int i = 0; i < segments; i++)
        {
            float t = (float)i / segments;
            float w = width * (1f - t * 0.3f);
            float wobble = 0.05f;
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "TrunkSeg";
            go.transform.SetParent(root);
            go.transform.localPosition = new Vector3(Random.Range(-wobble, wobble), segH * i + segH * 0.5f, Random.Range(-wobble, wobble));
            go.transform.localScale = new Vector3(w, segH * 1.1f, w);
            go.transform.localRotation = Quaternion.Euler(Random.Range(-1.5f, 1.5f), 0, Random.Range(-1.5f, 1.5f));
            var r = go.GetComponent<Renderer>();
            if (r != null) r.material.color = color;
            Object.Destroy(go.GetComponent<Collider>());
        }
    }

    private static void GrowLeafChain(Transform root, Vector3 startPos, Vector3 dir, Vector3 branchDir, float segLen, int remaining, Color color)
    {
        float wid = Random.Range(0.7f, 1.1f);
        Vector3 d = dir.normalized;

        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = "CoconutLeaf";
        go.transform.SetParent(root);
        go.transform.localPosition = startPos + d * (segLen * 0.5f);
        go.transform.localScale = new Vector3(wid, segLen, 0.01f);
        go.transform.localRotation = Quaternion.LookRotation(branchDir, d) * Quaternion.Euler(Random.Range(-1.5f, 1.5f), Random.Range(-4f, 4f), 0);
        var r = go.GetComponent<Renderer>();
        if (r != null) r.material.color = color;
        Object.Destroy(go.GetComponent<Collider>());

        remaining--;
        if (remaining <= 0) return;

        Vector3 head = startPos + d * segLen;
        Vector3 horzAxis = Vector3.Cross(d, Vector3.up).normalized;
        if (horzAxis.sqrMagnitude < 0.01f) horzAxis = Vector3.Cross(d, Vector3.forward).normalized;
        Vector3 nextDir = Quaternion.AngleAxis(-Random.Range(3f, 6f), horzAxis) * d;
        GrowLeafChain(root, head, nextDir.normalized, branchDir, segLen, remaining, color);
    }

    private static Vector3 GetPerpendicular(Vector3 v)
    {
        v.Normalize();
        if (Mathf.Abs(v.y) < 0.9f)
            return Vector3.Cross(v, Vector3.up).normalized;
        return Vector3.Cross(v, Vector3.right).normalized;
    }

    // ═══════════════════════════════════════════════════════════════
    //  STONES
    // ═══════════════════════════════════════════════════════════════

    public static GameObject BuildStone(Transform parent, Vector3 position, float scale = 1f, Quaternion rotation = default)
    {
        var root = new GameObject("Stone");
        root.transform.SetParent(parent);
        root.transform.position = position;
        root.transform.rotation = (rotation == default) ? Quaternion.identity : rotation;
        root.transform.localScale = Vector3.one * scale;

        Color stoneC = new Color(Random.Range(0.35f, 0.55f), Random.Range(0.35f, 0.5f), Random.Range(0.3f, 0.45f));

        float w = Random.Range(0.8f, 2.2f);
        float h = Random.Range(0.4f, 1.4f);
        float d = Random.Range(0.8f, 2f);

        var mainRock = MakeBlock("Rock", root.transform, new Vector3(w, h, d), new Vector3(0, h * 0.5f, 0), stoneC);
        mainRock.GetComponent<Collider>().isTrigger = true;

        if (Random.value > 0.4f)
        {
            float w2 = w * Random.Range(0.4f, 0.8f);
            float h2 = h * Random.Range(0.3f, 0.6f);
            float d2 = d * Random.Range(0.4f, 0.8f);
            var detail = MakeBlock("RockDetail", root.transform,
                new Vector3(w2, h2, d2),
                new Vector3(Random.Range(-0.3f, 0.3f), h + h2 * 0.5f, Random.Range(-0.3f, 0.3f)),
                stoneC);
            detail.GetComponent<Collider>().isTrigger = true;
        }

        return root;
    }

    // ═══════════════════════════════════════════════════════════════
    //  PLAYER HOUSE  (10 x 5 x 10, gabled roof, chimney, porch, bed)
    // ═══════════════════════════════════════════════════════════════

    public static GameObject BuildPlayerHouse(Transform parent, Vector3 position, float scale = 1f, Quaternion rotation = default)
    {
        var root = new GameObject("PlayerHouse");
        root.transform.SetParent(parent);
        root.transform.position = position;
        root.transform.rotation = (rotation == default) ? Quaternion.identity : rotation;
        root.transform.localScale = Vector3.one * scale;

        Color woodC    = new Color(0.63f, 0.39f, 0.18f);
        Color roofC    = new Color(0.635f, 0.243f, 0.149f);
        Color ridgeC   = new Color(0.345f, 0.11f, 0.039f);
        Color eaveC    = new Color(0.569f, 0.345f, 0.157f);
        Color stoneC   = new Color(0.439f, 0.4f, 0.361f);
        Color chimneyC = new Color(0.384f, 0.333f, 0.29f);
        Color winC     = new Color(0.549f, 0.784f, 0.863f);
        Color frameC   = new Color(0.165f, 0.094f, 0.031f);
        Color shuttC   = new Color(0.227f, 0.376f, 0.173f);
        Color porchC   = new Color(0.58f, 0.361f, 0.165f);

        // ── Walls + floor ──
        MakeBlock("Wall", root.transform, new Vector3(10f, 5f, 0.5f), new Vector3(0f, 2.5f, -5f), woodC);
        MakeBlock("Wall", root.transform, new Vector3(10f, 5f, 0.5f), new Vector3(0f, 2.5f, 5f), woodC);
        MakeBlock("Wall", root.transform, new Vector3(0.5f, 5f, 10f), new Vector3(-5f, 2.5f, 0f), woodC);
        MakeBlock("Wall", root.transform, new Vector3(0.5f, 5f, 3.5f), new Vector3(5f, 2.5f, -3.25f), woodC);
        MakeBlock("Wall", root.transform, new Vector3(0.5f, 5f, 3.5f), new Vector3(5f, 2.5f, 3.25f), woodC);
        MakeBlock("Transom", root.transform, new Vector3(0.5f, 1f, 3f), new Vector3(5f, 4.5f, 0f), woodC);
        MakeBlock("Floor", root.transform, new Vector3(10f, 0.5f, 10f), Vector3.zero, woodC);

        // ── Gabled roof ──
        float rise = 3f;
        float halfW = 5f;
        float panelLen = Mathf.Sqrt(halfW * halfW + rise * rise);
        float tilt = Mathf.Atan2(rise, halfW) * Mathf.Rad2Deg;
        float overhang = 1.6f;

        MakeBlock("RoofPanel", root.transform, new Vector3(panelLen, 0.65f, 10f + overhang * 2f),
            new Vector3(halfW / 2f, 5f + rise / 2f, 0f), roofC).transform.rotation = Quaternion.Euler(0f, 0f, -tilt);
        MakeBlock("RoofPanel", root.transform, new Vector3(panelLen, 0.65f, 10f + overhang * 2f),
            new Vector3(-halfW / 2f, 5f + rise / 2f, 0f), roofC).transform.rotation = Quaternion.Euler(0f, 0f, tilt);
        MakeBlock("Ridge", root.transform, new Vector3(0.68f, 0.38f, 10f + overhang * 2f + 0.2f),
            new Vector3(0f, 5f + rise + 0.1f, 0f), ridgeC);
        MakeBlock("Eave", root.transform, new Vector3(0.55f, 0.32f, 10f + overhang * 2f + 0.2f),
            new Vector3(halfW, 5.05f, 0f), eaveC);
        MakeBlock("Eave", root.transform, new Vector3(0.55f, 0.32f, 10f + overhang * 2f + 0.2f),
            new Vector3(-halfW, 5.05f, 0f), eaveC);

        // Gable end fill
        foreach (float gz in new[] { -5f, 5f })
        {
            float gzFace = gz + (gz > 0 ? 1f : -1f) * 0.04f;
            for (int i = 0; i < 6; i++)
            {
                float t = (i + 0.5f) / 6f;
                float sw = 10f * (1f - t) + 0.2f;
                float sy = 5f + (i + 0.5f) * rise / 6f;
                float sh = rise / 6f + 0.15f;
                MakeBlock("GableFill", root.transform, new Vector3(sw, sh, 0.55f),
                    new Vector3(0f, sy, gzFace), woodC);
            }
        }

        // ── Stone foundation ──
        MakeBlock("Foundation", root.transform, new Vector3(11.5f, 0.5f, 11.5f),
            new Vector3(0f, -0.27f, 0f), stoneC);
        MakeBlock("Foundation", root.transform, new Vector3(10.8f, 0.22f, 10.8f),
            new Vector3(0f, -0.52f, 0f), stoneC);

        // ── Chimney ──
        float chX = 2.8f, chZ = 2f;
        float chBot = 5f - 0.8f;
        float chTop = 5f + rise + 1.2f;
        float chH = chTop - chBot;
        MakeBlock("Chimney", root.transform, new Vector3(1.3f, chH, 1.3f),
            new Vector3(chX, (chBot + chTop) / 2f, chZ), chimneyC);
        MakeBlock("ChimneyCap", root.transform, new Vector3(1.65f, 0.44f, 1.65f),
            new Vector3(chX, chTop + 0.22f, chZ), new Color(0.259f, 0.212f, 0.18f));

        // ── Front wall windows ──
        foreach (float wx in new[] { -3f, 3f })
        {
            MakeBlock("WinGlass", root.transform, new Vector3(1.4f, 1.4f, 0.14f),
                new Vector3(wx, 2.8f, -5.03f), winC, true);
            MakeBlock("WinFrame", root.transform, new Vector3(0.1f, 1.4f, 0.16f),
                new Vector3(wx, 2.8f, -5.03f), frameC, true);
            MakeBlock("WinFrame", root.transform, new Vector3(1.4f, 0.1f, 0.16f),
                new Vector3(wx, 2.8f, -5.03f), frameC, true);
            MakeBlock("Shutter", root.transform, new Vector3(0.22f, 1.4f, 0.12f),
                new Vector3(wx - 0.88f, 2.8f, -5.03f), shuttC, true);
            MakeBlock("Shutter", root.transform, new Vector3(0.22f, 1.4f, 0.12f),
                new Vector3(wx + 0.88f, 2.8f, -5.03f), shuttC, true);
        }

        // ── Back wall window ──
        MakeBlock("WinGlass", root.transform, new Vector3(1.4f, 1.4f, 0.14f),
            new Vector3(0f, 2.8f, 5.03f), winC, true);
        MakeBlock("WinFrame", root.transform, new Vector3(0.1f, 1.4f, 0.16f),
            new Vector3(0f, 2.8f, 5.03f), frameC, true);
        MakeBlock("WinFrame", root.transform, new Vector3(1.4f, 0.1f, 0.16f),
            new Vector3(0f, 2.8f, 5.03f), frameC, true);

        // ── Left wall window ──
        MakeBlock("WinGlass", root.transform, new Vector3(0.14f, 1.4f, 1.4f),
            new Vector3(-5.03f, 2.8f, 0f), winC, true);
        MakeBlock("WinFrame", root.transform, new Vector3(0.16f, 0.1f, 1.4f),
            new Vector3(-5.03f, 2.8f, 0f), frameC, true);
        MakeBlock("WinFrame", root.transform, new Vector3(0.16f, 1.4f, 0.1f),
            new Vector3(-5.03f, 2.8f, 0f), frameC, true);

        // ── Right side entrance ──
        MakeBlock("DoorFrame", root.transform, new Vector3(0.32f, 4.2f, 0.32f),
            new Vector3(5.03f, 2.1f, -1.55f), frameC, true);
        MakeBlock("DoorFrame", root.transform, new Vector3(0.32f, 4.2f, 0.32f),
            new Vector3(5.03f, 2.1f, 1.55f), frameC, true);
        MakeBlock("DoorLintel", root.transform, new Vector3(0.32f, 0.35f, 3.42f),
            new Vector3(5.03f, 4.35f, 0f), frameC, true);
        MakeBlock("Porch", root.transform, new Vector3(1.2f, 0.3f, 4.2f),
            new Vector3(5.62f, 4.05f, 0f), porchC, true);
        MakeBlock("PorchColumn", root.transform, new Vector3(0.24f, 4.05f, 0.24f),
            new Vector3(6.12f, 2f, -1.8f), frameC, true);
        MakeBlock("PorchColumn", root.transform, new Vector3(0.24f, 4.05f, 0.24f),
            new Vector3(6.12f, 2f, 1.8f), frameC, true);

        // ── Bed ──
        var bed = MakeBlock("Bed", root.transform, new Vector3(2.8f, 0.5f, 1.8f),
            new Vector3(1.2f, 0.5f, -1.8f), new Color(0.608f, 0.216f, 0.216f), true);
        MakeBlock("BedPillow", bed.transform, new Vector3(0.2f, 0.5f, 0.4f),
            new Vector3(-0.4f, 0.7f, 0f), Color.white, true);
        MakeBlock("Headboard", bed.transform, new Vector3(0.1f, 2.2f, 1f),
            new Vector3(-0.55f, 0.5f, 0f), new Color(0.345f, 0.196f, 0.07f), true);

        return root;
    }

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
        MakeBlock("Sign", root.transform, new Vector3(3.5f, 0.8f, 0.2f),
            new Vector3(5.08f, 3.6f, 0f), signC, true);

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

    // ═══════════════════════════════════════════════════════════════
    //  WIFE HOUSE  (14 x 9 x 14, 2-storey, balcony, staircase)
    // ═══════════════════════════════════════════════════════════════

    public static GameObject BuildWifeHouse(Transform parent, Vector3 position, float scale = 1f, Quaternion rotation = default)
    {
        var root = new GameObject("WifeHouse");
        root.transform.SetParent(parent);
        root.transform.position = position;
        root.transform.rotation = (rotation == default) ? Quaternion.identity : rotation;
        root.transform.localScale = Vector3.one * scale;

        Color wallC   = new Color(0.522f, 0.337f, 0.18f);
        Color roofC   = new Color(0.404f, 0.204f, 0.114f);
        Color ridgeC  = new Color(0.345f, 0.11f, 0.039f);
        Color floorC  = new Color(0.447f, 0.263f, 0.157f);
        Color frameC  = new Color(0.165f, 0.094f, 0.031f);
        Color winC    = new Color(0.549f, 0.784f, 0.863f);
        Color stoneC  = new Color(0.439f, 0.4f, 0.361f);
        Color balcC   = new Color(0.325f, 0.208f, 0.114f);
        Color stairC  = new Color(0.455f, 0.353f, 0.2f);
        Color sofaC   = new Color(0.416f, 0.612f, 0.69f);
        Color tableC  = new Color(0.361f, 0.259f, 0.145f);

        float h1 = 5f, h2 = 4f;

        // ── 1st floor ──
        MakeBlock("Wall", root.transform, new Vector3(14f, h1, 0.5f), new Vector3(0f, h1 / 2f, -7f), wallC);
        MakeBlock("Wall", root.transform, new Vector3(14f, h1, 0.5f), new Vector3(0f, h1 / 2f, 7f), wallC);
        MakeBlock("Wall", root.transform, new Vector3(0.5f, h1, 14f), new Vector3(7f, h1 / 2f, 0f), wallC);
        MakeBlock("Floor", root.transform, new Vector3(14f, 0.5f, 14f), Vector3.zero, floorC);

        // ── 2nd floor ──
        MakeBlock("Wall2F", root.transform, new Vector3(14f, h2, 0.5f), new Vector3(0f, h1 + 0.25f + h2 / 2f, -7f), wallC);
        MakeBlock("Wall2F", root.transform, new Vector3(14f, h2, 0.5f), new Vector3(0f, h1 + 0.25f + h2 / 2f, 7f), wallC);
        MakeBlock("Wall2F", root.transform, new Vector3(0.5f, h2, 14f), new Vector3(7f, h1 + 0.25f + h2 / 2f, 0f), wallC);
        MakeBlock("Floor2F", root.transform, new Vector3(14f, 0.5f, 14f),
            new Vector3(0f, h1 + 0.25f, 0f), floorC);
        MakeBlock("Ceiling", root.transform, new Vector3(14f, 0.3f, 14f),
            new Vector3(0f, h1 + h2 + 0.4f, 0f), floorC);

        // ── Open side (-X) with entrance gap ──
        float wallH = h1 + h2 + 0.5f;
        float wallY = wallH / 2f;
        MakeBlock("WallSideL", root.transform, new Vector3(0.5f, wallH, 5.5f),
            new Vector3(-7f, wallY, -4.25f), wallC);
        MakeBlock("WallSideR", root.transform, new Vector3(0.5f, wallH, 5.5f),
            new Vector3(-7f, wallY, 4.25f), wallC);

        // ── Gabled roof ──
        float rise = 3.5f;
        float halfW = 7f;
        float panelLen = Mathf.Sqrt(halfW * halfW + rise * rise);
        float tilt = Mathf.Atan2(rise, halfW) * Mathf.Rad2Deg;
        float overhang = 1.8f;
        float roofZ = 14f + overhang * 2f;
        float roofY = h1 + h2 + 0.55f;

        MakeBlock("RoofPanel", root.transform, new Vector3(panelLen, 0.6f, roofZ),
            new Vector3(halfW / 2f, roofY + rise / 2f, 0f), roofC).transform.rotation = Quaternion.Euler(0f, 0f, -tilt);
        MakeBlock("RoofPanel", root.transform, new Vector3(panelLen, 0.6f, roofZ),
            new Vector3(-halfW / 2f, roofY + rise / 2f, 0f), roofC).transform.rotation = Quaternion.Euler(0f, 0f, tilt);
        MakeBlock("Ridge", root.transform, new Vector3(0.7f, 0.35f, roofZ + 0.2f),
            new Vector3(0f, roofY + rise + 0.1f, 0f), ridgeC);

        foreach (float gz in new[] { -7f, 7f })
        {
            float gzFace = gz + (gz > 0 ? 1f : -1f) * 0.04f;
            for (int i = 0; i < 7; i++)
            {
                float t = (i + 0.5f) / 7f;
                float sw = 14f * (1f - t) + 0.2f;
                float sy = roofY + (i + 0.5f) * rise / 7f;
                float sh = rise / 7f + 0.15f;
                MakeBlock("GableFill", root.transform, new Vector3(sw, sh, 0.55f),
                    new Vector3(0f, sy, gzFace), wallC);
            }
        }

        // ── Stone foundation ──
        MakeBlock("Foundation", root.transform, new Vector3(15.5f, 0.5f, 15.5f),
            new Vector3(0f, -0.27f, 0f), stoneC);

        // ── Windows ──
        foreach (float wx in new[] { -4f, 4f })
        {
            MakeBlock("WinGlass", root.transform, new Vector3(1.6f, 1.6f, 0.14f),
                new Vector3(wx, 2f, -7.03f), winC, true);
            MakeBlock("WinFrame", root.transform, new Vector3(0.1f, 1.6f, 0.16f),
                new Vector3(wx, 2f, -7.03f), frameC, true);
            MakeBlock("WinFrame", root.transform, new Vector3(1.6f, 0.1f, 0.16f),
                new Vector3(wx, 2f, -7.03f), frameC, true);
        }
        foreach (float wx in new[] { -4f, 4f })
        {
            MakeBlock("WinGlass2F", root.transform, new Vector3(1.6f, 1.4f, 0.14f),
                new Vector3(wx, h1 + 0.25f + 1.4f, -7.03f), winC, true);
            MakeBlock("WinFrame2F", root.transform, new Vector3(0.1f, 1.4f, 0.16f),
                new Vector3(wx, h1 + 0.25f + 1.4f, -7.03f), frameC, true);
            MakeBlock("WinFrame2F", root.transform, new Vector3(1.6f, 0.1f, 0.16f),
                new Vector3(wx, h1 + 0.25f + 1.4f, -7.03f), frameC, true);
        }

        // ── Entrance on -X side (facing road) ──
        MakeBlock("DoorFrame", root.transform, new Vector3(0.3f, 4.5f, 0.3f),
            new Vector3(-7.04f, 2.25f, -1.6f), frameC, true);
        MakeBlock("DoorFrame", root.transform, new Vector3(0.3f, 4.5f, 0.3f),
            new Vector3(-7.04f, 2.25f, 1.6f), frameC, true);
        MakeBlock("DoorLintel", root.transform, new Vector3(0.3f, 0.35f, 3.5f),
            new Vector3(-7.04f, 4.75f, 0f), frameC, true);

        // ── Balcony (at -X, 2F) ──
        float balcY = h1 + 0.25f;
        MakeBlock("BalconyDeck", root.transform, new Vector3(4f, 0.2f, 8f),
            new Vector3(-8.5f, balcY, 0f), balcC, true);
        for (float bz = -3.5f; bz <= 3.5f; bz += 1f)
        {
            MakeBlock("BalconyRail", root.transform, new Vector3(0.08f, 1.2f, 0.08f),
                new Vector3(-8.5f, balcY + 0.7f, bz), frameC, true);
        }
        MakeBlock("BalconyRailing", root.transform, new Vector3(0.08f, 1.2f, 7.6f),
            new Vector3(-9.52f, balcY + 0.7f, 0f), frameC, true);
        MakeBlock("BalconyHandrail", root.transform, new Vector3(0.08f, 0.12f, 8f),
            new Vector3(-8.5f, balcY + 1.2f, 0f), frameC, true);

        // ── Staircase (interior, +Z side) ──
        for (int i = 0; i < 6; i++)
        {
            float sy = i * 0.8f + 0.4f;
            float sz = -2.5f - i * 0.6f;
            MakeBlock("Stair", root.transform, new Vector3(2f, 0.15f, 1f),
                new Vector3(4.5f, sy, sz), stairC, true);
            MakeBlock("StairRiser", root.transform, new Vector3(2f, 0.3f, 0.1f),
                new Vector3(4.5f, sy - 0.15f, sz - 0.5f), stairC, true);
        }
        MakeBlock("StairLanding", root.transform, new Vector3(2.5f, 0.15f, 2f),
            new Vector3(4.5f, 5.4f, -6.5f), stairC, true);

        // ── 2F door to balcony ──
        MakeBlock("DoorFrame2F", root.transform, new Vector3(0.25f, 3.5f, 0.25f),
            new Vector3(-7.04f, balcY + 1.75f, -1.5f), frameC, true);
        MakeBlock("DoorFrame2F", root.transform, new Vector3(0.25f, 3.5f, 0.25f),
            new Vector3(-7.04f, balcY + 1.75f, 1.5f), frameC, true);
        MakeBlock("DoorLintel2F", root.transform, new Vector3(0.25f, 0.3f, 3.25f),
            new Vector3(-7.04f, balcY + 3.65f, 0f), frameC, true);

        // ── 1F furniture: sofas + coffee table ──
        MakeBlock("Sofa", root.transform, new Vector3(3f, 0.8f, 1.2f),
            new Vector3(-2.5f, 0.4f, 3f), sofaC, true);
        MakeBlock("SofaBack", root.transform, new Vector3(3f, 0.6f, 0.2f),
            new Vector3(-2.5f, 0.7f, 4.2f), sofaC, true);
        MakeBlock("Table", root.transform, new Vector3(1.5f, 0.5f, 1f),
            new Vector3(-2.5f, 0.25f, -4f), tableC, true);
        MakeBlock("Chair", root.transform, new Vector3(0.8f, 0.7f, 0.8f),
            new Vector3(-0.5f, 0.35f, -4f), wallC, true);

        return root;
    }

    // ═══════════════════════════════════════════════════════════════
    //  BUFFALO NPC
    // ═══════════════════════════════════════════════════════════════

    public static GameObject BuildBuffalo(Transform parent, Vector3 position, float scale = 1f, Quaternion rotation = default)
    {
        var root = new GameObject("BuffaloEntity");
        root.transform.SetParent(parent);
        root.transform.localPosition = position;
        root.transform.localRotation = rotation;
        root.transform.localScale = Vector3.one * scale;

        Color bodyC = new Color(0.25f, 0.16f, 0.07f);
        Color darkC = new Color(0.18f, 0.11f, 0.04f);
        Color hornC = new Color(0.6f, 0.6f, 0.6f);
        Color eyeC  = new Color(0.05f, 0.03f, 0.01f);

        // Body
        MakeBlock("Body", root.transform, new Vector3(1.4f, 0.7f, 0.6f), new Vector3(0f, 0.5f, 0f), bodyC, true);
        // Neck
        MakeBlock("Neck", root.transform, new Vector3(0.35f, 0.45f, 0.35f), new Vector3(0.85f, 0.75f, 0f), bodyC, true);
        // Head
        MakeBlock("Head", root.transform, new Vector3(0.45f, 0.3f, 0.35f), new Vector3(1.15f, 0.7f, 0f), bodyC, true);
        // Snout
        MakeBlock("Snout", root.transform, new Vector3(0.25f, 0.2f, 0.3f), new Vector3(1.5f, 0.55f, 0f), darkC, true);
        // Ears
        MakeBlock("EarL", root.transform, new Vector3(0.1f, 0.02f, 0.2f), new Vector3(1.0f, 0.8f, -0.3f), bodyC, true);
        MakeBlock("EarR", root.transform, new Vector3(0.1f, 0.02f, 0.2f), new Vector3(1.0f, 0.8f, 0.3f), bodyC, true);
        // Horns
        for (int s = -1; s <= 1; s += 2)
        {
            float z = s * 0.18f;
            MakeBlock("Horn" + (s > 0 ? "R" : "L") + "B", root.transform, new Vector3(0.15f, 0.04f, 0.04f), new Vector3(1.05f, 0.82f, z), hornC, true);
            MakeBlock("Horn" + (s > 0 ? "R" : "L") + "T", root.transform, new Vector3(0.04f, 0.04f, 0.14f), new Vector3(1.0f, 0.84f, z + s * 0.12f), hornC, true);
        }
        // Eyes
        MakeBlock("EyeL", root.transform, new Vector3(0.05f, 0.04f, 0.04f), new Vector3(1.2f, 0.73f, -0.14f), eyeC, true);
        MakeBlock("EyeR", root.transform, new Vector3(0.05f, 0.04f, 0.04f), new Vector3(1.2f, 0.73f, 0.14f), eyeC, true);
        // Legs
        float[][] legP = new float[][] {
            new float[] { -0.55f, -0.3f }, new float[] { -0.55f, 0.3f },
            new float[] { 0.6f, -0.3f }, new float[] { 0.6f, 0.3f }
        };
        foreach (var p in legP)
        {
            MakeBlock("Leg", root.transform, new Vector3(0.16f, 0.45f, 0.16f), new Vector3(p[0], 0.12f, p[1]), bodyC, true);
            MakeBlock("Hoof", root.transform, new Vector3(0.18f, 0.05f, 0.18f), new Vector3(p[0], -0.1f, p[1]), darkC, true);
        }
        // Tail
        MakeBlock("Tail", root.transform, new Vector3(0.04f, 0.3f, 0.04f), new Vector3(-0.95f, 0.25f, 0f), darkC, true);
        MakeBlock("Tuft", root.transform, new Vector3(0.1f, 0.1f, 0.1f), new Vector3(-0.95f, 0.05f, 0f), darkC, true);

        var col = root.AddComponent<BoxCollider>();
        col.size = new Vector3(2.5f, 1.4f, 1f);
        col.center = new Vector3(0.2f, 0.5f, 0f);
        col.isTrigger = true;

        return root;
    }

    // ═══════════════════════════════════════════════════════════════
    //  WIFE NPC
    // ═══════════════════════════════════════════════════════════════

    public static GameObject BuildWifeNpc(Transform parent, Vector3 position, float scale = 1f, Quaternion rotation = default)
    {
        var root = new GameObject("WifeNpc");
        root.transform.SetParent(parent);
        root.transform.localPosition = position;
        root.transform.localRotation = rotation;
        root.transform.localScale = Vector3.one * scale;

        Color skinC = new Color(220f / 255f, 178f / 255f, 132f / 255f);
        Color dressC = new Color(0.9f, 0.9f, 0.9f);
		Color hairC = new Color(0.95f, 0.85f, 0.55f);
		Color eyeC = new Color(0.5f, 0.75f, 1f);
		Color shoeC = new Color(0.2f, 0.2f, 0.2f);

		// Legs
		MakeBlock("LegL", root.transform, new Vector3(0.1f, 0.5f, 0.1f), new Vector3(-0.12f, -0.55f, 0f), skinC, true);
		MakeBlock("LegR", root.transform, new Vector3(0.1f, 0.5f, 0.1f), new Vector3(0.12f, -0.55f, 0f), skinC, true);
		MakeBlock("ShoeL", root.transform, new Vector3(0.14f, 0.08f, 0.22f), new Vector3(-0.14f, -0.82f, 0f), shoeC, true);
		MakeBlock("ShoeR", root.transform, new Vector3(0.14f, 0.08f, 0.22f), new Vector3(0.14f, -0.82f, 0f), shoeC, true);

		// Curvy dress — hourglass silhouette
		MakeBlock("Skirt", root.transform, new Vector3(0.6f, 0.35f, 0.38f), new Vector3(0f, -0.22f, 0f), dressC, true);
		MakeBlock("Hips", root.transform, new Vector3(0.52f, 0.2f, 0.32f), new Vector3(0f, 0f, 0f), dressC, true);
		MakeBlock("Waist", root.transform, new Vector3(0.35f, 0.2f, 0.24f), new Vector3(0f, 0.2f, 0f), dressC, true);
		MakeBlock("Bust", root.transform, new Vector3(0.48f, 0.25f, 0.3f), new Vector3(0f, 0.42f, 0f), dressC, true);

		// Neck
		MakeBlock("Neck", root.transform, new Vector3(0.15f, 0.1f, 0.15f), new Vector3(0f, 0.6f, 0f), skinC, true);

		// Head
		MakeBlock("Head", root.transform, new Vector3(0.36f, 0.32f, 0.34f), new Vector3(0f, 0.8f, 0f), skinC, true);

		// Hair — long blonde
		MakeBlock("HairBack", root.transform, new Vector3(0.5f, 0.6f, 0.15f), new Vector3(0f, 0.55f, 0.18f), hairC, true).transform.localRotation = Quaternion.Euler(0f, -90f, 0f);
		MakeBlock("HairTop", root.transform, new Vector3(0.42f, 0.08f, 0.38f), new Vector3(0f, 0.98f, 0f), hairC, true);
		MakeBlock("HairL", root.transform, new Vector3(0.4f, 0.7f, 0.15f), new Vector3(-0.4f, 0.7f, 0f), hairC, true);
		MakeBlock("HairR", root.transform, new Vector3(0.4f, 0.7f, 0.15f), new Vector3(0.4f, 0.7f, 0f), hairC, true);

		// Eyes — light blue
		MakeBlock("EyeL", root.transform, new Vector3(0.06f, 0.05f, 0.04f), new Vector3(-0.1f, 0.86f, -0.18f), eyeC, true);
		MakeBlock("EyeR", root.transform, new Vector3(0.06f, 0.05f, 0.04f), new Vector3(0.1f, 0.86f, -0.18f), eyeC, true);

        // Arms
        MakeBlock("ArmL", root.transform, new Vector3(0.1f, 0.45f, 0.1f), new Vector3(-0.33f, 0.3f, 0f), skinC, true);
        MakeBlock("ArmR", root.transform, new Vector3(0.1f, 0.45f, 0.1f), new Vector3(0.33f, 0.3f, 0f), skinC, true);

        var col = root.AddComponent<BoxCollider>();
        col.size = new Vector3(0.8f, 1.7f, 0.6f);
        col.center = new Vector3(0f, 0.25f, 0f);
        col.isTrigger = true;

        return root;
    }

    // ═══════════════════════════════════════════════════════════════
    //  PLAYER MODEL  (blocky farmer character)
    // ═══════════════════════════════════════════════════════════════

    public static GameObject BuildPlayerModel(Transform parent, float scale = 1f)
    {
        var root = new GameObject("PlayerModel");
        root.transform.SetParent(parent);
        root.transform.localPosition = Vector3.zero;
        root.transform.localRotation = Quaternion.identity;
        root.transform.localScale = Vector3.one * scale;

        Color skinC = new Color(220f / 255f, 178f / 255f, 132f / 255f);
        Color shirtC = new Color(0.2f, 0.6f, 0.9f);
        Color pantsC = new Color(0.25f, 0.25f, 0.35f);
        Color hairC = new Color(0.2f, 0.12f, 0.05f);
        Color eyeC = new Color(0.05f, 0.03f, 0.01f);
        Color shoeC = new Color(0.2f, 0.2f, 0.2f);

        MakeBlock("Body", root.transform, new Vector3(0.5f, 0.6f, 0.25f), new Vector3(0f, 0.05f, 0f), shirtC, true);
        MakeBlock("Head", root.transform, new Vector3(0.3f, 0.3f, 0.3f), new Vector3(0f, 0.65f, 0f), skinC, true);
        MakeBlock("Neck", root.transform, new Vector3(0.12f, 0.1f, 0.12f), new Vector3(0f, 0.4f, 0f), skinC, true);
        MakeBlock("ArmL", root.transform, new Vector3(0.12f, 0.5f, 0.12f), new Vector3(-0.33f, 0.12f, 0f), shirtC, true);
        MakeBlock("ArmR", root.transform, new Vector3(0.12f, 0.5f, 0.12f), new Vector3(0.33f, 0.12f, 0f), shirtC, true);
        MakeBlock("HandL", root.transform, new Vector3(0.12f, 0.08f, 0.12f), new Vector3(-0.33f, -0.14f, 0f), skinC, true);
        MakeBlock("HandR", root.transform, new Vector3(0.12f, 0.08f, 0.12f), new Vector3(0.33f, -0.14f, 0f), skinC, true);
        MakeBlock("LegL", root.transform, new Vector3(0.14f, 0.5f, 0.14f), new Vector3(-0.13f, -0.5f, 0f), pantsC, true);
        MakeBlock("LegR", root.transform, new Vector3(0.14f, 0.5f, 0.14f), new Vector3(0.13f, -0.5f, 0f), pantsC, true);
        MakeBlock("ShoeL", root.transform, new Vector3(0.16f, 0.08f, 0.22f), new Vector3(-0.13f, -0.82f, 0f), shoeC, true);
        MakeBlock("ShoeR", root.transform, new Vector3(0.16f, 0.08f, 0.22f), new Vector3(0.13f, -0.82f, 0f), shoeC, true);
        MakeBlock("Hair", root.transform, new Vector3(0.32f, 0.08f, 0.3f), new Vector3(0f, 0.82f, 0f), hairC, true);
        MakeBlock("HairL", root.transform, new Vector3(0.06f, 0.18f, 0.1f), new Vector3(-0.18f, 0.78f, 0f), hairC, true);
        MakeBlock("HairR", root.transform, new Vector3(0.06f, 0.18f, 0.1f), new Vector3(0.18f, 0.78f, 0f), hairC, true);
        MakeBlock("EyeL", root.transform, new Vector3(0.04f, 0.04f, 0.04f), new Vector3(-0.08f, 0.72f, -0.16f), eyeC, true);
        MakeBlock("EyeR", root.transform, new Vector3(0.04f, 0.04f, 0.04f), new Vector3(0.08f, 0.72f, -0.16f), eyeC, true);

        return root;
    }

}

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

        Vector3 trunkDir = Quaternion.Euler(Random.Range(0, 10), 0, Random.Range(0, 10)) * Vector3.up;
        Vector3 tip;
        int count = 0;
        GrowBranchSegment(root.transform, Vector3.zero, trunkDir, trunkH, trunkW, wood, leaf,
            ref count, maxBranches, 0,
            1, null, 0f, 1f, 0, "Trunk",
            out tip, out _);

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
            GrowBranchSegment(root.transform, tip, branchDir, branchLen, branchW, wood, leaf,
                ref count, maxBranches, 0,
                0, null, 0f, 1f, -1, "",
                out _, out _);
        }

        return root;
    }

    public static GameObject BuildCoconutTree(Transform parent, Vector3 position, float scale = 1f)
    {
        var root = new GameObject("TreeCoconut");
        root.transform.SetParent(parent);
        root.transform.position = position;
        root.transform.localScale = Vector3.one * scale;

        Color wood = new Color(0.55f, 0.4f, 0.2f);
        Color leaf = new Color(0.25f, 0.65f, 0.15f);

        float trunkH = Random.Range(3f, 5f);
        float trunkW = Random.Range(0.5f, 0.8f);

        float bendYaw = Random.Range(0f, 360f);
        Vector3 tiltAxis = Quaternion.Euler(0f, bendYaw, 0f) * Vector3.right;

        int segCount = Mathf.Max(3, Mathf.RoundToInt(trunkH));
        float segLen = trunkH / segCount * 1.1f;
        Quaternion topRot;
        Vector3 tip;
        int dummy = 0;
        GrowBranchSegment(root.transform, Vector3.zero, Vector3.up, segLen, trunkW, wood, leaf,
            ref dummy, 0, 0,
            segCount, tiltAxis, 5f, 0.92f, 0, "Trunk",
            out tip, out topRot);

        float angle = Random.Range(20f, 40f) * Mathf.Deg2Rad;
        Vector3 tiltDir = Quaternion.Euler(0f, bendYaw, 0f) * Vector3.forward;
        Vector3 horzDir = Vector3.ProjectOnPlane(tiltDir, topRot * Vector3.up).normalized;
        Vector3 branchDir = (topRot * Vector3.up * Mathf.Cos(angle) + horzDir * Mathf.Sin(angle)).normalized;
        float branchLen = trunkH * Random.Range(0.25f, 0.4f);
        float branchW = trunkW * Random.Range(0.5f, 0.7f);

        int branchSegs = 3;
        float branchSegLen = branchLen / branchSegs;
        Vector3 branchTip;
        Quaternion finalBranchRot;
        int branchDummy = 0;
        GrowBranchSegment(root.transform, tip, branchDir, branchSegLen, branchW, wood, leaf,
            ref branchDummy, 0, 0,
            branchSegs, tiltAxis, 5f, 0.92f, 0, "Branch",
            out branchTip, out finalBranchRot);

        Vector3 finalBranchDir = finalBranchRot * Vector3.up;
        Vector3 perpBranch = GetPerpendicular(finalBranchDir);
        float leafSegLen = Random.Range(0.9f, 1.3f);
        for (int j = 0; j < 3; j++)
        {
            Vector3 leafDir = Quaternion.AngleAxis(j * 120f, finalBranchDir) * perpBranch;
            Vector3 leafHorz = Vector3.Cross(leafDir.normalized, Vector3.up).normalized;
            if (leafHorz.sqrMagnitude < 0.01f) leafHorz = Vector3.Cross(leafDir.normalized, Vector3.forward).normalized;
            GrowLeafChain(root.transform, branchTip, leafDir.normalized, finalBranchDir, leafHorz, leafSegLen, 3, leaf);
        }

        return root;
    }

    private static void GrowBranchSegment(
        Transform root, Vector3 segStart, Vector3 dir,
        float segLen, float width, Color wood, Color leaf,
        ref int count, int maxCount, int depth,
        int chainRemaining,
        Vector3? bendAxis,
        float perSegAngle,
        float widthTaper,
        int subBranchOverride,
        string chainSegName,
        out Vector3 tipPos,
        out Quaternion tipRot)
    {
        if (chainRemaining == 0)
        {
            if (count >= maxCount || depth > 5 || segLen < 0.3f || width < 0.06f)
            {
                if (depth > 0) SpawnLeaves(root, segStart, leaf);
                tipPos = segStart;
                tipRot = Quaternion.identity;
                return;
            }
        }

        if (chainRemaining > 0 && bendAxis.HasValue)
            dir = (Quaternion.AngleAxis(perSegAngle, bendAxis.Value) * dir.normalized).normalized;

        string segName = chainRemaining > 0
            ? (depth == 0 ? chainSegName : (chainSegName == "Trunk" ? "TrunkSeg" : chainSegName))
            : "Branch";

        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = segName;
        go.transform.SetParent(root);
        go.transform.localPosition = segStart + dir.normalized * (segLen * 0.5f);
        go.transform.localRotation = Quaternion.FromToRotation(Vector3.up, dir.normalized);
        go.transform.localScale = new Vector3(width, segLen, width);
        var r = go.GetComponent<Renderer>();
        if (r != null)
        {
            var mat = GetWoodMaterial();
            if (mat != null)
                r.material = mat;
            else
                r.material.color = wood;
        }

        tipPos = segStart + dir.normalized * segLen;
        tipRot = go.transform.localRotation;

        if (chainRemaining == 0) count++;

        if (chainRemaining > 1)
        {
            GrowBranchSegment(root, tipPos, dir.normalized, segLen, width * widthTaper, wood, leaf,
                ref count, maxCount, depth + 1,
                chainRemaining - 1, bendAxis, perSegAngle, widthTaper, subBranchOverride, chainSegName,
                out tipPos, out tipRot);
            return;
        }

        int numSub = subBranchOverride >= 0 ? subBranchOverride : Random.Range(1, 4);
        for (int i = 0; i < numSub; i++)
        {
            float a = Random.Range(20f, 50f) * Mathf.Deg2Rad;
            float azi = Random.Range(0f, Mathf.PI * 2f);
            Vector3 p = GetPerpendicular(dir.normalized);
            Vector3 h = Quaternion.AngleAxis(azi * Mathf.Rad2Deg, dir.normalized) * p;
            Vector3 subDir = (dir.normalized * Mathf.Cos(a) + h * Mathf.Sin(a)).normalized;
            float subLen = segLen * Random.Range(0.45f, 0.7f);
            float subW = width * Random.Range(0.35f, 0.6f);
            if (subW < 0.06f)
            {
                SpawnLeaves(root, tipPos + subDir * subLen * 0.5f, leaf);
                continue;
            }
            GrowBranchSegment(root, tipPos, subDir, subLen, subW, wood, leaf,
                ref count, maxCount, depth + 1,
                0, null, 0f, 1f, -1, "",
                out _, out _);
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
        if (r != null)
        {
            var mat = GetLeafMaterial();
            if (mat != null)
                r.material = mat;
            else
                r.material.color = color;
        }
        Object.Destroy(go.GetComponent<Collider>());
    }

    private static void GrowLeafChain(Transform root, Vector3 startPos, Vector3 dir, Vector3 branchDir, Vector3 horzAxis, float segLen, int remaining, Color color)
    {
        float wid = Random.Range(0.7f, 1.1f);
        Vector3 d = dir.normalized;

        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = "Leaf";
        go.transform.SetParent(root);
        go.transform.localPosition = startPos + d * (segLen * 0.5f);
        go.transform.localScale = new Vector3(wid, segLen, 0.01f);
        go.transform.localRotation = Quaternion.LookRotation(branchDir, d) * Quaternion.Euler((3 - remaining) * -12f, 0f, 0f);
        var r = go.GetComponent<Renderer>();
        if (r != null)
        {
            var mat = GetLeafMaterial();
            if (mat != null)
                r.material = mat;
            else
                r.material.color = color;
        }
        Object.Destroy(go.GetComponent<Collider>());

        remaining--;
        if (remaining <= 0) return;

        Vector3 head = startPos + d * segLen * 0.85f;
        Vector3 nextDir = Quaternion.AngleAxis(-Random.Range(10f, 16f), horzAxis) * d;
        GrowLeafChain(root, head, nextDir.normalized, branchDir, horzAxis, segLen, remaining, color);
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

        if (Random.value > 0.4f)
        {
            float w2 = w * Random.Range(0.4f, 0.8f);
            float h2 = h * Random.Range(0.3f, 0.6f);
            float d2 = d * Random.Range(0.4f, 0.8f);
            var detail = MakeBlock("RockDetail", root.transform,
                new Vector3(w2, h2, d2),
                new Vector3(Random.Range(-0.3f, 0.3f), h + h2 * 0.5f, Random.Range(-0.3f, 0.3f)),
                stoneC);
        }

        return root;
    }

    public static GameObject BuildBorderRock(Transform parent, Vector3 position, float scale = 1f)
    {
        var root = new GameObject("BorderRock");
        root.transform.SetParent(parent);
        root.transform.position = position;
        root.transform.localScale = Vector3.one * scale;

        Color stoneC = new Color(Random.Range(0.28f, 0.45f), Random.Range(0.28f, 0.4f), Random.Range(0.25f, 0.38f));

        float w = Random.Range(3.5f, 7f);
        float h = Random.Range(5f, 8f);
        float d = Random.Range(3.5f, 7f);

        var mainRock = MakeBlock("Boulder", root.transform, new Vector3(w, h, d), new Vector3(0, h * 0.5f, 0), stoneC);

        if (Random.value > 0.3f)
        {
            float w2 = w * Random.Range(0.3f, 0.7f);
            float h2 = h * Random.Range(0.3f, 0.6f);
            float d2 = d * Random.Range(0.3f, 0.7f);
            MakeBlock("BoulderDetail", root.transform,
                new Vector3(w2, h2, d2),
                new Vector3(Random.Range(-w * 0.2f, w * 0.2f), h * Random.Range(0.4f, 0.8f), Random.Range(-d * 0.2f, d * 0.2f)),
                stoneC);
        }

        if (Random.value > 0.5f)
        {
            float w3 = w * Random.Range(0.2f, 0.5f);
            float h3 = h * Random.Range(0.2f, 0.4f);
            float d3 = d * Random.Range(0.2f, 0.5f);
            MakeBlock("BoulderDetail", root.transform,
                new Vector3(w3, h3, d3),
                new Vector3(Random.Range(-w * 0.3f, w * 0.3f), h * Random.Range(0.2f, 0.5f), Random.Range(-d * 0.3f, d * 0.3f)),
                stoneC);
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

        Color skinC     = new Color(220f / 255f, 178f / 255f, 132f / 255f);
        Color bootC     = new Color(0.15f, 0.08f, 0.2f);
        Color bootTrimC = new Color(0.35f, 0.18f, 0.42f);
        Color skirtC    = new Color(0.14f, 0.11f, 0.28f);
        Color topC      = new Color(0.92f, 0.9f, 0.95f);
        Color hairC     = new Color(0.95f, 0.85f, 0.55f);
        Color eyeC      = new Color(0.3f, 0.6f, 1f);
        Color eyeWhiteC = new Color(0.95f, 0.95f, 0.97f);

        // ═══ LEGS ROOT ═══
        var legsRoot = new GameObject("LegsRoot");
        legsRoot.transform.SetParent(root.transform);
        legsRoot.transform.localPosition = Vector3.zero;
        legsRoot.transform.localRotation = Quaternion.identity;
        legsRoot.transform.localScale = Vector3.one;

        // ── Tall boots ──
        MakeBlock("SoleL",    legsRoot.transform, new Vector3(0.15f, 0.04f, 0.24f), new Vector3(-0.12f, -0.86f, 0f), bootC, true);
        MakeBlock("SoleR",    legsRoot.transform, new Vector3(0.15f, 0.04f, 0.24f), new Vector3(0.12f, -0.86f, 0f), bootC, true);
        MakeBlock("HeelL",    legsRoot.transform, new Vector3(0.06f, 0.08f, 0.1f),  new Vector3(-0.12f, -0.88f, 0.04f), bootC, true);
        MakeBlock("HeelR",    legsRoot.transform, new Vector3(0.06f, 0.08f, 0.1f),  new Vector3(0.12f, -0.88f, 0.04f), bootC, true);
        MakeBlock("BootL",    legsRoot.transform, new Vector3(0.14f, 0.38f, 0.2f),  new Vector3(-0.12f, -0.65f, 0f), bootC, true);
        MakeBlock("BootR",    legsRoot.transform, new Vector3(0.14f, 0.38f, 0.2f),  new Vector3(0.12f, -0.65f, 0f), bootC, true);
        MakeBlock("CuffL",    legsRoot.transform, new Vector3(0.16f, 0.05f, 0.22f), new Vector3(-0.12f, -0.44f, 0f), bootTrimC, true);
        MakeBlock("CuffR",    legsRoot.transform, new Vector3(0.16f, 0.05f, 0.22f), new Vector3(0.12f, -0.44f, 0f), bootTrimC, true);
        // ── Upper legs (skin, between boots and skirt) ──
        MakeBlock("LegL", legsRoot.transform, new Vector3(0.1f, 0.2f, 0.1f), new Vector3(-0.12f, -0.3f, 0f), skinC, true);
        MakeBlock("LegR", legsRoot.transform, new Vector3(0.1f, 0.2f, 0.1f), new Vector3(0.12f, -0.3f, 0f), skinC, true);

        // ═══ BODY ROOT ═══
        var bodyRoot = new GameObject("BodyRoot");
        bodyRoot.transform.SetParent(root.transform);
        bodyRoot.transform.localPosition = Vector3.zero;
        bodyRoot.transform.localRotation = Quaternion.identity;
        bodyRoot.transform.localScale = Vector3.one;

        // ── Short skirt (A-line flare) ──
        MakeBlock("Skirt",     bodyRoot.transform, new Vector3(0.48f, 0.26f, 0.32f), new Vector3(0f, -0.07f, 0f), skirtC, true);
        MakeBlock("SkirtHem",  bodyRoot.transform, new Vector3(0.56f, 0.05f, 0.38f), new Vector3(0f, -0.21f, 0f), skirtC, true);
        MakeBlock("SkirtBelt", bodyRoot.transform, new Vector3(0.4f, 0.04f, 0.28f),  new Vector3(0f, 0.07f, 0f), bootTrimC, true);
        // ── Cropped top (shows midriff) ──
        MakeBlock("TopLower",  bodyRoot.transform, new Vector3(0.38f, 0.12f, 0.26f), new Vector3(0f, 0.2f, 0f), topC, true);
        MakeBlock("TopUpper",  bodyRoot.transform, new Vector3(0.42f, 0.18f, 0.28f), new Vector3(0f, 0.35f, 0f), topC, true);
        MakeBlock("TopCollar", bodyRoot.transform, new Vector3(0.18f, 0.05f, 0.14f), new Vector3(0f, 0.47f, -0.02f), bootTrimC, true);
        // ── Neck ──
        MakeBlock("Neck", bodyRoot.transform, new Vector3(0.12f, 0.1f, 0.12f), new Vector3(0f, 0.5f, 0f), skinC, true);
        // ── Head (anime-proportioned, slightly larger) ──
        MakeBlock("Head", bodyRoot.transform, new Vector3(0.34f, 0.3f, 0.32f), new Vector3(0f, 0.7f, 0f), skinC, true);
        // ── Hair — long braided ──
        MakeBlock("HairTop",  bodyRoot.transform, new Vector3(0.38f, 0.08f, 0.36f), new Vector3(0f, 0.86f, 0f), hairC, true);
        MakeBlock("HairBack", bodyRoot.transform, new Vector3(0.3f, 0.4f, 0.12f),  new Vector3(0f, 0.65f, 0.26f), hairC, true).transform.rotation = Quaternion.Euler(0f, 90f, 0f);
        MakeBlock("HairL",    bodyRoot.transform, new Vector3(0.42f, 0.375f, 0.12f), new Vector3(-0.22f, 0.65f, 0f), hairC, true);
        MakeBlock("HairR",    bodyRoot.transform, new Vector3(0.42f, 0.375f, 0.12f), new Vector3(0.22f, 0.65f, 0f), hairC, true);
        // ── Braid (flows from back of head downward, alternating weave) ──
        MakeBlock("Braid1",   bodyRoot.transform, new Vector3(0.24f, 0.14f, 0.12f), new Vector3(0f,       0.40f, 0.26f), hairC, true).transform.rotation = Quaternion.Euler(0f, 90f, 0f);
        MakeBlock("Braid2",   bodyRoot.transform, new Vector3(0.20f, 0.12f, 0.1f),  new Vector3(0.02f,   0.32f, 0.27f), hairC, true).transform.rotation = Quaternion.Euler(0f, 90f, 0f);
        MakeBlock("Braid3",   bodyRoot.transform, new Vector3(0.17f, 0.1f,  0.09f), new Vector3(-0.02f,  0.24f, 0.28f), hairC, true).transform.rotation = Quaternion.Euler(0f, 90f, 0f);
        MakeBlock("Braid4",   bodyRoot.transform, new Vector3(0.14f, 0.09f, 0.07f), new Vector3(0.015f,  0.16f, 0.28f), hairC, true).transform.rotation = Quaternion.Euler(0f, 90f, 0f);
        MakeBlock("Braid5",   bodyRoot.transform, new Vector3(0.12f, 0.07f, 0.06f), new Vector3(-0.01f,  0.08f, 0.27f), hairC, true).transform.rotation = Quaternion.Euler(0f, 90f, 0f);
        MakeBlock("Braid6",   bodyRoot.transform, new Vector3(0.09f, 0.06f, 0.05f), new Vector3(0.005f,  0.02f, 0.26f), hairC, true).transform.rotation = Quaternion.Euler(0f, 90f, 0f);
        MakeBlock("BraidEnd", bodyRoot.transform, new Vector3(0.07f, 0.05f, 0.04f), new Vector3(0f,     -0.03f, 0.26f), hairC, true).transform.rotation = Quaternion.Euler(0f, 90f, 0f);
        // ── Eyes (anime-style, larger) — facing forward (-Z = front) ──
        MakeBlock("EyeWhiteL", bodyRoot.transform, new Vector3(0.1f, 0.08f, 0.03f), new Vector3(-0.09f, 0.74f, -0.165f), eyeWhiteC, true).transform.rotation = Quaternion.Euler(0f, 90f, 0f);
        MakeBlock("EyeWhiteR", bodyRoot.transform, new Vector3(0.1f, 0.08f, 0.03f), new Vector3(0.09f, 0.74f, -0.165f), eyeWhiteC, true).transform.rotation = Quaternion.Euler(0f, 90f, 0f);
        MakeBlock("EyeIrisL",  bodyRoot.transform, new Vector3(0.06f, 0.06f, 0.04f), new Vector3(-0.09f, 0.73f, -0.178f), eyeC, true).transform.rotation = Quaternion.Euler(0f, 90f, 0f);
        MakeBlock("EyeIrisR",  bodyRoot.transform, new Vector3(0.06f, 0.06f, 0.04f), new Vector3(0.09f, 0.73f, -0.178f), eyeC, true).transform.rotation = Quaternion.Euler(0f, 90f, 0f);

        // ═══ LEFT ARM ROOT ═══
        var leftArmRoot = new GameObject("LeftArmRoot");
        leftArmRoot.transform.SetParent(root.transform);
        leftArmRoot.transform.localPosition = new Vector3(0.05f , -0.05f, 0f);
        leftArmRoot.transform.localRotation = Quaternion.Euler(0f, 0f, -15f);
        leftArmRoot.transform.localScale = Vector3.one;

        MakeBlock("SleeveL",     leftArmRoot.transform, new Vector3(0.13f, 0.26f, 0.13f), new Vector3(-0.34f, 0.2f, 0f), topC, true);
        MakeBlock("SleeveTrimL", leftArmRoot.transform, new Vector3(0.15f, 0.04f, 0.15f), new Vector3(-0.34f, 0.06f, 0f), bootTrimC, true);
        MakeBlock("UpperArmL",   leftArmRoot.transform, new Vector3(0.08f, 0.1f, 0.08f), new Vector3(-0.34f, 0.38f, 0f), skinC, true);
        MakeBlock("HandL",       leftArmRoot.transform, new Vector3(0.08f, 0.1f, 0.08f), new Vector3(-0.34f, -0.01f, 0f), skinC, true);

        

        // ═══ RIGHT ARM ROOT ═══
        var rightArmRoot = new GameObject("RightArmRoot");
        rightArmRoot.transform.SetParent(root.transform);
        rightArmRoot.transform.localPosition = new Vector3(-0.05f , -0.05f, 0f);
        rightArmRoot.transform.localRotation = Quaternion.Euler(0f, 0f, 15f);
        rightArmRoot.transform.localScale = Vector3.one;

        MakeBlock("SleeveR",     rightArmRoot.transform, new Vector3(0.13f, 0.26f, 0.13f), new Vector3(0.34f, 0.2f, 0f), topC, true);
        MakeBlock("SleeveTrimR", rightArmRoot.transform, new Vector3(0.15f, 0.04f, 0.15f), new Vector3(0.34f, 0.06f, 0f), bootTrimC, true);
        MakeBlock("UpperArmR",   rightArmRoot.transform, new Vector3(0.08f, 0.1f, 0.08f), new Vector3(0.34f, 0.38f, 0f), skinC, true);
        MakeBlock("HandR",       rightArmRoot.transform, new Vector3(0.08f, 0.1f, 0.08f), new Vector3(0.34f, -0.01f, 0f), skinC, true);

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
        root.transform.localPosition = new Vector3(0f, 0.86f, 0f);
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
        MakeBlock("EyeL", root.transform, new Vector3(0.04f, 0.04f, 0.04f), new Vector3(-0.08f, 0.72f, 0.15f), eyeC, true);
        MakeBlock("EyeR", root.transform, new Vector3(0.04f, 0.04f, 0.04f), new Vector3(0.08f, 0.72f, 0.15f), eyeC, true);

        return root;
    }

    // ═══════════════════════════════════════════════════════════════
    //  CAR MODEL  (blocky voxel car for cutscenes / menu)
    // ═══════════════════════════════════════════════════════════════

    public static GameObject BuildCar(Transform parent, Vector3 position = default, Color? bodyColor = null)
    {
        var root = new GameObject("Car");
        root.transform.SetParent(parent);
        root.transform.position = position;

        Color bodyC = bodyColor ?? new Color(0.2f, 0.55f, 0.9f);
        Color cabinC = new Color(0.15f, 0.4f, 0.75f);
        Color windowC = new Color(0.6f, 0.8f, 1f, 0.7f);
        Color wheelC = new Color(0.12f, 0.12f, 0.12f);
        Color rimC = new Color(0.6f, 0.6f, 0.6f);
        Color headlightC = new Color(1f, 0.95f, 0.7f);
        Color bumperC = new Color(0.3f, 0.3f, 0.3f);
        Color seatC = new Color(0.25f, 0.15f, 0.1f);

        // ── Chassis / body ──
        MakeBlock("Body", root.transform, new Vector3(2f, 0.6f, 4.2f), new Vector3(0f, 0.55f, 0f), bodyC, true);
        // ── Hood (front) ──
        MakeBlock("Hood", root.transform, new Vector3(1.8f, 0.3f, 1f), new Vector3(0f, 1.0f, 1.4f), bodyC, true);
        // ── Trunk (rear) ──
        MakeBlock("Trunk", root.transform, new Vector3(1.8f, 0.3f, 0.8f), new Vector3(0f, 1.0f, -1.7f), bodyC, true);
        // ── Bumpers ──
        MakeBlock("BumperF", root.transform, new Vector3(2.05f, 0.15f, 0.12f), new Vector3(0f, 0.43f, 2.15f), bumperC, true);
        MakeBlock("BumperR", root.transform, new Vector3(2.05f, 0.15f, 0.12f), new Vector3(0f, 0.43f, -2.15f), bumperC, true);
        // ── Headlights ──
        MakeBlock("HeadlightL", root.transform, new Vector3(0.2f, 0.15f, 0.06f), new Vector3(-0.7f, 0.6f, 2.14f), headlightC, true);
        MakeBlock("HeadlightR", root.transform, new Vector3(0.2f, 0.15f, 0.06f), new Vector3(0.7f, 0.6f, 2.14f), headlightC, true);
        // ── Roof ──
        MakeBlock("Roof", root.transform, new Vector3(1.8f, 0.08f, 2.2f), new Vector3(0f, 1.67f, -0.2f), cabinC, true);
        // ── A-pillars (front corners) ──
        MakeBlock("PillarFL", root.transform, new Vector3(0.1f, 0.75f, 0.1f), new Vector3(-0.85f, 1.27f, 0.88f), cabinC, true);
        MakeBlock("PillarFR", root.transform, new Vector3(0.1f, 0.75f, 0.1f), new Vector3(0.85f, 1.27f, 0.88f), cabinC, true);
        // ── C-pillars (rear corners) ──
        MakeBlock("PillarRL", root.transform, new Vector3(0.1f, 0.75f, 0.1f), new Vector3(-0.85f, 1.27f, -1.28f), cabinC, true);
        MakeBlock("PillarRR", root.transform, new Vector3(0.1f, 0.75f, 0.1f), new Vector3(0.85f, 1.27f, -1.28f), cabinC, true);
        // ── Door panels (below window line) ──
        MakeBlock("DoorL", root.transform, new Vector3(0.08f, 0.35f, 2.1f), new Vector3(-0.9f, 1.0f, -0.2f), bodyC, true);
        MakeBlock("DoorR", root.transform, new Vector3(0.08f, 0.35f, 2.1f), new Vector3(0.9f, 1.0f, -0.2f), bodyC, true);
        // ── Front wall (below windshield) ──
        MakeBlock("FrontWall", root.transform, new Vector3(1.6f, 0.28f, 0.08f), new Vector3(0f, 0.97f, 0.88f), cabinC, true);
        // ── Rear wall (below rear window) ──
        MakeBlock("RearWall", root.transform, new Vector3(1.5f, 0.28f, 0.08f), new Vector3(0f, 0.97f, -1.3f), cabinC, true);
        // ── Steering wheel ──
        MakeBlock("SteeringWheel", root.transform, new Vector3(0.35f, 0.35f, 0.05f), new Vector3(-0.35f, 1.15f, 0.35f), Color.black, true).transform.localRotation = Quaternion.Euler(60f, 0f, 0f);
        // ── Seats (base + backrest) ──
        MakeBlock("SeatBaseL", root.transform, new Vector3(0.35f, 0.12f, 0.35f), new Vector3(-0.35f, 0.65f, -0.2f), seatC, true);
        MakeBlock("SeatBackL", root.transform, new Vector3(0.35f, 0.35f, 0.08f), new Vector3(-0.35f, 0.85f, -0.38f), seatC, true);
        MakeBlock("SeatBaseR", root.transform, new Vector3(0.35f, 0.12f, 0.35f), new Vector3(0.35f, 0.65f, -0.2f), seatC, true);
        MakeBlock("SeatBackR", root.transform, new Vector3(0.35f, 0.35f, 0.08f), new Vector3(0.35f, 0.85f, -0.38f), seatC, true);
        // ── Interior floor ──
        MakeBlock("InteriorFloor", root.transform, new Vector3(1.6f, 0.06f, 1.8f), new Vector3(0f, 0.86f, -0.2f), new Color(0.18f, 0.18f, 0.18f), true);

        // ── Wheels (4) ──
        float wheelY = 0.37f;
        float wheelH = 0.42f;
        float wheelD = 0.42f;
        float wheelW = 0.3f;
        float xOff = 0.95f;
        float zFront = 1.3f;
        float zRear = -1.3f;
        MakeBlock("WheelFL", root.transform, new Vector3(wheelW, wheelH, wheelD), new Vector3(-xOff, wheelY, zFront), wheelC, true).transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
        MakeBlock("WheelFR", root.transform, new Vector3(wheelW, wheelH, wheelD), new Vector3(xOff, wheelY, zFront), wheelC, true).transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
        MakeBlock("WheelRL", root.transform, new Vector3(wheelW, wheelH, wheelD), new Vector3(-xOff, wheelY, zRear), wheelC, true).transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
        MakeBlock("WheelRR", root.transform, new Vector3(wheelW, wheelH, wheelD), new Vector3(xOff, wheelY, zRear), wheelC, true).transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
        // ── Rim caps ──
        float rimS = 0.12f;
        MakeBlock("RimFL", root.transform, new Vector3(rimS, rimS, 0.05f), new Vector3(-xOff - 0.12f, wheelY, zFront), rimC, true);
        MakeBlock("RimFR", root.transform, new Vector3(rimS, rimS, 0.05f), new Vector3(xOff + 0.12f, wheelY, zFront), rimC, true);
        MakeBlock("RimRL", root.transform, new Vector3(rimS, rimS, 0.05f), new Vector3(-xOff - 0.12f, wheelY, zRear), rimC, true);
        MakeBlock("RimRR", root.transform, new Vector3(rimS, rimS, 0.05f), new Vector3(xOff + 0.12f, wheelY, zRear), rimC, true);

        return root;
    }

    // ═══════════════════════════════════════════════════════════════
    //  SEATED PLAYER MODEL  (for inside car — arms reaching forward)
    // ═══════════════════════════════════════════════════════════════

    public static GameObject BuildSeatedPlayerModel(Transform parent, float scale = 1f)
    {
        var root = new GameObject("SeatedPlayerModel");
        root.transform.SetParent(parent);
        root.transform.localPosition = new Vector3(-0.35f, 0.65f, -0.1f);
        root.transform.localRotation = Quaternion.identity;
        root.transform.localScale = Vector3.one * scale;

        Color skinC = new Color(220f / 255f, 178f / 255f, 132f / 255f);
        Color shirtC = new Color(0.2f, 0.6f, 0.9f);
        Color pantsC = new Color(0.25f, 0.25f, 0.35f);
        Color hairC = new Color(0.2f, 0.12f, 0.05f);
        Color eyeC = new Color(0.05f, 0.03f, 0.01f);

        // ── Torso (seated, upright) ──
        MakeBlock("Body", root.transform, new Vector3(0.38f, 0.5f, 0.28f), new Vector3(0f, 0.25f, 0f), shirtC, true);
        // ── Head ──
        MakeBlock("Head", root.transform, new Vector3(0.28f, 0.28f, 0.28f), new Vector3(0f, 0.74f, 0f), skinC, true);
        MakeBlock("Neck", root.transform, new Vector3(0.1f, 0.08f, 0.1f), new Vector3(0f, 0.55f, 0f), skinC, true);
        // ── Hair ──
        MakeBlock("Hair", root.transform, new Vector3(0.3f, 0.07f, 0.28f), new Vector3(0f, 0.9f, 0f), hairC, true);
        MakeBlock("HairL", root.transform, new Vector3(0.05f, 0.16f, 0.1f), new Vector3(-0.16f, 0.86f, 0f), hairC, true);
        MakeBlock("HairR", root.transform, new Vector3(0.05f, 0.16f, 0.1f), new Vector3(0.16f, 0.86f, 0f), hairC, true);
        // ── Eyes ──
        MakeBlock("EyeL", root.transform, new Vector3(0.04f, 0.04f, 0.04f), new Vector3(-0.07f, 0.77f, 0.14f), eyeC, true);
        MakeBlock("EyeR", root.transform, new Vector3(0.04f, 0.04f, 0.04f), new Vector3(0.07f, 0.77f, 0.14f), eyeC, true);
        // ── Upper arms (reaching forward to steering wheel) ──
        MakeBlock("UpperArmL", root.transform, new Vector3(0.1f, 0.28f, 0.1f), new Vector3(-0.26f, 0.35f, 0.15f), shirtC, true).transform.localRotation = Quaternion.Euler(-70f, 0f, 0f);
        MakeBlock("UpperArmR", root.transform, new Vector3(0.1f, 0.28f, 0.1f), new Vector3(0.26f, 0.35f, 0.15f), shirtC, true).transform.localRotation = Quaternion.Euler(-70f, 0f, 0f);
        // ── Hands (at steering wheel height) ──
        MakeBlock("HandL", root.transform, new Vector3(0.09f, 0.09f, 0.09f), new Vector3(-0.26f, 0.42f, 0.38f), skinC, true);
        MakeBlock("HandR", root.transform, new Vector3(0.09f, 0.09f, 0.09f), new Vector3(0.26f, 0.42f, 0.38f), skinC, true);
        // ── Legs (seated, bent forward) ──
        MakeBlock("ThighL", root.transform, new Vector3(0.13f, 0.3f, 0.13f), new Vector3(-0.12f, 0.05f, 0.08f), pantsC, true).transform.localRotation = Quaternion.Euler(-80f, 0f, 0f);
        MakeBlock("ThighR", root.transform, new Vector3(0.13f, 0.3f, 0.13f), new Vector3(0.12f, 0.05f, 0.08f), pantsC, true).transform.localRotation = Quaternion.Euler(-80f, 0f, 0f);
        MakeBlock("ShinL", root.transform, new Vector3(0.11f, 0.28f, 0.11f), new Vector3(-0.12f, -0.15f, 0.22f), pantsC, true).transform.localRotation = Quaternion.Euler(10f, 0f, 0f);
        MakeBlock("ShinR", root.transform, new Vector3(0.11f, 0.28f, 0.11f), new Vector3(0.12f, -0.15f, 0.22f), pantsC, true).transform.localRotation = Quaternion.Euler(10f, 0f, 0f);

        return root;
    }

    // ═══════════════════════════════════════════════════════════════
    //  TREE TEXTURE SUPPORT
    // ═══════════════════════════════════════════════════════════════

    private static Material _woodMaterial;
    private static Material _leafMaterial;

    public static void SetTreeTextures(Texture2D woodTex, Texture2D leafTex = null)
    {
        if (woodTex != null)
        {
            _woodMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            _woodMaterial.mainTexture = woodTex;
        }
        if (leafTex != null)
        {
            _leafMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            _leafMaterial.mainTexture = leafTex;
        }
    }

    private static Material GetWoodMaterial()
    {
        if (_woodMaterial == null)
        {
            var tex = Resources.Load<Texture2D>("texture/wood_texture");
            if (tex != null)
            {
                _woodMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                _woodMaterial.mainTexture = tex;
            }
        }
        return _woodMaterial;
    }

    private static Material GetLeafMaterial()
    {
        if (_leafMaterial == null)
        {
            var tex = Resources.Load<Texture2D>("texture/leaves_texture");
            if (tex != null)
            {
                _leafMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                _leafMaterial.mainTexture = tex;
            }
        }
        return _leafMaterial;
    }

}

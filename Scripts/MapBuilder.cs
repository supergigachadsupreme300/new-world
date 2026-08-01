using UnityEngine;

public static class MapBuilder
{
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

        // ── Swinging door ──
        var doorPivot = new GameObject("Door");
        doorPivot.transform.SetParent(root.transform);
        doorPivot.transform.localPosition = new Vector3(5.03f, 2f, -1.55f);
        var doorPanel = GameObject.CreatePrimitive(PrimitiveType.Cube);
        doorPanel.name = "DoorPanel";
        doorPanel.transform.SetParent(doorPivot.transform);
        doorPanel.transform.localPosition = new Vector3(1.5f, 0f, 0f);
        doorPanel.transform.localScale = new Vector3(3f, 4f, 0.3f);
        doorPanel.GetComponent<MeshRenderer>().material.color = frameC;
        doorPanel.AddComponent<BoxCollider>();

        // ── Furniture ──
        var sofaC = new Color(0.4f, 0.55f, 0.35f);
        MakeBlock("Sofa", root.transform, new Vector3(2.5f, 0.8f, 1f),
            new Vector3(-2f, 0.5f, 2f), sofaC, true);
        MakeBlock("SofaBack", root.transform, new Vector3(2.5f, 0.5f, 0.15f),
            new Vector3(-2f, 0.8f, 2.6f), sofaC, true);
        MakeBlock("Table", root.transform, new Vector3(1.2f, 0.4f, 0.8f),
            new Vector3(-2f, 0.25f, 0f), frameC, true);
        MakeBlock("Chair", root.transform, new Vector3(0.8f, 0.7f, 0.8f),
            new Vector3(-3f, 0.5f, -2.5f), frameC, true);

        // ── Bed ──
        var bed = MakeBlock("Bed", root.transform, new Vector3(2.8f, 0.5f, 1.8f),
            new Vector3(1.2f, 0.5f, -1.8f), new Color(0.608f, 0.216f, 0.216f), true);
        var bedTrigger = bed.AddComponent<BoxCollider>();
        bedTrigger.isTrigger = true;
        bedTrigger.size = new Vector3(1f, 1f, 1f);
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
        Color goldC   = new Color(0.9f, 0.68f, 0.16f);
        Color brickC  = new Color(0.62f, 0.3f, 0.17f);
        Color leafC   = new Color(0.2f, 0.45f, 0.18f);
        Color flowerC = new Color(0.85f, 0.3f, 0.4f);
        Color amberC  = new Color(1f, 0.72f, 0.35f);
        Color rugC    = new Color(0.62f, 0.16f, 0.16f);
        Color bedC    = new Color(0.95f, 0.93f, 0.88f);
        Color stoveC  = new Color(0.12f, 0.12f, 0.12f);
        Color metalC  = new Color(0.55f, 0.55f, 0.57f);

        float h1 = 5f, h2 = 4f;

        // ── 1st floor walls (segmented with window openings) ──
        float w1 = 1.6f;                       // 1F window width (y 1.2..2.8)
        float sillY1 = 0.6f, sillH1 = 1.2f;    // below window
        float headY1 = 3.9f, headH1 = 2.2f;    // above window
        float w2 = 1.6f;                       // 2F window width (y 5.95..7.35)
        float sillY2 = h1 + 0.25f + 0.35f, sillH2 = 0.7f;
        float headY2 = h1 + 0.25f + 4f - 0.95f, headH2 = 1.9f;
        float y1 = h1 / 2f;                    // 1F wall center
        float y2 = h1 + 0.25f + h2 / 2f;       // 2F wall center

        // -Z face, 1F (windows x=±4)
        MakeBlock("Wall1F_Zneg_L", root.transform, new Vector3(2.2f, h1, 0.5f), new Vector3(-5.9f, y1, -7f), wallC);
        MakeBlock("Wall1F_Zneg_M", root.transform, new Vector3(6.4f, h1, 0.5f), new Vector3(0f, y1, -7f), wallC);
        MakeBlock("Wall1F_Zneg_R", root.transform, new Vector3(2.2f, h1, 0.5f), new Vector3(5.9f, y1, -7f), wallC);
        MakeBlock("Wall1F_Zneg_W1S", root.transform, new Vector3(w1, sillH1, 0.5f), new Vector3(4f, sillY1, -7f), wallC);
        MakeBlock("Wall1F_Zneg_W1H", root.transform, new Vector3(w1, headH1, 0.5f), new Vector3(4f, headY1, -7f), wallC);
        MakeBlock("Wall1F_Zneg_W2S", root.transform, new Vector3(w1, sillH1, 0.5f), new Vector3(-4f, sillY1, -7f), wallC);
        MakeBlock("Wall1F_Zneg_W2H", root.transform, new Vector3(w1, headH1, 0.5f), new Vector3(-4f, headY1, -7f), wallC);

        // +Z face, 1F (window x=4)
        MakeBlock("Wall1F_Zpos_L", root.transform, new Vector3(10.2f, h1, 0.5f), new Vector3(-1.9f, y1, 7f), wallC);
        MakeBlock("Wall1F_Zpos_R", root.transform, new Vector3(2.2f, h1, 0.5f), new Vector3(5.9f, y1, 7f), wallC);
        MakeBlock("Wall1F_Zpos_W1S", root.transform, new Vector3(w1, sillH1, 0.5f), new Vector3(4f, sillY1, 7f), wallC);
        MakeBlock("Wall1F_Zpos_W1H", root.transform, new Vector3(w1, headH1, 0.5f), new Vector3(4f, headY1, 7f), wallC);

        // +X face, 1F (window z=-4)
        MakeBlock("Wall1F_Xpos_Back", root.transform, new Vector3(0.5f, h1, 2.2f), new Vector3(7f, y1, -5.9f), wallC);
        MakeBlock("Wall1F_Xpos_Front", root.transform, new Vector3(0.5f, h1, 10.2f), new Vector3(7f, y1, 1.9f), wallC);
        MakeBlock("Wall1F_Xpos_W1S", root.transform, new Vector3(0.5f, sillH1, w1), new Vector3(7f, sillY1, -4f), wallC);
        MakeBlock("Wall1F_Xpos_W1H", root.transform, new Vector3(0.5f, headH1, w1), new Vector3(7f, headY1, -4f), wallC);

        MakeBlock("Floor", root.transform, new Vector3(14f, 0.5f, 14f), Vector3.zero, floorC);

        // ── 2nd floor walls (segmented with window openings) ──
        // -Z face, 2F (windows x=±4)
        MakeBlock("Wall2F_Zneg_L", root.transform, new Vector3(2.2f, h2, 0.5f), new Vector3(-5.9f, y2, -7f), wallC);
        MakeBlock("Wall2F_Zneg_M", root.transform, new Vector3(6.4f, h2, 0.5f), new Vector3(0f, y2, -7f), wallC);
        MakeBlock("Wall2F_Zneg_R", root.transform, new Vector3(2.2f, h2, 0.5f), new Vector3(5.9f, y2, -7f), wallC);
        MakeBlock("Wall2F_Zneg_W1S", root.transform, new Vector3(w2, sillH2, 0.5f), new Vector3(4f, sillY2, -7f), wallC);
        MakeBlock("Wall2F_Zneg_W1H", root.transform, new Vector3(w2, headH2, 0.5f), new Vector3(4f, headY2, -7f), wallC);
        MakeBlock("Wall2F_Zneg_W2S", root.transform, new Vector3(w2, sillH2, 0.5f), new Vector3(-4f, sillY2, -7f), wallC);
        MakeBlock("Wall2F_Zneg_W2H", root.transform, new Vector3(w2, headH2, 0.5f), new Vector3(-4f, headY2, -7f), wallC);

        // +Z face, 2F (window x=4)
        MakeBlock("Wall2F_Zpos_L", root.transform, new Vector3(10.2f, h2, 0.5f), new Vector3(-1.9f, y2, 7f), wallC);
        MakeBlock("Wall2F_Zpos_R", root.transform, new Vector3(2.2f, h2, 0.5f), new Vector3(5.9f, y2, 7f), wallC);
        MakeBlock("Wall2F_Zpos_W1S", root.transform, new Vector3(w2, sillH2, 0.5f), new Vector3(4f, sillY2, 7f), wallC);
        MakeBlock("Wall2F_Zpos_W1H", root.transform, new Vector3(w2, headH2, 0.5f), new Vector3(4f, headY2, 7f), wallC);

        // +X face, 2F (window z=3)
        MakeBlock("Wall2F_Xpos_Back", root.transform, new Vector3(0.5f, h2, 9.2f), new Vector3(7f, y2, -2.4f), wallC);
        MakeBlock("Wall2F_Xpos_Front", root.transform, new Vector3(0.5f, h2, 3.2f), new Vector3(7f, y2, 5.4f), wallC);
        MakeBlock("Wall2F_Xpos_W1S", root.transform, new Vector3(0.5f, sillH2, w2), new Vector3(7f, sillY2, 3f), wallC);
        MakeBlock("Wall2F_Xpos_W1H", root.transform, new Vector3(0.5f, headH2, w2), new Vector3(7f, headY2, 3f), wallC);
        MakeBlock("Floor2F_B", root.transform, new Vector3(10.5f, 0.5f, 14f),
            new Vector3(-1.75f, h1 + 0.25f, 0f), floorC);
        MakeBlock("Floor2F_E", root.transform, new Vector3(1.5f, 0.5f, 14f),
            new Vector3(6.25f, h1 + 0.25f, 0f), floorC);
        MakeBlock("Floor2F_NS", root.transform, new Vector3(2.2f, 0.5f, 8.2f),
            new Vector3(4.5f, h1 + 0.25f, 2.9f), floorC);
        MakeBlock("Floor2F_SS", root.transform, new Vector3(2.2f, 0.5f, 0.6f),
            new Vector3(4.5f, h1 + 0.25f, -6.7f), floorC);
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

        // ── Roof details: finials, fascia, chimney ──
        MakeBlock("RidgeFinial", root.transform, new Vector3(0.45f, 0.45f, 0.45f),
            new Vector3(0f, roofY + rise + 0.55f, 8.95f), goldC);
        MakeBlock("RidgeFinial", root.transform, new Vector3(0.45f, 0.45f, 0.45f),
            new Vector3(0f, roofY + rise + 0.55f, -8.95f), goldC);
        MakeBlock("EaveFascia", root.transform, new Vector3(0.25f, 1f, 17.6f),
            new Vector3(7.2f, roofY + 0.15f, 0f), roofC);
        MakeBlock("EaveFascia", root.transform, new Vector3(0.25f, 1f, 17.6f),
            new Vector3(-7.2f, roofY + 0.15f, 0f), roofC);
        MakeBlock("ChimneyCollar", root.transform, new Vector3(2.1f, 0.25f, 2.1f),
            new Vector3(3.5f, 11.7f, 3f), roofC);
        MakeBlock("Chimney", root.transform, new Vector3(1.4f, 1.9f, 1.4f),
            new Vector3(3.5f, 12.15f, 3f), brickC);
        MakeBlock("ChimneyCap", root.transform, new Vector3(1.7f, 0.15f, 1.7f),
            new Vector3(3.5f, 13.3f, 3f), ridgeC);

        // ── Stone foundation ──
        MakeBlock("Foundation", root.transform, new Vector3(15.5f, 0.5f, 15.5f),
            new Vector3(0f, -0.27f, 0f), stoneC);
        foreach (float cx in new[] { -7.45f, 7.45f })
            foreach (float cz in new[] { -7.45f, 7.45f })
                MakeBlock("FoundationCorner", root.transform, new Vector3(0.6f, 0.25f, 0.6f),
                    new Vector3(cx, -0.05f, cz), stoneC);

        // ── Windows ──
        foreach (float wx in new[] { -4f, 4f })
        {
            MakeBlock("WinGlass", root.transform, new Vector3(1.6f, 1.6f, 0.14f),
                new Vector3(wx, 2f, -7.03f), winC, true);
            MakeBlock("WinFrame", root.transform, new Vector3(0.1f, 1.6f, 0.16f),
                new Vector3(wx, 2f, -7.03f), frameC, true);
            MakeBlock("WinFrame", root.transform, new Vector3(1.6f, 0.1f, 0.16f),
                new Vector3(wx, 2f, -7.03f), frameC, true);
            AddWindowTrim(root.transform, new Vector3(wx, 2f, -7.03f), 1.6f, 1.6f,
                new Vector3(0f, 0f, -1f), frameC, stoneC, "B1");
        }
        foreach (float wx in new[] { -4f, 4f })
        {
            MakeBlock("WinGlass2F", root.transform, new Vector3(1.6f, 1.4f, 0.14f),
                new Vector3(wx, h1 + 0.25f + 1.4f, -7.03f), winC, true);
            MakeBlock("WinFrame2F", root.transform, new Vector3(0.1f, 1.4f, 0.16f),
                new Vector3(wx, h1 + 0.25f + 1.4f, -7.03f), frameC, true);
            MakeBlock("WinFrame2F", root.transform, new Vector3(1.6f, 0.1f, 0.16f),
                new Vector3(wx, h1 + 0.25f + 1.4f, -7.03f), frameC, true);
            AddWindowTrim(root.transform, new Vector3(wx, h1 + 0.25f + 1.4f, -7.03f), 1.6f, 1.4f,
                new Vector3(0f, 0f, -1f), frameC, stoneC, "B2");
        }

        // ── Windows on +Z face ──
        MakeBlock("WinGlassZ1", root.transform, new Vector3(1.6f, 1.6f, 0.14f),
            new Vector3(4f, 2f, 7.03f), winC, true);
        MakeBlock("WinFrameZ1", root.transform, new Vector3(0.1f, 1.6f, 0.16f),
            new Vector3(4f, 2f, 7.03f), frameC, true);
        MakeBlock("WinFrameZ1", root.transform, new Vector3(1.6f, 0.1f, 0.16f),
            new Vector3(4f, 2f, 7.03f), frameC, true);
        AddWindowTrim(root.transform, new Vector3(4f, 2f, 7.03f), 1.6f, 1.6f,
            new Vector3(0f, 0f, 1f), frameC, stoneC, "F1");
        MakeBlock("WinGlassZ2", root.transform, new Vector3(1.6f, 1.4f, 0.14f),
            new Vector3(4f, h1 + 0.25f + 1.4f, 7.03f), winC, true);
        MakeBlock("WinFrameZ2", root.transform, new Vector3(0.1f, 1.4f, 0.16f),
            new Vector3(4f, h1 + 0.25f + 1.4f, 7.03f), frameC, true);
        MakeBlock("WinFrameZ2", root.transform, new Vector3(1.6f, 0.1f, 0.16f),
            new Vector3(4f, h1 + 0.25f + 1.4f, 7.03f), frameC, true);
        AddWindowTrim(root.transform, new Vector3(4f, h1 + 0.25f + 1.4f, 7.03f), 1.6f, 1.4f,
            new Vector3(0f, 0f, 1f), frameC, stoneC, "F2");

        // ── Windows on +X face ──
        MakeBlock("WinGlassX1", root.transform, new Vector3(0.14f, 1.6f, 1.6f),
            new Vector3(7.03f, 2f, -4f), winC, true);
        MakeBlock("WinFrameX1", root.transform, new Vector3(0.16f, 1.6f, 0.1f),
            new Vector3(7.03f, 2f, -4f), frameC, true);
        MakeBlock("WinFrameX1", root.transform, new Vector3(0.16f, 0.1f, 1.6f),
            new Vector3(7.03f, 2f, -4f), frameC, true);
        MakeBlock("WinSillX1", root.transform, new Vector3(0.35f, 0.12f, 2.1f),
            new Vector3(7.33f, 1.14f, -4f), stoneC, true);
        MakeBlock("WinHeadX1", root.transform, new Vector3(0.35f, 0.16f, 2.1f),
            new Vector3(7.33f, 2.88f, -4f), stoneC, true);
        MakeBlock("ShutterLX1", root.transform, new Vector3(0.08f, 1.6f, 0.28f),
            new Vector3(7.23f, 2f, -4.96f), frameC, true);
        MakeBlock("ShutterRX1", root.transform, new Vector3(0.08f, 1.6f, 0.28f),
            new Vector3(7.23f, 2f, -3.04f), frameC, true);
        MakeBlock("WinGlassX2", root.transform, new Vector3(0.14f, 1.4f, 1.6f),
            new Vector3(7.03f, h1 + 0.25f + 1.4f, 3f), winC, true);
        MakeBlock("WinFrameX2", root.transform, new Vector3(0.16f, 1.4f, 0.1f),
            new Vector3(7.03f, h1 + 0.25f + 1.4f, 3f), frameC, true);
        MakeBlock("WinFrameX2", root.transform, new Vector3(0.16f, 0.1f, 1.6f),
            new Vector3(7.03f, h1 + 0.25f + 1.4f, 3f), frameC, true);
        MakeBlock("WinSillX2", root.transform, new Vector3(0.35f, 0.12f, 2.1f),
            new Vector3(7.33f, h1 + 0.25f + 1.4f - (1.4f / 2f + 0.06f), 3f), stoneC, true);
        MakeBlock("WinHeadX2", root.transform, new Vector3(0.35f, 0.16f, 2.1f),
            new Vector3(7.33f, h1 + 0.25f + 1.4f + (1.4f / 2f + 0.08f), 3f), stoneC, true);
        MakeBlock("ShutterLX2", root.transform, new Vector3(0.08f, 1.4f, 0.28f),
            new Vector3(7.23f, h1 + 0.25f + 1.4f, 2.04f), frameC, true);
        MakeBlock("ShutterRX2", root.transform, new Vector3(0.08f, 1.4f, 0.28f),
            new Vector3(7.23f, h1 + 0.25f + 1.4f, 3.96f), frameC, true);

        // ── Entrance on -X side (facing road) ──
        MakeBlock("DoorFrame", root.transform, new Vector3(0.3f, 4.5f, 0.3f),
            new Vector3(-7.04f, 2.25f, -1.6f), frameC, true);
        MakeBlock("DoorFrame", root.transform, new Vector3(0.3f, 4.5f, 0.3f),
            new Vector3(-7.04f, 2.25f, 1.6f), frameC, true);
        MakeBlock("DoorLintel", root.transform, new Vector3(0.3f, 0.35f, 3.5f),
            new Vector3(-7.04f, 4.75f, 0f), frameC, true);

        // ── Swinging door ──
        var wifeDoorPivot = new GameObject("Door");
        wifeDoorPivot.transform.SetParent(root.transform);
        wifeDoorPivot.transform.localPosition = new Vector3(-7.04f, 2.25f, -1.6f);
        var wifeDoorPanel = GameObject.CreatePrimitive(PrimitiveType.Cube);
        wifeDoorPanel.name = "DoorPanel";
        wifeDoorPanel.transform.SetParent(wifeDoorPivot.transform);
        wifeDoorPanel.transform.localPosition = new Vector3(1.5f, 0f, 0f);
        wifeDoorPanel.transform.localScale = new Vector3(3f, 4.5f, 0.3f);
        wifeDoorPanel.GetComponent<MeshRenderer>().material.color = wallC;
        wifeDoorPanel.AddComponent<BoxCollider>();

        // ── Entrance yard ──
        MakeBlock("ThresholdStep", root.transform, new Vector3(1.8f, 0.1f, 3.2f),
            new Vector3(-7.2f, 0.05f, 0f), stoneC);
        MakeBlock("WelcomeMat", root.transform, new Vector3(0.5f, 0.06f, 2.4f),
            new Vector3(-7.15f, 0.11f, 0f), rugC);
        MakeBlock("LintelCrown", root.transform, new Vector3(0.3f, 0.15f, 3.4f),
            new Vector3(-7.04f, 5f, 0f), goldC);
        MakeBlock("Lantern", root.transform, new Vector3(0.32f, 0.5f, 0.32f),
            new Vector3(-7.3f, 3.4f, 2.2f), goldC);
        MakeBlock("LanternGlow", root.transform, new Vector3(0.18f, 0.28f, 0.18f),
            new Vector3(-7.3f, 3.4f, 2.22f), amberC);
        foreach (Vector3 pt in new[]
        {
            new Vector3(-8.2f, 0.02f, 0f),
            new Vector3(-8.8f, 0.02f, 0.35f),
            new Vector3(-9.4f, 0.02f, -0.35f),
            new Vector3(-10f, 0.02f, 0.15f),
            new Vector3(-10.6f, 0.02f, -0.25f),
        })
            MakeBlock("PathTile", root.transform, new Vector3(0.9f, 0.04f, 0.55f), pt, stoneC);
        foreach (float px in new[] { -4f, 4f })
        {
            MakeBlock("Planter", root.transform, new Vector3(1.6f, 0.8f, 0.7f),
                new Vector3(px, 0.55f, -7.5f), frameC);
            foreach (float lx in new[] { -0.6f, 0f, 0.6f })
                MakeBlock("PlanterLeaf", root.transform, new Vector3(0.5f, 0.6f, 0.5f),
                    new Vector3(px + lx, 1.25f, -7.5f), leafC);
            MakeBlock("PlanterFlower", root.transform, new Vector3(0.25f, 0.25f, 0.25f),
                new Vector3(px - 0.5f, 1.55f, -7.4f), flowerC);
            MakeBlock("PlanterFlower", root.transform, new Vector3(0.25f, 0.25f, 0.25f),
                new Vector3(px + 0.5f, 1.55f, -7.6f), flowerC);
        }

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

        // ── Balcony furniture ──
        MakeBlock("BalconyTable", root.transform, new Vector3(0.8f, 0.5f, 0.8f),
            new Vector3(-7.5f, balcY + 0.35f, 2.5f), tableC);
        MakeBlock("BalconyChair", root.transform, new Vector3(0.7f, 0.6f, 0.7f),
            new Vector3(-7.95f, balcY + 0.25f, 2.9f), wallC);
        MakeBlock("BalconyPlantPot", root.transform, new Vector3(0.4f, 0.4f, 0.4f),
            new Vector3(-7.4f, balcY + 0.3f, -2f), frameC);
        MakeBlock("BalconyPlantLeaf", root.transform, new Vector3(0.6f, 0.6f, 0.5f),
            new Vector3(-7.4f, balcY + 0.75f, -2f), leafC);
        MakeBlock("BalconyPlantPot", root.transform, new Vector3(0.4f, 0.4f, 0.4f),
            new Vector3(-9.2f, balcY + 0.3f, 2.8f), frameC);
        MakeBlock("BalconyPlantLeaf", root.transform, new Vector3(0.6f, 0.6f, 0.5f),
            new Vector3(-9.2f, balcY + 0.75f, 2.8f), leafC);

        // ── Staircase (interior, +X side) — straight, auto-walkable (0.3 rise/run) ──
        for (int i = 0; i < 17; i++)
        {
            float sy = 0.25f + (i + 1) * 0.3f - 0.05f;
            float sz = -1.4f - i * 0.3f;
            float th = 0.1f;
            if (i == 0)
            {
                th = 0.3f;
                sy = 0.4f;
            }
            MakeBlock("Stair", root.transform, new Vector3(2f, th, 0.55f),
                new Vector3(4.5f, sy, sz), stairC, false);
        }

        // ── 2F door to balcony ──
        MakeBlock("DoorFrame2F", root.transform, new Vector3(0.25f, 3.5f, 0.25f),
            new Vector3(-7.04f, balcY + 1.75f, -1.5f), frameC, true);
        MakeBlock("DoorFrame2F", root.transform, new Vector3(0.25f, 3.5f, 0.25f),
            new Vector3(-7.04f, balcY + 1.75f, 1.5f), frameC, true);
        MakeBlock("DoorLintel2F", root.transform, new Vector3(0.25f, 0.3f, 3.25f),
            new Vector3(-7.04f, balcY + 3.65f, 0f), frameC, true);

        // ── 1F living set (SW corner, near the entrance) ──
        MakeBlock("Rug", root.transform, new Vector3(3.6f, 0.06f, 2.6f),
            new Vector3(-2.4f, 0.28f, -5.2f), rugC);
        MakeBlock("Sofa", root.transform, new Vector3(3f, 0.35f, 1.1f),
            new Vector3(-3f, 0.425f, -5.9f), sofaC);
        MakeBlock("SofaBack", root.transform, new Vector3(3f, 0.75f, 0.18f),
            new Vector3(-3f, 0.85f, -6.35f), sofaC);
        MakeBlock("SofaArmL", root.transform, new Vector3(0.25f, 0.55f, 1.1f),
            new Vector3(-4.375f, 0.625f, -5.9f), sofaC);
        MakeBlock("SofaArmR", root.transform, new Vector3(0.25f, 0.55f, 1.1f),
            new Vector3(-1.625f, 0.625f, -5.9f), sofaC);
        MakeBlock("SofaCushion", root.transform, new Vector3(1.15f, 0.12f, 0.85f),
            new Vector3(-3.55f, 0.66f, -5.9f), new Color(0.55f, 0.7f, 0.78f), true);
        MakeBlock("SofaCushion", root.transform, new Vector3(1.15f, 0.12f, 0.85f),
            new Vector3(-2.45f, 0.66f, -5.9f), new Color(0.5f, 0.66f, 0.75f), true);
        MakeBlock("TableTop", root.transform, new Vector3(1.3f, 0.08f, 0.75f),
            new Vector3(-3f, 0.58f, -4.1f), tableC);
        MakeBlock("TableLeg", root.transform, new Vector3(0.08f, 0.45f, 0.08f),
            new Vector3(-3.59f, 0.28f, -4.39f), frameC, true);
        MakeBlock("TableLeg", root.transform, new Vector3(0.08f, 0.45f, 0.08f),
            new Vector3(-2.41f, 0.28f, -4.39f), frameC, true);
        MakeBlock("TableLeg", root.transform, new Vector3(0.08f, 0.45f, 0.08f),
            new Vector3(-3.59f, 0.28f, -3.81f), frameC, true);
        MakeBlock("TableLeg", root.transform, new Vector3(0.08f, 0.45f, 0.08f),
            new Vector3(-2.41f, 0.28f, -3.81f), frameC, true);
        MakeBlock("Chair", root.transform, new Vector3(0.9f, 0.35f, 0.9f),
            new Vector3(-0.9f, 0.425f, -4.4f), wallC);
        MakeBlock("ChairBack", root.transform, new Vector3(0.9f, 0.7f, 0.16f),
            new Vector3(-0.9f, 0.8f, -5.2f), wallC);
        MakeBlock("LampPole", root.transform, new Vector3(0.06f, 1.4f, 0.06f),
            new Vector3(-5.3f, 0.75f, -4.4f), frameC, true);
        MakeBlock("LampBase", root.transform, new Vector3(0.3f, 0.08f, 0.3f),
            new Vector3(-5.3f, 0.29f, -4.4f), frameC, true);
        MakeBlock("LampShade", root.transform, new Vector3(0.35f, 0.25f, 0.35f),
            new Vector3(-5.3f, 1.6f, -4.4f), goldC, true);
        MakeBlock("LampGlow", root.transform, new Vector3(0.2f, 0.15f, 0.2f),
            new Vector3(-5.3f, 1.43f, -4.4f), amberC, true);
        MakeBlock("WallShelf", root.transform, new Vector3(2f, 0.12f, 0.6f),
            new Vector3(-3f, 3.2f, -6.45f), frameC, true);
        MakeBlock("PlantPot", root.transform, new Vector3(0.5f, 0.5f, 0.5f),
            new Vector3(-5.8f, 0.5f, -6f), frameC);
        MakeBlock("PlantLeaf", root.transform, new Vector3(0.9f, 0.8f, 0.9f),
            new Vector3(-5.8f, 1.1f, -6f), leafC, true);
        MakeBlock("PlantFlower", root.transform, new Vector3(0.25f, 0.25f, 0.25f),
            new Vector3(-5.8f, 1.5f, -5.8f), flowerC, true);

        // ── 1F kitchen + dining (east side) ──
        MakeBlock("KitchenCounter", root.transform, new Vector3(3.4f, 0.9f, 0.9f),
            new Vector3(1.5f, 0.7f, 5.7f), frameC);
        MakeBlock("CounterTop", root.transform, new Vector3(3.5f, 0.08f, 1f),
            new Vector3(1.5f, 1.2f, 5.7f), new Color(0.75f, 0.62f, 0.45f), true);
        MakeBlock("Sink", root.transform, new Vector3(0.8f, 0.12f, 0.55f),
            new Vector3(1.5f, 1.28f, 5.7f), metalC, true);
        MakeBlock("Stove", root.transform, new Vector3(1.5f, 0.9f, 0.9f),
            new Vector3(4.6f, 0.7f, 5.7f), stoveC);
        MakeBlock("StoveBurner", root.transform, new Vector3(0.45f, 0.06f, 0.45f),
            new Vector3(4.15f, 1.2f, 5.4f), metalC, true);
        MakeBlock("StoveBurner", root.transform, new Vector3(0.45f, 0.06f, 0.45f),
            new Vector3(4.15f, 1.2f, 6f), metalC, true);
        MakeBlock("StoveDoor", root.transform, new Vector3(1.1f, 0.35f, 0.06f),
            new Vector3(4.6f, 0.55f, 6.17f), new Color(0.18f, 0.18f, 0.2f), true);
        MakeBlock("KitchenShelf", root.transform, new Vector3(2.4f, 0.12f, 0.6f),
            new Vector3(2f, 3.2f, 6.45f), frameC, true);
        MakeBlock("DiningTable", root.transform, new Vector3(1.8f, 0.08f, 1.1f),
            new Vector3(1.5f, 0.6f, 2.6f), tableC);
        MakeBlock("DiningTableLeg", root.transform, new Vector3(0.09f, 0.4f, 0.09f),
            new Vector3(0.68f, 0.35f, 2.12f), frameC, true);
        MakeBlock("DiningTableLeg", root.transform, new Vector3(0.09f, 0.4f, 0.09f),
            new Vector3(2.32f, 0.35f, 2.12f), frameC, true);
        MakeBlock("DiningTableLeg", root.transform, new Vector3(0.09f, 0.4f, 0.09f),
            new Vector3(0.68f, 0.35f, 3.08f), frameC, true);
        MakeBlock("DiningTableLeg", root.transform, new Vector3(0.09f, 0.4f, 0.09f),
            new Vector3(2.32f, 0.35f, 3.08f), frameC, true);
        MakeBlock("DiningBench", root.transform, new Vector3(1.7f, 0.3f, 0.5f),
            new Vector3(1.5f, 0.4f, 1.9f), tableC);
        MakeBlock("DiningBench", root.transform, new Vector3(1.7f, 0.3f, 0.5f),
            new Vector3(1.5f, 0.4f, 3.3f), tableC);
        MakeBlock("LampCord", root.transform, new Vector3(0.06f, 0.6f, 0.06f),
            new Vector3(0f, 4.9f, 0.5f), frameC, true);
        MakeBlock("LampShade", root.transform, new Vector3(0.35f, 0.25f, 0.35f),
            new Vector3(0f, 4.6f, 0.5f), goldC, true);
        MakeBlock("LampGlow", root.transform, new Vector3(0.2f, 0.15f, 0.2f),
            new Vector3(0f, 4.42f, 0.5f), amberC, true);

        // ── 2F bedroom (matching stairwell opening on the NE) ──
        MakeBlock("BedFrame", root.transform, new Vector3(3f, 0.3f, 2.2f),
            new Vector3(-1f, 5.65f, 5.55f), frameC);
        MakeBlock("BedMattress", root.transform, new Vector3(2.8f, 0.3f, 2f),
            new Vector3(-1f, 5.95f, 5.55f), bedC);
        MakeBlock("BedBlanket", root.transform, new Vector3(2.8f, 0.1f, 1.6f),
            new Vector3(-1f, 6.2f, 5.25f), new Color(0.65f, 0.3f, 0.45f), true);
        MakeBlock("BedPillow", root.transform, new Vector3(1.1f, 0.18f, 0.45f),
            new Vector3(-1.5f, 6.2f, 6.3f), bedC, true);
        MakeBlock("BedPillow", root.transform, new Vector3(1.1f, 0.18f, 0.45f),
            new Vector3(-0.5f, 6.2f, 6.3f), bedC, true);
        MakeBlock("BedHeadboard", root.transform, new Vector3(3.2f, 1.2f, 0.15f),
            new Vector3(-1f, 6.4f, 6.62f), frameC);
        MakeBlock("BedFootboard", root.transform, new Vector3(2.4f, 0.6f, 0.12f),
            new Vector3(-1f, 6f, 4.42f), frameC, true);
        MakeBlock("Nightstand", root.transform, new Vector3(0.7f, 0.7f, 0.55f),
            new Vector3(-2.6f, 5.85f, 6.3f), tableC);
        MakeBlock("Nightstand", root.transform, new Vector3(0.7f, 0.7f, 0.55f),
            new Vector3(0.6f, 5.85f, 6.3f), tableC);
        MakeBlock("NightLamp", root.transform, new Vector3(0.28f, 0.4f, 0.28f),
            new Vector3(-2.6f, 6.4f, 6.3f), goldC, true);
        MakeBlock("NightLampGlow", root.transform, new Vector3(0.18f, 0.2f, 0.18f),
            new Vector3(-2.6f, 6.45f, 6.3f), amberC, true);
        MakeBlock("NightLamp", root.transform, new Vector3(0.28f, 0.4f, 0.28f),
            new Vector3(0.6f, 6.4f, 6.3f), goldC, true);
        MakeBlock("NightLampGlow", root.transform, new Vector3(0.18f, 0.2f, 0.18f),
            new Vector3(0.6f, 6.45f, 6.3f), amberC, true);
        MakeBlock("Wardrobe", root.transform, new Vector3(2.2f, 2.4f, 0.7f),
            new Vector3(-5.5f, 6.7f, -6.55f), frameC);
        MakeBlock("WardrobeDoor", root.transform, new Vector3(0.9f, 2.2f, 0.06f),
            new Vector3(-5.85f, 6.7f, -6.2f), new Color(0.45f, 0.3f, 0.15f), true);
        MakeBlock("WardrobeDoor", root.transform, new Vector3(0.9f, 2.2f, 0.06f),
            new Vector3(-5.15f, 6.7f, -6.2f), new Color(0.45f, 0.3f, 0.15f), true);
        MakeBlock("ReadingChair", root.transform, new Vector3(0.9f, 0.35f, 0.9f),
            new Vector3(4.8f, 5.85f, 2.5f), sofaC);
        MakeBlock("ReadingChairBack", root.transform, new Vector3(0.16f, 0.7f, 0.9f),
            new Vector3(5.42f, 6.2f, 2.5f), sofaC, true);
        MakeBlock("SideTable", root.transform, new Vector3(0.5f, 0.08f, 0.5f),
            new Vector3(4.2f, 6.2f, 3.1f), tableC, true);
        MakeBlock("SideTableLeg", root.transform, new Vector3(0.06f, 0.4f, 0.06f),
            new Vector3(4.2f, 5.97f, 3.1f), frameC, true);
        MakeBlock("Rug2F", root.transform, new Vector3(3.5f, 0.06f, 2.5f),
            new Vector3(-1f, 5.53f, 4f), rugC);
        MakeBlock("LampCord2F", root.transform, new Vector3(0.06f, 0.6f, 0.06f),
            new Vector3(0f, 8.95f, 0f), frameC, true);
        MakeBlock("LampShade2F", root.transform, new Vector3(0.4f, 0.25f, 0.4f),
            new Vector3(0f, 8.55f, 0f), goldC, true);
        MakeBlock("LampGlow2F", root.transform, new Vector3(0.22f, 0.16f, 0.22f),
            new Vector3(0f, 8.4f, 0f), amberC, true);

        return root;
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

    public static GameObject BuildCloud(Transform parent, Vector3 position, float scale = 1f)
    {
        var root = new GameObject("Cloud");
        root.transform.SetParent(parent);
        root.transform.position = position;
        root.transform.localScale = Vector3.one;

        float s = scale;
        Color c = new Color(1f, 1f, 1f, 0.85f);

        Vector3[] puffs = new Vector3[]
        {
            new Vector3(0f, 0f, 0f) * s,
            new Vector3(2.5f, 0.3f, 1.2f) * s,
            new Vector3(-2.2f, 0.5f, -1f) * s,
            new Vector3(1.8f, -0.2f, -1.8f) * s,
            new Vector3(-2f, -0.1f, 1.5f) * s,
            new Vector3(0.5f, 1f, 0.8f) * s,
            new Vector3(-1.2f, 0.9f, -0.5f) * s,
            new Vector3(0.8f, 0.7f, -1.2f) * s,
        };

        Vector3[] sizes = new Vector3[]
        {
            new Vector3(6f, 2.5f, 3.5f) * s,
            new Vector3(4f, 2f, 2.5f) * s,
            new Vector3(3.5f, 2.2f, 2f) * s,
            new Vector3(3f, 1.8f, 2.5f) * s,
            new Vector3(3.5f, 1.5f, 2.8f) * s,
            new Vector3(2.5f, 1.8f, 2f) * s,
            new Vector3(2f, 1.5f, 1.8f) * s,
            new Vector3(2.2f, 1.5f, 1.5f) * s,
        };

        for (int i = 0; i < puffs.Length; i++)
        {
            var block = MakeBlock("Puff" + i, root.transform, sizes[i], puffs[i], c, true);
            var r = block.GetComponent<Renderer>();
            if (r != null) SetTransparent(r, 0.85f);
        }

        return root;
    }

    public static GameObject BuildTornado(Transform parent, Vector3 position, float height = 60f)
    {
        var root = new GameObject("Tornado");
        root.transform.SetParent(parent);
        root.transform.position = position;

        int count = 30;
        float blockHeight = height / count;
        Color col = new Color(0.35f, 0.32f, 0.28f);

        for (int i = 0; i < count; i++)
        {
            float t = (float)i / (count - 1);
            float y = i * blockHeight + blockHeight * 0.5f;
            float width = 0.5f + t * 25f;

            var block = MakeBlock("Block" + i, root.transform,
                new Vector3(width, blockHeight, width),
                new Vector3(0f, y, 0f), col, true);
            block.transform.localRotation = Quaternion.Euler(0f, UnityEngine.Random.Range(0f, 360f), 0f);
        }

        root.AddComponent<TornadoBehavior>();
        return root;
    }
}

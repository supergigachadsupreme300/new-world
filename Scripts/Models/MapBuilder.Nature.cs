using UnityEngine;

public static partial class MapBuilder
{
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

        Color wood = new Color(0.65f, 0.5f, 0.3f);
        Color leaf = new Color(0.35f, 0.75f, 0.25f);

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

    // ═══════════════════════════════════════════════════════════════
    //  WEATHER (CLOUD / TORNADO)
    // ═══════════════════════════════════════════════════════════════

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

    public static GameObject BuildTornado(Transform parent, Vector3 position, float height = 90f)
    {
        var root = new GameObject("Tornado");
        root.transform.SetParent(parent);
        root.transform.position = position;

        int count = 45;
        float blockHeight = height / count;
        Color col = new Color(0.35f, 0.32f, 0.28f);

        for (int i = 0; i < count; i++)
        {
            float t = (float)i / (count - 1);
            float y = i * blockHeight + blockHeight * 0.5f;
            float width = 0.5f + t * 35f;

            var block = MakeBlock("Block" + i, root.transform,
                new Vector3(width, blockHeight, width),
                new Vector3(0f, y, 0f), col, true);
            block.transform.localRotation = Quaternion.Euler(0f, UnityEngine.Random.Range(0f, 360f), 0f);
        }

        root.AddComponent<TornadoBehavior>();
        return root;
    }
}

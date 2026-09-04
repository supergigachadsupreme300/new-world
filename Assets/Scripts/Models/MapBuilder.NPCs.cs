using UnityEngine;

public static partial class MapBuilder
{
    // ==================== MapBuilderAddictNpc.cs ====================
    // ═══════════════════════════════════════════════════════════════
    //  RAGGED ADDICT  (junkie hunched over a victim, demon ending)
    // ═══════════════════════════════════════════════════════════════

    public static GameObject BuildAddictNpc(Transform parent, Vector3 position = default, Quaternion rotation = default)
    {
        var root = new GameObject("RaggedAddict");
        root.transform.SetParent(parent);
        root.transform.position = position;
        root.transform.rotation = (rotation == default) ? Quaternion.identity : rotation;

        Color ragC = new Color(0.38f, 0.34f, 0.29f);
        Color ragDarkC = new Color(0.22f, 0.19f, 0.16f);
        Color skinC = new Color(0.62f, 0.5f, 0.42f);
        Color skinDarkC = new Color(0.32f, 0.25f, 0.2f);
        Color hairC = new Color(0.16f, 0.13f, 0.09f);

        // Bent legs — crouching over the body
        MakeBlock("LegL", root.transform, new Vector3(0.15f, 0.32f, 0.15f), new Vector3(-0.15f, 0.14f, 0.12f), ragDarkC, true);
        MakeBlock("LegR", root.transform, new Vector3(0.17f, 0.2f, 0.45f), new Vector3(0.15f, 0.13f, -0.18f), ragDarkC, true);
        MakeBlock("FootL", root.transform, new Vector3(0.16f, 0.08f, 0.24f), new Vector3(-0.15f, 0.04f, 0.28f), ragDarkC, true);
        MakeBlock("FootR", root.transform, new Vector3(0.16f, 0.08f, 0.26f), new Vector3(0.15f, 0.04f, -0.02f), ragDarkC, true);

        // Hunched torso, leaning toward the victim
        MakeBlock("Hip", root.transform, new Vector3(0.48f, 0.18f, 0.3f), new Vector3(0f, 0.24f, -0.04f), ragDarkC, true);
        MakeBlock("Body", root.transform, new Vector3(0.5f, 0.42f, 0.34f), new Vector3(0f, 0.42f, -0.08f), ragC, true);
        MakeBlock("RagWrap", root.transform, new Vector3(0.5f, 0.1f, 0.3f), new Vector3(0f, 0.28f, 0.05f), ragDarkC, true);
        MakeBlock("PatchL", root.transform, new Vector3(0.18f, 0.16f, 0.05f), new Vector3(-0.22f, 0.5f, -0.22f), ragDarkC, true);
        MakeBlock("PatchR", root.transform, new Vector3(0.16f, 0.14f, 0.05f), new Vector3(0.21f, 0.34f, -0.22f), ragDarkC, true);
        MakeBlock("Shawl", root.transform, new Vector3(0.56f, 0.14f, 0.34f), new Vector3(0f, 0.6f, -0.1f), ragDarkC, true);

        // Neck + head, low and forward
        MakeBlock("Neck", root.transform, new Vector3(0.14f, 0.1f, 0.14f), new Vector3(0f, 0.58f, -0.14f), skinC, true);
        MakeBlock("Head", root.transform, new Vector3(0.3f, 0.28f, 0.3f), new Vector3(0f, 0.68f, -0.24f), skinC, true);
        MakeBlock("EarL", root.transform, new Vector3(0.04f, 0.07f, 0.03f), new Vector3(-0.17f, 0.7f, -0.24f), skinC, true);
        MakeBlock("EarR", root.transform, new Vector3(0.04f, 0.07f, 0.03f), new Vector3(0.17f, 0.7f, -0.24f), skinC, true);
        MakeBlock("EyeWhiteL", root.transform, new Vector3(0.08f, 0.05f, 0.03f), new Vector3(-0.09f, 0.71f, -0.4f), new Color(0.95f, 0.95f, 0.97f), true);
        MakeBlock("EyeWhiteR", root.transform, new Vector3(0.08f, 0.05f, 0.03f), new Vector3(0.09f, 0.71f, -0.4f), new Color(0.95f, 0.95f, 0.97f), true);
        MakeBlock("PupilL", root.transform, new Vector3(0.03f, 0.03f, 0.02f), new Vector3(-0.09f, 0.71f, -0.42f), skinDarkC, true);
        MakeBlock("PupilR", root.transform, new Vector3(0.03f, 0.03f, 0.02f), new Vector3(0.09f, 0.71f, -0.42f), skinDarkC, true);
        MakeBlock("BrowL", root.transform, new Vector3(0.09f, 0.03f, 0.02f), new Vector3(-0.09f, 0.75f, -0.4f), hairC, true);
        MakeBlock("BrowR", root.transform, new Vector3(0.09f, 0.03f, 0.02f), new Vector3(0.09f, 0.75f, -0.4f), hairC, true);
        MakeBlock("Nose", root.transform, new Vector3(0.07f, 0.05f, 0.05f), new Vector3(0f, 0.69f, -0.42f), skinC, true);
        MakeBlock("Stubble", root.transform, new Vector3(0.24f, 0.06f, 0.08f), new Vector3(0f, 0.63f, -0.38f), skinDarkC, true);

        // Messy hair
        MakeBlock("Hair", root.transform, new Vector3(0.36f, 0.14f, 0.36f), new Vector3(0f, 0.84f, -0.24f), hairC, true);
        MakeBlock("HairTuftL", root.transform, new Vector3(0.08f, 0.12f, 0.08f), new Vector3(-0.15f, 0.92f, -0.27f), hairC, true);
        MakeBlock("HairTuftR", root.transform, new Vector3(0.08f, 0.1f, 0.08f), new Vector3(0.15f, 0.9f, -0.22f), hairC, true);

        // Arms reaching down toward the victim
        MakeBlock("ArmL", root.transform, new Vector3(0.13f, 0.38f, 0.13f), new Vector3(-0.34f, 0.44f, -0.05f), ragC, true);
        MakeBlock("ArmR", root.transform, new Vector3(0.13f, 0.38f, 0.13f), new Vector3(0.34f, 0.44f, -0.05f), ragC, true);
        MakeBlock("HandL", root.transform, new Vector3(0.11f, 0.11f, 0.11f), new Vector3(-0.34f, 0.22f, -0.18f), skinC, true);
        MakeBlock("HandR", root.transform, new Vector3(0.11f, 0.11f, 0.11f), new Vector3(0.34f, 0.22f, -0.18f), skinC, true);

        var col = root.AddComponent<BoxCollider>();
        col.size = new Vector3(0.8f, 1f, 0.8f);
        col.center = new Vector3(0f, 0.42f, 0f);
        col.isTrigger = true;

        return root;
    }

    // ==================== MapBuilderBuffalo.cs ====================
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
        MakeBlock("Body", root.transform, new Vector3(1.4f, 0.7f, 0.6f), new Vector3(0f, 0.7f, 0f), bodyC, true);
        // Neck
        MakeBlock("Neck", root.transform, new Vector3(0.35f, 0.45f, 0.35f), new Vector3(0.85f, 0.95f, 0f), bodyC, true);
        // Head
        MakeBlock("Head", root.transform, new Vector3(0.45f, 0.3f, 0.35f), new Vector3(1.15f, 0.9f, 0f), bodyC, true);
        // Snout
        MakeBlock("Snout", root.transform, new Vector3(0.25f, 0.2f, 0.3f), new Vector3(1.5f, 0.75f, 0f), darkC, true);
        // Ears
        MakeBlock("EarL", root.transform, new Vector3(0.1f, 0.02f, 0.2f), new Vector3(1.0f, 1.0f, -0.3f), bodyC, true);
        MakeBlock("EarR", root.transform, new Vector3(0.1f, 0.02f, 0.2f), new Vector3(1.0f, 1.0f, 0.3f), bodyC, true);
        // Horns (flipped upside — pointing upward)
        for (int s = -1; s <= 1; s += 2)
        {
            float z = s * 0.18f;
            MakeBlock("Horn" + (s > 0 ? "R" : "L") + "B", root.transform, new Vector3(0.04f, 0.15f, 0.04f), new Vector3(1.05f, 1.02f, z), hornC, true);
            MakeBlock("Horn" + (s > 0 ? "R" : "L") + "T", root.transform, new Vector3(0.04f, 0.14f, 0.04f), new Vector3(1.0f, 1.17f, z), hornC, true);
        }
        // Eyes (with white sclera — wider Z for side visibility)
        MakeBlock("EyeWhiteL", root.transform, new Vector3(0.07f, 0.06f, 0.08f), new Vector3(1.2f, 0.93f, -0.17f), new Color(0.95f, 0.95f, 0.97f), true);
        MakeBlock("EyeWhiteR", root.transform, new Vector3(0.07f, 0.06f, 0.08f), new Vector3(1.2f, 0.93f, 0.17f), new Color(0.95f, 0.95f, 0.97f), true);
        MakeBlock("EyeL", root.transform, new Vector3(0.06f, 0.05f, 0.05f), new Vector3(1.21f, 0.93f, -0.20f), new Color(0.15f, 0.10f, 0.05f), true);
        MakeBlock("EyeR", root.transform, new Vector3(0.06f, 0.05f, 0.05f), new Vector3(1.21f, 0.93f, 0.20f), new Color(0.15f, 0.10f, 0.05f), true);
        // Legs
        float[][] legP = new float[][] {
            new float[] { -0.55f, -0.3f }, new float[] { -0.55f, 0.3f },
            new float[] { 0.6f, -0.3f }, new float[] { 0.6f, 0.3f }
        };
        foreach (var p in legP)
        {
            MakeBlock("Leg", root.transform, new Vector3(0.16f, 0.45f, 0.16f), new Vector3(p[0], 0.22f, p[1]), bodyC, true);
            MakeBlock("Hoof", root.transform, new Vector3(0.18f, 0.05f, 0.18f), new Vector3(p[0], 0.0f, p[1]), darkC, true);
        }
        // Tail
        MakeBlock("Tail", root.transform, new Vector3(0.04f, 0.3f, 0.04f), new Vector3(-0.95f, 0.35f, 0f), darkC, true);
        MakeBlock("Tuft", root.transform, new Vector3(0.1f, 0.1f, 0.1f), new Vector3(-0.95f, 0.15f, 0f), darkC, true);

        var col = root.AddComponent<BoxCollider>();
        col.size = new Vector3(2.5f, 1.6f, 1f);
        col.center = new Vector3(0.2f, 0.7f, 0f);
        col.isTrigger = true;

        return root;
    }

    // ==================== MapBuilderMarketNpc.cs ====================
    // ═══════════════════════════════════════════════════════════════
    //  MARKET NPC  (blocky villager for tool shop & grocery stall)
    // ═══════════════════════════════════════════════════════════════

    public static GameObject BuildMarketNpc(Transform parent, string npcName, Vector3 localPosition, Quaternion rotation)
    {
        var root = new GameObject(npcName);
        root.transform.SetParent(parent);
        root.transform.localPosition = localPosition;
        root.transform.localRotation = rotation;

        bool isTool = npcName == "ToolShopNPC";
        Color apronC = isTool ? new Color(0.45f, 0.48f, 0.52f) : new Color(0.3f, 0.55f, 0.3f);
        Color apronDarkC = isTool ? new Color(0.3f, 0.32f, 0.36f) : new Color(0.2f, 0.42f, 0.2f);
        Color shirtC = new Color(0.75f, 0.6f, 0.3f);
        Color skinC = new Color(230f / 255f, 200f / 255f, 175f / 255f);
        Color pantsC = new Color(0.16f, 0.16f, 0.16f);
        Color bootC = new Color(0.1f, 0.1f, 0.1f);

        MakeBlock("LegL", root.transform, new Vector3(0.17f, 0.5f, 0.17f), new Vector3(-0.15f, -0.6f, 0f), pantsC, true);
        MakeBlock("LegR", root.transform, new Vector3(0.17f, 0.5f, 0.17f), new Vector3(0.15f, -0.6f, 0f), pantsC, true);
        MakeBlock("BootL", root.transform, new Vector3(0.2f, 0.09f, 0.3f), new Vector3(-0.15f, -0.88f, 0f), bootC, true);
        MakeBlock("BootR", root.transform, new Vector3(0.2f, 0.09f, 0.3f), new Vector3(0.15f, -0.88f, 0f), bootC, true);

        MakeBlock("Body", root.transform, new Vector3(0.52f, 0.55f, 0.3f), new Vector3(0f, 0f, 0f), shirtC, true);
        MakeBlock("Apron", root.transform, new Vector3(0.5f, 0.42f, 0.06f), new Vector3(0f, -0.05f, 0.11f), apronC, true);
        MakeBlock("ApronStrap", root.transform, new Vector3(0.06f, 0.45f, 0.05f), new Vector3(-0.16f, 0.05f, 0.12f), apronDarkC, true);
        MakeBlock("ApronStrap", root.transform, new Vector3(0.06f, 0.45f, 0.05f), new Vector3(0.16f, 0.05f, 0.12f), apronDarkC, true);
        MakeBlock("Belt", root.transform, new Vector3(0.54f, 0.07f, 0.06f), new Vector3(0f, 0.12f, -0.16f), pantsC, true);

        MakeBlock("Neck", root.transform, new Vector3(0.14f, 0.12f, 0.14f), new Vector3(0f, 0.36f, 0f), skinC, true);
        MakeBlock("Head", root.transform, new Vector3(0.32f, 0.3f, 0.32f), new Vector3(0f, 0.52f, 0f), skinC, true);
        MakeBlock("EyeWhiteL", root.transform, new Vector3(0.08f, 0.06f, 0.03f), new Vector3(-0.09f, 0.55f, -0.16f), new Color(0.95f, 0.95f, 0.97f), true);
        MakeBlock("EyeWhiteR", root.transform, new Vector3(0.08f, 0.06f, 0.03f), new Vector3(0.09f, 0.55f, -0.16f), new Color(0.95f, 0.95f, 0.97f), true);
        MakeBlock("Nose", root.transform, new Vector3(0.07f, 0.06f, 0.05f), new Vector3(0f, 0.51f, -0.17f), skinC, true);
        MakeBlock("Hair", root.transform, new Vector3(0.34f, 0.1f, 0.34f), new Vector3(0f, 0.7f, 0f), new Color(0.15f, 0.1f, 0.07f), true);

        MakeBlock("ArmL", root.transform, new Vector3(0.14f, 0.45f, 0.14f), new Vector3(-0.36f, 0.1f, 0f), shirtC, true);
        MakeBlock("ArmR", root.transform, new Vector3(0.14f, 0.45f, 0.14f), new Vector3(0.36f, 0.1f, 0f), shirtC, true);
        MakeBlock("HandL", root.transform, new Vector3(0.12f, 0.1f, 0.12f), new Vector3(-0.36f, -0.14f, 0f), skinC, true);
        MakeBlock("HandR", root.transform, new Vector3(0.12f, 0.1f, 0.12f), new Vector3(0.36f, -0.14f, 0f), skinC, true);

        var col = root.AddComponent<BoxCollider>();
        col.size = new Vector3(0.9f, 1.7f, 0.7f);
        col.center = new Vector3(0f, 0.45f, 0f);
        col.isTrigger = true;

        return root;
    }

    // ==================== MapBuilderMonkNpc.cs ====================
    // ═══════════════════════════════════════════════════════════════
    //  PAGODA MONK  (dialog-only tip giver)
    // ═══════════════════════════════════════════════════════════════

    public static GameObject BuildMonkNpc(Transform parent, Vector3 position = default, Quaternion rotation = default)
    {
        var root = new GameObject("PagodaMonkNpc");
        root.transform.SetParent(parent);
        root.transform.position = position;
        root.transform.rotation = (rotation == default) ? Quaternion.identity : rotation;

        Color robeC = new Color(0.95f, 0.58f, 0.22f);
        Color robeDarkC = new Color(0.82f, 0.46f, 0.16f);
        Color skinC = new Color(230f / 255f, 200f / 255f, 175f / 255f);
        Color sandalC = new Color(0.45f, 0.3f, 0.15f);

        MakeBlock("LegL", root.transform, new Vector3(0.2f, 0.5f, 0.2f), new Vector3(-0.15f, -0.6f, 0f), robeDarkC, true);
        MakeBlock("LegR", root.transform, new Vector3(0.2f, 0.5f, 0.2f), new Vector3(0.15f, -0.6f, 0f), robeDarkC, true);
        MakeBlock("SandalL", root.transform, new Vector3(0.22f, 0.07f, 0.34f), new Vector3(-0.15f, -0.88f, 0f), sandalC, true);
        MakeBlock("SandalR", root.transform, new Vector3(0.22f, 0.07f, 0.34f), new Vector3(0.15f, -0.88f, 0f), sandalC, true);

        MakeBlock("Robe", root.transform, new Vector3(0.48f, 0.6f, 0.36f), new Vector3(0f, 0f, 0f), robeC, true);
        MakeBlock("RobeDrape", root.transform, new Vector3(0.18f, 0.5f, 0.1f), new Vector3(-0.18f, 0.05f, 0.05f), robeDarkC, true);
        MakeBlock("Sash", root.transform, new Vector3(0.5f, 0.09f, 0.08f), new Vector3(0f, 0.1f, -0.17f), robeDarkC, true);

        MakeBlock("Neck", root.transform, new Vector3(0.14f, 0.12f, 0.14f), new Vector3(0f, 0.36f, 0f), skinC, true);
        MakeBlock("Head", root.transform, new Vector3(0.28f, 0.28f, 0.28f), new Vector3(0f, 0.52f, 0f), skinC, true);
        MakeBlock("EyeWhiteL", root.transform, new Vector3(0.08f, 0.06f, 0.03f), new Vector3(-0.09f, 0.55f, -0.15f), new Color(0.95f, 0.95f, 0.97f), true);
        MakeBlock("EyeWhiteR", root.transform, new Vector3(0.08f, 0.06f, 0.03f), new Vector3(0.09f, 0.55f, -0.15f), new Color(0.95f, 0.95f, 0.97f), true);
        MakeBlock("EyeIrisL", root.transform, new Vector3(0.05f, 0.05f, 0.04f), new Vector3(-0.09f, 0.55f, -0.158f), new Color(0.05f, 0.03f, 0.01f), true);
        MakeBlock("EyeIrisR", root.transform, new Vector3(0.05f, 0.05f, 0.04f), new Vector3(0.09f, 0.55f, -0.158f), new Color(0.05f, 0.03f, 0.01f), true);
        MakeBlock("BrowL", root.transform, new Vector3(0.09f, 0.03f, 0.03f), new Vector3(-0.09f, 0.59f, -0.15f), robeDarkC, true);
        MakeBlock("BrowR", root.transform, new Vector3(0.09f, 0.03f, 0.03f), new Vector3(0.09f, 0.59f, -0.15f), robeDarkC, true);
        MakeBlock("Nose", root.transform, new Vector3(0.07f, 0.06f, 0.05f), new Vector3(0f, 0.51f, -0.16f), skinC, true);
        MakeBlock("EarLobeL", root.transform, new Vector3(0.05f, 0.08f, 0.03f), new Vector3(-0.16f, 0.52f, 0f), skinC, true);
        MakeBlock("EarLobeR", root.transform, new Vector3(0.05f, 0.08f, 0.03f), new Vector3(0.16f, 0.52f, 0f), skinC, true);

        MakeBlock("ArmL", root.transform, new Vector3(0.14f, 0.45f, 0.14f), new Vector3(-0.32f, 0.05f, 0f), robeC, true);
        MakeBlock("ArmR", root.transform, new Vector3(0.14f, 0.45f, 0.14f), new Vector3(0.32f, 0.05f, 0f), robeC, true);
        MakeBlock("HandL", root.transform, new Vector3(0.11f, 0.1f, 0.11f), new Vector3(-0.32f, -0.14f, 0f), skinC, true);
        MakeBlock("HandR", root.transform, new Vector3(0.11f, 0.1f, 0.11f), new Vector3(0.32f, -0.14f, 0f), skinC, true);

        MakeBlock("Beads", root.transform, new Vector3(0.06f, 0.4f, 0.03f), new Vector3(0.14f, -0.02f, 0.12f), new Color(0.6f, 0.35f, 0.15f), true);

        var col = root.AddComponent<BoxCollider>();
        col.size = new Vector3(0.8f, 1.7f, 0.7f);
        col.center = new Vector3(0f, 0.45f, 0f);
        col.isTrigger = true;

        return root;
    }

    // ==================== MapBuilderImmigrantNpc.cs ====================
    // ═══════════════════════════════════════════════════════════════
    //  IMMIGRANT NPC  (newcomer villager with a carrying bundle)
    // ═══════════════════════════════════════════════════════════════

    public struct ImmigrantVariation
    {
        public Color SkinColor;
        public Color ShirtColor;
        public Color PantsColor;
        public Color BootColor;
        public Color HatColor;
        public Color BundleColor;
        public Color HairColor;
        public float EyeSpacing;
        public float HeadScale;
        public float ArmLength;
        public float BodyWidth;
        public float HeightOffset;
        public float LegWidth;
        public float HatTilt;
        public bool HasBeard;
        public bool RolledSleeves;

        public static ImmigrantVariation Random()
        {
            var v = new ImmigrantVariation();
            Color[] skins = {
                new Color(0.92f, 0.82f, 0.72f),
                new Color(0.84f, 0.71f, 0.59f),
                new Color(0.72f, 0.56f, 0.42f),
                new Color(0.6f, 0.44f, 0.32f),
                new Color(0.5f, 0.38f, 0.28f),
                new Color(0.88f, 0.76f, 0.65f),
                new Color(0.66f, 0.5f, 0.37f)
            };
            Color[] shirts = {
                new Color(0.35f, 0.5f, 0.28f),
                new Color(0.3f, 0.4f, 0.6f),
                new Color(0.65f, 0.3f, 0.25f),
                new Color(0.7f, 0.5f, 0.2f),
                new Color(0.5f, 0.5f, 0.55f),
                new Color(0.55f, 0.35f, 0.55f),
                new Color(0.8f, 0.75f, 0.3f),
                new Color(0.4f, 0.55f, 0.5f),
                new Color(0.75f, 0.35f, 0.35f),
                new Color(0.3f, 0.45f, 0.4f)
            };
            Color[] pants = {
                new Color(0.4f, 0.33f, 0.22f),
                new Color(0.3f, 0.35f, 0.5f),
                new Color(0.45f, 0.45f, 0.48f),
                new Color(0.25f, 0.25f, 0.28f),
                new Color(0.55f, 0.5f, 0.35f),
                new Color(0.35f, 0.42f, 0.3f),
                new Color(0.38f, 0.3f, 0.25f),
                new Color(0.42f, 0.38f, 0.42f)
            };
            Color[] boots = {
                new Color(0.35f, 0.25f, 0.12f),
                new Color(0.2f, 0.18f, 0.15f),
                new Color(0.5f, 0.28f, 0.18f),
                new Color(0.4f, 0.32f, 0.2f)
            };
            Color[] hats = {
                new Color(0.85f, 0.8f, 0.5f),
                new Color(0.7f, 0.6f, 0.35f),
                new Color(0.9f, 0.85f, 0.65f),
                new Color(0.6f, 0.5f, 0.3f),
                new Color(0.75f, 0.7f, 0.55f)
            };
            Color[] bundles = {
                new Color(0.75f, 0.7f, 0.6f),
                new Color(0.6f, 0.5f, 0.35f),
                new Color(0.55f, 0.55f, 0.58f),
                new Color(0.5f, 0.6f, 0.45f),
                new Color(0.65f, 0.55f, 0.4f)
            };
            Color[] hairs = {
                new Color(0.12f, 0.09f, 0.06f),
                new Color(0.25f, 0.18f, 0.1f),
                new Color(0.08f, 0.06f, 0.04f),
                new Color(0.35f, 0.25f, 0.15f),
                new Color(0.15f, 0.12f, 0.1f),
                new Color(0.05f, 0.04f, 0.03f)
            };
            v.SkinColor = skins[UnityEngine.Random.Range(0, skins.Length)];
            v.ShirtColor = shirts[UnityEngine.Random.Range(0, shirts.Length)];
            v.PantsColor = pants[UnityEngine.Random.Range(0, pants.Length)];
            v.BootColor = boots[UnityEngine.Random.Range(0, boots.Length)];
            v.HatColor = hats[UnityEngine.Random.Range(0, hats.Length)];
            v.BundleColor = bundles[UnityEngine.Random.Range(0, bundles.Length)];
            v.HairColor = hairs[UnityEngine.Random.Range(0, hairs.Length)];
            v.EyeSpacing = UnityEngine.Random.Range(0.05f, 0.14f);
            v.HeadScale = UnityEngine.Random.Range(0.24f, 0.42f);
            v.ArmLength = UnityEngine.Random.Range(0.28f, 0.65f);
            v.BodyWidth = UnityEngine.Random.Range(0.36f, 0.68f);
            v.HeightOffset = UnityEngine.Random.Range(-0.15f, 0.15f);
            v.LegWidth = UnityEngine.Random.Range(0.14f, 0.24f);
            v.HatTilt = UnityEngine.Random.Range(-12f, 12f);
            v.HasBeard = UnityEngine.Random.value < 0.35f;
            v.RolledSleeves = UnityEngine.Random.value < 0.4f;
            return v;
        }
    }

    public static GameObject BuildImmigrantNpc(Transform parent, Vector3 position = default, Quaternion rotation = default)
    {
        return BuildImmigrantNpc(parent, ImmigrantVariation.Random(), position, rotation);
    }

    public static GameObject BuildImmigrantNpc(Transform parent, ImmigrantVariation v, Vector3 position = default, Quaternion rotation = default)
    {
        var root = new GameObject("ImmigrantNpc");
        root.transform.SetParent(parent);
        root.transform.position = position + Vector3.up * v.HeightOffset;
        root.transform.rotation = (rotation == default) ? Quaternion.identity : rotation;

        Color beltC = new Color(0.25f, 0.18f, 0.1f);
        Color eyeWhite = new Color(0.95f, 0.95f, 0.97f);
        Color beardC = new Color(0.18f, 0.14f, 0.1f);

        float armW = v.RolledSleeves ? 0.11f : 0.14f;
        float armH = v.ArmLength;
        float handY = 0.1f - v.ArmLength * 0.55f;
        float bodyW = v.BodyWidth;
        float headS = v.HeadScale;
        float eyeX = v.EyeSpacing;
        float legW = v.LegWidth;

        MakeBlock("LegL", root.transform, new Vector3(legW, 0.5f, legW), new Vector3(-0.15f, -0.6f, 0f), v.PantsColor, true);
        MakeBlock("LegR", root.transform, new Vector3(legW, 0.5f, legW), new Vector3(0.15f, -0.6f, 0f), v.PantsColor, true);
        MakeBlock("BootL", root.transform, new Vector3(legW + 0.03f, 0.09f, 0.3f), new Vector3(-0.15f, -0.88f, 0f), v.BootColor, true);
        MakeBlock("BootR", root.transform, new Vector3(legW + 0.03f, 0.09f, 0.3f), new Vector3(0.15f, -0.88f, 0f), v.BootColor, true);
        MakeBlock("Body", root.transform, new Vector3(bodyW, 0.55f, 0.3f), new Vector3(0f, 0f, 0f), v.ShirtColor, true);
        MakeBlock("Belt", root.transform, new Vector3(bodyW + 0.02f, 0.07f, 0.32f), new Vector3(0f, -0.22f, 0f), beltC, true);
        MakeBlock("Neck", root.transform, new Vector3(0.14f, 0.12f, 0.14f), new Vector3(0f, 0.36f, 0f), v.SkinColor, true);
        MakeBlock("Head", root.transform, new Vector3(headS, headS * 0.94f, headS), new Vector3(0f, 0.52f, 0f), v.SkinColor, true);
        MakeBlock("Hair", root.transform, new Vector3(headS + 0.01f, 0.09f, headS + 0.01f), new Vector3(0f, 0.65f, 0f), v.HairColor, true);
        MakeBlock("ConicalHat", root.transform, new Vector3(0.6f, 0.06f, 0.6f), new Vector3(v.HatTilt, 0.7f, 0f), v.HatColor, true);
        MakeBlock("EyeWhiteL", root.transform, new Vector3(0.08f, 0.06f, 0.03f), new Vector3(-eyeX, 0.55f, -0.16f), eyeWhite, true);
        MakeBlock("EyeWhiteR", root.transform, new Vector3(0.08f, 0.06f, 0.03f), new Vector3(eyeX, 0.55f, -0.16f), eyeWhite, true);
        MakeBlock("PupilL", root.transform, new Vector3(0.04f, 0.04f, 0.03f), new Vector3(-eyeX, 0.55f, -0.18f), Color.black, true);
        MakeBlock("PupilR", root.transform, new Vector3(0.04f, 0.04f, 0.03f), new Vector3(eyeX, 0.55f, -0.18f), Color.black, true);
        MakeBlock("Nose", root.transform, new Vector3(0.07f, 0.06f, 0.05f), new Vector3(0f, 0.51f, -0.17f), v.SkinColor, true);
        MakeBlock("ArmL", root.transform, new Vector3(armW, armH, armW), new Vector3(-0.36f, 0.1f, 0f), v.ShirtColor, true);
        MakeBlock("ArmR", root.transform, new Vector3(armW, armH, armW), new Vector3(0.36f, 0.1f, 0f), v.ShirtColor, true);
        MakeBlock("HandL", root.transform, new Vector3(0.12f, 0.1f, 0.12f), new Vector3(-0.36f, handY, 0f), v.SkinColor, true);
        MakeBlock("HandR", root.transform, new Vector3(0.12f, 0.1f, 0.12f), new Vector3(0.36f, handY, 0f), v.SkinColor, true);
        MakeBlock("Bundle", root.transform, new Vector3(0.32f, 0.3f, 0.32f), new Vector3(0.42f, -0.05f, 0.1f), v.BundleColor, true);
        if (v.HasBeard)
        {
            MakeBlock("Beard", root.transform, new Vector3(0.12f, 0.08f, 0.06f), new Vector3(0f, 0.42f, -0.15f), beardC, true);
        }

        var col = root.AddComponent<BoxCollider>();
        col.size = new Vector3(0.9f, 1.7f, 0.7f);
        col.center = new Vector3(0f, 0.45f, 0f);
        col.isTrigger = true;

        return root;
    }

    // ==================== MapBuilderPoliceOfficer.cs ====================
    // ═══════════════════════════════════════════════════════════════
    //  POLICE OFFICER
    // ═══════════════════════════════════════════════════════════════

    public static GameObject BuildPoliceOfficer(Transform parent, Vector3 position = default, Quaternion rotation = default)
    {
        var root = new GameObject("PoliceOfficer");
        root.transform.SetParent(parent);
        root.transform.position = position;
        root.transform.rotation = rotation;

        Color uniC = new Color(0.42f, 0.47f, 0.28f);
        Color skinC = new Color(230f / 255f, 200f / 255f, 175f / 255f);
        Color hatC = new Color(0.42f, 0.47f, 0.28f);
        Color goldC = new Color(0.92f, 0.78f, 0.3f);
        Color pantsC = new Color(0.34f, 0.39f, 0.22f);
        Color bootC = new Color(0.1f, 0.1f, 0.1f);
        Color visorC = new Color(0.08f, 0.08f, 0.08f);
        Color redC = new Color(0.72f, 0.12f, 0.12f);

        MakeBlock("Body", root.transform, new Vector3(0.5f, 0.55f, 0.3f), new Vector3(0f, 0f, 0f), uniC, true);
        MakeBlock("Belt", root.transform, new Vector3(0.52f, 0.07f, 0.06f), new Vector3(0f, 0.12f, -0.16f), new Color(0.1f, 0.1f, 0.1f), true);
        MakeBlock("BeltBuckle", root.transform, new Vector3(0.1f, 0.06f, 0.05f), new Vector3(0f, 0.12f, -0.19f), goldC, true);
        MakeBlock("Badge", root.transform, new Vector3(0.12f, 0.1f, 0.04f), new Vector3(0f, 0.35f, -0.16f), goldC, true);
        MakeBlock("Collar", root.transform, new Vector3(0.2f, 0.08f, 0.06f), new Vector3(0f, 0.34f, -0.15f), uniC, true);
        MakeBlock("CollarTabL", root.transform, new Vector3(0.09f, 0.1f, 0.05f), new Vector3(-0.07f, 0.3f, -0.16f), redC, true);
        MakeBlock("CollarTabR", root.transform, new Vector3(0.09f, 0.1f, 0.05f), new Vector3(0.07f, 0.3f, -0.16f), redC, true);
        MakeBlock("CollarStarL", root.transform, new Vector3(0.04f, 0.04f, 0.04f), new Vector3(-0.07f, 0.31f, -0.19f), goldC, true);
        MakeBlock("CollarStarR", root.transform, new Vector3(0.04f, 0.04f, 0.04f), new Vector3(0.07f, 0.31f, -0.19f), goldC, true);

        MakeBlock("Neck", root.transform, new Vector3(0.14f, 0.12f, 0.14f), new Vector3(0f, 0.36f, 0f), skinC, true);
        MakeBlock("Head", root.transform, new Vector3(0.32f, 0.3f, 0.32f), new Vector3(0f, 0.52f, 0f), skinC, true);
        MakeBlock("EyeWhiteL", root.transform, new Vector3(0.08f, 0.06f, 0.03f), new Vector3(-0.09f, 0.55f, -0.16f), new Color(0.95f, 0.95f, 0.97f), true);
        MakeBlock("EyeWhiteR", root.transform, new Vector3(0.08f, 0.06f, 0.03f), new Vector3(0.09f, 0.55f, -0.16f), new Color(0.95f, 0.95f, 0.97f), true);
        MakeBlock("Nose", root.transform, new Vector3(0.07f, 0.06f, 0.05f), new Vector3(0f, 0.51f, -0.17f), skinC, true);

        MakeBlock("HatCrown", root.transform, new Vector3(0.34f, 0.12f, 0.34f), new Vector3(0f, 0.68f, 0f), hatC, true);
        MakeBlock("HatBrim", root.transform, new Vector3(0.44f, 0.05f, 0.28f), new Vector3(0f, 0.64f, 0f), visorC, true);
        MakeBlock("HatBand", root.transform, new Vector3(0.36f, 0.04f, 0.36f), new Vector3(0f, 0.645f, 0f), redC, true);
        MakeBlock("HatBadgeBack", root.transform, new Vector3(0.1f, 0.04f, 0.04f), new Vector3(0f, 0.67f, -0.18f), redC, true);
        MakeBlock("HatBadge", root.transform, new Vector3(0.08f, 0.05f, 0.04f), new Vector3(0f, 0.665f, -0.2f), goldC, true);

        // ── Shoulder pivots ──
        var shoulderL = new GameObject("ShoulderL");
        shoulderL.transform.SetParent(root.transform);
        shoulderL.transform.localPosition = new Vector3(-0.34f, 0.325f, 0f);
        shoulderL.transform.localRotation = Quaternion.identity;

        var shoulderR = new GameObject("ShoulderR");
        shoulderR.transform.SetParent(root.transform);
        shoulderR.transform.localPosition = new Vector3(0.34f, 0.325f, 0f);
        shoulderR.transform.localRotation = Quaternion.identity;

        MakeBlock("ArmL", shoulderL.transform, new Vector3(0.14f, 0.45f, 0.14f), new Vector3(0f, -0.225f, 0f), uniC, true);
        MakeBlock("HandL", shoulderL.transform, new Vector3(0.12f, 0.1f, 0.12f), new Vector3(0f, -0.465f, 0f), skinC, true);
        MakeBlock("ArmR", shoulderR.transform, new Vector3(0.14f, 0.45f, 0.14f), new Vector3(0f, -0.225f, 0f), uniC, true);
        MakeBlock("HandR", shoulderR.transform, new Vector3(0.12f, 0.1f, 0.12f), new Vector3(0f, -0.465f, 0f), skinC, true);

        // ── Hip pivots ──
        var hipL = new GameObject("HipL");
        hipL.transform.SetParent(root.transform);
        hipL.transform.localPosition = new Vector3(-0.15f, -0.275f, 0f);
        hipL.transform.localRotation = Quaternion.identity;

        var hipR = new GameObject("HipR");
        hipR.transform.SetParent(root.transform);
        hipR.transform.localPosition = new Vector3(0.15f, -0.275f, 0f);
        hipR.transform.localRotation = Quaternion.identity;

        MakeBlock("LegL", hipL.transform, new Vector3(0.17f, 0.65f, 0.17f), new Vector3(0f, -0.325f, 0f), pantsC, true);
        MakeBlock("BootL", hipL.transform, new Vector3(0.2f, 0.09f, 0.3f), new Vector3(0f, -0.605f, 0f), bootC, true);
        MakeBlock("LegR", hipR.transform, new Vector3(0.17f, 0.65f, 0.17f), new Vector3(0f, -0.325f, 0f), pantsC, true);
        MakeBlock("BootR", hipR.transform, new Vector3(0.2f, 0.09f, 0.3f), new Vector3(0f, -0.605f, 0f), bootC, true);

        var col = root.AddComponent<BoxCollider>();
        col.size = new Vector3(0.9f, 1.7f, 0.7f);
        col.center = new Vector3(0f, 0.45f, 0f);
        col.isTrigger = true;

        return root;
    }
}

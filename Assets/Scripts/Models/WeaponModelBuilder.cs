using UnityEngine;

/// <summary>
/// Procedural cube-based models for all 15 weapons. Each builder returns the
/// root Transform so the caller can parent it. All colliders are destroyed.
/// </summary>
public static class WeaponModelBuilder
{
    private static readonly Color SteelSilver = new Color(0.72f, 0.72f, 0.75f);
    private static readonly Color DarkSteel = new Color(0.35f, 0.35f, 0.40f);
    private static readonly Color Bronze = new Color(0.72f, 0.55f, 0.25f);
    private static readonly Color DarkGold = new Color(0.55f, 0.45f, 0.15f);
    private static readonly Color WoodBrown = new Color(0.45f, 0.30f, 0.15f);
    private static readonly Color DarkWood = new Color(0.30f, 0.20f, 0.10f);
    private static readonly Color LeatherBrown = new Color(0.35f, 0.22f, 0.12f);
    private static readonly Color Ivory = new Color(0.90f, 0.88f, 0.80f);
    private static readonly Color Cream = new Color(0.95f, 0.92f, 0.80f);
    private static readonly Color Gold = new Color(0.85f, 0.75f, 0.20f);
    private static readonly Color BlueCrystal = new Color(0.30f, 0.50f, 0.90f);
    private static readonly Color Cyan = new Color(0.20f, 0.80f, 0.85f);
    private static readonly Color DarkPurple = new Color(0.35f, 0.15f, 0.45f);
    private static readonly Color PaleGold = new Color(0.90f, 0.85f, 0.50f);
    private static readonly Color WarmBrown = new Color(0.55f, 0.35f, 0.18f);

    private static GameObject MakeBlock(string name, Transform parent, Vector3 scale, Vector3 position, Color color)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localScale = scale;
        go.transform.localPosition = position;
        var r = go.GetComponent<Renderer>();
        if (r != null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            r.sharedMaterial = new Material(shader) { color = color };
        }
        var col = go.GetComponent<Collider>();
        if (col != null) Object.Destroy(col);
        return go;
    }

    // ──────────────────────────────────────────────────────────
    //  1. IRON SWORD
    // ──────────────────────────────────────────────────────────
    public static Transform BuildIronSword(Transform parent)
    {
        var root = new GameObject("IronSword").transform;
        root.SetParent(parent, false);
        MakeBlock("Blade", root, new Vector3(0.06f, 0.70f, 0.04f), new Vector3(0f, 0.55f, 0f), SteelSilver);
        MakeBlock("Edge", root, new Vector3(0.02f, 0.65f, 0.02f), new Vector3(0.025f, 0.55f, 0f), DarkSteel);
        MakeBlock("Crossguard", root, new Vector3(0.20f, 0.05f, 0.06f), new Vector3(0f, 0.18f, 0f), Bronze);
        MakeBlock("Grip", root, new Vector3(0.05f, 0.15f, 0.05f), new Vector3(0f, 0.08f, 0f), LeatherBrown);
        MakeBlock("Pommel", root, new Vector3(0.07f, 0.04f, 0.07f), new Vector3(0f, 0.02f, 0f), Gold);
        return root;
    }

    // ──────────────────────────────────────────────────────────
    //  2. GREATSWORD
    // ──────────────────────────────────────────────────────────
    public static Transform BuildGreatsword(Transform parent)
    {
        var root = new GameObject("Greatsword").transform;
        root.SetParent(parent, false);
        MakeBlock("Blade", root, new Vector3(0.10f, 0.90f, 0.04f), new Vector3(0f, 0.65f, 0f), SteelSilver);
        MakeBlock("Spine", root, new Vector3(0.03f, 0.80f, 0.05f), new Vector3(0f, 0.65f, 0f), DarkSteel);
        MakeBlock("Tip", root, new Vector3(0.06f, 0.12f, 0.04f), new Vector3(0f, 1.12f, 0f), SteelSilver);
        MakeBlock("Crossguard", root, new Vector3(0.28f, 0.06f, 0.07f), new Vector3(0f, 0.18f, 0f), Bronze);
        MakeBlock("Grip", root, new Vector3(0.06f, 0.20f, 0.06f), new Vector3(0f, 0.08f, 0f), LeatherBrown);
        MakeBlock("Pommel", root, new Vector3(0.09f, 0.05f, 0.09f), new Vector3(0f, 0.02f, 0f), Gold);
        return root;
    }

    // ──────────────────────────────────────────────────────────
    //  3. DAGGER
    // ──────────────────────────────────────────────────────────
    public static Transform BuildDagger(Transform parent)
    {
        var root = new GameObject("Dagger").transform;
        root.SetParent(parent, false);
        MakeBlock("Blade", root, new Vector3(0.04f, 0.35f, 0.03f), new Vector3(0f, 0.30f, 0f), SteelSilver);
        MakeBlock("Guard", root, new Vector3(0.12f, 0.03f, 0.04f), new Vector3(0f, 0.12f, 0f), Bronze);
        MakeBlock("Grip", root, new Vector3(0.04f, 0.12f, 0.04f), new Vector3(0f, 0.06f, 0f), LeatherBrown);
        MakeBlock("Pommel", root, new Vector3(0.05f, 0.03f, 0.05f), new Vector3(0f, 0.01f, 0f), Gold);
        return root;
    }

    // ──────────────────────────────────────────────────────────
    //  4. KATANA
    // ──────────────────────────────────────────────────────────
    public static Transform BuildKatana(Transform parent)
    {
        var root = new GameObject("Katana").transform;
        root.SetParent(parent, false);
        MakeBlock("Blade", root, new Vector3(0.04f, 0.85f, 0.03f), new Vector3(0.01f, 0.60f, 0f), SteelSilver);
        MakeBlock("Edge", root, new Vector3(0.015f, 0.75f, 0.015f), new Vector3(0.025f, 0.60f, 0f), DarkSteel);
        MakeBlock("Tip", root, new Vector3(0.03f, 0.10f, 0.03f), new Vector3(0.02f, 1.05f, 0f), SteelSilver);
        MakeBlock("Tsuba", root, new Vector3(0.16f, 0.03f, 0.06f), new Vector3(0f, 0.16f, 0f), DarkGold);
        MakeBlock("Handle", root, new Vector3(0.045f, 0.22f, 0.045f), new Vector3(0f, 0.06f, 0f), LeatherBrown);
        MakeBlock("Kashira", root, new Vector3(0.05f, 0.03f, 0.05f), new Vector3(0f, 0.01f, 0f), DarkGold);
        return root;
    }

    // ──────────────────────────────────────────────────────────
    //  5. GREATAXE
    // ──────────────────────────────────────────────────────────
    public static Transform BuildGreataxe(Transform parent)
    {
        var root = new GameObject("Greataxe").transform;
        root.SetParent(parent, false);
        MakeBlock("Shaft", root, new Vector3(0.06f, 0.90f, 0.06f), new Vector3(0f, 0.50f, 0f), DarkWood);
        MakeBlock("AxeHead", root, new Vector3(0.30f, 0.20f, 0.06f), new Vector3(0.10f, 0.88f, 0f), SteelSilver);
        MakeBlock("AxeEdge", root, new Vector3(0.04f, 0.18f, 0.04f), new Vector3(0.22f, 0.88f, 0f), DarkSteel);
        MakeBlock("BackSpike", root, new Vector3(0.06f, 0.15f, 0.06f), new Vector3(-0.10f, 0.88f, 0f), DarkSteel);
        MakeBlock("GripWrap", root, new Vector3(0.07f, 0.15f, 0.07f), new Vector3(0f, 0.12f, 0f), LeatherBrown);
        MakeBlock("Pommel", root, new Vector3(0.08f, 0.04f, 0.08f), new Vector3(0f, 0.02f, 0f), Bronze);
        return root;
    }

    // ──────────────────────────────────────────────────────────
    //  6. KNIGHT'S LANCE
    // ──────────────────────────────────────────────────────────
    public static Transform BuildLance(Transform parent)
    {
        var root = new GameObject("Lance").transform;
        root.SetParent(parent, false);
        MakeBlock("Shaft", root, new Vector3(0.05f, 1.20f, 0.05f), new Vector3(0f, 0.65f, 0f), DarkWood);
        MakeBlock("Tip", root, new Vector3(0.04f, 0.15f, 0.04f), new Vector3(0f, 1.28f, 0f), SteelSilver);
        MakeBlock("TipEdge", root, new Vector3(0.025f, 0.10f, 0.025f), new Vector3(0f, 1.38f, 0f), DarkSteel);
        MakeBlock("GuardL", root, new Vector3(0.14f, 0.04f, 0.04f), new Vector3(-0.07f, 0.20f, 0f), Bronze);
        MakeBlock("GuardR", root, new Vector3(0.14f, 0.04f, 0.04f), new Vector3(0.07f, 0.20f, 0f), Bronze);
        MakeBlock("Grip", root, new Vector3(0.06f, 0.18f, 0.06f), new Vector3(0f, 0.10f, 0f), LeatherBrown);
        return root;
    }

    // ──────────────────────────────────────────────────────────
    //  7. GAUNTLETS
    // ──────────────────────────────────────────────────────────
    public static Transform BuildGauntlets(Transform parent)
    {
        var root = new GameObject("Gauntlets").transform;
        root.SetParent(parent, false);
        MakeBlock("HandPlate", root, new Vector3(0.12f, 0.08f, 0.10f), new Vector3(0f, 0.06f, 0f), SteelSilver);
        MakeBlock("KnuckleRidge", root, new Vector3(0.10f, 0.04f, 0.04f), new Vector3(0f, 0.10f, 0.03f), DarkSteel);
        MakeBlock("FingerGuard1", root, new Vector3(0.025f, 0.06f, 0.025f), new Vector3(-0.03f, 0.13f, 0.02f), SteelSilver);
        MakeBlock("FingerGuard2", root, new Vector3(0.025f, 0.06f, 0.025f), new Vector3(0.0f, 0.14f, 0.02f), SteelSilver);
        MakeBlock("FingerGuard3", root, new Vector3(0.025f, 0.06f, 0.025f), new Vector3(0.03f, 0.13f, 0.02f), SteelSilver);
        MakeBlock("WristCuff", root, new Vector3(0.13f, 0.06f, 0.11f), new Vector3(0f, 0.02f, 0f), LeatherBrown);
        MakeBlock("WristBand", root, new Vector3(0.14f, 0.02f, 0.12f), new Vector3(0f, 0.045f, 0f), Bronze);
        return root;
    }

    // ──────────────────────────────────────────────────────────
    //  8. WARHAMMER
    // ──────────────────────────────────────────────────────────
    public static Transform BuildWarhammer(Transform parent)
    {
        var root = new GameObject("Warhammer").transform;
        root.SetParent(parent, false);
        MakeBlock("Shaft", root, new Vector3(0.06f, 0.80f, 0.06f), new Vector3(0f, 0.45f, 0f), DarkWood);
        MakeBlock("Head", root, new Vector3(0.25f, 0.15f, 0.15f), new Vector3(0f, 0.85f, 0f), SteelSilver);
        MakeBlock("HeadEdge", root, new Vector3(0.22f, 0.12f, 0.10f), new Vector3(0f, 0.85f, 0f), DarkSteel);
        MakeBlock("BackSpike", root, new Vector3(0.05f, 0.12f, 0.05f), new Vector3(-0.12f, 0.85f, 0f), DarkSteel);
        MakeBlock("HolySymbol", root, new Vector3(0.04f, 0.04f, 0.02f), new Vector3(0f, 0.85f, 0.08f), PaleGold);
        MakeBlock("GripWrap", root, new Vector3(0.07f, 0.15f, 0.07f), new Vector3(0f, 0.12f, 0f), LeatherBrown);
        MakeBlock("Pommel", root, new Vector3(0.08f, 0.04f, 0.08f), new Vector3(0f, 0.02f, 0f), Bronze);
        return root;
    }

    // ──────────────────────────────────────────────────────────
    //  9. LONGBOW
    // ──────────────────────────────────────────────────────────
    public static Transform BuildLongbow(Transform parent)
    {
        var root = new GameObject("Longbow").transform;
        root.SetParent(parent, false);
        MakeBlock("UpperLimb", root, new Vector3(0.04f, 0.50f, 0.04f), new Vector3(0.01f, 0.55f, 0f), WoodBrown);
        MakeBlock("LowerLimb", root, new Vector3(0.04f, 0.50f, 0.04f), new Vector3(-0.01f, -0.05f, 0f), WoodBrown);
        MakeBlock("UpperTip", root, new Vector3(0.03f, 0.06f, 0.03f), new Vector3(0.02f, 0.82f, 0f), DarkWood);
        MakeBlock("LowerTip", root, new Vector3(0.03f, 0.06f, 0.03f), new Vector3(-0.02f, -0.32f, 0f), DarkWood);
        MakeBlock("Grip", root, new Vector3(0.05f, 0.12f, 0.05f), new Vector3(0f, 0.25f, 0f), DarkWood);
        MakeBlock("String", root, new Vector3(0.008f, 0.90f, 0.008f), new Vector3(0f, 0.25f, -0.03f), PaleGold);
        MakeBlock("ArrowRest", root, new Vector3(0.03f, 0.02f, 0.03f), new Vector3(0.02f, 0.25f, 0.01f), Bronze);
        return root;
    }

    // ──────────────────────────────────────────────────────────
    //  10. THROWING HAMMER
    // ──────────────────────────────────────────────────────────
    public static Transform BuildThrowingHammer(Transform parent)
    {
        var root = new GameObject("ThrowingHammer").transform;
        root.SetParent(parent, false);
        MakeBlock("Handle", root, new Vector3(0.04f, 0.25f, 0.04f), new Vector3(0f, 0.18f, 0f), WoodBrown);
        MakeBlock("Head", root, new Vector3(0.12f, 0.10f, 0.10f), new Vector3(0f, 0.32f, 0f), SteelSilver);
        MakeBlock("HeadRim", root, new Vector3(0.10f, 0.08f, 0.08f), new Vector3(0f, 0.32f, 0f), DarkSteel);
        MakeBlock("Spike", root, new Vector3(0.03f, 0.08f, 0.03f), new Vector3(0f, 0.04f, 0f), DarkSteel);
        MakeBlock("Wrap", root, new Vector3(0.05f, 0.06f, 0.05f), new Vector3(0f, 0.18f, 0f), LeatherBrown);
        return root;
    }

    // ──────────────────────────────────────────────────────────
    //  11. MAGE'S STAFF
    // ──────────────────────────────────────────────────────────
    public static Transform BuildStaff(Transform parent)
    {
        var root = new GameObject("Staff").transform;
        root.SetParent(parent, false);
        MakeBlock("LowerShaft", root, new Vector3(0.04f, 0.50f, 0.04f), new Vector3(0f, 0.30f, 0f), DarkWood);
        MakeBlock("UpperShaft", root, new Vector3(0.035f, 0.50f, 0.035f), new Vector3(0f, 0.80f, 0f), DarkWood);
        MakeBlock("WrapLow", root, new Vector3(0.05f, 0.02f, 0.05f), new Vector3(0f, 0.40f, 0f), Gold);
        MakeBlock("WrapHigh", root, new Vector3(0.045f, 0.02f, 0.045f), new Vector3(0f, 0.70f, 0f), Gold);
        MakeBlock("OrbBase", root, new Vector3(0.06f, 0.03f, 0.06f), new Vector3(0f, 1.05f, 0f), Gold);
        MakeBlock("OrbCore", root, new Vector3(0.08f, 0.08f, 0.08f), new Vector3(0f, 1.12f, 0f), BlueCrystal);
        MakeBlock("OrbGlow1", root, new Vector3(0.04f, 0.04f, 0.04f), new Vector3(0.04f, 1.15f, 0f), PaleGold);
        MakeBlock("OrbGlow2", root, new Vector3(0.03f, 0.03f, 0.03f), new Vector3(-0.03f, 1.10f, 0.02f), PaleGold);
        MakeBlock("Pommel", root, new Vector3(0.05f, 0.03f, 0.05f), new Vector3(0f, 0.02f, 0f), Bronze);
        return root;
    }

    // ──────────────────────────────────────────────────────────
    //  12. HOLY BOOK
    // ──────────────────────────────────────────────────────────
    public static Transform BuildHolyBook(Transform parent)
    {
        var root = new GameObject("HolyBook").transform;
        root.SetParent(parent, false);
        MakeBlock("Cover", root, new Vector3(0.14f, 0.18f, 0.04f), new Vector3(0f, 0.15f, 0f), Cream);
        MakeBlock("Spine", root, new Vector3(0.02f, 0.18f, 0.04f), new Vector3(-0.07f, 0.15f, 0f), LeatherBrown);
        MakeBlock("ClaspTop", root, new Vector3(0.02f, 0.02f, 0.05f), new Vector3(0.06f, 0.23f, 0f), Gold);
        MakeBlock("ClaspBot", root, new Vector3(0.02f, 0.02f, 0.05f), new Vector3(0.06f, 0.07f, 0f), Gold);
        MakeBlock("Cross", root, new Vector3(0.02f, 0.10f, 0.01f), new Vector3(0f, 0.15f, 0.025f), Gold);
        MakeBlock("CrossBar", root, new Vector3(0.08f, 0.02f, 0.01f), new Vector3(0f, 0.18f, 0.025f), Gold);
        MakeBlock("GlowWisp1", root, new Vector3(0.025f, 0.025f, 0.025f), new Vector3(0.05f, 0.22f, 0.03f), PaleGold);
        MakeBlock("GlowWisp2", root, new Vector3(0.02f, 0.02f, 0.02f), new Vector3(-0.04f, 0.10f, 0.03f), PaleGold);
        return root;
    }

    // ──────────────────────────────────────────────────────────
    //  13. BONE WAND
    // ──────────────────────────────────────────────────────────
    public static Transform BuildBoneWand(Transform parent)
    {
        var root = new GameObject("BoneWand").transform;
        root.SetParent(parent, false);
        MakeBlock("LowerShaft", root, new Vector3(0.035f, 0.35f, 0.035f), new Vector3(0f, 0.25f, 0f), Ivory);
        MakeBlock("UpperShaft", root, new Vector3(0.03f, 0.30f, 0.03f), new Vector3(0f, 0.60f, 0f), Ivory);
        MakeBlock("JointLow", root, new Vector3(0.045f, 0.03f, 0.045f), new Vector3(0f, 0.42f, 0f), Ivory);
        MakeBlock("JointHigh", root, new Vector3(0.04f, 0.025f, 0.04f), new Vector3(0f, 0.72f, 0f), Ivory);
        MakeBlock("Skull", root, new Vector3(0.06f, 0.06f, 0.05f), new Vector3(0f, 0.85f, 0f), Ivory);
        MakeBlock("SkullJaw", root, new Vector3(0.04f, 0.02f, 0.04f), new Vector3(0f, 0.81f, 0.01f), Ivory);
        MakeBlock("EyeSocket1", root, new Vector3(0.015f, 0.015f, 0.01f), new Vector3(-0.015f, 0.86f, 0.025f), DarkPurple);
        MakeBlock("EyeSocket2", root, new Vector3(0.015f, 0.015f, 0.01f), new Vector3(0.015f, 0.86f, 0.025f), DarkPurple);
        MakeBlock("RuneBand", root, new Vector3(0.045f, 0.03f, 0.045f), new Vector3(0f, 0.45f, 0f), DarkPurple);
        MakeBlock("Base", root, new Vector3(0.04f, 0.03f, 0.04f), new Vector3(0f, 0.02f, 0f), DarkSteel);
        return root;
    }

    // ──────────────────────────────────────────────────────────
    //  14. CONTROL ORB
    // ──────────────────────────────────────────────────────────
    public static Transform BuildControlOrb(Transform parent)
    {
        var root = new GameObject("ControlOrb").transform;
        root.SetParent(parent, false);
        MakeBlock("RingBase", root, new Vector3(0.18f, 0.02f, 0.18f), new Vector3(0f, 0.15f, 0f), Gold);
        MakeBlock("ProngL", root, new Vector3(0.025f, 0.12f, 0.025f), new Vector3(-0.06f, 0.22f, 0f), Bronze);
        MakeBlock("ProngR", root, new Vector3(0.025f, 0.12f, 0.025f), new Vector3(0.06f, 0.22f, 0f), Bronze);
        MakeBlock("ProngF", root, new Vector3(0.025f, 0.10f, 0.025f), new Vector3(0f, 0.21f, 0.06f), Bronze);
        MakeBlock("Cradle", root, new Vector3(0.10f, 0.03f, 0.10f), new Vector3(0f, 0.28f, 0f), Gold);
        MakeBlock("OrbFront", root, new Vector3(0.09f, 0.09f, 0.09f), new Vector3(0f, 0.35f, 0f), Cyan);
        MakeBlock("OrbCore", root, new Vector3(0.06f, 0.06f, 0.06f), new Vector3(0f, 0.35f, 0f), BlueCrystal);
        MakeBlock("WindWisp1", root, new Vector3(0.03f, 0.015f, 0.03f), new Vector3(0.05f, 0.38f, 0.02f), PaleGold);
        MakeBlock("WindWisp2", root, new Vector3(0.025f, 0.012f, 0.025f), new Vector3(-0.04f, 0.33f, -0.02f), PaleGold);
        MakeBlock("WindWisp3", root, new Vector3(0.02f, 0.01f, 0.02f), new Vector3(0.01f, 0.40f, -0.03f), PaleGold);
        return root;
    }

    // ──────────────────────────────────────────────────────────
    //  15. BARD'S LUTE
    // ──────────────────────────────────────────────────────────
    public static Transform BuildLute(Transform parent)
    {
        var root = new GameObject("Lute").transform;
        root.SetParent(parent, false);
        MakeBlock("BodyLower", root, new Vector3(0.15f, 0.12f, 0.08f), new Vector3(0f, 0.08f, 0f), WarmBrown);
        MakeBlock("BodyUpper", root, new Vector3(0.11f, 0.09f, 0.07f), new Vector3(0f, 0.18f, 0f), WarmBrown);
        MakeBlock("SoundHole", root, new Vector3(0.05f, 0.05f, 0.01f), new Vector3(0f, 0.08f, 0.045f), DarkWood);
        MakeBlock("Neck", root, new Vector3(0.03f, 0.25f, 0.03f), new Vector3(0f, 0.35f, 0f), DarkWood);
        MakeBlock("Fretboard", root, new Vector3(0.025f, 0.22f, 0.01f), new Vector3(0f, 0.35f, 0.02f), LeatherBrown);
        MakeBlock("Headstock", root, new Vector3(0.04f, 0.06f, 0.03f), new Vector3(0f, 0.48f, 0f), DarkWood);
        MakeBlock("Peg1", root, new Vector3(0.015f, 0.015f, 0.015f), new Vector3(-0.025f, 0.50f, 0f), Bronze);
        MakeBlock("Peg2", root, new Vector3(0.015f, 0.015f, 0.015f), new Vector3(0.025f, 0.50f, 0f), Bronze);
        MakeBlock("String1", root, new Vector3(0.005f, 0.35f, 0.005f), new Vector3(-0.008f, 0.28f, 0.025f), Gold);
        MakeBlock("String2", root, new Vector3(0.005f, 0.35f, 0.005f), new Vector3(0.008f, 0.28f, 0.025f), Gold);
        MakeBlock("Bridge", root, new Vector3(0.06f, 0.015f, 0.02f), new Vector3(0f, 0.08f, 0.03f), DarkWood);
        return root;
    }

    // ──────────────────────────────────────────────────────────
    //  DISPATCHER
    // ──────────────────────────────────────────────────────────
    public static Transform Build(string weaponId, Transform parent)
    {
        if (parent == null || string.IsNullOrEmpty(weaponId))
            return null;

        switch (weaponId)
        {
            case "iron_sword":       return BuildIronSword(parent);
            case "greatsword":       return BuildGreatsword(parent);
            case "dagger":           return BuildDagger(parent);
            case "katana":           return BuildKatana(parent);
            case "greataxe":         return BuildGreataxe(parent);
            case "lance":            return BuildLance(parent);
            case "gauntlets":        return BuildGauntlets(parent);
            case "warhammer":        return BuildWarhammer(parent);
            case "longbow":          return BuildLongbow(parent);
            case "throwing_hammer":  return BuildThrowingHammer(parent);
            case "staff":            return BuildStaff(parent);
            case "holy_book":        return BuildHolyBook(parent);
            case "bone_wand":        return BuildBoneWand(parent);
            case "control_orb":      return BuildControlOrb(parent);
            case "lute":             return BuildLute(parent);
            default:                 return null;
        }
    }
}

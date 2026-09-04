using UnityEngine;

public static partial class MapBuilder
{
    // ==================== MapBuilderPlayerModel.cs ====================
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

        bool female = ActiveGender == PlayerGender.Female;

        Color skinC = new Color(220f / 255f, 178f / 255f, 132f / 255f);
        Color shirtC = new Color(0.2f, 0.6f, 0.9f);
        Color pantsC = new Color(0.25f, 0.25f, 0.35f);
        Color hairC = new Color(0.2f, 0.12f, 0.05f);
        Color eyeC = new Color(0.05f, 0.03f, 0.01f);
        Color shoeC = new Color(0.2f, 0.2f, 0.2f);
        Color dressC = new Color(0.14f, 0.44f, 0.72f);

        MakeBlock("Body", root.transform, new Vector3(female ? 0.46f : 0.5f, 0.6f, 0.25f), new Vector3(0f, 0.05f, 0f), shirtC, true);
        if (female)
        {
            MakeBlock("Skirt", root.transform, new Vector3(0.52f, 0.28f, 0.3f), new Vector3(0f, -0.27f, 0f), dressC, true);
            MakeBlock("SkirtHem", root.transform, new Vector3(0.56f, 0.06f, 0.34f), new Vector3(0f, -0.42f, 0f), new Color(0.09f, 0.3f, 0.52f), true);
        }
        MakeBlock("Head", root.transform, new Vector3(0.3f, 0.3f, 0.3f), new Vector3(0f, 0.65f, 0f), skinC, true);
        MakeBlock("Neck", root.transform, new Vector3(0.12f, 0.1f, 0.12f), new Vector3(0f, 0.4f, 0f), skinC, true);

        // ── Shoulder pivots (rotate from shoulder joint) ──
        var shoulderL = new GameObject("ShoulderL");
        shoulderL.transform.SetParent(root.transform);
        shoulderL.transform.localPosition = new Vector3(-0.33f, 0.37f, 0f);
        shoulderL.transform.localRotation = Quaternion.identity;

        var shoulderR = new GameObject("ShoulderR");
        shoulderR.transform.SetParent(root.transform);
        shoulderR.transform.localPosition = new Vector3(0.33f, 0.37f, 0f);
        shoulderR.transform.localRotation = Quaternion.identity;

        MakeBlock("ArmL", shoulderL.transform, new Vector3(0.12f, 0.5f, 0.12f), new Vector3(0f, -0.25f, 0f), shirtC, true);
        MakeBlock("HandL", shoulderL.transform, new Vector3(0.12f, 0.08f, 0.12f), new Vector3(0f, -0.51f, 0f), skinC, true);
        MakeBlock("ArmR", shoulderR.transform, new Vector3(0.12f, 0.5f, 0.12f), new Vector3(0f, -0.25f, 0f), shirtC, true);
        MakeBlock("HandR", shoulderR.transform, new Vector3(0.12f, 0.08f, 0.12f), new Vector3(0f, -0.51f, 0f), skinC, true);

        // ── Hip pivots (rotate from hip joint) ──
        var hipL = new GameObject("HipL");
        hipL.transform.SetParent(root.transform);
        hipL.transform.localPosition = new Vector3(-0.13f, -0.25f, 0f);
        hipL.transform.localRotation = Quaternion.identity;

        var hipR = new GameObject("HipR");
        hipR.transform.SetParent(root.transform);
        hipR.transform.localPosition = new Vector3(0.13f, -0.25f, 0f);
        hipR.transform.localRotation = Quaternion.identity;

        MakeBlock("LegL", hipL.transform, new Vector3(0.14f, 0.5f, 0.14f), new Vector3(0f, -0.25f, 0f), pantsC, true);
        MakeBlock("ShoeL", hipL.transform, new Vector3(0.16f, 0.08f, 0.22f), new Vector3(0f, -0.57f, 0f), shoeC, true);
        MakeBlock("LegR", hipR.transform, new Vector3(0.14f, 0.5f, 0.14f), new Vector3(0f, -0.25f, 0f), pantsC, true);
        MakeBlock("ShoeR", hipR.transform, new Vector3(0.16f, 0.08f, 0.22f), new Vector3(0f, -0.57f, 0f), shoeC, true);

        MakeBlock("Hair", root.transform, new Vector3(0.32f, 0.08f, 0.3f), new Vector3(0f, 0.82f, 0f), hairC, true);
        MakeBlock("HairL", root.transform, new Vector3(0.08f, 0.32f, 0.26f), new Vector3(-0.19f, 0.69f, 0f), hairC, true);
        MakeBlock("HairR", root.transform, new Vector3(0.08f, 0.32f, 0.26f), new Vector3(0.19f, 0.69f, 0f), hairC, true);
        if (female)
        {
            MakeBlock("HairBack", root.transform, new Vector3(0.3f, 0.3f, 0.1f), new Vector3(0f, 0.7f, -0.16f), hairC, true);
            MakeBlock("HairBand", root.transform, new Vector3(0.34f, 0.05f, 0.32f), new Vector3(0f, 0.8f, 0f), new Color(0.1f, 0.34f, 0.56f), true);
            MakeBlock("Ponytail1", root.transform, new Vector3(0.18f, 0.24f, 0.14f), new Vector3(0f, 0.7f, -0.23f), hairC, true);
            MakeBlock("Ponytail2", root.transform, new Vector3(0.15f, 0.22f, 0.13f), new Vector3(0f, 0.5f, -0.27f), hairC, true);
            MakeBlock("Ponytail3", root.transform, new Vector3(0.12f, 0.2f, 0.12f), new Vector3(0f, 0.3f, -0.29f), hairC, true);
        }
        else
        {
            MakeBlock("HairBack", root.transform, new Vector3(0.3f, 0.26f, 0.1f), new Vector3(0f, 0.72f, -0.16f), hairC, true);
        }
        Color eyeWhiteC = new Color(0.95f, 0.95f, 0.97f);
        MakeBlock("EyeWhiteL", root.transform, new Vector3(0.09f, 0.07f, 0.03f), new Vector3(-0.08f, 0.72f, 0.155f), eyeWhiteC, true);
        MakeBlock("EyeWhiteR", root.transform, new Vector3(0.09f, 0.07f, 0.03f), new Vector3(0.08f, 0.72f, 0.155f), eyeWhiteC, true);
        MakeBlock("EyeIrisL", root.transform, new Vector3(0.055f, 0.055f, 0.04f), new Vector3(-0.08f, 0.72f, 0.165f), eyeC, true);
        MakeBlock("EyeIrisR", root.transform, new Vector3(0.055f, 0.055f, 0.04f), new Vector3(0.08f, 0.72f, 0.165f), eyeC, true);

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

        bool female = ActiveGender == PlayerGender.Female;

        Color skinC = new Color(220f / 255f, 178f / 255f, 132f / 255f);
        Color shirtC = new Color(0.2f, 0.6f, 0.9f);
        Color pantsC = new Color(0.25f, 0.25f, 0.35f);
        Color hairC = female ? new Color(0.16f, 0.1f, 0.08f) : new Color(0.2f, 0.12f, 0.05f);
        Color eyeC = new Color(0.05f, 0.03f, 0.01f);
        Color dressC = new Color(0.14f, 0.44f, 0.72f);

        // ── Torso (seated, upright) ──
        MakeBlock("Body", root.transform, new Vector3(0.38f, 0.5f, 0.28f), new Vector3(0f, 0.25f, 0f), shirtC, true);
        if (female)
        {
            MakeBlock("Skirt", root.transform, new Vector3(0.42f, 0.2f, 0.32f), new Vector3(0f, -0.02f, 0f), dressC, true);
            MakeBlock("SkirtHem", root.transform, new Vector3(0.46f, 0.05f, 0.36f), new Vector3(0f, -0.13f, 0f), new Color(0.09f, 0.3f, 0.52f), true);
        }
        // ── Head ──
        MakeBlock("Head", root.transform, new Vector3(0.28f, 0.28f, 0.28f), new Vector3(0f, 0.74f, 0f), skinC, true);
        MakeBlock("Neck", root.transform, new Vector3(0.1f, 0.08f, 0.1f), new Vector3(0f, 0.55f, 0f), skinC, true);
        // ── Hair ──
        MakeBlock("Hair", root.transform, new Vector3(0.3f, 0.07f, 0.28f), new Vector3(0f, 0.9f, 0f), hairC, true);
        MakeBlock("HairL", root.transform, new Vector3(0.08f, 0.3f, 0.12f), new Vector3(-0.18f, 0.72f, 0f), hairC, true);
        MakeBlock("HairR", root.transform, new Vector3(0.08f, 0.3f, 0.12f), new Vector3(0.18f, 0.72f, 0f), hairC, true);
        if (female)
        {
            MakeBlock("HairBack", root.transform, new Vector3(0.28f, 0.26f, 0.08f), new Vector3(0f, 0.8f, -0.14f), hairC, true);
            MakeBlock("HairBand", root.transform, new Vector3(0.3f, 0.05f, 0.3f), new Vector3(0f, 0.9f, 0f), new Color(0.1f, 0.34f, 0.56f), true);
            MakeBlock("Ponytail1", root.transform, new Vector3(0.16f, 0.22f, 0.12f), new Vector3(0f, 0.76f, -0.19f), hairC, true);
            MakeBlock("Ponytail2", root.transform, new Vector3(0.13f, 0.2f, 0.11f), new Vector3(0f, 0.58f, -0.23f), hairC, true);
            MakeBlock("Ponytail3", root.transform, new Vector3(0.11f, 0.18f, 0.1f), new Vector3(0f, 0.4f, -0.25f), hairC, true);
        }
        // ── Eyes ──
        Color eyeWhiteC = new Color(0.95f, 0.95f, 0.97f);
        MakeBlock("EyeWhiteL", root.transform, new Vector3(0.08f, 0.06f, 0.03f), new Vector3(-0.07f, 0.77f, 0.145f), eyeWhiteC, true);
        MakeBlock("EyeWhiteR", root.transform, new Vector3(0.08f, 0.06f, 0.03f), new Vector3(0.07f, 0.77f, 0.145f), eyeWhiteC, true);
        MakeBlock("EyeIrisL", root.transform, new Vector3(0.05f, 0.05f, 0.04f), new Vector3(-0.07f, 0.77f, 0.155f), eyeC, true);
        MakeBlock("EyeIrisR", root.transform, new Vector3(0.05f, 0.05f, 0.04f), new Vector3(0.07f, 0.77f, 0.155f), eyeC, true);
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
    //  SITTING PLAYER MODEL  (neutral pose — hips on the seat, arms on lap)
    // ═══════════════════════════════════════════════════════════════

    public static GameObject BuildSitPlayerModel(Transform parent, float scale = 1f)
    {
        var root = new GameObject("SitPlayerModel");
        root.transform.SetParent(parent);
        root.transform.localPosition = Vector3.zero;
        root.transform.localRotation = Quaternion.identity;
        root.transform.localScale = Vector3.one * scale;

        bool female = ActiveGender == PlayerGender.Female;

        Color skinC = new Color(220f / 255f, 178f / 255f, 132f / 255f);
        Color shirtC = new Color(0.2f, 0.6f, 0.9f);
        Color pantsC = new Color(0.25f, 0.25f, 0.35f);
        Color hairC = new Color(0.2f, 0.12f, 0.05f);
        Color eyeC = new Color(0.05f, 0.03f, 0.01f);
        Color shoeC = new Color(0.2f, 0.2f, 0.2f);
        Color dressC = new Color(0.14f, 0.44f, 0.72f);

        // ── Legs (thighs forward, shins down, feet on floor) ──
        MakeBlock("ThighL", root.transform, new Vector3(0.14f, 0.26f, 0.14f), new Vector3(-0.13f, -0.03f, 0.16f), pantsC, true, Quaternion.Euler(-75f, 0f, 0f));
        MakeBlock("ThighR", root.transform, new Vector3(0.14f, 0.26f, 0.14f), new Vector3(0.13f, -0.03f, 0.16f), pantsC, true, Quaternion.Euler(-75f, 0f, 0f));
        MakeBlock("ShinL", root.transform, new Vector3(0.12f, 0.3f, 0.12f), new Vector3(-0.13f, -0.3f, 0.24f), pantsC, true);
        MakeBlock("ShinR", root.transform, new Vector3(0.12f, 0.3f, 0.12f), new Vector3(0.13f, -0.3f, 0.24f), pantsC, true);
        MakeBlock("ShoeL", root.transform, new Vector3(0.16f, 0.08f, 0.24f), new Vector3(-0.13f, -0.47f, 0.26f), shoeC, true);
        MakeBlock("ShoeR", root.transform, new Vector3(0.16f, 0.08f, 0.24f), new Vector3(0.13f, -0.47f, 0.26f), shoeC, true);

        // ── Torso (hips on the seat) ──
        MakeBlock("Torso", root.transform, new Vector3(female ? 0.42f : 0.46f, 0.36f, 0.28f), new Vector3(0f, 0.18f, 0f), shirtC, true);
        if (female)
        {
            MakeBlock("Skirt", root.transform, new Vector3(0.48f, 0.2f, 0.32f), new Vector3(0f, 0.02f, 0f), dressC, true);
            MakeBlock("SkirtHem", root.transform, new Vector3(0.52f, 0.05f, 0.35f), new Vector3(0f, -0.08f, 0f), new Color(0.09f, 0.3f, 0.52f), true);
        }
        MakeBlock("Chest", root.transform, new Vector3(0.44f, 0.28f, 0.26f), new Vector3(0f, 0.42f, 0f), shirtC, true);

        // ── Neck + head ──
        MakeBlock("Neck", root.transform, new Vector3(0.12f, 0.1f, 0.12f), new Vector3(0f, 0.62f, 0f), skinC, true);
        MakeBlock("Head", root.transform, new Vector3(0.3f, 0.3f, 0.3f), new Vector3(0f, 0.78f, 0f), skinC, true);
        MakeBlock("EyeWhiteL", root.transform, new Vector3(0.09f, 0.07f, 0.03f), new Vector3(-0.08f, 0.85f, 0.155f), new Color(0.95f, 0.95f, 0.97f), true);
        MakeBlock("EyeWhiteR", root.transform, new Vector3(0.09f, 0.07f, 0.03f), new Vector3(0.08f, 0.85f, 0.155f), new Color(0.95f, 0.95f, 0.97f), true);
        MakeBlock("EyeIrisL", root.transform, new Vector3(0.055f, 0.055f, 0.04f), new Vector3(-0.08f, 0.85f, 0.165f), eyeC, true);
        MakeBlock("EyeIrisR", root.transform, new Vector3(0.055f, 0.055f, 0.04f), new Vector3(0.08f, 0.85f, 0.165f), eyeC, true);
        MakeBlock("Hair", root.transform, new Vector3(0.32f, 0.08f, 0.3f), new Vector3(0f, 0.95f, 0f), hairC, true);
        MakeBlock("HairL", root.transform, new Vector3(0.08f, 0.32f, 0.26f), new Vector3(-0.19f, 0.82f, 0f), hairC, true);
        MakeBlock("HairR", root.transform, new Vector3(0.08f, 0.32f, 0.26f), new Vector3(0.19f, 0.82f, 0f), hairC, true);
        if (female)
        {
            MakeBlock("HairBack", root.transform, new Vector3(0.3f, 0.3f, 0.1f), new Vector3(0f, 0.83f, -0.16f), hairC, true);
            MakeBlock("HairBand", root.transform, new Vector3(0.34f, 0.05f, 0.32f), new Vector3(0f, 0.93f, 0f), new Color(0.1f, 0.34f, 0.56f), true);
        }
        else
        {
            MakeBlock("HairBack", root.transform, new Vector3(0.3f, 0.26f, 0.1f), new Vector3(0f, 0.85f, -0.16f), hairC, true);
        }

        // ── Arms resting on the lap ──
        MakeBlock("ArmL", root.transform, new Vector3(0.12f, 0.34f, 0.12f), new Vector3(-0.27f, 0.3f, 0.06f), shirtC, true, Quaternion.Euler(-60f, 0f, 0f));
        MakeBlock("ArmR", root.transform, new Vector3(0.12f, 0.34f, 0.12f), new Vector3(0.27f, 0.3f, 0.06f), shirtC, true, Quaternion.Euler(-60f, 0f, 0f));
        MakeBlock("HandL", root.transform, new Vector3(0.11f, 0.09f, 0.11f), new Vector3(-0.27f, 0.14f, 0.2f), skinC, true);
        MakeBlock("HandR", root.transform, new Vector3(0.11f, 0.09f, 0.11f), new Vector3(0.27f, 0.14f, 0.2f), skinC, true);

        return root;
    }
    // ==================== MapBuilderShop.cs ====================
}

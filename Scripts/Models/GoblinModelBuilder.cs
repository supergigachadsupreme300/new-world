using UnityEngine;

public static class GoblinModelBuilder
{
    private static GameObject MakeBlock(string name, Transform parent, Vector3 scale, Vector3 position, Color color)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localScale = scale;
        go.transform.localPosition = position;
        var r = go.GetComponent<Renderer>();
        if (r != null) r.material.color = color;
        Object.Destroy(go.GetComponent<Collider>());
        return go;
    }

    public static Transform BuildGoblin(Transform parent)
    {
        Color skinGreen = new Color(0.27f, 0.62f, 0.3f);
        Color skinDark = new Color(0.16f, 0.45f, 0.2f);
        Color ragBrown = new Color(0.42f, 0.3f, 0.15f);
        Color ragTan = new Color(0.58f, 0.43f, 0.22f);
        Color eyeYellow = new Color(0.95f, 0.85f, 0.2f);
        Color toothWhite = new Color(0.93f, 0.93f, 0.92f);
        Color shoeDark = new Color(0.12f, 0.08f, 0.04f);

        var root = new GameObject("GoblinModel");
        root.transform.SetParent(parent, false);
        root.transform.localPosition = Vector3.zero;

        // ═══════════════════════════════════════════
        //  TORSO (3 segments, hunched forward)
        // ═══════════════════════════════════════════

        // Upper torso — tilted forward 12°  |  width 0.32 → edges ±0.16
        var upperTorso = new GameObject("UpperTorso");
        upperTorso.transform.SetParent(root.transform, false);
        upperTorso.transform.localPosition = new Vector3(0f, 0.6f, 0f);
        upperTorso.transform.localRotation = Quaternion.Euler(12f, 0f, 0f);

        MakeBlock("BodyUpper", upperTorso.transform, new Vector3(0.32f, 0.2f, 0.2f), new Vector3(0f, 0.08f, 0f), skinGreen);
        MakeBlock("RagChest", upperTorso.transform, new Vector3(0.3f, 0.14f, 0.02f), new Vector3(0f, 0f, 0.1f), ragBrown);
        MakeBlock("RagChestTornL", upperTorso.transform, new Vector3(0.08f, 0.06f, 0.02f), new Vector3(-0.14f, -0.08f, 0.1f), ragBrown);
        MakeBlock("RagChestTornR", upperTorso.transform, new Vector3(0.08f, 0.06f, 0.02f), new Vector3(0.14f, -0.08f, 0.1f), ragBrown);

        // Shoulder rags (torn, hanging off the sides)
        MakeBlock("RagShoulderL", upperTorso.transform, new Vector3(0.1f, 0.12f, 0.06f), new Vector3(-0.19f, -0.05f, 0.02f), ragBrown);
        MakeBlock("RagShoulderR", upperTorso.transform, new Vector3(0.1f, 0.12f, 0.06f), new Vector3(0.19f, -0.05f, 0.02f), ragBrown);

        // Mid torso — tilted forward 8°
        var midTorso = new GameObject("MidTorso");
        midTorso.transform.SetParent(root.transform, false);
        midTorso.transform.localPosition = new Vector3(0f, 0.5f, -0.01f);
        midTorso.transform.localRotation = Quaternion.Euler(8f, 0f, 0f);

        MakeBlock("BodyMid", midTorso.transform, new Vector3(0.34f, 0.22f, 0.21f), new Vector3(0f, 0f, 0f), skinGreen);
        MakeBlock("RagBelt", midTorso.transform, new Vector3(0.34f, 0.08f, 0.21f), new Vector3(0f, -0.07f, 0f), ragBrown);
        MakeBlock("RagBeltTornL", midTorso.transform, new Vector3(0.1f, 0.06f, 0.05f), new Vector3(-0.17f, -0.12f, 0f), ragTan);
        MakeBlock("RagBeltTornR", midTorso.transform, new Vector3(0.1f, 0.06f, 0.05f), new Vector3(0.17f, -0.12f, 0f), ragTan);

        // Lower torso — tilted forward 5°
        var lowerTorso = new GameObject("LowerTorso");
        lowerTorso.transform.SetParent(root.transform, false);
        lowerTorso.transform.localPosition = new Vector3(0f, 0.4f, -0.015f);
        lowerTorso.transform.localRotation = Quaternion.Euler(5f, 0f, 0f);

        MakeBlock("BodyLower", lowerTorso.transform, new Vector3(0.34f, 0.18f, 0.21f), new Vector3(0f, -0.02f, 0f), skinGreen);

        // Loincloth skirt (tattered)
        MakeBlock("SkirtFront", lowerTorso.transform, new Vector3(0.3f, 0.16f, 0.06f), new Vector3(0f, -0.1f, 0.1f), ragBrown);
        MakeBlock("SkirtBack", lowerTorso.transform, new Vector3(0.26f, 0.12f, 0.05f), new Vector3(0f, -0.12f, -0.1f), ragBrown);
        MakeBlock("SkirtSideL", lowerTorso.transform, new Vector3(0.06f, 0.18f, 0.12f), new Vector3(-0.17f, -0.1f, 0f), ragBrown);
        MakeBlock("SkirtSideR", lowerTorso.transform, new Vector3(0.06f, 0.18f, 0.12f), new Vector3(0.17f, -0.1f, 0f), ragBrown);
        MakeBlock("SkirtTornL", lowerTorso.transform, new Vector3(0.05f, 0.05f, 0.05f), new Vector3(-0.1f, -0.2f, 0.09f), ragTan);
        MakeBlock("SkirtTornR", lowerTorso.transform, new Vector3(0.05f, 0.05f, 0.05f), new Vector3(0.1f, -0.2f, 0.09f), ragTan);

        // ═══════════════════════════════════════════
        //  NECK + HEAD (big goblin head)
        // ═══════════════════════════════════════════
        MakeBlock("Neck", upperTorso.transform, new Vector3(0.1f, 0.05f, 0.1f), new Vector3(0f, 0.26f, 0f), skinGreen);

        var head = new GameObject("Head");
        head.transform.SetParent(upperTorso.transform, false);
        head.transform.localPosition = new Vector3(0f, 0.38f, 0f);

        MakeBlock("HeadBlock", head.transform, new Vector3(0.26f, 0.24f, 0.24f), Vector3.zero, skinGreen);

        // ── Pointy ears (thin, angled up-outward) ──
        var earLPivot = new GameObject("EarLPivot");
        earLPivot.transform.SetParent(head.transform, false);
        earLPivot.transform.localPosition = new Vector3(-0.135f, 0.04f, 0f);
        earLPivot.transform.localRotation = Quaternion.Euler(10f, 0f, 28f);
        MakeBlock("EarL", earLPivot.transform, new Vector3(0.05f, 0.2f, 0.05f), new Vector3(0f, 0.1f, 0f), skinGreen);
        MakeBlock("EarLInner", earLPivot.transform, new Vector3(0.025f, 0.12f, 0.05f), new Vector3(0.008f, 0.08f, 0f), skinDark);

        var earRPivot = new GameObject("EarRPivot");
        earRPivot.transform.SetParent(head.transform, false);
        earRPivot.transform.localPosition = new Vector3(0.135f, 0.04f, 0f);
        earRPivot.transform.localRotation = Quaternion.Euler(10f, 0f, -28f);
        MakeBlock("EarR", earRPivot.transform, new Vector3(0.05f, 0.2f, 0.05f), new Vector3(0f, 0.1f, 0f), skinGreen);
        MakeBlock("EarRInner", earRPivot.transform, new Vector3(0.025f, 0.12f, 0.05f), new Vector3(-0.008f, 0.08f, 0f), skinDark);

        // ── Face: yellow eyes, big nose, buck teeth ──
        MakeBlock("EyeL", head.transform, new Vector3(0.06f, 0.05f, 0.02f), new Vector3(-0.06f, 0.04f, 0.12f), eyeYellow);
        MakeBlock("EyeR", head.transform, new Vector3(0.06f, 0.05f, 0.02f), new Vector3(0.06f, 0.04f, 0.12f), eyeYellow);
        MakeBlock("PupilL", head.transform, new Vector3(0.02f, 0.02f, 0.02f), new Vector3(-0.06f, 0.04f, 0.125f), skinDark);
        MakeBlock("PupilR", head.transform, new Vector3(0.02f, 0.02f, 0.02f), new Vector3(0.06f, 0.04f, 0.125f), skinDark);

        MakeBlock("Nose", head.transform, new Vector3(0.1f, 0.07f, 0.08f), new Vector3(0f, -0.02f, 0.13f), skinDark);
        MakeBlock("NoseBridge", head.transform, new Vector3(0.04f, 0.04f, 0.04f), new Vector3(0f, 0.05f, 0.13f), skinDark);

        MakeBlock("Mouth", head.transform, new Vector3(0.2f, 0.025f, 0.02f), new Vector3(0f, -0.09f, 0.125f), skinDark);
        MakeBlock("ToothL", head.transform, new Vector3(0.035f, 0.055f, 0.02f), new Vector3(-0.035f, -0.1f, 0.13f), toothWhite);
        MakeBlock("ToothR", head.transform, new Vector3(0.035f, 0.055f, 0.02f), new Vector3(0.035f, -0.1f, 0.13f), toothWhite);

        // ── Brow ridges (grumpy goblin) ──
        MakeBlock("BrowL", head.transform, new Vector3(0.07f, 0.025f, 0.03f), new Vector3(-0.06f, 0.08f, 0.12f), skinDark);
        MakeBlock("BrowR", head.transform, new Vector3(0.07f, 0.025f, 0.03f), new Vector3(0.06f, 0.08f, 0.12f), skinDark);

        // ═══════════════════════════════════════════
        //  LEFT ARM (stubby, big hands, green skin)
        // ═══════════════════════════════════════════
        var armLPivot = new GameObject("ArmL");
        armLPivot.transform.SetParent(upperTorso.transform, false);
        armLPivot.transform.localPosition = new Vector3(-0.19f, 0.14f, 0f);

        MakeBlock("ArmUpperL", armLPivot.transform, new Vector3(0.08f, 0.18f, 0.08f), new Vector3(0f, -0.08f, 0f), skinGreen);
        MakeBlock("ArmLowerL", armLPivot.transform, new Vector3(0.07f, 0.16f, 0.07f), new Vector3(0f, -0.24f, 0f), skinGreen);
        MakeBlock("HandL", armLPivot.transform, new Vector3(0.09f, 0.08f, 0.09f), new Vector3(0f, -0.36f, 0f), skinDark);

        // ═══════════════════════════════════════════
        //  RIGHT ARM
        // ═══════════════════════════════════════════
        var armRPivot = new GameObject("ArmR");
        armRPivot.transform.SetParent(upperTorso.transform, false);
        armRPivot.transform.localPosition = new Vector3(0.19f, 0.14f, 0f);

        MakeBlock("ArmUpperR", armRPivot.transform, new Vector3(0.08f, 0.18f, 0.08f), new Vector3(0f, -0.08f, 0f), skinGreen);
        MakeBlock("ArmLowerR", armRPivot.transform, new Vector3(0.07f, 0.16f, 0.07f), new Vector3(0f, -0.24f, 0f), skinGreen);
        MakeBlock("HandR", armRPivot.transform, new Vector3(0.09f, 0.08f, 0.09f), new Vector3(0f, -0.36f, 0f), skinDark);

        // ═══════════════════════════════════════════
        //  LEFT LEG (short, green, bare foot in shoe)
        // ═══════════════════════════════════════════
        var legLPivot = new GameObject("LegL");
        legLPivot.transform.SetParent(root.transform, false);
        legLPivot.transform.localPosition = new Vector3(-0.09f, 0.4f, 0f);

        MakeBlock("UpperLegL", legLPivot.transform, new Vector3(0.1f, 0.2f, 0.1f), new Vector3(0f, -0.09f, 0f), skinGreen);

        var kneeLPivot = new GameObject("KneeL");
        kneeLPivot.transform.SetParent(legLPivot.transform, false);
        kneeLPivot.transform.localPosition = new Vector3(0f, -0.2f, 0f);

        MakeBlock("LowerLegL", kneeLPivot.transform, new Vector3(0.09f, 0.16f, 0.09f), new Vector3(0f, -0.08f, 0f), skinGreen);
        MakeBlock("FootL", kneeLPivot.transform, new Vector3(0.11f, 0.06f, 0.16f), new Vector3(0f, -0.18f, 0.02f), shoeDark);

        // ═══════════════════════════════════════════
        //  RIGHT LEG
        // ═══════════════════════════════════════════
        var legRPivot = new GameObject("LegR");
        legRPivot.transform.SetParent(root.transform, false);
        legRPivot.transform.localPosition = new Vector3(0.09f, 0.4f, 0f);

        MakeBlock("UpperLegR", legRPivot.transform, new Vector3(0.1f, 0.2f, 0.1f), new Vector3(0f, -0.09f, 0f), skinGreen);

        var kneeRPivot = new GameObject("KneeR");
        kneeRPivot.transform.SetParent(legRPivot.transform, false);
        kneeRPivot.transform.localPosition = new Vector3(0f, -0.2f, 0f);

        MakeBlock("LowerLegR", kneeRPivot.transform, new Vector3(0.09f, 0.16f, 0.09f), new Vector3(0f, -0.08f, 0f), skinGreen);
        MakeBlock("FootR", kneeRPivot.transform, new Vector3(0.11f, 0.06f, 0.16f), new Vector3(0f, -0.18f, 0.02f), shoeDark);

        return root.transform;
    }
}

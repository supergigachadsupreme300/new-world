using UnityEngine;

public static class EnemyModelBuilder
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

    public static Transform BuildRegularEnemy(Transform parent)
    {
        Color coatBlack = new Color(0.08f, 0.08f, 0.1f);
        Color headBlack = new Color(0.05f, 0.05f, 0.06f);
        Color darkPants = new Color(0.1f, 0.1f, 0.12f);
        Color bootBlack = new Color(0.06f, 0.06f, 0.06f);
        Color handRed = new Color(0.7f, 0.1f, 0.1f);
        Color teethWhite = new Color(0.93f, 0.93f, 0.95f);

        var root = new GameObject("EnemyModel");
        root.transform.SetParent(parent, false);
        root.transform.localPosition = Vector3.zero;

        // ═══════════════════════════════════════════
        //  TORSO (3 segments, each tilted forward)
        // ═══════════════════════════════════════════

        // Upper torso — tilted forward 8°  |  width 0.32 → edges ±0.16
        var upperTorso = new GameObject("UpperTorso");
        upperTorso.transform.SetParent(root.transform, false);
        upperTorso.transform.localPosition = new Vector3(0f, 0.62f, 0f);
        upperTorso.transform.localRotation = Quaternion.Euler(8f, 0f, 0f);

        MakeBlock("BodyUpper", upperTorso.transform, new Vector3(0.32f, 0.2f, 0.2f), new Vector3(0f, 0.1f, 0f), coatBlack);
        MakeBlock("CoatTornUL", upperTorso.transform, new Vector3(0.08f, 0.08f, 0.06f), new Vector3(-0.16f, -0.02f, 0f), coatBlack);
        MakeBlock("CoatTornUR", upperTorso.transform, new Vector3(0.08f, 0.08f, 0.06f), new Vector3(0.16f, -0.02f, 0f), coatBlack);

        MakeBlock("CollarBase", upperTorso.transform, new Vector3(0.22f, 0.08f, 0.08f), new Vector3(0f, 0.22f, 0.02f), coatBlack);

        // Mid torso — tilted forward 5°  |  width 0.34 → edges ±0.17
        var midTorso = new GameObject("MidTorso");
        midTorso.transform.SetParent(root.transform, false);
        midTorso.transform.localPosition = new Vector3(0f, 0.52f, -0.01f);
        midTorso.transform.localRotation = Quaternion.Euler(5f, 0f, 0f);

        MakeBlock("BodyMid", midTorso.transform, new Vector3(0.34f, 0.22f, 0.21f), new Vector3(0f, 0f, 0f), coatBlack);
        MakeBlock("CoatTornML", midTorso.transform, new Vector3(0.09f, 0.09f, 0.06f), new Vector3(-0.17f, -0.14f, 0f), coatBlack);
        MakeBlock("CoatTornMR", midTorso.transform, new Vector3(0.09f, 0.09f, 0.06f), new Vector3(0.17f, -0.14f, 0f), coatBlack);
        MakeBlock("CoatTornMB", midTorso.transform, new Vector3(0.18f, 0.08f, 0.06f), new Vector3(0f, -0.14f, -0.1f), coatBlack);

        // Lower torso — tilted forward 3°  |  width 0.34 → edges ±0.17
        var lowerTorso = new GameObject("LowerTorso");
        lowerTorso.transform.SetParent(root.transform, false);
        lowerTorso.transform.localPosition = new Vector3(0f, 0.42f, -0.015f);
        lowerTorso.transform.localRotation = Quaternion.Euler(3f, 0f, 0f);

        MakeBlock("BodyLower", lowerTorso.transform, new Vector3(0.34f, 0.2f, 0.21f), new Vector3(0f, -0.02f, 0f), coatBlack);
        MakeBlock("CoatTornLL", lowerTorso.transform, new Vector3(0.1f, 0.1f, 0.06f), new Vector3(-0.17f, -0.14f, 0f), coatBlack);
        MakeBlock("CoatTornLR", lowerTorso.transform, new Vector3(0.1f, 0.1f, 0.06f), new Vector3(0.17f, -0.14f, 0f), coatBlack);
        MakeBlock("CoatTornLB", lowerTorso.transform, new Vector3(0.2f, 0.09f, 0.06f), new Vector3(0f, -0.14f, -0.1f), coatBlack);
        MakeBlock("CoatTornLF", lowerTorso.transform, new Vector3(0.07f, 0.07f, 0.05f), new Vector3(-0.12f, -0.08f, 0.1f), coatBlack);
        MakeBlock("CoatTornRF", lowerTorso.transform, new Vector3(0.07f, 0.07f, 0.05f), new Vector3(0.12f, -0.08f, 0.1f), coatBlack);

        // ═══════════════════════════════════════════
        //  NECK + HEAD
        // ═══════════════════════════════════════════
        MakeBlock("Neck", upperTorso.transform, new Vector3(0.1f, 0.06f, 0.1f), new Vector3(0f, 0.28f, 0f), headBlack);

        var head = new GameObject("Head");
        head.transform.SetParent(upperTorso.transform, false);
        head.transform.localPosition = new Vector3(0f, 0.4f, 0f);

        MakeBlock("HeadBlock", head.transform, new Vector3(0.28f, 0.26f, 0.26f), Vector3.zero, headBlack);

        // ── Square eyes ──
        MakeBlock("EyeL", head.transform, new Vector3(0.06f, 0.06f, 0.02f), new Vector3(-0.06f, 0.03f, 0.13f), teethWhite);
        MakeBlock("EyeR", head.transform, new Vector3(0.06f, 0.06f, 0.02f), new Vector3(0.06f, 0.03f, 0.13f), teethWhite);

        // ── Wide grinning smile (strong upward curve at corners) ──
        float mouthY = -0.035f;
        float mouthZ = 0.135f;
        float toothWidth = 0.02f;
        float toothGap = 0.003f;
        float totalToothW = toothWidth + toothGap;
        int teethCount = 11;
        float halfSpan = (teethCount - 1) * totalToothW * 0.5f;

        for (int i = 0; i < teethCount; i++)
        {
            float t = (i - (teethCount - 1) * 0.5f) / ((teethCount - 1) * 0.5f);
            float curve = t * t * t * t * 0.05f;
            float tx = -halfSpan + i * totalToothW;
            float ty = mouthY + curve;
            MakeBlock("Tooth" + i, head.transform, new Vector3(toothWidth, 0.02f, 0.016f), new Vector3(tx, ty, mouthZ), teethWhite);
        }

        MakeBlock("MouthBG", head.transform, new Vector3(0.26f, 0.045f, 0.008f), new Vector3(0f, mouthY + 0.005f, mouthZ - 0.01f), headBlack);
        MakeBlock("GumTop", head.transform, new Vector3(0.26f, 0.006f, 0.01f), new Vector3(0f, mouthY + 0.03f, mouthZ + 0.002f), headBlack);

        // ═══════════════════════════════════════════
        //  LEFT ARM (pivot at shoulder, very long)
        // ═══════════════════════════════════════════
        var armLPivot = new GameObject("ArmL");
        armLPivot.transform.SetParent(upperTorso.transform, false);
        armLPivot.transform.localPosition = new Vector3(-0.22f, 0.18f, 0f);

        MakeBlock("ArmUpperL", armLPivot.transform, new Vector3(0.1f, 0.36f, 0.1f), new Vector3(0f, -0.17f, 0f), coatBlack);
        MakeBlock("ArmLowerL", armLPivot.transform, new Vector3(0.09f, 0.34f, 0.09f), new Vector3(0f, -0.51f, 0f), coatBlack);
        MakeBlock("HandL", armLPivot.transform, new Vector3(0.08f, 0.1f, 0.08f), new Vector3(0f, -0.72f, 0f), handRed);

        // ═══════════════════════════════════════════
        //  RIGHT ARM (pivot at shoulder, very long)
        // ═══════════════════════════════════════════
        var armRPivot = new GameObject("ArmR");
        armRPivot.transform.SetParent(upperTorso.transform, false);
        armRPivot.transform.localPosition = new Vector3(0.22f, 0.18f, 0f);

        MakeBlock("ArmUpperR", armRPivot.transform, new Vector3(0.1f, 0.36f, 0.1f), new Vector3(0f, -0.17f, 0f), coatBlack);
        MakeBlock("ArmLowerR", armRPivot.transform, new Vector3(0.09f, 0.34f, 0.09f), new Vector3(0f, -0.51f, 0f), coatBlack);
        MakeBlock("HandR", armRPivot.transform, new Vector3(0.08f, 0.1f, 0.08f), new Vector3(0f, -0.72f, 0f), handRed);

        // ═══════════════════════════════════════════
        //  LEFT LEG (hip pivot → knee pivot → boot)
        // ═══════════════════════════════════════════
        var legLPivot = new GameObject("LegL");
        legLPivot.transform.SetParent(root.transform, false);
        legLPivot.transform.localPosition = new Vector3(-0.1f, 0.42f, 0f);

        MakeBlock("UpperLegL", legLPivot.transform, new Vector3(0.11f, 0.22f, 0.11f), new Vector3(0f, -0.1f, 0f), darkPants);

        var kneeLPivot = new GameObject("KneeL");
        kneeLPivot.transform.SetParent(legLPivot.transform, false);
        kneeLPivot.transform.localPosition = new Vector3(0f, -0.22f, 0f);

        MakeBlock("LowerLegL", kneeLPivot.transform, new Vector3(0.1f, 0.2f, 0.1f), new Vector3(0f, -0.1f, 0f), darkPants);
        MakeBlock("BootL", kneeLPivot.transform, new Vector3(0.12f, 0.07f, 0.16f), new Vector3(0f, -0.24f, 0.02f), bootBlack);

        // ═══════════════════════════════════════════
        //  RIGHT LEG (hip pivot → knee pivot → boot)
        // ═══════════════════════════════════════════
        var legRPivot = new GameObject("LegR");
        legRPivot.transform.SetParent(root.transform, false);
        legRPivot.transform.localPosition = new Vector3(0.1f, 0.42f, 0f);

        MakeBlock("UpperLegR", legRPivot.transform, new Vector3(0.11f, 0.22f, 0.11f), new Vector3(0f, -0.1f, 0f), darkPants);

        var kneeRPivot = new GameObject("KneeR");
        kneeRPivot.transform.SetParent(legRPivot.transform, false);
        kneeRPivot.transform.localPosition = new Vector3(0f, -0.22f, 0f);

        MakeBlock("LowerLegR", kneeRPivot.transform, new Vector3(0.1f, 0.2f, 0.1f), new Vector3(0f, -0.1f, 0f), darkPants);
        MakeBlock("BootR", kneeRPivot.transform, new Vector3(0.12f, 0.07f, 0.16f), new Vector3(0f, -0.24f, 0.02f), bootBlack);

        return root.transform;
    }

    public static Transform BuildGiantEnemy(Transform parent)
    {
        Color coatBlack = new Color(0.06f, 0.06f, 0.08f);
        Color headBlack = new Color(0.04f, 0.04f, 0.05f);
        Color darkPants = new Color(0.08f, 0.08f, 0.1f);
        Color bootBlack = new Color(0.05f, 0.05f, 0.05f);
        Color handRed = new Color(0.7f, 0.1f, 0.1f);
        Color teethWhite = new Color(0.93f, 0.93f, 0.95f);

        var root = new GameObject("GiantEnemyModel");
        root.transform.SetParent(parent, false);
        root.transform.localPosition = Vector3.zero;

        // ═══════════════════════════════════════════
        //  TORSO (3 segments, each tilted forward)
        // ═══════════════════════════════════════════

        // Upper torso — tilted forward 10°  |  width 0.42 → edges ±0.21
        var upperTorso = new GameObject("UpperTorso");
        upperTorso.transform.SetParent(root.transform, false);
        upperTorso.transform.localPosition = new Vector3(0f, 0.72f, -0.02f);
        upperTorso.transform.localRotation = Quaternion.Euler(10f, 0f, 0f);

        MakeBlock("BodyUpper", upperTorso.transform, new Vector3(0.42f, 0.28f, 0.26f), new Vector3(0f, 0.14f, 0f), coatBlack);
        MakeBlock("CoatTornUL", upperTorso.transform, new Vector3(0.1f, 0.1f, 0.06f), new Vector3(-0.21f, -0.02f, 0f), coatBlack);
        MakeBlock("CoatTornUR", upperTorso.transform, new Vector3(0.1f, 0.1f, 0.06f), new Vector3(0.21f, -0.02f, 0f), coatBlack);

        MakeBlock("CollarBase", upperTorso.transform, new Vector3(0.3f, 0.1f, 0.09f), new Vector3(0f, 0.3f, 0.03f), coatBlack);

        // Mid torso — tilted forward 7°  |  width 0.44 → edges ±0.22
        var midTorso = new GameObject("MidTorso");
        midTorso.transform.SetParent(root.transform, false);
        midTorso.transform.localPosition = new Vector3(0f, 0.6f, -0.03f);
        midTorso.transform.localRotation = Quaternion.Euler(7f, 0f, 0f);

        MakeBlock("BodyMid", midTorso.transform, new Vector3(0.44f, 0.26f, 0.27f), new Vector3(0f, 0f, 0f), coatBlack);
        MakeBlock("CoatTornML", midTorso.transform, new Vector3(0.11f, 0.11f, 0.06f), new Vector3(-0.22f, -0.16f, 0f), coatBlack);
        MakeBlock("CoatTornMR", midTorso.transform, new Vector3(0.11f, 0.11f, 0.06f), new Vector3(0.22f, -0.16f, 0f), coatBlack);
        MakeBlock("CoatTornMB", midTorso.transform, new Vector3(0.24f, 0.1f, 0.06f), new Vector3(0f, -0.16f, -0.14f), coatBlack);

        // Lower torso — tilted forward 4°  |  width 0.44 → edges ±0.22
        var lowerTorso = new GameObject("LowerTorso");
        lowerTorso.transform.SetParent(root.transform, false);
        lowerTorso.transform.localPosition = new Vector3(0f, 0.48f, -0.04f);
        lowerTorso.transform.localRotation = Quaternion.Euler(4f, 0f, 0f);

        MakeBlock("BodyLower", lowerTorso.transform, new Vector3(0.44f, 0.26f, 0.28f), new Vector3(0f, -0.02f, 0f), coatBlack);
        MakeBlock("CoatTornLL", lowerTorso.transform, new Vector3(0.12f, 0.12f, 0.06f), new Vector3(-0.22f, -0.18f, 0f), coatBlack);
        MakeBlock("CoatTornLR", lowerTorso.transform, new Vector3(0.12f, 0.12f, 0.06f), new Vector3(0.22f, -0.18f, 0f), coatBlack);
        MakeBlock("CoatTornLB", lowerTorso.transform, new Vector3(0.26f, 0.11f, 0.06f), new Vector3(0f, -0.18f, -0.14f), coatBlack);
        MakeBlock("CoatTornLF", lowerTorso.transform, new Vector3(0.08f, 0.08f, 0.05f), new Vector3(-0.16f, -0.1f, 0.12f), coatBlack);
        MakeBlock("CoatTornRF", lowerTorso.transform, new Vector3(0.08f, 0.08f, 0.05f), new Vector3(0.16f, -0.1f, 0.12f), coatBlack);

        // ═══════════════════════════════════════════
        //  NECK + HEAD
        // ═══════════════════════════════════════════
        MakeBlock("Neck", upperTorso.transform, new Vector3(0.12f, 0.08f, 0.12f), new Vector3(0f, 0.36f, 0f), headBlack);

        var head = new GameObject("Head");
        head.transform.SetParent(upperTorso.transform, false);
        head.transform.localPosition = new Vector3(0f, 0.52f, 0f);

        MakeBlock("HeadBlock", head.transform, new Vector3(0.36f, 0.34f, 0.32f), Vector3.zero, headBlack);

        // ── Square eyes ──
        MakeBlock("EyeL", head.transform, new Vector3(0.08f, 0.08f, 0.025f), new Vector3(-0.08f, 0.04f, 0.16f), teethWhite);
        MakeBlock("EyeR", head.transform, new Vector3(0.08f, 0.08f, 0.025f), new Vector3(0.08f, 0.04f, 0.16f), teethWhite);

        // ── Wide grinning smile (strong upward curve at corners) ──
        float mouthY = -0.045f;
        float mouthZ = 0.165f;
        float toothWidth = 0.028f;
        float toothGap = 0.004f;
        float totalToothW = toothWidth + toothGap;
        int teethCount = 13;
        float halfSpan = (teethCount - 1) * totalToothW * 0.5f;

        for (int i = 0; i < teethCount; i++)
        {
            float t = (i - (teethCount - 1) * 0.5f) / ((teethCount - 1) * 0.5f);
            float curve = t * t * t * t * 0.07f;
            float tx = -halfSpan + i * totalToothW;
            float ty = mouthY + curve;
            MakeBlock("Tooth" + i, head.transform, new Vector3(toothWidth, 0.026f, 0.018f), new Vector3(tx, ty, mouthZ), teethWhite);
        }

        MakeBlock("MouthBG", head.transform, new Vector3(0.38f, 0.055f, 0.01f), new Vector3(0f, mouthY + 0.008f, mouthZ - 0.012f), headBlack);
        MakeBlock("GumTop", head.transform, new Vector3(0.38f, 0.008f, 0.012f), new Vector3(0f, mouthY + 0.04f, mouthZ + 0.003f), headBlack);

        // ═══════════════════════════════════════════
        //  LEFT ARM (very long)
        // ═══════════════════════════════════════════
        var armLPivot = new GameObject("ArmL");
        armLPivot.transform.SetParent(upperTorso.transform, false);
        armLPivot.transform.localPosition = new Vector3(-0.28f, 0.22f, 0f);

        MakeBlock("ArmUpperL", armLPivot.transform, new Vector3(0.12f, 0.42f, 0.12f), new Vector3(0f, -0.2f, 0f), coatBlack);
        MakeBlock("ArmLowerL", armLPivot.transform, new Vector3(0.11f, 0.38f, 0.11f), new Vector3(0f, -0.58f, 0f), coatBlack);
        MakeBlock("HandL", armLPivot.transform, new Vector3(0.1f, 0.12f, 0.1f), new Vector3(0f, -0.82f, 0f), handRed);

        // ═══════════════════════════════════════════
        //  RIGHT ARM (very long)
        // ═══════════════════════════════════════════
        var armRPivot = new GameObject("ArmR");
        armRPivot.transform.SetParent(upperTorso.transform, false);
        armRPivot.transform.localPosition = new Vector3(0.28f, 0.22f, 0f);

        MakeBlock("ArmUpperR", armRPivot.transform, new Vector3(0.12f, 0.42f, 0.12f), new Vector3(0f, -0.2f, 0f), coatBlack);
        MakeBlock("ArmLowerR", armRPivot.transform, new Vector3(0.11f, 0.38f, 0.11f), new Vector3(0f, -0.58f, 0f), coatBlack);
        MakeBlock("HandR", armRPivot.transform, new Vector3(0.1f, 0.12f, 0.1f), new Vector3(0f, -0.82f, 0f), handRed);

        // ═══════════════════════════════════════════
        //  LEFT LEG
        // ═══════════════════════════════════════════
        var legLPivot = new GameObject("LegL");
        legLPivot.transform.SetParent(root.transform, false);
        legLPivot.transform.localPosition = new Vector3(-0.14f, 0.44f, 0f);

        MakeBlock("UpperLegL", legLPivot.transform, new Vector3(0.14f, 0.28f, 0.14f), new Vector3(0f, -0.13f, 0f), darkPants);

        var kneeLPivot = new GameObject("KneeL");
        kneeLPivot.transform.SetParent(legLPivot.transform, false);
        kneeLPivot.transform.localPosition = new Vector3(0f, -0.28f, 0f);

        MakeBlock("LowerLegL", kneeLPivot.transform, new Vector3(0.12f, 0.24f, 0.12f), new Vector3(0f, -0.12f, 0f), darkPants);
        MakeBlock("BootL", kneeLPivot.transform, new Vector3(0.15f, 0.08f, 0.2f), new Vector3(0f, -0.28f, 0.03f), bootBlack);

        // ═══════════════════════════════════════════
        //  RIGHT LEG
        // ═══════════════════════════════════════════
        var legRPivot = new GameObject("LegR");
        legRPivot.transform.SetParent(root.transform, false);
        legRPivot.transform.localPosition = new Vector3(0.14f, 0.44f, 0f);

        MakeBlock("UpperLegR", legRPivot.transform, new Vector3(0.14f, 0.28f, 0.14f), new Vector3(0f, -0.13f, 0f), darkPants);

        var kneeRPivot = new GameObject("KneeR");
        kneeRPivot.transform.SetParent(legRPivot.transform, false);
        kneeRPivot.transform.localPosition = new Vector3(0f, -0.28f, 0f);

        MakeBlock("LowerLegR", kneeRPivot.transform, new Vector3(0.12f, 0.24f, 0.12f), new Vector3(0f, -0.12f, 0f), darkPants);
        MakeBlock("BootR", kneeRPivot.transform, new Vector3(0.15f, 0.08f, 0.2f), new Vector3(0f, -0.28f, 0.03f), bootBlack);

        return root.transform;
    }
}

using UnityEngine;

public static class BossModelBuilder
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

    private static void Rotate(Transform block, float rx, float ry, float rz)
    {
        if (block != null)
            block.localRotation = Quaternion.Euler(rx, ry, rz);
    }

    // A plain child GameObject used to bake a static pose (bow-legs, splayed arms)
    // without touching the animated pivot's own rotation.
    private static Transform MakePoseGroup(string name, Transform parent, Vector3 position, Quaternion rotation)
    {
        var g = new GameObject(name);
        g.transform.SetParent(parent, false);
        g.transform.localPosition = position;
        g.transform.localRotation = rotation;
        return g.transform;
    }

    public static Transform BuildBoss(Transform parent)
    {
        Color bodyDark = new Color(0.14f, 0.02f, 0.03f);
        Color bodyMid = new Color(0.27f, 0.05f, 0.06f);
        Color armorBlack = new Color(0.05f, 0.03f, 0.04f);
        Color boneWhite = new Color(0.85f, 0.80f, 0.72f);
        Color hornColor = new Color(0.72f, 0.68f, 0.56f);
        Color goldColor = new Color(0.85f, 0.68f, 0.22f);
        Color eyeGlow = new Color(1f, 0.6f, 0.08f);
        Color emberCore = new Color(1f, 0.35f, 0.05f);
        Color clawBlack = new Color(0.1f, 0.08f, 0.06f);
        Color hoofBlack = new Color(0.09f, 0.07f, 0.07f);
        Color mouthBlack = new Color(0.01f, 0.01f, 0.01f);

        var root = new GameObject("BossModel");
        root.transform.SetParent(parent, false);
        root.transform.localPosition = Vector3.zero;

        // ═══════════════════════════════════════════
        //  LEFT LEG (thick, bow-legged stump)
        // ═══════════════════════════════════════════
        var legLPivot = new GameObject("LegL");
        legLPivot.transform.SetParent(root.transform, false);
        legLPivot.transform.localPosition = new Vector3(-0.30f, 0.62f, 0.02f);
        var legLGeo = MakePoseGroup("LegLGeo", legLPivot.transform, Vector3.zero, Quaternion.Euler(0f, 0f, 10f));
        MakeBlock("UpperLegL", legLGeo, new Vector3(0.28f, 0.40f, 0.28f), new Vector3(0f, -0.19f, 0f), bodyMid);
        MakeBlock("ThighPlateL", legLGeo, new Vector3(0.34f, 0.14f, 0.30f), new Vector3(0f, -0.10f, -0.05f), armorBlack);
        var thighSpikeL = MakeBlock("ThighSpikeL", legLGeo, new Vector3(0.06f, 0.16f, 0.06f), new Vector3(0.16f, -0.12f, 0f), boneWhite);
        Rotate(thighSpikeL.transform, 0f, 0f, 30f);

        var kneeLPivot = new GameObject("KneeL");
        kneeLPivot.transform.SetParent(legLPivot.transform, false);
        kneeLPivot.transform.localPosition = new Vector3(0f, -0.36f, 0f);
        MakeBlock("LowerLegL", kneeLPivot.transform, new Vector3(0.24f, 0.34f, 0.24f), new Vector3(0f, -0.17f, 0f), bodyDark);
        MakeBlock("KneePadL", kneeLPivot.transform, new Vector3(0.26f, 0.12f, 0.26f), new Vector3(0f, 0.02f, 0.02f), armorBlack);
        MakeBlock("ShinGuardL", kneeLPivot.transform, new Vector3(0.26f, 0.16f, 0.26f), new Vector3(0f, -0.18f, 0.04f), armorBlack);
        MakeBlock("HoofL", kneeLPivot.transform, new Vector3(0.34f, 0.18f, 0.48f), new Vector3(0f, -0.36f, 0.06f), hoofBlack);
        var hoofSpikeL = MakeBlock("HoofSpikeL", kneeLPivot.transform, new Vector3(0.05f, 0.12f, 0.05f), new Vector3(0.14f, -0.32f, 0.12f), boneWhite);
        Rotate(hoofSpikeL.transform, 0f, 0f, 35f);

        // ═══════════════════════════════════════════
        //  RIGHT LEG
        // ═══════════════════════════════════════════
        var legRPivot = new GameObject("LegR");
        legRPivot.transform.SetParent(root.transform, false);
        legRPivot.transform.localPosition = new Vector3(0.30f, 0.62f, 0.02f);
        var legRGeo = MakePoseGroup("LegRGeo", legRPivot.transform, Vector3.zero, Quaternion.Euler(0f, 0f, -10f));
        MakeBlock("UpperLegR", legRGeo, new Vector3(0.28f, 0.40f, 0.28f), new Vector3(0f, -0.19f, 0f), bodyMid);
        MakeBlock("ThighPlateR", legRGeo, new Vector3(0.34f, 0.14f, 0.30f), new Vector3(0f, -0.10f, -0.05f), armorBlack);
        var thighSpikeR = MakeBlock("ThighSpikeR", legRGeo, new Vector3(0.06f, 0.16f, 0.06f), new Vector3(-0.16f, -0.12f, 0f), boneWhite);
        Rotate(thighSpikeR.transform, 0f, 0f, -30f);

        var kneeRPivot = new GameObject("KneeR");
        kneeRPivot.transform.SetParent(legRPivot.transform, false);
        kneeRPivot.transform.localPosition = new Vector3(0f, -0.36f, 0f);
        MakeBlock("LowerLegR", kneeRPivot.transform, new Vector3(0.24f, 0.34f, 0.24f), new Vector3(0f, -0.17f, 0f), bodyDark);
        MakeBlock("KneePadR", kneeRPivot.transform, new Vector3(0.26f, 0.12f, 0.26f), new Vector3(0f, 0.02f, 0.02f), armorBlack);
        MakeBlock("ShinGuardR", kneeRPivot.transform, new Vector3(0.26f, 0.16f, 0.26f), new Vector3(0f, -0.18f, 0.04f), armorBlack);
        MakeBlock("HoofR", kneeRPivot.transform, new Vector3(0.34f, 0.18f, 0.48f), new Vector3(0f, -0.36f, 0.06f), hoofBlack);
        var hoofSpikeR = MakeBlock("HoofSpikeR", kneeRPivot.transform, new Vector3(0.05f, 0.12f, 0.05f), new Vector3(-0.14f, -0.32f, 0.12f), boneWhite);
        Rotate(hoofSpikeR.transform, 0f, 0f, -35f);

        // ═══════════════════════════════════════════
        //  LOWER TORSO + PELVIS + TAIL
        // ═══════════════════════════════════════════
        var lowerTorso = new GameObject("LowerTorso");
        lowerTorso.transform.SetParent(root.transform, false);
        lowerTorso.transform.localPosition = new Vector3(0f, 0.62f, -0.02f);

        MakeBlock("BodyLower", lowerTorso.transform, new Vector3(0.60f, 0.40f, 0.42f), new Vector3(0f, 0.04f, 0f), bodyDark);
        MakeBlock("PelvisBlock", lowerTorso.transform, new Vector3(0.64f, 0.26f, 0.44f), new Vector3(0f, -0.02f, 0f), bodyDark);
        MakeBlock("Loincloth", lowerTorso.transform, new Vector3(0.50f, 0.24f, 0.07f), new Vector3(0f, -0.18f, 0.20f), bodyDark);
        MakeBlock("DemonBelt", lowerTorso.transform, new Vector3(0.64f, 0.10f, 0.46f), new Vector3(0f, -0.04f, 0.02f), armorBlack);
        MakeBlock("BeltBuckle", lowerTorso.transform, new Vector3(0.10f, 0.10f, 0.05f), new Vector3(0f, -0.04f, 0.24f), goldColor);

        // Segmented tail sweeping back and down
        MakeBlock("Tail1", lowerTorso.transform, new Vector3(0.14f, 0.14f, 0.50f), new Vector3(0f, 0.06f, -0.24f), bodyMid);
        MakeBlock("Tail2", lowerTorso.transform, new Vector3(0.12f, 0.12f, 0.42f), new Vector3(0f, 0.04f, -0.54f), bodyDark);
        MakeBlock("Tail3", lowerTorso.transform, new Vector3(0.10f, 0.10f, 0.34f), new Vector3(0f, 0.02f, -0.80f), bodyDark);
        var tailSpike = MakeBlock("TailSpike", lowerTorso.transform, new Vector3(0.08f, 0.08f, 0.40f), new Vector3(0f, -0.02f, -1.06f), boneWhite);
        Rotate(tailSpike.transform, -20f, 0f, 0f);

        // ═══════════════════════════════════════════
        //  MID TORSO
        // ═══════════════════════════════════════════
        var midTorso = new GameObject("MidTorso");
        midTorso.transform.SetParent(root.transform, false);
        midTorso.transform.localPosition = new Vector3(0f, 1.02f, -0.06f);

        MakeBlock("BodyMid", midTorso.transform, new Vector3(0.68f, 0.50f, 0.46f), new Vector3(0f, 0.02f, 0f), bodyDark);
        MakeBlock("BellyPlate", midTorso.transform, new Vector3(0.50f, 0.14f, 0.09f), new Vector3(0f, -0.02f, 0.24f), armorBlack);
        MakeBlock("BellyRune", midTorso.transform, new Vector3(0.16f, 0.06f, 0.03f), new Vector3(0f, 0.02f, 0.28f), emberCore);
        MakeBlock("BoneStudL", midTorso.transform, new Vector3(0.09f, 0.09f, 0.09f), new Vector3(-0.28f, 0.08f, 0.14f), boneWhite);
        MakeBlock("BoneStudR", midTorso.transform, new Vector3(0.09f, 0.09f, 0.09f), new Vector3(0.28f, 0.08f, 0.14f), boneWhite);

        var backSpike1 = MakeBlock("BackSpike1", midTorso.transform, new Vector3(0.10f, 0.40f, 0.10f), new Vector3(-0.14f, -0.02f, -0.24f), boneWhite);
        Rotate(backSpike1.transform, 35f, 0f, 0f);
        var backSpike2 = MakeBlock("BackSpike2", midTorso.transform, new Vector3(0.10f, 0.46f, 0.10f), new Vector3(0f, 0.0f, -0.24f), boneWhite);
        Rotate(backSpike2.transform, 35f, 0f, 0f);
        var backSpike3 = MakeBlock("BackSpike3", midTorso.transform, new Vector3(0.10f, 0.40f, 0.10f), new Vector3(0.14f, -0.02f, -0.24f), boneWhite);
        Rotate(backSpike3.transform, 35f, 0f, 0f);

        var ribLowL = MakeBlock("RibLowL", midTorso.transform, new Vector3(0.34f, 0.07f, 0.06f), new Vector3(-0.14f, -0.14f, 0.18f), boneWhite);
        Rotate(ribLowL.transform, 0f, 0f, 12f);
        var ribLowR = MakeBlock("RibLowR", midTorso.transform, new Vector3(0.34f, 0.07f, 0.06f), new Vector3(0.14f, -0.14f, 0.18f), boneWhite);
        Rotate(ribLowR.transform, 0f, 0f, -12f);

        // ═══════════════════════════════════════════
        //  LOWER ARMS (second pair, hangs along the ribs)
        // ═══════════════════════════════════════════
        var armL2Pivot = new GameObject("ArmL2");
        armL2Pivot.transform.SetParent(midTorso.transform, false);
        armL2Pivot.transform.localPosition = new Vector3(-0.42f, -0.02f, -0.02f);
        var armL2Geo = MakePoseGroup("ArmL2Geo", armL2Pivot.transform, Vector3.zero, Quaternion.Euler(0f, 0f, -6f));
        MakeBlock("ArmUpper2L", armL2Geo, new Vector3(0.20f, 0.44f, 0.20f), new Vector3(0f, -0.22f, 0f), bodyMid);
        MakeBlock("ElbowPad2L", armL2Geo, new Vector3(0.24f, 0.10f, 0.24f), new Vector3(0f, -0.44f, 0.02f), armorBlack);
        MakeBlock("ArmLower2L", armL2Geo, new Vector3(0.18f, 0.44f, 0.18f), new Vector3(0f, -0.66f, 0f), bodyDark);
        MakeBlock("Hand2L", armL2Geo, new Vector3(0.22f, 0.16f, 0.28f), new Vector3(0f, -0.88f, 0f), bodyMid);
        MakeBlock("Claw2L1", armL2Geo, new Vector3(0.05f, 0.18f, 0.05f), new Vector3(-0.06f, -1.00f, 0.04f), clawBlack);
        MakeBlock("Claw2L2", armL2Geo, new Vector3(0.05f, 0.18f, 0.05f), new Vector3(0f, -1.02f, -0.04f), clawBlack);
        MakeBlock("Claw2L3", armL2Geo, new Vector3(0.05f, 0.18f, 0.05f), new Vector3(0.06f, -1.00f, 0.04f), clawBlack);

        var armR2Pivot = new GameObject("ArmR2");
        armR2Pivot.transform.SetParent(midTorso.transform, false);
        armR2Pivot.transform.localPosition = new Vector3(0.42f, -0.02f, -0.02f);
        var armR2Geo = MakePoseGroup("ArmR2Geo", armR2Pivot.transform, Vector3.zero, Quaternion.Euler(0f, 0f, 6f));
        MakeBlock("ArmUpper2R", armR2Geo, new Vector3(0.20f, 0.44f, 0.20f), new Vector3(0f, -0.22f, 0f), bodyMid);
        MakeBlock("ElbowPad2R", armR2Geo, new Vector3(0.24f, 0.10f, 0.24f), new Vector3(0f, -0.44f, 0.02f), armorBlack);
        MakeBlock("ArmLower2R", armR2Geo, new Vector3(0.18f, 0.44f, 0.18f), new Vector3(0f, -0.66f, 0f), bodyDark);
        MakeBlock("Hand2R", armR2Geo, new Vector3(0.22f, 0.16f, 0.28f), new Vector3(0f, -0.88f, 0f), bodyMid);
        MakeBlock("Claw2R1", armR2Geo, new Vector3(0.05f, 0.18f, 0.05f), new Vector3(-0.06f, -1.00f, 0.04f), clawBlack);
        MakeBlock("Claw2R2", armR2Geo, new Vector3(0.05f, 0.18f, 0.05f), new Vector3(0f, -1.02f, -0.04f), clawBlack);
        MakeBlock("Claw2R3", armR2Geo, new Vector3(0.05f, 0.18f, 0.05f), new Vector3(0.06f, -1.00f, 0.04f), clawBlack);

        // ═══════════════════════════════════════════
        //  UPPER TORSO + SHOULDERS
        // ═══════════════════════════════════════════
        var upperTorso = new GameObject("UpperTorso");
        upperTorso.transform.SetParent(root.transform, false);
        upperTorso.transform.localPosition = new Vector3(0f, 1.50f, -0.10f);

        MakeBlock("BodyUpper", upperTorso.transform, new Vector3(0.72f, 0.50f, 0.48f), new Vector3(0f, -0.01f, 0f), bodyDark);
        MakeBlock("ChestPlate", upperTorso.transform, new Vector3(0.58f, 0.20f, 0.11f), new Vector3(0f, 0.0f, 0.24f), armorBlack);
        MakeBlock("PectoralL", upperTorso.transform, new Vector3(0.28f, 0.16f, 0.13f), new Vector3(-0.18f, 0.02f, 0.22f), bodyDark);
        MakeBlock("PectoralR", upperTorso.transform, new Vector3(0.28f, 0.16f, 0.13f), new Vector3(0.18f, 0.02f, 0.22f), bodyDark);
        MakeBlock("ChestEmber", upperTorso.transform, new Vector3(0.10f, 0.16f, 0.05f), new Vector3(0f, -0.08f, 0.27f), emberCore);
        MakeBlock("ChestStud", upperTorso.transform, new Vector3(0.06f, 0.06f, 0.06f), new Vector3(0f, 0.16f, 0.26f), goldColor);
        MakeBlock("CollarBoneL", upperTorso.transform, new Vector3(0.16f, 0.06f, 0.08f), new Vector3(-0.18f, 0.22f, 0.0f), boneWhite);
        MakeBlock("CollarBoneR", upperTorso.transform, new Vector3(0.16f, 0.06f, 0.08f), new Vector3(0.18f, 0.22f, 0.0f), boneWhite);

        for (int i = 0; i < 3; i++)
        {
            var ribL = MakeBlock("RibL" + i, upperTorso.transform, new Vector3(0.34f, 0.06f, 0.05f), new Vector3(-0.15f, 0.04f - i * 0.10f, 0.18f), boneWhite);
            Rotate(ribL.transform, 0f, 0f, 10f);
            var ribR = MakeBlock("RibR" + i, upperTorso.transform, new Vector3(0.34f, 0.06f, 0.05f), new Vector3(0.15f, 0.04f - i * 0.10f, 0.18f), boneWhite);
            Rotate(ribR.transform, 0f, 0f, -10f);
        }

        var backBlade = MakeBlock("BackBlade", upperTorso.transform, new Vector3(0.07f, 0.60f, 0.20f), new Vector3(0f, -0.06f, -0.25f), boneWhite);
        Rotate(backBlade.transform, 20f, 0f, 0f);

        MakeBlock("ShoulderPadL", upperTorso.transform, new Vector3(0.40f, 0.20f, 0.40f), new Vector3(-0.40f, 0.06f, 0.0f), armorBlack);
        MakeBlock("ShoulderPadR", upperTorso.transform, new Vector3(0.40f, 0.20f, 0.40f), new Vector3(0.40f, 0.06f, 0.0f), armorBlack);
        var shoulderSpikeL = MakeBlock("ShoulderSpikeL", upperTorso.transform, new Vector3(0.10f, 0.30f, 0.10f), new Vector3(-0.40f, 0.22f, 0.0f), boneWhite);
        Rotate(shoulderSpikeL.transform, -15f, 0f, 30f);
        var shoulderSpikeR = MakeBlock("ShoulderSpikeR", upperTorso.transform, new Vector3(0.10f, 0.30f, 0.10f), new Vector3(0.40f, 0.22f, 0.0f), boneWhite);
        Rotate(shoulderSpikeR.transform, -15f, 0f, -30f);

        // ═══════════════════════════════════════════
        //  UPPER ARMS (main pair, knuckle-dragging)
        // ═══════════════════════════════════════════
        var armLPivot = new GameObject("ArmL");
        armLPivot.transform.SetParent(upperTorso.transform, false);
        armLPivot.transform.localPosition = new Vector3(-0.42f, 0.06f, -0.02f);
        var armLGeo = MakePoseGroup("ArmLGeo", armLPivot.transform, Vector3.zero, Quaternion.Euler(0f, 0f, -8f));
        MakeBlock("ArmUpperL", armLGeo, new Vector3(0.22f, 0.58f, 0.22f), new Vector3(0f, -0.24f, 0f), bodyMid);
        MakeBlock("ElbowPadL", armLGeo, new Vector3(0.26f, 0.14f, 0.26f), new Vector3(0f, -0.55f, 0.03f), armorBlack);
        var elbowSpikeL = MakeBlock("ElbowSpikeL", armLGeo, new Vector3(0.06f, 0.16f, 0.06f), new Vector3(0.08f, -0.54f, -0.06f), boneWhite);
        Rotate(elbowSpikeL.transform, -30f, 0f, 0f);
        MakeBlock("ArmLowerL", armLGeo, new Vector3(0.20f, 0.60f, 0.20f), new Vector3(0f, -0.88f, 0f), bodyDark);
        MakeBlock("VambraceL", armLGeo, new Vector3(0.24f, 0.16f, 0.24f), new Vector3(0f, -1.10f, 0.02f), armorBlack);
        MakeBlock("WristSpikeL", armLGeo, new Vector3(0.04f, 0.14f, 0.04f), new Vector3(0f, -1.18f, 0.12f), boneWhite);
        MakeBlock("HandL", armLGeo, new Vector3(0.26f, 0.20f, 0.34f), new Vector3(0f, -1.30f, 0f), bodyMid);
        MakeBlock("ClawL1", armLGeo, new Vector3(0.06f, 0.30f, 0.06f), new Vector3(-0.08f, -1.44f, 0.07f), clawBlack);
        MakeBlock("ClawL2", armLGeo, new Vector3(0.06f, 0.30f, 0.06f), new Vector3(-0.02f, -1.46f, -0.07f), clawBlack);
        MakeBlock("ClawL3", armLGeo, new Vector3(0.06f, 0.30f, 0.06f), new Vector3(0.08f, -1.44f, 0.07f), clawBlack);

        var armRPivot = new GameObject("ArmR");
        armRPivot.transform.SetParent(upperTorso.transform, false);
        armRPivot.transform.localPosition = new Vector3(0.42f, 0.06f, -0.02f);
        var armRGeo = MakePoseGroup("ArmRGeo", armRPivot.transform, Vector3.zero, Quaternion.Euler(0f, 0f, 8f));
        MakeBlock("ArmUpperR", armRGeo, new Vector3(0.22f, 0.58f, 0.22f), new Vector3(0f, -0.24f, 0f), bodyMid);
        MakeBlock("ElbowPadR", armRGeo, new Vector3(0.26f, 0.14f, 0.26f), new Vector3(0f, -0.55f, 0.03f), armorBlack);
        var elbowSpikeR = MakeBlock("ElbowSpikeR", armRGeo, new Vector3(0.06f, 0.16f, 0.06f), new Vector3(-0.08f, -0.54f, -0.06f), boneWhite);
        Rotate(elbowSpikeR.transform, -30f, 0f, 0f);
        MakeBlock("ArmLowerR", armRGeo, new Vector3(0.20f, 0.60f, 0.20f), new Vector3(0f, -0.88f, 0f), bodyDark);
        MakeBlock("VambraceR", armRGeo, new Vector3(0.24f, 0.16f, 0.24f), new Vector3(0f, -1.10f, 0.02f), armorBlack);
        MakeBlock("WristSpikeR", armRGeo, new Vector3(0.04f, 0.14f, 0.04f), new Vector3(0f, -1.18f, 0.12f), boneWhite);
        MakeBlock("HandR", armRGeo, new Vector3(0.26f, 0.20f, 0.34f), new Vector3(0f, -1.30f, 0f), bodyMid);
        MakeBlock("ClawR1", armRGeo, new Vector3(0.06f, 0.30f, 0.06f), new Vector3(-0.08f, -1.44f, 0.07f), clawBlack);
        MakeBlock("ClawR2", armRGeo, new Vector3(0.06f, 0.30f, 0.06f), new Vector3(-0.02f, -1.46f, -0.07f), clawBlack);
        MakeBlock("ClawR3", armRGeo, new Vector3(0.06f, 0.30f, 0.06f), new Vector3(0.08f, -1.44f, 0.07f), clawBlack);

        // ═══════════════════════════════════════════
        //  HEAD + FACE + HORNS (neckless, jutting forward)
        // ═══════════════════════════════════════════
        var head = new GameObject("Head");
        head.transform.SetParent(upperTorso.transform, false);
        head.transform.localPosition = new Vector3(0f, -0.02f, 0.02f);

        MakeBlock("HeadBlock", head.transform, new Vector3(0.64f, 0.52f, 0.58f), new Vector3(0f, 0.02f, 0f), bodyDark);
        MakeBlock("BrowRidge", head.transform, new Vector3(0.56f, 0.09f, 0.18f), new Vector3(0f, 0.16f, 0.26f), armorBlack);
        MakeBlock("EyeL", head.transform, new Vector3(0.13f, 0.07f, 0.04f), new Vector3(-0.17f, 0.08f, 0.29f), eyeGlow);
        MakeBlock("EyeR", head.transform, new Vector3(0.13f, 0.07f, 0.04f), new Vector3(0.17f, 0.08f, 0.29f), eyeGlow);
        MakeBlock("NoseRidge", head.transform, new Vector3(0.09f, 0.18f, 0.07f), new Vector3(0f, 0.04f, 0.31f), boneWhite);
        MakeBlock("JawBlock", head.transform, new Vector3(0.44f, 0.16f, 0.14f), new Vector3(0f, -0.22f, 0.18f), bodyDark);
        MakeBlock("MouthBG", head.transform, new Vector3(0.48f, 0.07f, 0.03f), new Vector3(0f, -0.18f, 0.31f), mouthBlack);
        MakeBlock("FangL", head.transform, new Vector3(0.05f, 0.16f, 0.04f), new Vector3(-0.14f, -0.12f, 0.30f), boneWhite);
        MakeBlock("FangR", head.transform, new Vector3(0.05f, 0.16f, 0.04f), new Vector3(0.14f, -0.12f, 0.30f), boneWhite);

        for (int i = 0; i < 9; i++)
        {
            float tx = -0.20f + i * 0.05f;
            MakeBlock("ToothU" + i, head.transform, new Vector3(0.03f, 0.04f, 0.02f), new Vector3(tx, -0.10f, 0.30f), boneWhite);
        }
        for (int i = 0; i < 7; i++)
        {
            float tx = -0.15f + i * 0.05f;
            MakeBlock("ToothL" + i, head.transform, new Vector3(0.03f, 0.03f, 0.02f), new Vector3(tx, -0.28f, 0.24f), boneWhite);
        }

        var cheekSpikeL = MakeBlock("CheekSpikeL", head.transform, new Vector3(0.07f, 0.20f, 0.07f), new Vector3(-0.30f, -0.02f, 0.14f), boneWhite);
        Rotate(cheekSpikeL.transform, 20f, 0f, 35f);
        var cheekSpikeR = MakeBlock("CheekSpikeR", head.transform, new Vector3(0.07f, 0.20f, 0.07f), new Vector3(0.30f, -0.02f, 0.14f), boneWhite);
        Rotate(cheekSpikeR.transform, 20f, 0f, -35f);
        var chinSpike = MakeBlock("ChinSpike", head.transform, new Vector3(0.06f, 0.20f, 0.06f), new Vector3(0f, -0.34f, 0.16f), hornColor);
        Rotate(chinSpike.transform, 30f, 0f, 0f);

        for (int i = 0; i < 3; i++)
        {
            float tx = -0.10f + i * 0.10f;
            var crown = MakeBlock("CrownSpike" + i, head.transform, new Vector3(0.06f, 0.26f, 0.06f), new Vector3(tx, 0.28f, -0.06f), boneWhite);
            Rotate(crown.transform, -15f, 0f, 0f);
        }

        // Forward-swept curved horns (3 segments + tip)
        MakeBlock("HornLBase", head.transform, new Vector3(0.11f, 0.36f, 0.11f), new Vector3(-0.22f, 0.16f, 0.04f), hornColor).transform.localRotation = Quaternion.Euler(40f, 0f, 20f);
        MakeBlock("HornL2", head.transform, new Vector3(0.09f, 0.32f, 0.09f), new Vector3(-0.24f, 0.40f, 0.18f), hornColor).transform.localRotation = Quaternion.Euler(30f, 0f, 32f);
        MakeBlock("HornL3", head.transform, new Vector3(0.07f, 0.28f, 0.07f), new Vector3(-0.25f, 0.62f, 0.30f), hornColor).transform.localRotation = Quaternion.Euler(18f, 0f, 44f);
        MakeBlock("HornLTip", head.transform, new Vector3(0.04f, 0.16f, 0.04f), new Vector3(-0.26f, 0.82f, 0.38f), hornColor).transform.localRotation = Quaternion.Euler(10f, 0f, 50f);
        MakeBlock("HornRBase", head.transform, new Vector3(0.11f, 0.36f, 0.11f), new Vector3(0.22f, 0.16f, 0.04f), hornColor).transform.localRotation = Quaternion.Euler(40f, 0f, -20f);
        MakeBlock("HornR2", head.transform, new Vector3(0.09f, 0.32f, 0.09f), new Vector3(0.24f, 0.40f, 0.18f), hornColor).transform.localRotation = Quaternion.Euler(30f, 0f, -32f);
        MakeBlock("HornR3", head.transform, new Vector3(0.07f, 0.28f, 0.07f), new Vector3(0.25f, 0.62f, 0.30f), hornColor).transform.localRotation = Quaternion.Euler(18f, 0f, -44f);
        MakeBlock("HornRTip", head.transform, new Vector3(0.04f, 0.16f, 0.04f), new Vector3(0.26f, 0.82f, 0.38f), hornColor).transform.localRotation = Quaternion.Euler(10f, 0f, -50f);

        return root.transform;
    }
}

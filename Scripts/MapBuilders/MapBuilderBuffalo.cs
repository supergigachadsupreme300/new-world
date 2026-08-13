using System.Collections.Generic;
using UnityEngine;

public static partial class MapBuilder
{
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

}

using System.Collections.Generic;
using UnityEngine;

public static partial class MapBuilder
{
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

using System.Collections.Generic;
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

}

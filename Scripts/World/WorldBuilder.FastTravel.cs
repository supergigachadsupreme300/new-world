using UnityEngine;

public partial class WorldBuilder
{
    private static readonly (string label, Vector3 pos)[] FastTravelDestinations =
    {
        ("Trang Trại", new Vector3(19f, 0f, 3f)),
        ("Chùa Làng", new Vector3(19f, 0f, 27f)),
        ("Khu Chợ", new Vector3(19f, 0f, 48f)),
        ("Hộp Đêm & Nhà Hàng", new Vector3(19f, 0f, 102f)),
        ("Dinh Phú Ông", new Vector3(52f, 0f, 128f)),
        ("Võ Đài Quỷ Vương", new Vector3(278f, 0f, 80f))
    };

    private static readonly Vector3 HorseSpawnPosition = new Vector3(19f, 0f, 1f);

    private bool _fastTravelSpawned;

    public void SpawnFastTravelSigns()
    {
        if (_fastTravelSpawned || _worldRoot == null)
            return;
        _fastTravelSpawned = true;
        for (int i = 0; i < FastTravelDestinations.Length; i++)
            BuildFastTravelSign(FastTravelDestinations[i].label, FastTravelDestinations[i].pos, i);
    }

    public void SpawnRidableHorse()
    {
        if (_worldRoot == null)
            return;
        HorseMount.Spawn(HorseSpawnPosition);
    }

    public void ClearFastTravelSpots()
    {
        foreach (var dest in FastTravelDestinations)
            PruneTreesAndRocksInBox(dest.pos.x, dest.pos.z, 2.2f, 2.2f);
        PruneTreesAndRocksInBox(HorseSpawnPosition.x, HorseSpawnPosition.z, 2f, 2f);
    }

    private void BuildFastTravelSign(string label, Vector3 position, int index)
    {
        var root = new GameObject("RoadSign");
        root.transform.SetParent(_worldRoot.transform);
        root.transform.position = position;
        root.transform.rotation = Quaternion.identity;

        var sign = root.AddComponent<FastTravelSign>();
        sign.Index = index;
        sign.Label = label;

        var col = root.AddComponent<BoxCollider>();
        col.isTrigger = true;
        col.center = new Vector3(0f, 0.85f, 0f);
        col.size = new Vector3(1.6f, 1.7f, 0.5f);

        Color postColor = new Color(0.55f, 0.4f, 0.22f);
        Color boardColor = new Color(0.68f, 0.52f, 0.3f);
        BuildFastTravelBlock("SignPost", root.transform,
            new Vector3(0.12f, 1.4f, 0.12f), new Vector3(0f, 0.7f, 0f), postColor);
        BuildFastTravelBlock("SignArm", root.transform,
            new Vector3(0.09f, 0.09f, 0.45f), new Vector3(0.55f, 1.55f, 0f), postColor);
        BuildFastTravelBlock("SignBoard", root.transform,
            new Vector3(1.1f, 0.45f, 0.09f), new Vector3(0f, 1.55f, 0f), boardColor);
    }

    private static GameObject BuildFastTravelBlock(string name, Transform parent,
        Vector3 scale, Vector3 position, Color color)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localScale = scale;
        go.transform.localPosition = position;
        go.GetComponent<MeshRenderer>().material.color = color;
        Object.Destroy(go.GetComponent<Collider>());
        return go;
    }
}
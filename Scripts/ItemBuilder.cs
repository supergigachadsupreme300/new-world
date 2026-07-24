using UnityEngine;
using static CountryLife.Helpers.PickupVisualHelper;

public static class ItemBuilder
{
    public static void BuildItem(Transform parent, string itemType)
    {
        if (parent == null)
        {
            Debug.LogWarning("[ItemBuilder] Parent transform is null, cannot build item.");
            return;
        }

        if (string.IsNullOrEmpty(itemType))
        {
            Debug.LogWarning("[ItemBuilder] Item type is null or empty.");
            return;
        }

        switch (itemType)
        {
            case "arm": BuildArm(parent); break;
            case "axe": BuildAxe(parent); break;
            case "pickaxe": BuildPickaxe(parent); break;
            case "hoe": BuildHoe(parent); break;
            case "hammer": BuildHammer(parent); break;
            case "sword": BuildSword(parent); break;
            case "gun": BuildGun(parent); break;
            case "scythe": BuildScythe(parent); break;
            case "ammo": BuildAmmo(parent); break;
            case "mobspawner": BuildMobSpawner(parent); break;

            case "field": BuildField(parent); break;
            case "fertilizer": BuildFertilizer(parent); break;
            case "wheat_seed": BuildSeed(parent, new Color(1f, 0.85f, 0.3f)); break;
            case "corn_seed": BuildSeed(parent, new Color(1f, 0.95f, 0.5f)); break;
            case "peashooter_seed": BuildPeashooterSeed(parent); break;
            case "potato_seed": BuildPotatoSeed(parent); break;
            case "wheat": BuildWheatPickup(parent, new Color(1f, 0.9f, 0.2f)); break;
            case "damaged_wheat": BuildDamagedCrop(parent); break;
            case "corn": BuildCornPickup(parent); break;
            case "damaged_corn": BuildDamagedCrop(parent); break;
            case "potato": BuildPotatoPickup(parent); break;
            case "damaged_potato": BuildDamagedCrop(parent); break;
            case "watering_can": BuildWateringCan(parent); break;
            case "carrot_seed": BuildSeed(parent, new Color(1f, 0.5f, 0f)); break;
            case "tomato_seed": BuildSeed(parent, new Color(1f, 0.3f, 0.1f)); break;
            case "strawberry_seed": BuildSeed(parent, new Color(1f, 0.2f, 0.2f)); break;
            case "pumpkin_seed": BuildSeed(parent, new Color(1f, 0.7f, 0.1f)); break;
            case "onion_seed": BuildSeed(parent, new Color(0.7f, 0.5f, 0.3f)); break;
            case "sugarcane_seed": BuildSeed(parent, new Color(0.4f, 0.7f, 0.2f)); break;
            case "rice_seed": BuildSeed(parent, new Color(0.9f, 0.85f, 0.4f)); break;
            case "carrot": BuildCarrotPickup(parent); break;
            case "tomato": BuildTomatoPickup(parent); break;
            case "strawberry": BuildStrawberryPickup(parent); break;
            case "pumpkin": BuildPumpkinPickup(parent); break;
            case "onion": BuildOnionPickup(parent); break;
            case "sugarcane": BuildSugarcanePickup(parent); break;
            case "rice": BuildRicePickup(parent); break;
            case "damaged_carrot":
            case "damaged_tomato":
            case "damaged_strawberry":
            case "damaged_pumpkin":
            case "damaged_onion":
            case "damaged_sugarcane":
            case "damaged_rice": BuildDamagedCrop(parent); break;
            case "mi_hao_hao": BuildMiHaoHao(parent); break;
            case "club": BuildClub(parent); break;
            case "cage_big": BuildCageBig(parent); break;
            case "cage_small": BuildCageSmall(parent); break;
        }
    }

    public static void BuildArm(Transform parent)
    {
        CreatePickupCube(parent, new Vector3(0f, 0f, 0f), new Vector3(0.3f, 1f, 0.3f), new Color(0.6f, 0.3f, 0.1f));
    }

    public static void BuildAxe(Transform parent)
    {
        CreatePickupCube(parent, new Vector3(0f, 0f, 0f), new Vector3(0.15f, 0.8f, 0.15f), new Color(0.5f, 0.2f, 0.05f));
        CreatePickupCube(parent, new Vector3(0f, 0.5f, 0.25f), new Vector3(0.2f, 0.3f, 0.7f), new Color(0.6f, 0.6f, 0.6f));
        CreatePickupCube(parent, new Vector3(0f, 0.5f, 0.5f), new Vector3(0.2f, 0.5f, 0.2f), new Color(0.6f, 0.6f, 0.6f));
    }

    public static void BuildPickaxe(Transform parent)
    {
        CreatePickupCube(parent, new Vector3(0f, 0f, 0f), new Vector3(0.15f, 0.8f, 0.15f), new Color(0.5f, 0.2f, 0.05f));
        CreatePickupCube(parent, new Vector3(0f, 0.5f, 0f), new Vector3(0.2f, 0.2f, 0.8f), new Color(0.6f, 0.6f, 0.6f));
        CreatePickupCube(parent, new Vector3(0f, 0.4f, 0.35f), new Vector3(0.25f, 0.125f, 0.25f), new Color(0.6f, 0.6f, 0.6f));
        CreatePickupCube(parent, new Vector3(0f, 0.4f, -0.35f), new Vector3(0.25f, 0.125f, 0.25f), new Color(0.6f, 0.6f, 0.6f));
    }

    public static void BuildHoe(Transform parent)
    {
        CreatePickupCube(parent, new Vector3(0f, 0f, 0f), new Vector3(0.18f, 0.8f, 0.18f), new Color(0.5f, 0.2f, 0.05f));
        CreatePickupCube(parent, new Vector3(0f, 0.4f, 0.3f), new Vector3(0.3f, 0.15f, 0.7f), new Color(0.6f, 0.6f, 0.6f));
    }

    public static void BuildHammer(Transform parent)
    {
        CreatePickupCube(parent, new Vector3(0f, 0f, 0f), new Vector3(0.12f, 0.7f, 0.12f), new Color(0.5f, 0.2f, 0.05f));
        CreatePickupCube(parent, new Vector3(0f, 0.35f, 0f), new Vector3(0.4f, 0.25f, 0.4f), new Color(0.5f, 0.5f, 0.5f));
    }

    public static void BuildSword(Transform parent)
    {
        CreatePickupCube(parent, new Vector3(0f, 0f, 0f), new Vector3(0.1f, 0.4f, 0.1f), Color.gray);
        CreatePickupCube(parent, new Vector3(0f, 0.25f, 0f), new Vector3(0.2f, 0.05f, 0.2f), new Color(1f, 0.84f, 0f));
        CreatePickupCube(parent, new Vector3(0f, 0.7f, 0f), new Vector3(0.05f, 1f, 0.3f), Color.white);
        var bladeTip = CreatePickupCube(parent, new Vector3(0f, 1.15f, 0f), new Vector3(0.05f, 0.3f, 0.3f), Color.white);
        bladeTip.transform.localRotation = Quaternion.Euler(45f, 0f, 0f);
    }

    public static void BuildGun(Transform parent)
    {
        var body = CreatePickupCube(parent, new Vector3(0f, 0f, 0f), new Vector3(0.15f, 0.5f, 0.15f), Color.black);
        body.transform.localRotation = Quaternion.Euler(45f, 0f, 0f);
        CreatePickupCube(parent, new Vector3(0f, 0.2f, 0.4f), new Vector3(0.2f, 0.2f, 1f), Color.gray);
    }

    public static void BuildScythe(Transform parent)
    {
        CreatePickupCube(parent, new Vector3(0f, 0f, 0f), new Vector3(0.15f, 0.7f, 0.15f), new Color(0.5f, 0.2f, 0.05f));
        CreatePickupCube(parent, new Vector3(0.3f, 0.35f, 0f), new Vector3(0.5f, 0.08f, 0.08f), new Color(0.6f, 0.6f, 0.6f));
    }

    public static void BuildAmmo(Transform parent)
    {
        CreatePickupCube(parent, new Vector3(0f, 0.2f, 0f), new Vector3(0.4f, 0.2f, 0.15f), new Color(0.85f, 0.85f, 0.85f));
        CreatePickupCube(parent, new Vector3(0f, 0.3f, 0f), new Vector3(0.35f, 0.1f, 0.1f), new Color(0.4f, 0.4f, 0.4f));
    }

    public static void BuildMobSpawner(Transform parent)
    {
        CreatePickupCube(parent, new Vector3(0f, 0.2f, 0f), new Vector3(0.4f, 0.4f, 0.4f), new Color(0.25f, 0.25f, 0.25f));
        CreatePickupSphere(parent, new Vector3(0f, 0.65f, 0f), 0.11f, Color.red);
        CreatePickupCube(parent, new Vector3(0f, 0.05f, 0f), new Vector3(0.15f, 0.6f, 0.15f), Color.black);
    }

    public static void BuildField(Transform parent)
    {
        CreatePickupCube(parent, new Vector3(0f, 0.1f, 0f), new Vector3(1f, 0.2f, 1f), new Color(0.45f, 0.28f, 0.12f));
    }

    public static void BuildFertilizer(Transform parent)
    {
        CreatePickupCube(parent, new Vector3(0f, 0.1f, 0f), new Vector3(0.2f, 0.2f, 0.2f), new Color(0.2f, 0.7f, 0.2f));
    }

    public static void BuildSeed(Transform parent, Color color, int count = 6)
    {
        for (int i = 0; i < count; i++)
        {
            float size = Random.Range(0.03f, 0.05f);
            float x = Random.Range(-0.08f, 0.08f);
            float z = Random.Range(-0.08f, 0.08f);
            CreatePickupCube(parent, new Vector3(x, size * 0.5f, z), new Vector3(size, size, size), color, false);
        }
    }

    public static void BuildPeashooterSeed(Transform parent)
    {
        CreatePickupCube(parent, new Vector3(0f, 0.05f, 0f), new Vector3(0.06f, 0.1f, 0.06f), new Color(0.8f, 0.7f, 0.3f), false);
    }

    public static void BuildPotatoSeed(Transform parent)
    {
        Color potatoSeedColor = new Color(0.7f, 0.5f, 0.2f);
        CreatePickupSphere(parent, new Vector3(-0.05f, 0.05f, 0f), 0.08f, potatoSeedColor, false);
        CreatePickupSphere(parent, new Vector3(0.05f, 0.05f, 0f), 0.08f, potatoSeedColor, false);
    }

    public static void BuildWheatPickup(Transform parent, Color color)
    {
        int count = Random.Range(9, 11);
        for (int i = 0; i < count; i++)
        {
            float width = 0.05f;
            float height = Random.Range(0.5f, 0.7f);
            float x = Random.Range(-0.05f, 0.05f);
            float z = Random.Range(-0.05f, 0.05f);
            CreatePickupCube(parent, new Vector3(x, height / 2f, z), new Vector3(width, height, width), color, false);
        }
        CreatePickupCube(parent, new Vector3(0f, 0.1f, 0f), new Vector3(0.2f, 0.05f, 0.2f), new Color(0.627f, 0.431f, 0.235f), false);
    }

    public static void BuildCornPickup(Transform parent)
    {
        Color cornColor = new Color(1f, 0.85f, 0.2f);
        for (int i = 0; i < 5; i++)
        {
            for (int j = 0; j < 5; j++)
            {
                float angle = j * 72f;
                CreatePickupCube(parent, new Vector3(0f, 0.5f+0.04f*i, 0f), new Vector3(0.2f, 0.04f, 0.05f), new Vector3(0f, angle + i*18f, 0f), cornColor, false);
            }
        }
        CreatePickupCube(parent, new Vector3(0f, 0.1f, 0f), new Vector3(0.06f, 0.8f, 0.06f), new Color(0.3f, 0.7f, 0.25f), false);
    }

    public static void BuildPotatoPickup(Transform parent)
    {
        CreatePickupSphere(parent, new Vector3(-0.04f, 0.06f, 0f), 0.14f, new Color(0.627f, 0.431f, 0.235f), false);
        CreatePickupSphere(parent, new Vector3(0.04f, -0.06f, 0f), 0.14f, new Color(0.3f, 0.2f, 0.1f), false);
    }

    public static void BuildDamagedCrop(Transform parent)
    {
        CreatePickupCube(parent, new Vector3(0f, 0.15f, 0f), new Vector3(0.3f, 0.25f, 0.3f), new Color(0.6f, 0.4f, 0.2f), false);
    }

    public static void BuildMiHaoHao(Transform parent)
    {
        CreatePickupCube(parent, new Vector3(0f, 0.2f, 0f), new Vector3(0.3f, 0.1f, 0.3f), Color.red);
    }

    public static void BuildWateringCan(Transform parent)
    {
        Color metalC = new Color(0.4f, 0.5f, 0.6f);
        Color darkC = new Color(0.3f, 0.35f, 0.4f);

        var body = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        body.transform.SetParent(parent, false);
        body.transform.localScale = new Vector3(0.12f, 0.22f, 0.12f);
        body.transform.localPosition = new Vector3(0f, 0.12f, 0f);
        body.GetComponent<Renderer>().material.color = metalC;
        Object.Destroy(body.GetComponent<Collider>());

        var spout = GameObject.CreatePrimitive(PrimitiveType.Cube);
        spout.transform.SetParent(parent, false);
        spout.transform.localScale = new Vector3(0.03f, 0.03f, 0.2f);
        spout.transform.localPosition = new Vector3(0f, 0.22f, 0.2f);
        spout.transform.localRotation = Quaternion.Euler(20f, 0f, 0f);
        spout.GetComponent<Renderer>().material.color = metalC;
        Object.Destroy(spout.GetComponent<Collider>());

        var rose = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        rose.transform.SetParent(parent, false);
        rose.transform.localScale = new Vector3(0.06f, 0.02f, 0.06f);
        rose.transform.localPosition = new Vector3(0f, 0.26f, 0.34f);
        rose.transform.localRotation = Quaternion.Euler(20f, 0f, 0f);
        rose.GetComponent<Renderer>().material.color = darkC;
        Object.Destroy(rose.GetComponent<Collider>());

        var handle = GameObject.CreatePrimitive(PrimitiveType.Cube);
        handle.transform.SetParent(parent, false);
        handle.transform.localScale = new Vector3(0.06f, 0.04f, 0.15f);
        handle.transform.localPosition = new Vector3(0f, 0.28f, -0.1f);
        handle.GetComponent<Renderer>().material.color = darkC;
        Object.Destroy(handle.GetComponent<Collider>());

        var handleGrip = GameObject.CreatePrimitive(PrimitiveType.Cube);
        handleGrip.transform.SetParent(handle.transform, false);
        handleGrip.transform.localScale = new Vector3(0.07f, 0.05f, 0.06f);
        handleGrip.transform.localPosition = new Vector3(0f, 0.04f, 0f);
        handleGrip.GetComponent<Renderer>().material.color = new Color(0.2f, 0.12f, 0.06f);
        Object.Destroy(handleGrip.GetComponent<Collider>());
    }

    public static void BuildCarrotPickup(Transform parent)
    {
        CreatePickupCube(parent, new Vector3(0f, 0.1f, 0f), new Vector3(0.06f, 0.15f, 0.06f), new Color(1f, 0.55f, 0.1f), false);
        CreatePickupCube(parent, new Vector3(0f, 0.2f, 0f), new Vector3(0.04f, 0.06f, 0.04f), new Color(0.2f, 0.6f, 0.15f), false);
        CreatePickupCube(parent, new Vector3(0.03f, 0.2f, 0f), new Vector3(0.02f, 0.04f, 0.02f), new Color(0.2f, 0.6f, 0.15f), false);
        CreatePickupCube(parent, new Vector3(-0.03f, 0.2f, 0f), new Vector3(0.02f, 0.04f, 0.02f), new Color(0.2f, 0.6f, 0.15f), false);
    }

    public static void BuildTomatoPickup(Transform parent)
    {
        CreatePickupSphere(parent, new Vector3(0f, 0.08f, 0f), 0.18f, new Color(1f, 0.2f, 0.1f), false);
        CreatePickupCube(parent, new Vector3(0f, 0.18f, 0f), new Vector3(0.03f, 0.04f, 0.03f), new Color(0.2f, 0.5f, 0.15f), false);
    }

    public static void BuildStrawberryPickup(Transform parent)
    {
        CreatePickupSphere(parent, new Vector3(0f, 0.05f, 0f), 0.12f, new Color(1f, 0.15f, 0.15f), false);
        CreatePickupCube(parent, new Vector3(0f, 0.1f, 0f), new Vector3(0.06f, 0.02f, 0.06f), new Color(0.15f, 0.55f, 0.1f), false);
    }

    public static void BuildPumpkinPickup(Transform parent)
    {
        CreatePickupSphere(parent, new Vector3(0f, 0.08f, 0f), 0.2f, new Color(1f, 0.6f, 0.1f), false);
        CreatePickupCube(parent, new Vector3(0f, 0.18f, 0f), new Vector3(0.04f, 0.03f, 0.04f), new Color(0.2f, 0.5f, 0.1f), false);
    }

    public static void BuildOnionPickup(Transform parent)
    {
        CreatePickupSphere(parent, new Vector3(0f, 0.06f, 0f), 0.16f, new Color(0.8f, 0.5f, 0.2f), false);
        for (int i = 0; i < 3; i++)
        {
            CreatePickupCube(parent, new Vector3(Random.Range(-0.03f, 0.03f), 0.15f, Random.Range(-0.03f, 0.03f)),
                new Vector3(0.015f, 0.04f, 0.015f), new Color(0.2f, 0.5f, 0.1f), false);
        }
    }

    public static void BuildSugarcanePickup(Transform parent)
    {
        CreatePickupCube(parent, new Vector3(0f, 0.15f, 0f), new Vector3(0.05f, 0.3f, 0.05f), new Color(0.3f, 0.7f, 0.15f), false);
        CreatePickupCube(parent, new Vector3(0f, 0.3f, 0f), new Vector3(0.06f, 0.015f, 0.06f), new Color(0.6f, 0.8f, 0.3f), false);
    }

    public static void BuildRicePickup(Transform parent)
    {
        CreatePickupCube(parent, new Vector3(0f, 0.15f, 0f), new Vector3(0.025f, 0.3f, 0.025f), new Color(0.25f, 0.6f, 0.15f), false);
        CreatePickupSphere(parent, new Vector3(0f, 0.32f, 0f), 0.08f, new Color(1f, 0.9f, 0.3f), false);
    }

    public static void BuildClub(Transform parent)
    {
        CreatePickupCube(parent, new Vector3(0f, 0f, 0f), new Vector3(0.12f, 1.05f, 0.12f), new Color(0.5f, 0.25f, 0.05f));
        CreatePickupCube(parent, new Vector3(0f, 0.66f, 0f), new Vector3(0.3f, 0.36f, 0.3f), new Color(0.4f, 0.2f, 0.05f));
    }

    public static void BuildCageBig(Transform parent)
    {
        BuildDetailedCage(parent, 0.5f, 0.4f, 0.4f);
    }

    public static void BuildCageSmall(Transform parent)
    {
        BuildDetailedCage(parent, 0.35f, 0.3f, 0.3f);
    }

    public static void BuildDetailedCage(Transform parent, float w, float h, float d)
    {
        Color frame = new Color(0.62f, 0.62f, 0.65f);
        Color mesh = new Color(0.55f, 0.55f, 0.58f);
        Color bracket = new Color(0.58f, 0.58f, 0.61f);
        Color floorMat = new Color(0.45f, 0.45f, 0.48f);
        Color springCol = new Color(0.72f, 0.62f, 0.28f);
        Color latchCol = new Color(0.52f, 0.52f, 0.55f);

        float ft = Mathf.Min(w, h, d) * 0.065f;
        float mt = Mathf.Min(w, h, d) * 0.022f;
        float bs = ft * 1.5f;

        CreatePickupCube(parent, new Vector3(0f, 0f, 0f), new Vector3(w, ft, d), floorMat);

        for (int i = 0; i < 4; i++)
        {
            float x = (i < 2 ? -1f : 1f) * (w * 0.5f - ft * 0.5f);
            float z = (i % 2 == 0 ? -1f : 1f) * (d * 0.5f - ft * 0.5f);
            CreatePickupCube(parent, new Vector3(x, h * 0.5f, z), new Vector3(ft, h, ft), frame);
        }

        for (int i = 0; i < 2; i++)
        {
            float z = (i == 0 ? -1f : 1f) * (d * 0.5f - ft * 0.5f);
            CreatePickupCube(parent, new Vector3(0f, ft * 0.5f, z), new Vector3(w - ft * 2f, ft, ft), frame);
            CreatePickupCube(parent, new Vector3(0f, h, z), new Vector3(w - ft * 2f, ft, ft), frame);
        }
        for (int i = 0; i < 2; i++)
        {
            float x = (i == 0 ? -1f : 1f) * (w * 0.5f - ft * 0.5f);
            CreatePickupCube(parent, new Vector3(x, ft * 0.5f, 0f), new Vector3(ft, ft, d - ft * 2f), frame);
            CreatePickupCube(parent, new Vector3(x, h, 0f), new Vector3(ft, ft, d - ft * 2f), frame);
        }

        for (int i = 0; i < 8; i++)
        {
            int y = i / 4;
            int ci = i % 4;
            float ry = y == 0 ? ft * 0.5f : h - ft * 0.5f;
            float bx = (ci < 2 ? -1f : 1f) * (w * 0.5f - ft * 0.5f);
            float bz = (ci % 2 == 0 ? -1f : 1f) * (d * 0.5f - ft * 0.5f);
            CreatePickupCube(parent, new Vector3(bx, ry, bz), new Vector3(bs, bs, bs), bracket);
        }

        int hBars = Mathf.Max(3, Mathf.RoundToInt(h / 0.08f));
        int vBars = Mathf.Max(3, Mathf.RoundToInt(w / 0.08f));

        for (int side = 0; side < 4; side++)
        {
            bool alongX = side < 2;
            float wallPos = side == 0 ? d * 0.5f : (side == 1 ? -d * 0.5f : 0f);
            float wallPosX = side == 2 ? -w * 0.5f : (side == 3 ? w * 0.5f : 0f);
            float wallLen = alongX ? w - ft * 2f : d - ft * 2f;
            int sideVBars = alongX ? vBars : Mathf.Max(3, Mathf.RoundToInt(d / 0.08f));

            for (int j = 0; j < hBars; j++)
            {
                float barY = ft + (h - ft) * (j + 1f) / (hBars + 1f);
                if (alongX)
                    CreatePickupCube(parent, new Vector3(0f, barY, wallPos), new Vector3(wallLen, mt, mt), mesh);
                else
                    CreatePickupCube(parent, new Vector3(wallPosX, barY, 0f), new Vector3(mt, mt, wallLen), mesh);
            }

            for (int j = 0; j < sideVBars; j++)
            {
                float barOff = wallLen * (j + 1f) / (sideVBars + 1f) - wallLen * 0.5f;
                if (alongX)
                    CreatePickupCube(parent, new Vector3(barOff, h * 0.5f, wallPos), new Vector3(mt, h - ft - ft, mt), mesh);
                else
                    CreatePickupCube(parent, new Vector3(wallPosX, h * 0.5f, barOff), new Vector3(mt, h - ft - ft, mt), mesh);
            }
        }

        int topBX = Mathf.Max(2, Mathf.RoundToInt(w / 0.1f));
        int topBZ = Mathf.Max(2, Mathf.RoundToInt(d / 0.1f));
        for (int j = 0; j < topBX; j++)
        {
            float xp = (w - ft * 2f) * (j + 1f) / (topBX + 1f) - (w - ft * 2f) * 0.5f;
            CreatePickupCube(parent, new Vector3(xp, h, 0f), new Vector3(mt, mt, d - ft * 2f), mesh);
        }
        for (int j = 0; j < topBZ; j++)
        {
            float zp = (d - ft * 2f) * (j + 1f) / (topBZ + 1f) - (d - ft * 2f) * 0.5f;
            CreatePickupCube(parent, new Vector3(0f, h, zp), new Vector3(w - ft * 2f, mt, mt), mesh);
        }

        float doorW = w * 0.38f;
        float doorH = h * 0.82f;
        float doorY = ft + doorH * 0.5f;
        float doorZ = d * 0.5f - ft * 0.5f;
        float dpw = ft * 1.4f;

        CreatePickupCube(parent, new Vector3(-doorW * 0.5f - dpw * 0.5f, doorY, doorZ), new Vector3(dpw, doorH, ft), latchCol);
        CreatePickupCube(parent, new Vector3(doorW * 0.5f + dpw * 0.5f, doorY, doorZ), new Vector3(dpw, doorH, ft), latchCol);
        CreatePickupCube(parent, new Vector3(0f, ft + doorH, doorZ), new Vector3(doorW + dpw, ft, ft), latchCol);

        int doorBars = Mathf.Max(2, Mathf.RoundToInt(doorW / 0.06f));
        for (int j = 0; j < doorBars; j++)
        {
            float dx = doorW * (j + 1f) / (doorBars + 1f) - doorW * 0.5f;
            CreatePickupCube(parent, new Vector3(dx, doorY, doorZ), new Vector3(mt * 1.3f, doorH - ft, mt), mesh);
        }

        CreatePickupCube(parent, new Vector3(doorW * 0.5f + dpw * 1.2f, ft + doorH + ft * 0.3f, doorZ), new Vector3(ft * 0.7f, ft * 1.5f, ft * 0.7f), springCol);

        CreatePickupCube(parent, new Vector3(0f, ft + mt, d * 0.33f), new Vector3(doorW * 0.4f, mt, doorW * 0.28f), new Color(0.42f, 0.38f, 0.28f));
    }
}

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
            case "scythe": BuildScythe(parent); break;
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
            case "com_trang": BuildRiceDish(parent, new Color(1f, 0.97f, 0.9f)); break;
            case "com_tam": BuildRiceDish(parent, new Color(0.85f, 0.75f, 0.55f)); break;
            case "com_ga": BuildRiceDish(parent, new Color(1f, 0.6f, 0.35f)); break;
            case "com_chieu": BuildRiceDish(parent, new Color(0.9f, 0.6f, 0.2f)); break;
            case "club": BuildClub(parent); break;
            case "cage_big": BuildCageBig(parent); break;
            case "cage_small": BuildCageSmall(parent); break;
            case "fishing_rod": BuildFishingRod(parent); break;
            case "rosary": BuildRosary(parent); break;
            case "fish_carp": BuildFishPickup(parent, new Color(1f, 0.7f, 0.2f)); break;
            case "fish_salmon": BuildFishPickup(parent, new Color(1f, 0.5f, 0.4f)); break;
            case "fish_tuna": BuildFishPickup(parent, new Color(0.3f, 0.3f, 0.5f)); break;
            case "fish_pufferfish": BuildFishPickup(parent, new Color(0.6f, 0.8f, 0.3f)); break;

            // Convenience store items
            case "nuoc_dau": BuildCoconutWater(parent); break;
            case "tra_da": BuildIcedTea(parent); break;
            case "soda": BuildSoda(parent); break;
            case "banh_mi": BuildBread(parent); break;
            case "banh_tet": BuildStickyRiceCake(parent); break;
            case "keo": BuildCandy(parent); break;

            // Cafe items
            case "cafe_den": BuildCoffee(parent); break;

            // Grocery store items
            case "tu_gao": BuildBagOfRice(parent); break;
            case "duong": BuildSugar(parent); break;
            case "muoi": BuildSalt(parent); break;
            case "xap_phong": BuildSoap(parent); break;
            case "mi_chinh": BuildMsg(parent); break;
        }
    }

    public static void BuildArm(Transform parent)
    {
        Color shirtC = new Color(0.24f, 0.45f, 0.64f);
        Color skinC  = new Color(0.88f, 0.72f, 0.52f);

        CreatePickupCube(parent, new Vector3(0f, 0f, 0f), new Vector3(0.2f, 0.5f, 0.2f), shirtC);
        CreatePickupCube(parent, new Vector3(0f, -0.35f, 0f), new Vector3(0.18f, 0.2f, 0.18f), skinC);
        CreatePickupCube(parent, new Vector3(-0.05f, -0.48f, 0.02f), new Vector3(0.06f, 0.08f, 0.06f), skinC);
        CreatePickupCube(parent, new Vector3(0.05f, -0.48f, 0.02f), new Vector3(0.06f, 0.08f, 0.06f), skinC);
        CreatePickupCube(parent, new Vector3(0f, -0.48f, -0.04f), new Vector3(0.06f, 0.08f, 0.06f), skinC);
        CreatePickupCube(parent, new Vector3(0f, -0.5f, 0.04f), new Vector3(0.16f, 0.06f, 0.14f), skinC);
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

    public static void BuildScythe(Transform parent)
    {
        CreatePickupCube(parent, new Vector3(0f, 0f, 0f), new Vector3(0.15f, 0.7f, 0.15f), new Color(0.5f, 0.2f, 0.05f));
        CreatePickupCube(parent, new Vector3(0.3f, 0.35f, 0f), new Vector3(0.5f, 0.08f, 0.08f), new Color(0.6f, 0.6f, 0.6f));
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

    public static void BuildRiceDish(Transform parent, Color topping)
    {
        CreatePickupCylinder(parent, new Vector3(0f, 0.05f, 0f), new Vector3(0.28f, 0.06f, 0.28f), new Color(0.85f, 0.85f, 0.85f), false);
        CreatePickupCube(parent, new Vector3(0f, 0.1f, 0f), new Vector3(0.2f, 0.05f, 0.2f), new Color(1f, 1f, 1f), false);
        CreatePickupCube(parent, new Vector3(0f, 0.14f, 0f), new Vector3(0.14f, 0.04f, 0.14f), topping, false);
        CreatePickupCube(parent, new Vector3(0f, 0.02f, 0.12f), new Vector3(0.04f, 0.02f, 0.2f), new Color(0.55f, 0.35f, 0.15f), false);
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

    public static void BuildFishingRod(Transform parent)
    {
        Color handleC = new Color(0.3f, 0.16f, 0.05f);
        Color woodMid = new Color(0.55f, 0.3f, 0.08f);
        Color woodLight = new Color(0.72f, 0.5f, 0.22f);
        Color metalC = new Color(0.65f, 0.65f, 0.7f);
        Color goldC = new Color(0.9f, 0.72f, 0.25f);
        Color lineC = new Color(0.85f, 0.85f, 0.9f);
        Color hookC = new Color(0.55f, 0.55f, 0.6f);
        Color tipC = new Color(0.9f, 0.15f, 0.1f);

        // tilt the whole rod so it points up-forward when held in the tool slot
        var rod = new GameObject("Rod");
        rod.transform.SetParent(parent, false);
        rod.transform.localRotation = Quaternion.Euler(22f, 0f, 0f);

        // handle (butt grip) + reel seat
        CreatePickupCube(rod.transform, new Vector3(0f, 0.18f, 0f), new Vector3(0.09f, 0.32f, 0.09f), handleC);
        CreatePickupCube(rod.transform, new Vector3(0f, 0.38f, 0f), new Vector3(0.08f, 0.09f, 0.08f), metalC);
        // reel spool (axis across the rod) + crank hub
        CreatePickupCylinder(rod.transform, new Vector3(0f, 0.41f, 0.1f), new Vector3(0.07f, 0.16f, 0.07f), new Vector3(90f, 0f, 0f), metalC);
        CreatePickupCube(rod.transform, new Vector3(0f, 0.41f, 0.16f), new Vector3(0.03f, 0.04f, 0.03f), goldC);
        // tapered pole
        CreatePickupCube(rod.transform, new Vector3(0f, 0.62f, 0f), new Vector3(0.07f, 0.45f, 0.07f), woodMid);
        CreatePickupCube(rod.transform, new Vector3(0f, 0.95f, 0f), new Vector3(0.05f, 0.35f, 0.05f), woodLight);
        CreatePickupCube(rod.transform, new Vector3(0f, 1.18f, 0f), new Vector3(0.03f, 0.28f, 0.03f), woodLight);
        CreatePickupCube(rod.transform, new Vector3(0f, 1.36f, 0f), new Vector3(0.025f, 0.06f, 0.025f), tipC);
        // guide eye near the tip
        CreatePickupCube(rod.transform, new Vector3(0f, 1.1f, 0f), new Vector3(0.06f, 0.02f, 0.06f), metalC);

        // line + hook hang straight down from the tilted tip (parent space)
        float cos = Mathf.Cos(22f * Mathf.Deg2Rad);
        float sin = Mathf.Sin(22f * Mathf.Deg2Rad);
        float tipY = 1.36f * cos;
        float tipZ = 1.36f * sin;
        CreatePickupCube(parent, new Vector3(0f, tipY - 0.35f, tipZ), new Vector3(0.012f, 0.7f, 0.012f), lineC);
        CreatePickupSphere(parent, new Vector3(0f, tipY - 0.72f, tipZ), 0.05f, hookC);
    }

    public static void BuildRosary(Transform parent)
    {
        Color gold = new Color(1f, 0.84f, 0.2f);
        CreatePickupSphere(parent, new Vector3(0f, 0.08f, 0f), 0.16f, gold);

        Color beadGold = new Color(1f, 0.9f, 0.35f);
        int beadCount = 12;
        for (int i = 0; i < beadCount; i++)
        {
            float angle = (i / (float)beadCount) * Mathf.PI * 2f;
            var pos = new Vector3(Mathf.Cos(angle) * 0.18f, 0.08f, Mathf.Sin(angle) * 0.18f);
            CreatePickupSphere(parent, pos, 0.05f, beadGold);
        }
    }

    public static void BuildPalm(Transform parent)
    {
        Color palmC = new Color(0.95f, 0.78f, 0.6f);
        Color auraC = new Color(1f, 0.9f, 0.5f);

        CreatePickupCube(parent, new Vector3(0f, 0f, 0f), new Vector3(0.7f, 0.22f, 0.06f), palmC, false);
        CreatePickupCube(parent, new Vector3(0f, 0.25f, 0f), new Vector3(0.24f, 0.4f, 0.06f), palmC, false);
        CreatePickupCube(parent, new Vector3(0f, 0.18f, 0f), new Vector3(0.7f, 0.28f, 0.04f), auraC, false);
        for (int i = 0; i < 5; i++)
        {
            float x = -0.16f + i * 0.08f;
            CreatePickupCube(parent, new Vector3(x, 0.48f, 0f), new Vector3(0.06f, 0.16f, 0.05f), palmC, false);
        }
    }

    public static void BuildFishPickup(Transform parent, Color bodyColor)
    {
        CreatePickupSphere(parent, new Vector3(0f, 0.06f, 0f), 0.12f, bodyColor, false);
        CreatePickupCube(parent, new Vector3(0f, 0.16f, 0f), new Vector3(0.02f, 0.06f, 0.02f), new Color(0.7f, 0.7f, 0.7f), false);
    }

    public static void BuildCoconutWater(Transform parent)
    {
        Color bottleC = new Color(0.75f, 0.9f, 0.75f);
        Color capC = new Color(0.9f, 0.95f, 0.9f);
        Color labelC = new Color(0.55f, 0.75f, 0.35f);
        CreatePickupCylinder(parent, new Vector3(0f, 0.1f, 0f), new Vector3(0.12f, 0.18f, 0.12f), bottleC, false);
        CreatePickupCube(parent, new Vector3(0f, 0.22f, 0f), new Vector3(0.06f, 0.05f, 0.06f), capC, false);
        CreatePickupCube(parent, new Vector3(0f, 0.14f, 0f), new Vector3(0.13f, 0.03f, 0.13f), labelC, false);
    }

    public static void BuildIcedTea(Transform parent)
    {
        Color cupC = new Color(0.75f, 0.5f, 0.3f);
        Color teaC = new Color(0.85f, 0.55f, 0.2f);
        Color strawC = new Color(0.9f, 0.25f, 0.25f);
        CreatePickupCylinder(parent, new Vector3(0f, 0.12f, 0f), new Vector3(0.14f, 0.22f, 0.14f), cupC, false);
        CreatePickupCube(parent, new Vector3(0f, 0.03f, 0f), new Vector3(0.14f, 0.04f, 0.14f), teaC, false);
        CreatePickupCube(parent, new Vector3(0f, 0.28f, 0.04f), new Vector3(0.025f, 0.16f, 0.025f), strawC, false);
    }

    public static void BuildSoda(Transform parent)
    {
        Color canC = new Color(0.85f, 0.2f, 0.15f);
        Color topC = new Color(0.75f, 0.75f, 0.78f);
        Color labelC = new Color(0.95f, 0.95f, 0.95f);
        CreatePickupCylinder(parent, new Vector3(0f, 0.12f, 0f), new Vector3(0.13f, 0.22f, 0.13f), canC, false);
        CreatePickupCylinder(parent, new Vector3(0f, 0.24f, 0f), new Vector3(0.1f, 0.02f, 0.1f), topC, false);
        CreatePickupCube(parent, new Vector3(0f, 0.15f, 0f), new Vector3(0.14f, 0.04f, 0.14f), labelC, false);
    }

    public static void BuildCoffee(Transform parent)
    {
        Color cupC = new Color(0.95f, 0.95f, 0.96f);
        Color coffeeC = new Color(0.3f, 0.18f, 0.08f);
        Color handleC = new Color(0.9f, 0.9f, 0.92f);
        Color lidC = new Color(0.35f, 0.28f, 0.22f);
        CreatePickupCylinder(parent, new Vector3(0f, 0.12f, 0f), new Vector3(0.13f, 0.22f, 0.13f), cupC, false);
        CreatePickupCylinder(parent, new Vector3(0f, 0.245f, 0f), new Vector3(0.1f, 0.03f, 0.1f), lidC, false);
        CreatePickupCube(parent, new Vector3(0.18f, 0.12f, 0f), new Vector3(0.05f, 0.09f, 0.05f), handleC, false);
        CreatePickupCube(parent, new Vector3(0f, 0.14f, 0f), new Vector3(0.13f, 0.03f, 0.13f), coffeeC, false);
    }

    public static void BuildBread(Transform parent)
    {
        Color crustC = new Color(0.75f, 0.5f, 0.2f);
        Color breadC = new Color(0.95f, 0.85f, 0.6f);
        Color seedC = new Color(0.6f, 0.4f, 0.15f);
        CreatePickupCube(parent, new Vector3(0f, 0.08f, 0f), new Vector3(0.24f, 0.14f, 0.14f), crustC, false);
        CreatePickupCube(parent, new Vector3(0f, 0.09f, 0f), new Vector3(0.2f, 0.08f, 0.12f), breadC, false);
        CreatePickupCube(parent, new Vector3(0f, 0.05f, 0f), new Vector3(0.26f, 0.03f, 0.16f), crustC, false);
        for (int i = 0; i < 4; i++)
        {
            CreatePickupCube(parent, new Vector3(-0.09f + i * 0.06f, 0.1f, 0.06f),
                new Vector3(0.015f, 0.015f, 0.015f), seedC, false);
        }
    }

    public static void BuildStickyRiceCake(Transform parent)
    {
        Color leafC = new Color(0.2f, 0.55f, 0.2f);
        Color bandC = new Color(0.95f, 0.85f, 0.4f);
        CreatePickupCylinder(parent, new Vector3(0f, 0.08f, 0f), new Vector3(0.22f, 0.16f, 0.22f), leafC, false);
        CreatePickupCube(parent, new Vector3(0f, 0.08f, 0f), new Vector3(0.24f, 0.02f, 0.24f), bandC, false);
        CreatePickupCube(parent, new Vector3(0f, 0.16f, 0f), new Vector3(0.02f, 0.02f, 0.24f), bandC, false);
    }

    public static void BuildCandy(Transform parent)
    {
        Color candyC = new Color(0.95f, 0.35f, 0.55f);
        Color wrapC = new Color(0.9f, 0.85f, 0.9f);
        CreatePickupSphere(parent, new Vector3(0f, 0.05f, 0f), 0.1f, candyC, false);
        CreatePickupCube(parent, new Vector3(-0.08f, 0.05f, 0f), new Vector3(0.06f, 0.03f, 0.03f), wrapC, false);
        CreatePickupCube(parent, new Vector3(0.08f, 0.05f, 0f), new Vector3(0.06f, 0.03f, 0.03f), wrapC, false);
    }

    public static void BuildBagOfRice(Transform parent)
    {
        Color bagC = new Color(0.95f, 0.93f, 0.88f);
        Color ribbonC = new Color(0.85f, 0.25f, 0.2f);
        CreatePickupCube(parent, new Vector3(0f, 0.09f, 0f), new Vector3(0.22f, 0.16f, 0.16f), bagC, false);
        CreatePickupCube(parent, new Vector3(0f, 0.19f, 0f), new Vector3(0.07f, 0.05f, 0.07f), bagC, false);
        CreatePickupCube(parent, new Vector3(0f, 0.16f, 0f), new Vector3(0.24f, 0.02f, 0.18f), ribbonC, false);
    }

    public static void BuildSugar(Transform parent)
    {
        Color boxC = new Color(0.95f, 0.92f, 0.82f);
        Color stripeC = new Color(0.9f, 0.6f, 0.1f);
        CreatePickupCube(parent, new Vector3(0f, 0.07f, 0f), new Vector3(0.16f, 0.13f, 0.12f), boxC, false);
        CreatePickupCube(parent, new Vector3(0f, 0.07f, 0f), new Vector3(0.17f, 0.03f, 0.13f), stripeC, false);
    }

    public static void BuildSalt(Transform parent)
    {
        Color boxC = new Color(0.93f, 0.94f, 0.96f);
        Color stripeC = new Color(0.4f, 0.5f, 0.65f);
        CreatePickupCube(parent, new Vector3(0f, 0.07f, 0f), new Vector3(0.14f, 0.12f, 0.12f), boxC, false);
        CreatePickupCube(parent, new Vector3(0f, 0.07f, 0f), new Vector3(0.15f, 0.03f, 0.13f), stripeC, false);
    }

    public static void BuildSoap(Transform parent)
    {
        Color soapC = new Color(0.75f, 0.85f, 0.95f);
        Color stripeC = new Color(0.55f, 0.7f, 0.85f);
        CreatePickupCube(parent, new Vector3(0f, 0.05f, 0f), new Vector3(0.14f, 0.09f, 0.1f), soapC, false);
        CreatePickupCube(parent, new Vector3(0f, 0.05f, 0f), new Vector3(0.15f, 0.02f, 0.11f), stripeC, false);
    }

    public static void BuildMsg(Transform parent)
    {
        Color boxC = new Color(0.9f, 0.2f, 0.18f);
        Color lidC = new Color(0.95f, 0.95f, 0.95f);
        CreatePickupCube(parent, new Vector3(0f, 0.08f, 0f), new Vector3(0.16f, 0.14f, 0.12f), boxC, false);
        CreatePickupCube(parent, new Vector3(0f, 0.16f, 0f), new Vector3(0.17f, 0.03f, 0.13f), lidC, false);
    }
}

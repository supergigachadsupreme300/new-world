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
            case "mi_hao_hao": BuildMiHaoHao(parent); break;
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
            CreatePickupCube(parent, new Vector3(x, size * 0.5f, z), new Vector3(size, size, size), color);
        }
    }

    public static void BuildPeashooterSeed(Transform parent)
    {
        CreatePickupCube(parent, new Vector3(0f, 0.05f, 0f), new Vector3(0.06f, 0.1f, 0.06f), new Color(0.8f, 0.7f, 0.3f));
    }

    public static void BuildPotatoSeed(Transform parent)
    {
        Color potatoSeedColor = new Color(0.7f, 0.5f, 0.2f);
        CreatePickupSphere(parent, new Vector3(-0.05f, 0.05f, 0f), 0.08f, potatoSeedColor);
        CreatePickupSphere(parent, new Vector3(0.05f, 0.05f, 0f), 0.08f, potatoSeedColor);
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
            CreatePickupCube(parent, new Vector3(x, height / 2f, z), new Vector3(width, height, width), color);
        }
        CreatePickupCube(parent, new Vector3(0f, 0.1f, 0f), new Vector3(0.2f, 0.05f, 0.2f), new Color(0.627f, 0.431f, 0.235f));
    }

    public static void BuildCornPickup(Transform parent)
    {
        Color cornColor = new Color(1f, 0.85f, 0.2f);
        for (int i = 0; i < 5; i++)
        {
            for (int j = 0; j < 5; j++)
            {
                float angle = j * 72f;
                CreatePickupCube(parent, new Vector3(0f, 0.5f+0.04f*i, 0f), new Vector3(0.2f, 0.04f, 0.05f), new Vector3(0f, angle + i*18f, 0f), cornColor);
            }
        }
        CreatePickupCube(parent, new Vector3(0f, 0.1f, 0f), new Vector3(0.06f, 0.8f, 0.06f), new Color(0.3f, 0.7f, 0.25f));
    }

    public static void BuildPotatoPickup(Transform parent)
    {
        CreatePickupSphere(parent, new Vector3(-0.04f, 0.06f, 0f), 0.14f, new Color(0.627f, 0.431f, 0.235f));
        CreatePickupSphere(parent, new Vector3(0.04f, -0.06f, 0f), 0.14f, new Color(0.3f, 0.2f, 0.1f));
    }

    public static void BuildDamagedCrop(Transform parent)
    {
        CreatePickupCube(parent, new Vector3(0f, 0.15f, 0f), new Vector3(0.3f, 0.25f, 0.3f), new Color(0.6f, 0.4f, 0.2f));
    }

    public static void BuildMiHaoHao(Transform parent)
    {
        CreatePickupCube(parent, new Vector3(0f, 0.2f, 0f), new Vector3(0.3f, 0.1f, 0.3f), Color.red);
    }
}

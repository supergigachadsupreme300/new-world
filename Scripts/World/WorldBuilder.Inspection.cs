using System.Collections.Generic;
using UnityEngine;
using TMPro;
using static CountryLife.Helpers.PickupVisualHelper;

public partial class WorldBuilder
{
    // â”€â”€ Inspection Area: Building Models â”€â”€

    private void CreateBuildingModels()
    {
        float baseX = 50f;
        float baseZ = -128f;
        float step = 6f;

        var house = MapBuilder.BuildPlayerHouse(_worldRoot.transform, new Vector3(baseX, 0f, baseZ), 0.25f);
        house.name = "Model_PlayerHouse";

        var shop = MapBuilder.BuildShop(_worldRoot.transform, new Vector3(baseX + step, 0f, baseZ), 0.25f);
        shop.name = "Model_Shop";

        var wifeHouse = MapBuilder.BuildWifeHouse(_worldRoot.transform, new Vector3(baseX + step * 2, 0f, baseZ), 0.25f);
        wifeHouse.name = "Model_WifeHouse";
    }

    // â”€â”€ Inspection Area: Category Labels â”€â”€

    private void CreateInspectionLabels()
    {
        CreateSectionLabel("SEEDS & SUPPLIES", new Vector3(57.5f, 3f, -50f));
        CreateSectionLabel("TOOLS", new Vector3(57.5f, 3f, -58f));
        CreateSectionLabel("HARVESTED CROPS", new Vector3(57.5f, 3f, -63f));
        CreateSectionLabel("ANIMALS", new Vector3(57.5f, 3f, -76f));
        CreateSectionLabel("CROP GROWTH STAGES", new Vector3(55.25f, 3f, -93f));
        CreateSectionLabel("BUILDINGS", new Vector3(56f, 3f, -126f));
    }

    private void CreateSectionLabel(string text, Vector3 position)
    {
        var labelGO = new GameObject("SectionLabel_" + text.Replace(" ", ""));
        labelGO.transform.SetParent(_worldRoot.transform);
        labelGO.transform.position = position;

        var tmp = labelGO.AddComponent<TMPro.TextMeshPro>();
        tmp.text = text;
        tmp.fontSize = 2f;
        tmp.alignment = TMPro.TextAlignmentOptions.Center;
        tmp.color = Color.white;
        tmp.outlineWidth = 0.15f;
        tmp.outlineColor = Color.black;
        tmp.rectTransform.sizeDelta = new Vector3(20f, 2f);
    }

    // â”€â”€ Inspection Area: Section Dividers â”€â”€

    private void CreateSectionDividers()
    {
        CreateDivider(new Vector3(57.5f, 0.05f, -57.5f), new Vector3(18f, 0.15f, 0.15f));
        CreateDivider(new Vector3(57.5f, 0.05f, -62.5f), new Vector3(18f, 0.15f, 0.15f));
        CreateDivider(new Vector3(57.5f, 0.05f, -74.5f), new Vector3(18f, 0.15f, 0.15f));
        CreateDivider(new Vector3(57.5f, 0.05f, -92.5f), new Vector3(18f, 0.15f, 0.15f));
        CreateDivider(new Vector3(57.5f, 0.05f, -124.1f), new Vector3(18f, 0.15f, 0.15f));
    }

    private void CreateDivider(Vector3 position, Vector3 scale)
    {
        var divider = GameObject.CreatePrimitive(PrimitiveType.Cube);
        divider.name = "Divider";
        divider.transform.SetParent(_worldRoot.transform);
        divider.transform.position = position;
        divider.transform.localScale = scale;
        var rend = divider.GetComponent<Renderer>();
        if (rend != null)
            rend.material.color = new Color(0.5f, 0.5f, 0.5f, 0.8f);
        Destroy(divider.GetComponent<Collider>());
    }

    private void CreateEnemyDisplay()
    {
        var displayRoot = new GameObject("EnemyDisplay");
        displayRoot.transform.SetParent(_worldRoot.transform);

        CreateSectionLabel("ENEMY", new Vector3(57.5f, 3f, -86f));

        var pedestal = GameObject.CreatePrimitive(PrimitiveType.Cube);
        pedestal.name = "Pedestal";
        pedestal.transform.SetParent(displayRoot.transform);
        pedestal.transform.position = new Vector3(57.5f, 0.15f, -88f);
        pedestal.transform.localScale = new Vector3(1.5f, 0.3f, 1.5f);
        var pedR = pedestal.GetComponent<Renderer>();
        if (pedR != null) pedR.material.color = new Color(0.15f, 0.15f, 0.18f);
        Destroy(pedestal.GetComponent<Collider>());

        var model = EnemyModelBuilder.BuildRegularEnemy(displayRoot.transform);
        model.transform.position = new Vector3(57.5f, 0.3f, -88f);
        model.transform.localScale = Vector3.one * 0.8f;
        model.name = "EnemyDisplayModel";
    }

    private void CreateAnimalDisplay()
    {
        var displayRoot = new GameObject("AnimalDisplay");
        displayRoot.transform.SetParent(_worldRoot.transform);

        float baseX = 50f;
        float spacing = 3.5f;
        float rowZ = -78f;
        int idx = 0;

        var types = new Livestock.AnimalType[] {
            Livestock.AnimalType.Cow, Livestock.AnimalType.Pig, Livestock.AnimalType.Sheep,
            Livestock.AnimalType.Goat, Livestock.AnimalType.Chicken, Livestock.AnimalType.Duck,
            Livestock.AnimalType.Turkey
        };

        foreach (var t in types)
        {
            float x = baseX + idx * spacing;
            var pedestal = GameObject.CreatePrimitive(PrimitiveType.Cube);
            pedestal.name = "Pedestal_" + t;
            pedestal.transform.SetParent(displayRoot.transform);
            pedestal.transform.position = new Vector3(x, 0.15f, rowZ);
            pedestal.transform.localScale = new Vector3(1.2f, 0.3f, 1.2f);
            var pedR = pedestal.GetComponent<Renderer>();
            if (pedR != null) pedR.material.color = new Color(0.15f, 0.18f, 0.15f);
            Destroy(pedestal.GetComponent<Collider>());

            var model = new GameObject("Animal_" + t);
            model.transform.SetParent(displayRoot.transform);
            model.transform.position = new Vector3(x, 0.3f, rowZ);
            model.transform.localScale = Vector3.one * 0.6f;
            Livestock.BuildModelInto(model.transform, t);
            idx++;
        }
    }
}


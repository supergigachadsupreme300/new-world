using System.Collections.Generic;
using UnityEngine;
using TMPro;
using static CountryLife.Helpers.PickupVisualHelper;

public partial class WorldBuilder
{
    private void CreateCropDemo()
    {
        var demoRoot = new GameObject("CropDemo");
        demoRoot.transform.SetParent(_worldRoot.transform);

        string[] cropTypes = { "wheat", "corn", "potato", "carrot", "tomato", "strawberry", "pumpkin", "onion", "sugarcane", "rice" };
        string[] cropLabels = { "Wheat", "Corn", "Potato", "Carrot", "Tomato", "Strawberry", "Pumpkin", "Onion", "Sugarcane", "Rice" };
        float startX = 50f;
        float startZ = -95f;
        float xStep = 3.5f;
        float zStep = 2.8f;

        for (int c = 0; c < cropTypes.Length; c++)
        {
            for (int s = 1; s <= 4; s++)
            {
                float x = startX + (s - 1) * xStep;
                float z = startZ - c * zStep;

                var plot = GameObject.CreatePrimitive(PrimitiveType.Quad);
                plot.name = "FieldTile";
                plot.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
                plot.transform.position = new Vector3(x, 0.01f, z);
                plot.transform.localScale = new Vector3(2f, 2f, 2f);
                plot.transform.SetParent(demoRoot.transform);
                plot.GetComponent<MeshRenderer>().material.color = new Color(0.35f, 0.2f, 0.08f);
                Destroy(plot.GetComponent<Collider>());
                AddFieldBorder(plot.transform);

                var plotLabel = new GameObject("PlotLabel");
                plotLabel.transform.SetParent(demoRoot.transform);
                plotLabel.transform.position = new Vector3(x, 1f, z);
                var tmp = plotLabel.AddComponent<TextMeshPro>();
                tmp.text = cropLabels[c] + "\nS" + s;
                tmp.fontSize = 0.3f;
                tmp.alignment = TextAlignmentOptions.Center;
                tmp.color = Color.white;
                tmp.outlineWidth = 0.2f;
                tmp.outlineColor = Color.black;

                var cropRoot = new GameObject(cropTypes[c] + "_Stage" + s);
                cropRoot.transform.SetParent(plot.transform, false);
                cropRoot.transform.localPosition = Vector3.up * 0.05f;
                cropRoot.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);

                switch (cropTypes[c])
                {
                    case "corn": CreateFieldCorn(cropRoot.transform, s); break;
                    case "potato": CreateFieldPotato(cropRoot.transform, s); break;
                    case "carrot": CreateFieldCarrot(cropRoot.transform, s); break;
                    case "tomato": CreateFieldTomato(cropRoot.transform, s); break;
                    case "strawberry": CreateFieldStrawberry(cropRoot.transform, s); break;
                    case "pumpkin": CreateFieldPumpkin(cropRoot.transform, s); break;
                    case "onion": CreateFieldOnion(cropRoot.transform, s); break;
                    case "sugarcane": CreateFieldSugarcane(cropRoot.transform, s); break;
                    case "rice": CreateFieldRice(cropRoot.transform, s); break;
                    default: CreateFieldWheat(cropRoot.transform, s); break;
                }
            }
        }
    }

    public FieldState GetFieldAt(Vector3 position)
    {
        foreach (var field in _fields)
        {
            if (field.FieldObject == null) continue;
            if (Vector3.Distance(field.FieldObject.transform.position, position) < 2f)
                return field;
        }
        return null;
    }

    public FieldState TillGround(Vector3 position)
    {
        position.x = Mathf.Round(position.x);
        position.z = Mathf.Round(position.z);
        bool onRoad = IsOnRoad(position);
        position.y = onRoad ? GetRoadSurfaceY() + 0.01f : 0f;
        var field = GetFieldAt(position);
        if (field != null)
        {
            field.Tilled = true;
            field.IsHarvested = false;
            field.HasCrop = false;
            field.CropType = null;
            field.Stage = 0;
            field.Watered = false;
            field.Fertilized = false;
            field.GrowTimer = 0f;
            UpdateFieldVisual(field);
            return field;
        }

        var tile = GameObject.CreatePrimitive(PrimitiveType.Quad);
        tile.name = "FieldTile";
        tile.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        tile.transform.position = position + Vector3.up * 0.01f;
        tile.transform.localScale = new Vector3(2f, 2f, 2f);
        tile.transform.SetParent(_worldRoot.transform);
        var tileRenderer = tile.GetComponent<MeshRenderer>();
        var dirtTex = Resources.Load<Texture2D>("texture/dirt_texture");
        if (dirtTex != null)
        {
            tileRenderer.material = new Material(Shader.Find("Standard"));
            tileRenderer.material.mainTexture = dirtTex;
        }
        else
            tileRenderer.material.color = new Color(0.45f, 0.28f, 0.12f);
        tile.AddComponent<BoxCollider>().isTrigger = true;
        AddFieldBorder(tile.transform);

        field = new FieldState
        {
            FieldObject = tile,
            Tilled = true,
            Stage = 0,
            HasCrop = false,
            GrowTimer = 0f,
            NextStageTime = 12f,
            Watered = false,
            Fertilized = false,
            WaterTimer = 0f
        };
        _fields.Add(field);
        return field;
    }

    public bool PlantCrop(FieldState field, string cropType)
    {
        if (field == null || !field.Tilled || field.HasCrop)
            return false;

        string actualCropType = cropType switch
        {
            "wheat_seed" => "wheat",
            "corn_seed" => "corn",
            "wheat" => "wheat",
            "corn" => "corn",
            "potato" => "potato",
            "potato_seed" => "potato",
            "carrot_seed" => "carrot",
            "carrot" => "carrot",
            "tomato_seed" => "tomato",
            "tomato" => "tomato",
            "strawberry_seed" => "strawberry",
            "strawberry" => "strawberry",
            "pumpkin_seed" => "pumpkin",
            "pumpkin" => "pumpkin",
            "onion_seed" => "onion",
            "onion" => "onion",
            "sugarcane_seed" => "sugarcane",
            "sugarcane" => "sugarcane",
            "rice_seed" => "rice",
            "rice" => "rice",
            _ => null
        };

        if (actualCropType == null)
            return false;

        field.CropType = actualCropType;
        field.HasCrop = true;
        field.IsHarvested = false;
        field.Stage = 1;
        field.GrowTimer = 0f;
        field.Watered = false;
        field.NextStageTime = 12f;
        UpdateCropVisual(field);
        return true;
    }

    public bool HarvestField(FieldState field, out string harvestedItem)
    {
        harvestedItem = null;
        if (field == null || !field.HasCrop || field.Stage < 4)
            return false;

        harvestedItem = field.CropType switch
        {
            "wheat" => "wheat",
            "corn" => "corn",
            "potato" => "potato",
            "carrot" => "carrot",
            "tomato" => "tomato",
            "strawberry" => "strawberry",
            "pumpkin" => "pumpkin",
            "onion" => "onion",
            "sugarcane" => "sugarcane",
            "rice" => "rice",
            _ => field.CropType
        };

        if (field.CropObject != null)
            Destroy(field.CropObject);

        field.HasCrop = false;
        field.IsHarvested = true;
        field.CropType = null;
        field.Stage = 0;
        UpdateFieldVisual(field);
        return true;
    }

    public bool WaterField(Vector3 position)
    {
        var field = GetFieldAt(position);
        if (field == null || !field.Tilled || !field.HasCrop || field.IsHarvested)
            return false;
        field.Watered = true;
        field.WaterTimer = 30f;
        UpdateFieldVisual(field);
        return true;
    }

    public bool FertilizeField(Vector3 position)
    {
        var field = GetFieldAt(position);
        if (field == null || !field.Tilled || !field.HasCrop || field.IsHarvested)
            return false;
        field.Fertilized = true;
        UpdateFieldVisual(field);
        return true;
    }

    public bool BoostFieldGrowth(Vector3 position)
    {
        var field = GetFieldAt(position);
        if (field == null || !field.Tilled || !field.HasCrop || field.IsHarvested || field.Stage >= 4)
            return false;
        field.Stage++;
        field.GrowTimer = 0f;
        UpdateCropVisual(field);
        return true;
    }
}


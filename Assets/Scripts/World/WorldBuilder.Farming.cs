using System.Collections.Generic;
using UnityEngine;
using TMPro;
using static CountryLife.Helpers.PickupVisualHelper;

public partial class WorldBuilder
{
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

    // Base grow time per crop (second per stage). Phase 3B.
    public float CropGrowTime(string cropType)
    {
        switch (cropType)
        {
            case "carrot": return 10f;
            case "wheat": return 12f;
            case "rice": return 12f;
            case "onion": return 11f;
            case "corn": return 14f;
            case "potato": return 13f;
            case "sugarcane": return 13f;
            case "tomato": return 15f;
            case "pumpkin": return 16f;
            case "strawberry": return 17f;
            default: return 12f;
        }
    }

    // Phase 3B: quality tiers (0 normal, 1 good, 2 great) at maturation.
    public static string QualityName(int quality)
    {
        return quality >= 2 ? Localization.T("Chất Lượng Tuyệt") :
               quality == 1 ? Localization.T("Chất Lượng Tốt") : "";
    }

    // Quality rules: fertilized = Good; fertilized + mostly watered = Great.
    private void EvaluateCropQuality(FieldState field, float growTime)
    {
        float coverage = growTime > 0f ? field.WateredTime / growTime : 0f;
        if (field.Fertilized)
            field.Quality = coverage >= 0.6f ? 2 : 1;
        else
            field.Quality = 0;
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
            field.Quality = 0;
            field.WateredTime = 0f;
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
            WaterTimer = 0f,
            Quality = 0,
            WateredTime = 0f
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
        field.NextStageTime = CropGrowTime(actualCropType);
        field.Quality = 0;
        field.WateredTime = 0f;
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


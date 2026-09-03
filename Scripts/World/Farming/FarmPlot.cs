using System;
using UnityEngine;

/// <summary>
/// A tilled farming plot (planning Task 6.1, game-design §5.1). Owns a crop's lifecycle:
/// planted → watered → grows a stage per accumulated game-hour → harvestable at max stage.
/// Growth is advanced by <see cref="FarmingManager"/> each game cycle, and is tied to the
/// day/night cycle (game-design §7.3): daylight accumulates grow time, night halts/pauses it.
/// A generated cube stand-in represents the plot until crop art arrives.
/// </summary>
public class FarmPlot : MonoBehaviour
{
    [Header("Crop")]
    public CropType Crop = CropType.Rice;
    [Tooltip("Current growth stage (0 = tilled, 1..Stages).")]
    public int Stage;

    [Header("State")]
    public bool Tilled;
    public bool Watered;
    public bool Harvested;

    private float _growHours;
    private GameObject _cropVisual;
    private GameObject _fieldVisual;

    public bool HasCrop => Tilled && Stage > 0;
    public bool IsMature => CropData != null && Stage >= CropData.Stages;

    private CropData CropData => CropRegistry.Get(Crop);

    /// <summary>Advance growth by elapsed game-hours; night halts growth per vignette #2.</summary>
    public void TickCycle(float gameHours, bool isNight)
    {
        if (!HasCrop || IsMature) return;
        if (CropData == null) return;
        if (isNight) return; // crops only grow during the day.
        if (CropData.NeedsWater && !Watered) return;

        _growHours += gameHours;
        float perStage = CropData.BaseGrowHours / Mathf.Max(1, CropData.Stages);
        int targetStage = 1 + Mathf.FloorToInt(_growHours / Mathf.Max(0.001f, perStage));
        int newStage = Mathf.Clamp(targetStage, 1, CropData.Stages);
        if (newStage != Stage)
        {
            Stage = newStage;
            UpdateVisual();
        }
    }

    public bool Plant(CropType type)
    {
        if (!Tilled || HasCrop) return false;
        Crop = type;
        Stage = 1;
        Watered = false;
        Harvested = false;
        _growHours = 0f;
        UpdateVisual();
        return true;
    }

    public bool Water()
    {
        if (!Tilled || !HasCrop || Harvested) return false;
        Watered = true;
        UpdateVisual();
        return true;
    }

    public bool Harvest(out string yieldId)
    {
        yieldId = null;
        if (!IsMature || Harvested) return false;
        var data = CropData;
        if (data != null) yieldId = data.HarvestItemId;
        DestroyCropVisual();
        Stage = 0;
        _growHours = 0f;
        Watered = false;
        Harvested = true;
        UpdateVisual();
        return true;
    }

    private void UpdateVisual()
    {
        DestroyCropVisual();
        if (!HasCrop) return;

        _cropVisual = GameObject.CreatePrimitive(PrimitiveType.Cube);
        _cropVisual.name = "Crop_" + Crop;
        _cropVisual.transform.SetParent(transform, false);
        float height = 0.15f + Stage * 0.18f;
        _cropVisual.transform.localScale = new Vector3(0.8f, height, 0.8f);
        _cropVisual.transform.localPosition = new Vector3(0f, height * 0.5f + 0.02f, 0f);
        var r = _cropVisual.GetComponent<MeshRenderer>();
        if (r != null)
            r.material.color = IsMature ? new Color(0.35f, 0.85f, 0.35f) : new Color(0.5f, 0.75f, 0.35f);
        Destroy(_cropVisual.GetComponent<Collider>());
    }

    private void DestroyCropVisual()
    {
        if (_cropVisual != null)
        {
            Destroy(_cropVisual);
            _cropVisual = null;
        }
    }

    private void OnDestroy()
    {
        DestroyCropVisual();
        if (_fieldVisual != null) Destroy(_fieldVisual);
    }

    /// <summary>Build the tilled dirt tile (stand-in). Usually called by <see cref="FarmingManager"/>.</summary>
    public void BuildFieldVisual(Color tint)
    {
        _fieldVisual = GameObject.CreatePrimitive(PrimitiveType.Quad);
        _fieldVisual.name = "TilledTile";
        _fieldVisual.transform.SetParent(transform, false);
        _fieldVisual.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        _fieldVisual.transform.localScale = new Vector3(1.8f, 1.8f, 1f);
        _fieldVisual.transform.localPosition = new Vector3(0f, 0.01f, 0f);
        var r = _fieldVisual.GetComponent<MeshRenderer>();
        if (r != null) r.material.color = tint;
        Destroy(_fieldVisual.GetComponent<Collider>());
    }
}
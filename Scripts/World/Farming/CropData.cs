using System;
using UnityEngine;

/// <summary>
/// Crop taxonomy (planning Task 6.1, game-design §5.1). Mirrors the country crop set
/// (rice, carrot, wheat, …) that feeding/cooking recipes already reference, so harvests feed
/// straight into the existing cooking pipeline.
/// </summary>
public enum CropType
{
    Rice = 0,
    Carrot = 1,
    Wheat = 2,
    Tomato = 3,
    Pumpkin = 4,
    Strawberry = 5,
    Onion = 6,
    Sugarcane = 7,
    Potato = 8,
    Corn = 9
}

/// <summary>
/// A single crop definition (planning Task 6.1). Data-only so growth tuning, water needs, and
/// harvest yields resolve from one place; <see cref="FarmPlot"/> uses it to advance stages by
/// the day/night cycle (game-design §7.3) rather than raw unscaled timers.
/// </summary>
[CreateAssetMenu(fileName = "Crop", menuName = "New World/Farming/Crop", order = 72)]
public class CropData : ScriptableObject
{
    [Tooltip("Stable id, also used as the harvested item id for cooking/vendors.")]
    public CropType Type;
    public string DisplayName;

    [Header("Growth")]
    [Tooltip("Base grow time in game-hours across all stages (day/night-aware).")]
    [Min(1f)] public float BaseGrowHours = 12f;
    [Tooltip("Stage count before harvestable (e.g. 4 = sprout..mature).")]
    [Min(1)] public int Stages = 4;
    [Tooltip("Requires water to continue growing (game-design §5.1 plant/water/harvest).")]
    public bool NeedsWater = true;

    [Header("Harvest")]
    [Tooltip("Item id added to the player's inventory on harvest (e.g. crop name for cooking).")]
    public string HarvestItemId;
    [Min(1)] public int HarvestCount = 1;
}
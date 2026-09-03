using UnityEngine;

/// <summary>
/// Fish taxonomy (planning Task 6.2, game-design §5.2). Mirrors the species the legacy minigame
/// already uses (carp/salmon/tuna/pufferfish) plus a couple of biome reef fish, so catches feed
/// into the existing tool/economy pipeline (fish are consumables / sell items).
/// </summary>
public enum FishType
{
    Carp = 0,
    Salmon = 1,
    Tuna = 2,
    Pufferfish = 3,
    ReefFish = 4,
    Eel = 5
}

/// <summary>
/// A single fish species definition (planning Task 6.2). Data-only; <see cref="FishingSpot"/>
/// rolls the <see cref="FishRegistry"/> for a species by weight tier and adds it to the player's
/// tool inventory. Higher tiers reel harder and sell for more (§5.2 fish as food/sell).
/// </summary>
[CreateAssetMenu(fileName = "Fish", menuName = "New World/Fishing/Fish", order = 73)]
public class FishData : ScriptableObject
{
    [Tooltip("Stable id, also used as the ToolManager item id when caught (e.g. 'fish_carp').")]
    public FishType Type;
    public string DisplayName;

    [Header("Catch")]
    [Tooltip("Relative spawn weight; higher = more common.")]
    public float Weight = 40f;
    [Tooltip("Flop chance on landing (0..1); lower = easier land, mirrors reeling flop).")]
    [Range(0f, 1f)] public float Flop = 0.3f;
    [Tooltip("Reeling difficulty multiplier (lower = harder).")]
    public float ReelFactor = 1f;

    [Header("Reward")]
    [Tooltip("ToolManager item id granted on catch; used by vendors/cooking.")]
    public string ToolItemId;
    [Tooltip("Base vendor sell value in credits.")]
    public int SellValue;
    [Tooltip("If true the fish is edible (food buff / consumable).")]
    public bool IsConsumable = true;
}
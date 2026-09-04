using UnityEngine;

/// <summary>
/// Player-configurable chunk streaming distance. Controls how many terrain
/// chunks (each 30x30 tiles) are loaded around the current focus position:
///   radius 3  ->  49 chunks = 44,100 tiles
///   radius 10 -> 441 chunks = 396,900 tiles
///
/// The WorldStreamer reads RenderDistance.Radius each frame and (un)loads
/// terrain chunks accordingly. Player can raise/lower it via settings.
/// </summary>
[CreateAssetMenu(fileName = "RenderDistanceConfig", menuName = "NewWorld/Render Distance", order = 1)]
public class RenderDistanceController : ScriptableObject
{
    [Range(1, 160)] public int Radius = 3;
    [Range(1, 160)] public int MaxRadius = 160;
    [Range(1, 8)] public int MinRadius = 1;

    /// <summary>Total number of terrain chunks in a square of the current radius.</summary>
    public int ChunkCountForRadius => (Radius * 2 + 1) * (Radius * 2 + 1);

    public void SetRadius(int value)
    {
        Radius = Mathf.Clamp(value, MinRadius, MaxRadius);
    }
}

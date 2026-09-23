using UnityEngine;

/// <summary>
/// Player-configurable chunk streaming distance. Controls how many terrain
/// chunks (each 30x30 tiles) are loaded around the current focus position:
///   radius 3  ->  49 chunks = 44,100 tiles
///   radius 10 ->  441 chunks = 396,900 tiles
///   radius 30 ->  3,721 chunks (1eo default/max ≈ 900 m; was 67 / ~2,000 m)
///
/// The WorldStreamer reads RenderDistance.Radius each frame and (un)loads
/// terrain chunks accordingly. Player can raise/lower it via settings.
/// 1eo: the default AND max both sit at 30, so the loaded range cannot be
/// pushed past ~900 m without editing this class.
/// </summary>
[CreateAssetMenu(fileName = "RenderDistanceConfig", menuName = "NewWorld/Render Distance", order = 1)]
public class RenderDistanceController : ScriptableObject
{
    [Range(1, 30)] public int Radius = 30;
    [Range(1, 30)] public int MaxRadius = 30;
    [Range(1, 8)] public int MinRadius = 1;

    /// <summary>Total number of terrain chunks in a square of the current radius.</summary>
    public int ChunkCountForRadius => (Radius * 2 + 1) * (Radius * 2 + 1);

    public void SetRadius(int value)
    {
        Radius = Mathf.Clamp(value, MinRadius, MaxRadius);
    }
}

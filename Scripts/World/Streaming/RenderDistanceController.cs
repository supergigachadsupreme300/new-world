using UnityEngine;

/// <summary>
/// Player-configurable chunk streaming distance. Controls how many chunks are
/// loaded around the current focus (player) position:
///   default radius 5  -> 121 chunks
///   maximum radius 32 -> 4,225 chunks
///
/// The WorldStreamer reads RenderDistance.Radius each frame and (un)loads chunks
/// accordingly. Player can raise/lower it (e.g. via a settings slider).
/// </summary>
[CreateAssetMenu(fileName = "RenderDistanceConfig", menuName = "NewWorld/Render Distance", order = 1)]
public class RenderDistanceController : ScriptableObject
{
    [Range(1, 32)] public int Radius = 5;
    [Range(1, 32)] public int MaxRadius = 32;
    [Range(1, 8)] public int MinRadius = 1;

    /// <summary>Total number of chunks in a square of the current radius (excluding nothing).</summary>
    public int ChunkCountForRadius => (Radius * 2 + 1) * (Radius * 2 + 1);

    public void SetRadius(int value)
    {
        Radius = Mathf.Clamp(value, MinRadius, MaxRadius);
    }
}

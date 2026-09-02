using System;
using UnityEngine;

/// <summary>
/// Configuration for a single Perlin noise octave. Each octave contributes a
/// distinct frequency/amplitude layer to the final terrain height.
/// </summary>
[Serializable]
public class NoiseLayerConfig
{
    public string Name = "Layer";
    [Range(0.0001f, 1f)] public float Frequency = 0.01f;
    public float Amplitude = 1f;
    public Vector2 Offset = Vector2.zero;
}

/// <summary>
/// Combines multiple Perlin noise octaves into a deterministic height function.
/// Given the same seed and world coordinates, GetHeight always returns the same
/// value — this is what lets two players on a shared seed see identical terrain.
///
/// Each octave uses a per-layer seed offset (layerIndex * 7919) so layers do not
/// overlap and produce independently shaped patterns.
/// </summary>
public static class TerrainNoiseGenerator
{
    /// <summary>
    /// Each octave derives its own private seed stream from the world seed plus
    /// this constant multiplied by the layer index.
    /// </summary>
    private const int LayerSeedPrime = 7919;

    // Default spreadsheet for the 5 documented octaves.
    // Layer 1 - Continental landmass, Layer 2 - Hills, Layer 3 - Detail,
    // Layer 4 - Roughness, Layer 5 - Pivot angle offsets.
    public static readonly NoiseLayerConfig[] DefaultLayers =
    {
        new NoiseLayerConfig { Name = "Continental", Frequency = 0.001f, Amplitude = 40f },
        new NoiseLayerConfig { Name = "Hills",        Frequency = 0.005f, Amplitude = 15f },
        new NoiseLayerConfig { Name = "Detail",       Frequency = 0.02f,  Amplitude = 5f },
        new NoiseLayerConfig { Name = "Roughness",    Frequency = 0.08f,  Amplitude = 1.5f },
        new NoiseLayerConfig { Name = "PivotAngle",   Frequency = 0.01f,  Amplitude = 2f },
    };

    /// <summary>
    /// Sums all configured octaves (plus a configurable base height) into a
    /// single absolute world-space height for a coordinate.
    /// </summary>
    public static float GetHeight(long seed, float worldX, float worldZ, NoiseLayerConfig[] layers, float baseHeight = 0f)
    {
        if (layers == null || layers.Length == 0)
            layers = DefaultLayers;

        float total = baseHeight;
        for (int i = 0; i < layers.Length; i++)
        {
            NoiseLayerConfig layer = layers[i];
            if (layer == null || layer.Amplitude <= 0f)
                continue;

            // Derive a unique per-layer noise stream from the world seed.
            System.Random rng = new System.Random(GetSeed(seed, i));
            float offsetX = (float)(rng.NextDouble() * 100000.0) + layer.Offset.x;
            float offsetZ = (float)(rng.NextDouble() * 100000.0) + layer.Offset.y;

            // map the layer into the [-1, 1] range so it can raise and lower ground.
            float value = Mathf.PerlinNoise(
                (worldX + offsetX) * layer.Frequency,
                (worldZ + offsetZ) * layer.Frequency) * 2f - 1f;

            total += value * layer.Amplitude;
        }

        return total;
    }

    /// <summary>
    /// Convenience overload using the default 5-layer spreadsheet.
    /// </summary>
    public static float GetHeight(long seed, float worldX, float worldZ, float baseHeight = 0f)
    {
        return GetHeight(seed, worldX, worldZ, DefaultLayers, baseHeight);
    }

    /// <summary>Compute the per-layer seed offset.</summary>
    private static int GetSeed(long seed, int layerIndex)
    {
        unchecked
        {
            long s = seed + layerIndex * LayerSeedPrime;
            return (int)(s & 0x7FFFFFFF);
        }
    }
}

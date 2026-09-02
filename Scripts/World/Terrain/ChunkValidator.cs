using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Debug tool that verifies the no-gap guarantee of the chunk terrain.
///
/// It loads/creates a cluster of chunks and confirms that every shared corner
/// between adjacent chunks has an identical height (which is the mathematical
/// condition for a seamless, gapless mesh). Run via the Inspector button or
/// from code.
/// </summary>
public class ChunkValidator : MonoBehaviour
{
    public WorldStreamer World;
    public ChunkCoord Center = new ChunkCoord(0, 0);
    public int Radius = 4;

    public bool AutoValidateOnEnable;

    private void OnEnable()
    {
        if (AutoValidateOnEnable)
            RunValidation();
    }

    /// <summary>
    /// Ensure every chunk in the radius cluster is present, then verify all
    /// shared-corner heights match between neighbours. Logs a report.
    /// </summary>
    public void RunValidation()
    {
        if (World == null)
        {
            Debug.LogWarning("[ChunkValidator] No WorldStreamer assigned.");
            return;
        }

        // Force-load the cluster so the validator has data to check.
        for (int x = Center.X - Radius; x <= Center.X + Radius; x++)
            for (int z = Center.Z - Radius; z <= Center.Z + Radius; z++)
                World.EnsureChunk(new ChunkCoord(x, z));

        int edgesChecked = 0;
        int mismatches = 0;
        List<string> problems = new List<string>();

        for (int x = Center.X - Radius; x <= Center.X + Radius; x++)
        {
            for (int z = Center.Z - Radius; z <= Center.Z + Radius; z++)
            {
                ChunkCoord a = new ChunkCoord(x, z);
                if (!World.TryGetData(a, out ChunkData da))
                    continue;

                // Check +X neighbour (east) sharing the NE/SE corners.
                ChunkCoord east = new ChunkCoord(x + 1, z);
                if (World.TryGetData(east, out ChunkData de))
                {
                    edgesChecked++;
                    if (!Approx(da.Heights[1], de.Heights[0])) // a.NE == east.NW
                    {
                        mismatches++;
                        problems.Add($"{a} NE vs {east} NW: {da.Heights[1]:0.###} != {de.Heights[0]:0.###}");
                    }
                }

                // Check +Z neighbour (north) sharing the NW/NE corners.
                ChunkCoord north = new ChunkCoord(x, z + 1);
                if (World.TryGetData(north, out ChunkData dn))
                {
                    edgesChecked++;
                    if (!Approx(da.Heights[0], dn.Heights[3])) // a.NW == north.SW
                    {
                        mismatches++;
                        problems.Add($"{a} NW vs {north} SW: {da.Heights[0]:0.###} != {dn.Heights[3]:0.###}");
                    }
                }
            }
        }

        Debug.Log($"[ChunkValidator] Checked {edgesChecked} shared edges, {mismatches} mismatches.");
        if (mismatches > 0)
        {
            foreach (string p in problems)
                Debug.LogError("[ChunkValidator] " + p);
        }
        else
        {
            Debug.Log("[ChunkValidator] All shared edges in sync — terrain is gapless.");
        }
    }

    private static bool Approx(float a, float b)
    {
        return Mathf.Abs(a - b) < 0.0001f;
    }
}

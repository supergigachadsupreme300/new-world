using UnityEngine;

public partial class WorldBuilder
{
    public const float WellWaterRadius = 6f;
    public const float WatchtowerSlowRadius = 12f;
    public const float WatchtowerSlowFactor = 0.5f;
    public const float FenceProtectRadius = 3f;

    public bool IsBuildingStanding(BuildingState b)
    {
        if (b == null) return false;
        if (b.PartStates == null || b.PartStates.Count == 0)
            return b.CurrentHealth > 0;
        return b.DestroyedParts < b.TotalParts;
    }

    // Well: auto-water any crop within radius. Returns true if a standing well covers position.
    public bool IsCoveredByWell(Vector3 position)
    {
        foreach (var b in _buildings)
        {
            if (b.Type != "well" || !IsBuildingStanding(b)) continue;
            if (Vector3.Distance(b.Position, position) <= WellWaterRadius)
                return true;
        }
        return false;
    }

    // Watchtower: slow enemies within radius.
    public float WatchtowerSlowFactorAt(Vector3 position)
    {
        foreach (var b in _buildings)
        {
            if (b.Type != "watchtower" || !IsBuildingStanding(b)) continue;
            if (Vector3.Distance(b.Position, position) <= WatchtowerSlowRadius)
                return WatchtowerSlowFactor;
        }
        return 1f;
    }

    // Fence: nearest standing fence part entity protecting `position` from melee, or null.
    public GameObject FindFencePartToProtect(Vector3 position)
    {
        GameObject best = null;
        float bestDist = FenceProtectRadius;
        foreach (var b in _buildings)
        {
            if (b.Type != "fence" || !IsBuildingStanding(b)) continue;
            if (b.PartStates == null) continue;
            foreach (var ps in b.PartStates)
            {
                if (ps.Entity == null) continue;
                float d = Vector3.Distance(position, ps.Entity.transform.position);
                if (d <= bestDist)
                {
                    bestDist = d;
                    best = ps.Entity;
                }
            }
        }
        return best;
    }
}

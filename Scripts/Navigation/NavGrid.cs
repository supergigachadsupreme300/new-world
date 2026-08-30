using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Runtime navigation grid + A* used by story NPCs (e.g. the rich man) so they walk
/// around walls instead of clipping through them. Roads are weighted so routes follow
/// the street network; colliders hanging under a "Door" parent are ignored (doors can
/// be opened by the NPC when he reaches them).
/// </summary>
public class NavGrid : MonoBehaviour
{
    public static NavGrid Instance { get; private set; }

    private const float CellSize = 1f;
    private const float MinX = -8f;
    private const float MaxX = 90f;
    private const float MinZ = -20f;
    private const float MaxZ = 168f;

    // A surface whose top is up to this far above the natural ground is a normal
    // floor/deck/stoop you walk on. Higher slabs (2F floors, roofs) are ceilings.
    private const float StepMax = 0.65f;
    private const float GroundProbeTop = 12f;

    // Block a cell when a solid static object pokes more than this above the walk surface.
    // Mansion entrance steps are ~0.32 above dirt, so 0.36 lets the rich man climb them.
    private const float ObstacleMinAbove = 0.36f;

    private static readonly string[] SurfaceIgnoreKeywords =
    {
        "Table", "Chair", "Stool", "Sofa", "Couch", "Bed", "Mattress", "Pillow",
        "Blanket", "Rug", "Carpet", "Sink", "Bathtub", "Bath", "Cabinet", "Counter",
        "Desk", "Shelf", "Stove", "Oven", "Fridge", "Lamp", "Water", "Sea", "Sign",
        "Railing", "Fence", "Crop", "Field", "Cage", "Debris", "Crack", "Label", "Ghost"
    };
    private static readonly string[] CeilingIgnoreKeywords = { "Roof", "Ceiling" };
    private static readonly string[] RoadCostKeywords = { "Road" };
    // Furniture blocks cells even when its top is below the generic height threshold.
    private static readonly string[] AlwaysBlockKeywords =
    {
        "Sofa", "Couch", "Bed", "TableTop", "TableLeg", "Cabinet", "Desk", "Sink",
        "Bathtub", "Stove", "Oven", "Fridge", "Piano", "Statue", "Fountain"
    };

    private int _columns;
    private int _rows;
    private float _originX;
    private float _originZ;
    private Cell[] _cells;
    private bool _dirty = true;
    private bool _building;

    private const byte WalkableMask = 1;
    private const byte RoadMask = 2;

    private class Cell
    {
        public float Ground;
        public byte Flags;
    }

    private struct OpenNode
    {
        public int CellIndex;
        public float F;
    }

    public static void EnsureCreated()
    {
        if (Instance != null)
            return;
        var go = new GameObject("NavGrid");
        Instance = go.AddComponent<NavGrid>();
    }

    public void MarkDirty()
    {
        _dirty = true;
        if (!_building)
            StartCoroutine(RebuildCoroutine());
    }

    private System.Collections.IEnumerator RebuildCoroutine()
    {
        _building = true;
        // Wait a frame so freshly created colliders are registered by the physics engine.
        yield return null;
        do
        {
            _dirty = false;
            Rebuild();
        } while (_dirty);
        _building = false;
    }

    private void Rebuild()
    {
        _originX = MinX;
        _originZ = MinZ;
        _columns = Mathf.CeilToInt((MaxX - MinX) / CellSize);
        _rows = Mathf.CeilToInt((MaxZ - MinZ) / CellSize);

        if (_cells == null || _cells.Length != _columns * _rows)
            _cells = new Cell[_columns * _rows];

        for (int z = 0; z < _rows; z++)
        {
            for (int x = 0; x < _columns; x++)
            {
                int id = z * _columns + x;
                if (_cells[id] == null)
                    _cells[id] = new Cell();
                float cx = _originX + (x + 0.5f) * CellSize;
                float cz = _originZ + (z + 0.5f) * CellSize;
                AnalyzeCell(_cells[id], cx, cz);
            }
        }
    }

    private void AnalyzeCell(Cell cell, float cx, float cz)
    {
        cell.Flags = 0;
        cell.Ground = float.MinValue;

        RaycastHit[] hits = Physics.RaycastAll(
            new Vector3(cx, GroundProbeTop, cz), Vector3.down, GroundProbeTop + 2f,
            Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);

        if (hits.Length == 0)
            return;

        // Natural ground base: lowest solid surface near the floor band.
        float baseY = float.MaxValue;
        float surfaceY = float.MinValue;
        Collider surfaceCol = null;
        bool hasSurfaces = false;

        for (int i = 0; i < hits.Length; i++)
        {
            Collider col = hits[i].collider;
            if (!IsStaticBlocking(col))
                continue;
            string name = col.gameObject.name;
            if (NameContainsAny(name, SurfaceIgnoreKeywords)
                || NameContainsAny(name, CeilingIgnoreKeywords))
                continue;

            float top = col.bounds.max.y;
            if (top < baseY && top <= 1.5f)
                baseY = top;
        }
        if (baseY == float.MaxValue)
            return;

        for (int i = 0; i < hits.Length; i++)
        {
            Collider col = hits[i].collider;
            if (!IsStaticBlocking(col))
                continue;
            string name = col.gameObject.name;
            if (NameContainsAny(name, SurfaceIgnoreKeywords)
                || NameContainsAny(name, CeilingIgnoreKeywords))
                continue;

            float top = col.bounds.max.y;
            if (top >= baseY - 0.15f && top <= baseY + StepMax)
            {
                hasSurfaces = true;
                if (top > surfaceY)
                {
                    surfaceY = top;
                    surfaceCol = col;
                }
            }
        }

        float ground = hasSurfaces ? surfaceY : baseY;
        if (ground < -0.5f)
            return;

        cell.Ground = ground;

        // Obstacle probe at waist height: anything solid that pokes up above the walk
        // surface by more than a small step blocks the cell.
        // Probe the FULL cell so colliders whose centerline sits exactly on a grid
        // line (many mansion walls) are still detected. Half-extents of exactly
        // CellSize*0.5 means every boundary belongs to both neighbours, so nothing
        // can fall between two probes.
        Vector3 probeCenter = new Vector3(cx, ground + 0.9f, cz);
        Collider[] blockers = Physics.OverlapBox(probeCenter,
            new Vector3(CellSize * 0.5f, 0.5f, CellSize * 0.5f), Quaternion.identity,
            Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);

        bool blocked = false;
        for (int i = 0; i < blockers.Length; i++)
        {
            Collider col = blockers[i];
            if (!IsStaticBlocking(col))
                continue;
            string colName = col.gameObject.name;
            if (NameContainsAny(colName, AlwaysBlockKeywords) && col.bounds.max.y >= ground + 0.1f)
            {
                blocked = true;
                break;
            }
            if (col.bounds.max.y >= ground + ObstacleMinAbove)
            {
                blocked = true;
                break;
            }
        }
        if (blocked)
            return;

        cell.Flags |= WalkableMask;
        if (surfaceCol != null)
        {
            string surfaceName = surfaceCol.gameObject.name;
            if (NameContainsAny(surfaceName, RoadCostKeywords))
                cell.Flags |= RoadMask;
        }
    }

    private static bool IsStaticBlocking(Collider col)
    {
        if (col == null) return false;
        if (col.isTrigger) return false;
        if (col is CharacterController) return false;
        if (col.attachedRigidbody != null) return false;
        if (col.GetComponentInParent<PlayerController>() != null) return false;
        // Doors are handled by the NPC door-opening logic, not the grid.
        Transform t = col.transform;
        while (t != null)
        {
            if (t.name == "Door")
                return false;
            t = t.parent;
        }
        return true;
    }

    /// <summary>The NPC movement guard reuses the same collider rules as the grid.</summary>
    public static bool IsWall(Collider col)
        => IsStaticBlocking(col);

    /// <summary>
    /// True if a solid wall exists near the given position. Used by NPC movement as a
    /// final guarantee they never cross a wall even if a path is stale (slide-to-avoid).
    /// </summary>
    public static bool IsBlockedAt(Vector3 pos, float radius)
    {
        float ground = Instance != null ? Instance.SampleGroundY(pos) : pos.y - 0.86f;
        Collider[] cols = Physics.OverlapSphere(pos, radius,
            Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < cols.Length; i++)
        {
            Collider col = cols[i];
            if (!IsStaticBlocking(col))
                continue;
            if (col.bounds.max.y >= ground + 0.46f)
                return true;
        }
        return false;
    }

    private static bool NameContainsAny(string name, string[] keywords)
    {
        for (int i = 0; i < keywords.Length; i++)
        {
            if (name.Contains(keywords[i]))
                return true;
        }
        return false;
    }

    private CellInfo IndexAt(float wx, float wz)
    {
        int x = Mathf.FloorToInt((wx - _originX) / CellSize);
        int z = Mathf.FloorToInt((wz - _originZ) / CellSize);
        if (x < 0 || z < 0 || x >= _columns || z >= _rows)
            return default;
        return new CellInfo { X = x, Z = z, Index = z * _columns + x, Cell = _cells[z * _columns + x] };
    }

    private struct CellInfo
    {
        public int X;
        public int Z;
        public int Index;
        public Cell Cell;
    }

    /// <summary>Walk surface height (floor/deck/ground top) at a world position.</summary>
    public float SampleGroundY(Vector3 pos)
    {
        var info = IndexAt(pos.x, pos.z);
        if (info.Cell == null)
            return pos.y;
        float ground = info.Cell.Ground;
        return ground > float.MinValue && ground > -0.5f ? ground : pos.y;
    }

    /// <summary>True if the exact cell under a world position is walkable.</summary>
    public bool IsWalkableAt(Vector3 pos)
    {
        var info = IndexAt(pos.x, pos.z);
        return info.Cell != null && (info.Cell.Flags & WalkableMask) != 0;
    }

    /// <summary>Nearest walkable world point to a given position (radius-limited search).</summary>
    public bool NearestWalkable(Vector3 pos, out Vector3 result)
    {
        result = pos;
        var info = IndexAt(pos.x, pos.z);
        if (info.Cell != null && (info.Cell.Flags & WalkableMask) != 0)
            return true;

        int maxRing = 10;
        for (int ring = 1; ring <= maxRing; ring++)
        {
            for (int dz = -ring; dz <= ring; dz++)
            {
                for (int dx = -ring; dx <= ring; dx++)
                {
                    if (Mathf.Abs(dx) != ring && Mathf.Abs(dz) != ring)
                        continue;
                    int nx = info.X + dx;
                    int nz = info.Z + dz;
                    if (nx < 0 || nz < 0 || nx >= _columns || nz >= _rows)
                        continue;
                    Cell c = _cells[nz * _columns + nx];
                    if ((c.Flags & WalkableMask) == 0)
                        continue;
                    result = new Vector3(_originX + (nx + 0.5f) * CellSize, c.Ground + 1f, _originZ + (nz + 0.5f) * CellSize);
                    return true;
                }
            }
        }
        return false;
    }

    /// <summary>
    /// A* path from start to end over the walkable grid.
    /// Returns waypoints as world positions (cell centers, smoothed), or false if unreachable.
    /// </summary>
    public bool FindPath(Vector3 start, Vector3 end, List<Vector3> outPath)
    {
        outPath?.Clear();
        if (_dirty)
            Rebuild();

        var startInfo = IndexAt(start.x, start.z);
        var endInfo = IndexAt(end.x, end.z);
        if (startInfo.Cell == null || endInfo.Cell == null)
        {
            // Fall back to nearest walkable rings.
            if (!NearestWalkable(start, out _)) return false;
            if (!NearestWalkable(end, out _)) return false;
            startInfo = IndexAt(start.x, start.z);
            endInfo = IndexAt(end.x, end.z);
            if (startInfo.Cell == null || endInfo.Cell == null) return false;
        }
        if ((startInfo.Cell.Flags & WalkableMask) == 0 || (endInfo.Cell.Flags & WalkableMask) == 0)
            return false;
        if (startInfo.Index == endInfo.Index)
        {
            outPath?.Add(new Vector3(end.x, endInfo.Cell.Ground, end.z));
            return true;
        }

        // A* state (reused arrays sized to the grid).
        EnsureSearchBuffers();
        int total = _cells.Length;
        for (int i = 0; i < total; i++)
        {
            _gScore[i] = float.MaxValue;
            _fScore[i] = float.MaxValue;
            _cameFrom[i] = -1;
            _closed[i] = false;
            _inOpen[i] = false;
        }

        _gScore[startInfo.Index] = 0f;
        float startH = Heuristic(startInfo.X, startInfo.Z, endInfo.X, endInfo.Z);
        _fScore[startInfo.Index] = startH;

        var open = new Heap();
        open.Push(new OpenNode { CellIndex = startInfo.Index, F = startH });
        _inOpen[startInfo.Index] = true;

        int current = -1;
        while (open.Count > 0)
        {
            current = open.Pop().CellIndex;
            _inOpen[current] = false;
            if (_closed[current])
                continue;
            _closed[current] = true;

            if (current == endInfo.Index)
                break;
            if (_gScore[current] >= 2000f)
                continue;

            int cx = current % _columns;
            int cz = current / _columns;
            for (int d = 0; d < 8; d++)
            {
                int nx = cx + DirX[d];
                int nz = cz + DirZ[d];
                if (nx < 0 || nz < 0 || nx >= _columns || nz >= _rows)
                    continue;
                int nid = nz * _columns + nx;
                if ((_cells[nid].Flags & WalkableMask) == 0)
                    continue;

                float stepCost = (DirX[d] != 0 && DirZ[d] != 0) ? 1.414f : 1f;
                float terrainCost = (_cells[nid].Flags & RoadMask) != 0 ? 0.3f : 1f;
                float tentative = _gScore[current] + stepCost * terrainCost;
                if (tentative >= _gScore[nid])
                    continue;

                _gScore[nid] = tentative;
                _fScore[nid] = tentative + Heuristic(nx, nz, endInfo.X, endInfo.Z);
                _cameFrom[nid] = current;
                if (!_inOpen[nid])
                {
                    open.Push(new OpenNode { CellIndex = nid, F = _fScore[nid] });
                    _inOpen[nid] = true;
                }
            }
        }

        if (current != endInfo.Index)
            return false;

        // Reconstruct cell path.
        var indices = new List<int>();
        int walk = endInfo.Index;
        int guard = 0;
        while (walk != startInfo.Index && walk != -1 && guard++ < 100000)
        {
            indices.Add(walk);
            walk = _cameFrom[walk];
        }
        indices.Add(startInfo.Index);
        indices.Reverse();

        // Convert to world positions (cell centers) and smooth collinear waypoints.
        var raw = new List<Vector3>(indices.Count);
        for (int i = 0; i < indices.Count; i++)
        {
            int id = indices[i];
            int ix = id % _columns;
            int iz = id / _columns;
            raw.Add(new Vector3(_originX + (ix + 0.5f) * CellSize, _cells[id].Ground + 1f, _originZ + (iz + 0.5f) * CellSize));
        }

        if (outPath == null)
            return true;
        outPath.Clear();
        for (int i = 0; i < raw.Count - 1; i++)
        {
            Vector3 a = raw[i];
            Vector3 b = raw[i + 1];
            bool collinear = i + 2 < raw.Count && SameAxis(a, b, raw[i + 2]);
            if (!collinear)
                outPath.Add(a);
        }
        outPath.Add(raw[raw.Count - 1]);
        return true;
    }

    private static bool SameAxis(Vector3 a, Vector3 b, Vector3 c)
    {
        bool sameX = Mathf.Approximately(a.x, b.x) && Mathf.Approximately(b.x, c.x);
        bool sameZ = Mathf.Approximately(a.z, b.z) && Mathf.Approximately(b.z, c.z);
        return sameX || sameZ;
    }

    private float Heuristic(int x, int z, int ex, int ez)
    {
        float dx = Mathf.Abs(x - ex);
        float dz = Mathf.Abs(z - ez);
        return Mathf.Max(dx, dz) + (Mathf.Min(dx, dz) * 0.414f);
    }

    private static readonly int[] DirX = { 0, 1, 0, -1, 1, 1, -1, -1 };
    private static readonly int[] DirZ = { 1, 0, -1, 0, 1, -1, 1, -1 };

    private float[] _gScore;
    private float[] _fScore;
    private int[] _cameFrom;
    private bool[] _closed;
    private bool[] _inOpen;

    private void EnsureSearchBuffers()
    {
        int total = _cells.Length;
        if (_gScore == null || _gScore.Length != total)
        {
            _gScore = new float[total];
            _fScore = new float[total];
            _cameFrom = new int[total];
            _closed = new bool[total];
            _inOpen = new bool[total];
        }
    }

    private sealed class Heap
    {
        private readonly List<OpenNode> _items = new List<OpenNode>();
        public int Count => _items.Count;

        public void Push(OpenNode node)
        {
            _items.Add(node);
            int i = _items.Count - 1;
            while (i > 0)
            {
                int parent = (i - 1) / 2;
                if (_items[parent].F <= _items[i].F)
                    break;
                (_items[parent], _items[i]) = (_items[i], _items[parent]);
                i = parent;
            }
        }

        public OpenNode Pop()
        {
            var top = _items[0];
            int last = _items.Count - 1;
            _items[0] = _items[last];
            _items.RemoveAt(last);
            int i = 0;
            while (true)
            {
                int l = i * 2 + 1;
                int r = i * 2 + 2;
                int smallest = i;
                if (l < _items.Count && _items[l].F < _items[smallest].F)
                    smallest = l;
                if (r < _items.Count && _items[r].F < _items[smallest].F)
                    smallest = r;
                if (smallest == i)
                    break;
                (_items[smallest], _items[i]) = (_items[i], _items[smallest]);
                i = smallest;
            }
            return top;
        }
    }
}
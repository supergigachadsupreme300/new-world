using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Runtime MonoBehaviour for a single loaded terrain chunk. Owns the merged
/// heightmap mesh, its single derived collider, and the chunk's props
/// (trees/rocks). One ChunkObject per TerrainChunkCoord replaces the old 900
/// per-tile objects — physics, draw calls and scene-graph cost drop ~1000x.
/// Tile heightmaps stay addressable through WorldStreamer._loadedData /
/// ChunkSaveManager; no per-tile GameObject is created anymore.
/// </summary>
[RequireComponent(typeof(MeshFilter))]
[RequireComponent(typeof(MeshRenderer))]
[RequireComponent(typeof(MeshCollider))]
public class ChunkObject : MonoBehaviour
{
    public TerrainChunkCoord ChunkCoord { get; private set; }

    private readonly List<GameObject> _props = new List<GameObject>();

    private MeshFilter _mf;
    private MeshRenderer _mr;
    private MeshCollider _mc;

    // CPU-side copy of the merged chunk mesh arrays, kept so terrain deformation can patch only
    // the touched tiles' vertices without re-running a full 900-tile rebuild.
    private MergedChunkMeshData _merged;

    // Incremental prop spawning (one deterministic Random per chunk, spread over ticks).
    private long _propSeed;
    private System.Random _propRng;
    private int[] _propTiles;
    private int _propCursor;

    /// <summary>Nature-prop spawn odds per tile: 1-in-<see cref="PropSpawnOdds"/> for BOTH trees and
    /// rocks. Was 200 (1/200 each) until `1dm` cut the ratio to a fifth → 1-in-1000, so a chunk
    /// (~900 tiles) now averages ~2 cube-heavy props instead of ~9.</summary>
    private const int PropSpawnOdds = 1000;

    private void Awake()
    {
        _mf = GetComponent<MeshFilter>();
        _mr = GetComponent<MeshRenderer>();
        _mc = GetComponent<MeshCollider>();
    }

    public void Init(TerrainChunkCoord chunkCoord)
    {
        ChunkCoord = chunkCoord;
        name = $"TerrainChunk_{chunkCoord.X}_{chunkCoord.Z}";
    }

    /// <summary>
    /// Applies the pre-built merged chunk mesh arrays to this object. Main thread only.
    /// </summary>
    public void ApplyMerged(MergedChunkMeshData md, Material material, bool buildCollider = true)
    {
        _merged = md;
        Mesh mesh = ChunkMeshGenerator.CreateMeshFromMerged(md, $"ChunkMesh_{ChunkCoord.X}_{ChunkCoord.Z}");

        // Point the filter/collider at the new mesh BEFORE destroying the old one so neither ever
        // references a destroyed mesh (the collider is queried every physics step).
        Mesh previous = _mf != null ? _mf.sharedMesh : null;
        if (_mf != null)
            _mf.sharedMesh = mesh;

        if (_mr != null && material != null)
            _mr.sharedMaterial = material;

        if (buildCollider && _mc != null)
            _mc.sharedMesh = mesh;

        if (previous != null && previous != mesh)
            Destroy(previous);
    }

    /// <summary>
    /// Replaces the mesh quads of the given local-tile rectangle in place (deformation). The
    /// region tiles are pre-built (BuildMeshData) for local coords minX..maxX / minZ..maxZ and
    /// must cover the whole rectangle (including untouched neighbours, so shared corners line up).
    /// Only the touched region is rebuilt + re-uploaded; the collider is re-cooked once.
    /// </summary>
    public void PatchRegion(int localMinX, int localMinZ, int localMaxX, int localMaxZ, ChunkMeshData[] region, long seed)
    {
        if (_merged.Vertices == null || _mf == null)
            return; // mesh not present — restore via full rebuild path

        Mesh mesh = _mf.sharedMesh;
        if (mesh == null)
            return;

        int cs = TerrainChunkCoord.ChunkSize;
        int w = localMaxX - localMinX + 1;
        int h = localMaxZ - localMinZ + 1;
        float minY = float.MaxValue;
        float maxY = float.MinValue;

        // The color channel tracks the strata bands per corner; lazily back-fill it so a patched
        // region always has a writable array even if a mesh was built without one.
        if (_merged.Colors == null)
        {
            _merged.Colors = new Color[_merged.Vertices.Length];
            for (int i = 0; i < _merged.Colors.Length; i++)
                _merged.Colors[i] = ColorPalette.GrassGreen;
        }

        for (int lz = localMinZ; lz <= localMaxZ; lz++)
        {
            for (int lx = localMinX; lx <= localMaxX; lx++)
            {
                ChunkMeshData tile = region[(lz - localMinZ) * w + (lx - localMinX)];
                int baseIndex = (lz * cs + lx) * 4;
                Vector3 offset = new Vector3(lx, 0f, lz);
                for (int k = 0; k < 4; k++)
                {
                    Vector3 p = tile.Vertices[k] + offset;
                    p.y = ChunkMeshGenerator.SanitizeHeight(p.y);
                    int v = baseIndex + k;
                    _merged.Vertices[v] = p;
                    if (k < tile.UV.Length) _merged.UV[v] = tile.UV[k];
                    if (k < tile.Normals.Length) _merged.Normals[v] = tile.Normals[k];
                    int wx, wz;
                    switch (k)
                    {
                        case 0: wx = tile.Coord.X; wz = tile.Coord.Z + 1; break;      // NW
                        case 1: wx = tile.Coord.X + 1; wz = tile.Coord.Z + 1; break;  // NE
                        case 2: wx = tile.Coord.X + 1; wz = tile.Coord.Z; break;      // SE
                        default: wx = tile.Coord.X; wz = tile.Coord.Z; break;         // SW
                    }
                    _merged.Colors[v] = ChunkMeshGenerator.TerrainBandColor(seed, wx, wz, p.y);
                }
            }
        }

        // Bounds from the full CPU vertex array (cheap, 3600 scans).
        for (int i = 0; i < _merged.Vertices.Length; i++)
        {
            float y = _merged.Vertices[i].y;
            if (y < minY) minY = y;
            if (y > maxY) maxY = y;
        }
        float span = cs * ChunkData.Size;
        _merged.Bounds = new Bounds(
            new Vector3(span * 0.5f, minY < maxY ? (minY + maxY) * 0.5f : minY, span * 0.5f),
            new Vector3(span, Mathf.Max(0.1f, (maxY - minY) + 0.1f), span));

        // Re-upload the modified channels once.
        mesh.SetVertices(_merged.Vertices);
        mesh.SetNormals(_merged.Normals);
        mesh.SetUVs(0, _merged.UV);
        if (_merged.Colors != null)
            mesh.SetColors(_merged.Colors);
        mesh.bounds = _merged.Bounds;

        // Force the collider to re-cook against the new heights. The null→assign pair runs inside a
        // single synchronous call, so no physics step ever observes the null collider (Unity only
        // re-cooks when the mesh reference actually changes).
        if (_mc != null)
        {
            _mc.sharedMesh = null;
            _mc.sharedMesh = mesh;
        }
    }

    /// <summary>True while this chunk's prop stream has been queued (inside the prop ring).
    /// False both before the ring reaches it and after the ring drops its props.</summary>
    public bool PropsOn => _propRng != null;

    /// <summary>True while this chunk still has prop tiles waiting to spawn.</summary>
    public bool PropsPending => PropsOn && _propTiles != null && _propCursor < _propTiles.Length;

    /// <summary>
    /// Queue the chunk's props (trees/rocks) for incremental spawning. One deterministic Random
    /// stream per chunk (previously 900 per-tile Random allocations). No-op when the props are
    /// already queued, so the prop ring re-entry path can call it idempotently.
    /// </summary>
    public void BeginProps(long seed)
    {
        if (PropsOn)
            return;
        _propSeed = seed;
        int cs = TerrainChunkCoord.ChunkSize;
        _propRng = new System.Random(seed.GetHashCode() ^ (ChunkCoord.X * 73856093) ^ (ChunkCoord.Z * 19349663));
        if (_propTiles == null || _propTiles.Length != cs * cs)
            _propTiles = new int[cs * cs];
        int i = 0;
        for (int z = 0; z < cs; z++)
            for (int x = 0; x < cs; x++)
                _propTiles[i++] = z * cs + x;
        _propCursor = 0;
    }

    /// <summary>
    /// Spawn props for up to <paramref name="budget"/> more tiles. Returns tiles consumed
    /// (caller shares a global budget across pending chunks). Deterministic across steps.
    /// </summary>
    public int StepProps(int budget)
    {
        if (_propRng == null || _propTiles == null) return 0;
        int cs = TerrainChunkCoord.ChunkSize;
        int consumed = 0;
        while (consumed < budget && _propCursor < _propTiles.Length)
        {
            int idx = _propTiles[_propCursor++];
            int tileX = ChunkCoord.X * cs + (idx % cs);
            int tileZ = ChunkCoord.Z * cs + (idx / cs);

            // Nature props (trees/rocks) spawn on every tile — the test ground no longer carves
            // or suppresses anything, so the procedural world is left exactly as generated. Each
            // 1-in-N roll is 1-in-1000 per tile since `1dm` (tree AND rock cut to a fifth of the
            // original 1/200 density).
            if (_propRng.Next(PropSpawnOdds) == 0)
                SpawnTree(_propSeed, tileX, tileZ, _propRng);
            if (_propRng.Next(PropSpawnOdds) == 0)
                SpawnRock(_propSeed, tileX, tileZ, _propRng);
            consumed++;
        }
        return consumed;
    }

    private void SpawnTree(long seed, int tileX, int tileZ, System.Random rng)
    {
        float localX = (float)rng.NextDouble();
        float localZ = (float)rng.NextDouble();
        float worldX = tileX + localX;
        float worldZ = tileZ + localZ;
        float worldY = TerrainNoiseGenerator.GetHeight(seed, worldX, worldZ);
        var tree = MapBuilder.BuildTree(transform, new Vector3(worldX, worldY, worldZ));
        tree.name = $"Tree_{tileX}_{tileZ}";
        _props.Add(tree);
    }

    private void SpawnRock(long seed, int tileX, int tileZ, System.Random rng)
    {
        float localX = (float)rng.NextDouble();
        float localZ = (float)rng.NextDouble();
        float worldX = tileX + localX;
        float worldZ = tileZ + localZ;
        float worldY = TerrainNoiseGenerator.GetHeight(seed, worldX, worldZ);
        var rock = MapBuilder.BuildStone(transform, new Vector3(worldX, worldY, worldZ));
        rock.name = $"Rock_{tileX}_{tileZ}";
        _props.Add(rock);
    }

    /// <summary>
    /// Destroys this chunk's spawned props and drops the pending stream state. The merged terrain
    /// mesh + collider are untouched, so the chunk stays rendered and collidable. Used by the prop
    /// ring (1di): a chunk keeps its ground but loses its trees/rocks once it falls outside the
    /// prop radius; re-entering the ring restarts the same deterministic stream via BeginProps.
    /// </summary>
    public void ReleaseProps()
    {
        for (int i = _props.Count - 1; i >= 0; i--)
        {
            if (_props[i] != null)
                Destroy(_props[i]);
        }
        _props.Clear();
        _propRng = null;
        _propCursor = 0;
    }

    public void Release()
    {
        ReleaseProps();
        _merged = default;

        if (_mf != null && _mf.sharedMesh != null)
            Destroy(_mf.sharedMesh);
        if (_mc != null)
            _mc.sharedMesh = null;
    }
}
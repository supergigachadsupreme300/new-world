using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Runtime MonoBehaviour for a single loaded chunk. Holds the generated mesh,
/// derived collider, and the source data so modifications can be persisted.
/// Each chunk owns its own prop list (trees/rocks) — no global tracking needed.
/// </summary>
[RequireComponent(typeof(MeshFilter))]
[RequireComponent(typeof(MeshRenderer))]
[RequireComponent(typeof(MeshCollider))]
public class ChunkObject : MonoBehaviour
{
    public ChunkCoord Coord { get; private set; }

    private readonly List<GameObject> _props = new List<GameObject>();

    private MeshFilter _mf;
    private MeshRenderer _mr;
    private MeshCollider _mc;

    private void Awake()
    {
        _mf = GetComponent<MeshFilter>();
        _mr = GetComponent<MeshRenderer>();
        _mc = GetComponent<MeshCollider>();
    }

    public void Init(ChunkCoord coord)
    {
        Coord = coord;
        name = $"Tile_{coord.X}_{coord.Z}";
    }

    /// <summary>
    /// Applies pre-built mesh data arrays to this chunk. Main thread only.
    /// Creates a Unity Mesh from the thread-safe arrays and assigns components.
    /// </summary>
    public void ApplyMeshData(ChunkMeshData md, Material material, bool buildCollider = true)
    {
        Mesh mesh = ChunkMeshGenerator.CreateMeshFromData(md);

        if (_mf != null)
        {
            if (_mf.sharedMesh != null)
                Destroy(_mf.sharedMesh);
            _mf.sharedMesh = mesh;
        }

        if (_mr != null && material != null)
            _mr.sharedMaterial = material;

        if (buildCollider && _mc != null)
            _mc.sharedMesh = mesh;
    }

    /// <summary>
    /// Applies serialized ChunkData to this object: rebuilds the mesh and
    /// collider. Synchronous fallback path — builds mesh on main thread.
    /// </summary>
    public void Apply(ChunkData data, NoiseLayerConfig[] layers, Material material, bool buildCollider = true)
    {
        Mesh mesh = ChunkMeshGenerator.BuildMesh(data, layers);

        if (_mf != null)
        {
            if (_mf.sharedMesh != null)
                Destroy(_mf.sharedMesh);
            _mf.sharedMesh = mesh;
        }

        if (_mr != null && material != null)
            _mr.sharedMaterial = material;

        if (buildCollider && _mc != null)
            _mc.sharedMesh = mesh;
    }

    public void SpawnProps(long seed)
    {
        var rng = new System.Random(seed.GetHashCode() ^ (Coord.X * 73856093) ^ (Coord.Z * 19349663));

        if (rng.Next(4) == 0)
            SpawnTree(seed, rng);

        if (rng.Next(6) == 0)
            SpawnRock(seed, rng);
    }

    private void SpawnTree(long seed, System.Random rng)
    {
        float localX = (float)rng.NextDouble();
        float localZ = (float)rng.NextDouble();
        float worldX = Coord.X + localX;
        float worldZ = Coord.Z + localZ;
        float worldY = TerrainNoiseGenerator.GetHeight(seed, worldX, worldZ);
        var tree = MapBuilder.BuildTree(transform, new Vector3(localX, worldY, localZ));
        tree.name = $"Tree_{Coord.X}_{Coord.Z}";
        _props.Add(tree);
    }

    private void SpawnRock(long seed, System.Random rng)
    {
        float localX = (float)rng.NextDouble();
        float localZ = (float)rng.NextDouble();
        float worldX = Coord.X + localX;
        float worldZ = Coord.Z + localZ;
        float worldY = TerrainNoiseGenerator.GetHeight(seed, worldX, worldZ);
        var rock = MapBuilder.BuildStone(transform, new Vector3(localX, worldY, localZ));
        rock.name = $"Rock_{Coord.X}_{Coord.Z}";
        _props.Add(rock);
    }

    public ChunkData CaptureData()
    {
        ChunkData data = new ChunkData(Coord.X, Coord.Z, 0) { Heights = new float[ChunkData.VertexCount] };
        return data;
    }

    public void Release()
    {
        for (int i = _props.Count - 1; i >= 0; i--)
        {
            if (_props[i] != null)
                Destroy(_props[i]);
        }
        _props.Clear();

        if (_mf != null && _mf.sharedMesh != null)
            Destroy(_mf.sharedMesh);
        if (_mc != null)
            _mc.sharedMesh = null;
    }
}

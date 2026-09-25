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

    // Collider-on-demand (1dq): only chunks near the player or near active magic carry a
    // MeshCollider. The far radius-N world still renders its full meshes; its physics load
    // (collider cooks + ~7k-tri broadphase bodies) is gated to what the gameplay uses.
    private bool _colliderActive;

    /// <summary>True while this chunk's MeshCollider is assigned (near the player or magic, 1dq).</summary>
    public bool HasCollider => _colliderActive;

    /// <summary>
    /// True while the chunk is DORMANT (hidden-but-retained, 1gc): the streamer demoted it out of the
    /// loaded ring but keeps its data/mesh/GameObject alive so re-entry wakes it instantly.
    /// ChunkLodManager skips dormant entries so the band sweep can never re-enable a hidden chunk's
    /// visuals during the timer-gated unregister gap.
    /// </summary>
    [System.NonSerialized] public bool Dormant;

    /// <summary>
    /// Globally-unique counter bumped on EVERY mesh apply/upload (ApplyMerged + PatchRegion, 1gd).
    /// A background seam-rebuild result captures this stamp when it is dispatched and the streamer's
    /// DrainRebuildResults rejects any finished result whose stamp no longer matches — so a stale
    /// async upload can never overwrite a newer edit or a chunk that unloaded and reloaded anew.
    /// Values come from the shared monotonic counter (<see cref="NextMeshRebuildStamp"/>), not a
    /// per-object increment: a per-object counter restarted at 0 on every chunk lifecycle, so a
    /// reloaded chunk could re-issue a stamp a stale in-flight result already held and slip past the
    /// drains check to overwrite fresh geometry.
    /// </summary>
    [System.NonSerialized] public int MeshRebuildStamp;

    /// <summary>Shared monotonic source for <see cref="MeshRebuildStamp"/> (main thread only).</summary>
    private static int _meshRebuildStampCounter;

    private static int NextMeshRebuildStamp()
    {
        return ++_meshRebuildStampCounter;
    }

    /// <summary>
    /// Toggle the chunk's physics collider without touching the mesh or re-running the merged
    /// builder (1dq). Enabling cooks the cached collider once; disabling drops it to zero physics.
    /// </summary>
    public void SetColliderActive(bool active)
    {
        if (_colliderActive == active)
            return;
        _colliderActive = active;
        RefreshCollider(active);
    }

    /// <summary>
    /// Assignment point for the chunk's MeshCollider (1hi). Smooth chunks cook a DECIMATED lattice
    /// instead of the full render mesh — 2 m by default, or the chunk's OWN low-poly facet step
    /// (1hi.1 <see cref="ColliderStep"/>, so you stand exactly on the visible 3 m facets) — so the
    /// per-enable PhysX cook on the gameplay frame is ~4x cheaper; the lattice shares the EXACT
    /// world corners the LOD children (and neighbour chunks) use, so the physics surface is
    /// seam-proof across chunks by construction. Voxel mode (already chunky 1 m columns) keeps the
    /// render mesh as its collider, unchanged from pre-1hi. <paramref name="md"/> carries
    /// worker-thread-built collider arrays on the merge path (0 main-thread build); enabling a
    /// collider later (1dq collider ring, 1gg exempt cooks) derives them here — 256 verts, once per
    /// enable. Disabling nulls the collider (zero physics) but KEEPS the pooled collider mesh, so a
    /// re-enable re-cooks the same instance.
    /// </summary>
    private void RefreshCollider(bool active, MergedChunkMeshData md = default)
    {
        if (_mc == null)
            return;
        if (!active)
        {
            _mc.sharedMesh = null;
            return;
        }
        if (VoxelMesh)
        {
            _mc.sharedMesh = null;
            _mc.sharedMesh = _mf != null ? _mf.sharedMesh : null;
            return;
        }
        if (_colliderMesh == null)
        {
            _colliderMesh = ChunkMeshGenerator.AcquireChunkMesh($"ColliderMesh_{ChunkCoord.X}_{ChunkCoord.Z}");
            if (md.ColliderVertices != null)
                UploadCollider(md.ColliderVertices, md.ColliderTriangles);
            else if (_merged.Corners.Y != null)
            {
                ChunkMeshGenerator.BuildDecimatedCollider(_merged.Corners, ColliderStep,
                    out Vector3[] cv, out int[] ct);
                UploadCollider(cv, ct);
            }
        }
        else if (md.ColliderVertices != null)
        {
            // A rebuild (FullRebuildChunk / 1ea save-scan) re-applied FRESH merged arrays while the
            // chunk already had a collider — always re-upload, or the cooked surface silently stays
            // the pre-rebuild geometry until the next patch/Deform.
            UploadCollider(md.ColliderVertices, md.ColliderTriangles);
        }
        _mc.sharedMesh = null;
        _mc.sharedMesh = _colliderMesh;
    }

    /// <summary>Uploads the decimated collider arrays into <see cref="_colliderMesh"/> (overwrite-only
    /// reuse, same discipline as UploadMerged: the clear handles the vertex-count change) — then
    /// UploadMeshData(false) publishes once instead of per-setter dirty passes. Main thread only.</summary>
    private void UploadCollider(Vector3[] vertices, int[] triangles)
    {
        if (_colliderMesh.vertexCount != vertices.Length)
            _colliderMesh.Clear();
        _colliderMesh.SetVertices(vertices);
        _colliderMesh.SetTriangles(triangles, 0);
        _colliderMesh.RecalculateBounds();
        _colliderMesh.UploadMeshData(false);
    }

    /// <summary>Re-cooks the collider against the CURRENT lattice after a patch (1hi). The smooth
    /// path re-derives the decimated surface from <see cref="_merged.Corners"/> (PatchCornerGrid just
    /// re-stamped it), so the physics surface tracks every excavation; 256 verts, main thread, safe
    /// per patch. The null→assign pair runs inside this single synchronous call, so no physics step
    /// ever observes the null collider. No-op for collider-less chunks (1dq) and voxel mode never
    /// reaches here.</summary>
    private void RebuildColliderSurface()
    {
        if (_mc == null || _colliderMesh == null)
            return;
        if (_merged.Corners.Y != null)
        {
            ChunkMeshGenerator.BuildDecimatedCollider(_merged.Corners, ColliderStep,
                out Vector3[] cv, out int[] ct);
            UploadCollider(cv, ct);
        }
        _mc.sharedMesh = null;
        _mc.sharedMesh = _colliderMesh;
    }

    /// <summary>
    /// Show/hide the chunk's visuals (root merged mesh + LOD children) without releasing anything
    /// (1gc dormant keep-ring): a dormant chunk keeps its pooled mesh, tile data and LOD children so
    /// a wake is an instant re-show. Does NOT touch the collider (the streamer's collider ring owns
    /// that) or the props (the prop ring owns those) — its mirror image is the single merged surface.
    /// </summary>
    public void SetVisualActive(bool active)
    {
        if (_mr != null)
            _mr.enabled = active;
        if (_lod1Go != null)
            _lod1Go.SetActive(active);
        if (_lod2Go != null)
            _lod2Go.SetActive(active);
    }

    // CPU-side copy of the merged chunk mesh arrays, kept so terrain deformation can patch only
    // the touched tiles' vertices without re-running a full 900-tile rebuild.
    private MergedChunkMeshData _merged;

    // One pooled Mesh for this chunk's whole life (1dv): acquired from the freed-mesh pool on the
    // first ApplyMerged, then re-uploaded in place on every rebuild — no new+Destroy churn and no
    // transient double GPU buffer. Returned to the pool on Release() for the next chunk to reuse.
    private Mesh _mesh;

    // (1hi) One pooled Mesh for this chunk's DECIMATED collider surface (smooth path only). The
    // MeshCollider cooks this ~256-vert / ~450-tri lattice (every 2nd node of the 31x31 corner grid)
    // instead of the full render surface, so the gameplay-frame per-enable PhysX cook is ~4x cheaper
    // — the MeshCollider only needs a surface the player stands on. Lazily acquired on first enable
    // (a chunk that never enters the collider ring allocates nothing), retained across en/disables,
    // and returned to the pool on Release — the same pool/discipline as _mesh (1dv). Voxel mode
    // never uses it (its chunky 1 m render columns stay the collider). It is a SEPARATE input mesh,
    // so its vertex indices bear no relation to the render mesh's.
    private Mesh _colliderMesh;
    private const int ColliderDecimation = ChunkMeshGenerator.ChunkColliderDecimation;

    // (1hi.1) Coarse facet step this chunk's ROOT mesh was built at (0 = full-resolution 1 m per-tile
    // surface — the smooth look). Set from MergedChunkMeshData.LowPolyStep on every apply; drives
    // both the low-poly PatchRegion re-sample (the root is re-emitted from the restamped lattice
    // instead of a per-tile skim) and the collider decimation, so physics always rides the SAME step
    // as the visible facets.
    private int _meshStep;

    /// <summary>Collider decimation step for THIS chunk (1hi.1): the low-poly root's own step when
    /// coarse (stand exactly on the visual facets), else the standard 2 m decimation.</summary>
    private int ColliderStep => _meshStep > 0 ? _meshStep : ColliderDecimation;

    // 1e6: LOD children. Each chunk builds two decimated grid meshes ("Lod1"/"Lod2" children, name
    // matched by ChunkLodManager's band DetailNames) sampled from its own merged top-terrain block,
    // so the far bands render ~1/4 ("Lod1", every 2nd tile) to ~1/9 ("Lod2", every 3rd tile) of the
    // full mesh. Built lazily and marked stale by every apply/patch — deformation never shows a hole
    // because a stale LOD is rebuilt before a band is switched onto it.
    private GameObject _lod1Go;
    private GameObject _lod2Go;
    private MeshFilter _lod1Mf;
    private MeshFilter _lod2Mf;
    private bool _lodDirty = true;

    /// <summary>True when either Lod child is stale (a merged apply/patch dirtied the surface and the
    /// decimated grids have not been rebuilt yet). ChunkLodManager polls this so a chunk already
    /// showing a detail band refreshes its active child after deformation without waiting for the
    /// next band change (1fz).</summary>
    public bool LodDirty => _lodDirty;

    /// <summary>
    /// True while this chunk renders a stepped voxel mesh (1et). Its LOD children (1eu) are
    /// decimated COLUMN samples of <see cref="VoxelStore"/> (Built via BuildVoxelLodChild) instead
    /// of the smooth TOPS-FIRST corner grid — see <see cref="RefreshLodMeshes"/>.
    /// </summary>
    public bool VoxelMesh;

    /// <summary>
    /// The live column-run store backing this chunk's stepped mesh (1eu). The streamer attaches it
    /// on build (BuildVoxelChunk) and clear-only rebuilds reuse it, so caves/overhangs carved by the
    /// volume ops survive rebuilds that merely overlay surface edits. Null for smooth chunks. The
    /// column run data is also the save source — the file is written from this store at flush.
    /// </summary>
    public VoxelChunkData VoxelStore;

    // Incremental prop spawning (one deterministic Random per chunk, spread over ticks).
    private long _propSeed;
    private System.Random _propRng;
    private int[] _propTiles;
    private int _propCursor;

    // Prop-ring keep-alive (1du): the RNG/tiles/cursor survive release so a chunk that slips out of
    // the prop ring and re-enters reactivates its SAME GameObjects instead of destroy/respawn
    // churn. _propActive mirrors exactly "props currently visible".
    private bool _propActive;

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
        _meshStep = md.LowPolyStep;
        // Pooled mesh (1dv): a chunk owns one Mesh instance. The first apply acquires it from the
        // freed-mesh pool (or allocates when the pool is empty); rebuilds re-upload into the SAME
        // instance, so FullRebuildChunk/etc. stop spinning new Mesh objects + Destroying the old.
        // Chunk meshes share a uniform vertex/index count (~961 verts / ~1800 tris), so a reused
        // buffer normally uploads as-is; a count change (slab side walls add verts) is handled by
        // UploadMerged clearing the mesh before re-specifying (1dv fix — Unity buffers never shrink).
        if (_mesh == null)
            _mesh = ChunkMeshGenerator.AcquireChunkMesh($"ChunkMesh_{ChunkCoord.X}_{ChunkCoord.Z}");
        ChunkMeshGenerator.UploadMerged(md, _mesh);

        // Point the filter/collider at the pooled mesh. The sharedMesh reference never changes after
        // the first apply, so the collider keeps cooking against the same instance across rebuilds.
        if (_mf != null)
            _mf.sharedMesh = _mesh;

        if (_mr != null && material != null)
            _mr.sharedMaterial = material;

        // Collider is assigned only for chunks the streamer has routed into the near ring (1dq).
        // Keeping the flag in sync means a later FullRebuildChunk preserves the intended state
        // and PatchRegion only re-cooks colliders that are actually live.
        // (1hi) Smooth chunks cook the DECIMATED collider lattice carried on md (worker-thread built,
        // ~4x cheaper than the render mesh); voxel mode keeps the render mesh as its collider.
        RefreshCollider(buildCollider, md);
        _colliderActive = buildCollider;

        // LOD children are stale after any apply; they rebuild lazily on the next band switch
        // (ApplyBand -> RefreshLodMeshes), so near-band chunks never pay for them.
        _lodDirty = true;

        // (1gd) Any in-flight async seam rebuild snapshot taken before this apply is now stale.
        MeshRebuildStamp = NextMeshRebuildStamp();
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

        // (1hi.1) Low-poly roots have NO per-tile blocks: the merged arrays are the whole
        // coarse facet surface (~121 quads at 3 m), so a patch cannot skim `count` vertices per
        // tile — it re-samples the whole root from the restamped lattice below (far cheaper than
        // the 1 m skim anyway).
        bool lowPoly = _meshStep > 0;

        // The color channel tracks the strata bands per corner; lazily back-fill it so a patched
        // region always has a writable array even if a mesh was built without one.
        if (_merged.Colors == null)
        {
            _merged.Colors = new Color[_merged.Vertices.Length];
            for (int i = 0; i < _merged.Colors.Length; i++)
                _merged.Colors[i] = ColorPalette.GrassGreen;
        }

        if (!lowPoly)
        {
            for (int lz = localMinZ; lz <= localMaxZ; lz++)
            {
                for (int lx = localMinX; lx <= localMaxX; lx++)
                {
                ChunkMeshData tile = region[(lz - localMinZ) * w + (lx - localMinX)];
                // 1ew: coarse quads and refined 2x2 blocks mix in one chunk mesh, so the block's
                // start inside the merged buffer comes from the TileVertexBase table, not a fixed
                // (tileIndex * 4) stride (the count is whatever the builder emitted).
                int tileIdx = lz * cs + lx;
                int baseIndex = _merged.TileVertexBase != null && tileIdx < _merged.TileVertexBase.Length
                    ? _merged.TileVertexBase[tileIdx]
                    : tileIdx * ChunkData.VertexCount;
                Vector3 offset = new Vector3(lx, 0f, lz);
                int count = tile.Vertices != null ? tile.Vertices.Length : ChunkData.VertexCount;
                for (int k = 0; k < count; k++)
                {
                    Vector3 p = tile.Vertices[k] + offset;
                    p.y = ChunkMeshGenerator.SanitizeHeight(p.y);
                    int v = baseIndex + k;
                    _merged.Vertices[v] = p;
                    if (k < tile.UV.Length) _merged.UV[v] = tile.UV[k];
                    if (k < tile.Normals.Length) _merged.Normals[v] = tile.Normals[k];
                    // Strata by the world corner under the vertex (refined sub-quads sit at
                    // fractional local coords but are coloured by the tile cell they occupy).
                    float tileLocalX = p.x - offset.x;
                    float tileLocalZ = p.z - offset.z;
                    int wx = tile.Coord.X + Mathf.FloorToInt(tileLocalX + 0.0001f);
                    int wz = tile.Coord.Z + Mathf.FloorToInt(tileLocalZ + 0.0001f);
                    _merged.Colors[v] = ChunkMeshGenerator.TerrainBandColor(seed, wx, wz, p.y);
                }
            }
        }
        }

        // Keep the LOD corner lattice in sync with the patch (1ew): re-stamp every lattice node
        // whose canonical owner tile lives inside the region, so far-band children reflect the edit.
        if (_merged.Corners.Y != null)
            ChunkMeshGenerator.PatchCornerGrid(_merged.Corners, region, cs,
                localMinX, localMinZ, w, h, seed);

        // Low-poly root (1hi.1): re-sample the WHOLE surface from the just-restamped lattice — ~121 quads
        // at 3 m, cheaper than the 1 m per-tile skim it replaces, and it rebuilds the bounds too.
        // Full-res path: bounds from the full CPU vertex array as before.
        if (lowPoly)
        {
            _merged = ChunkMeshGenerator.ResampleLowPolySurface(_merged, _meshStep);
        }
        else
        {
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
        }

        // Re-upload the modified channels once.
        mesh.SetVertices(_merged.Vertices);
        mesh.SetNormals(_merged.Normals);
        mesh.SetUVs(0, _merged.UV);
        if (_merged.Colors != null)
            mesh.SetColors(_merged.Colors);
        mesh.bounds = _merged.Bounds;

        // Force the collider to re-cook against the new heights (PatchCornerGrid above already re-stamped
        // the lattice). Smooth chunks re-skim the DECIMATED 2 m surface (256 verts) so the physical
        // ground tracks the excavation exactly; voxel chunks re-cook their render mesh. Skipped for
        // collider-less chunks (1dq).
        if (_mc != null && _colliderActive)
        {
            if (VoxelMesh)
            {
                _mc.sharedMesh = null;
                _mc.sharedMesh = mesh;
            }
            else
            {
                if (_colliderMesh == null && _merged.Corners.Y != null)
                {
                    // Collider going live with this first patch (rare: enabled mid-edit) — build once.
                    _colliderMesh = ChunkMeshGenerator.AcquireChunkMesh($"ColliderMesh_{ChunkCoord.X}_{ChunkCoord.Z}");
                    ChunkMeshGenerator.BuildDecimatedCollider(_merged.Corners, ColliderStep,
                        out Vector3[] cv, out int[] ct);
                    UploadCollider(cv, ct);
                    _mc.sharedMesh = null;
                    _mc.sharedMesh = _colliderMesh;
                }
                else
                    RebuildColliderSurface();
            }
        }

        // Deformation changed the heights — a far-band chunk showing stale Lod1/Lod2 would display
        // a pre-excavation surface. RefreshLodMeshes() is lazy, so this just flags the rebuild.
        _lodDirty = true;

        // (1gd) Any in-flight async seam rebuild snapshot taken before this patch is now stale.
        MeshRebuildStamp = NextMeshRebuildStamp();
    }

    /// <summary>
    /// Rebuilds the Lod1/Lod2 child meshes from the current merged terrain when they are stale
    /// (1e6). A regular sample of the 31x31 tile-corner grid (every <c>step</c> tiles) shares
    /// vertices inside each decimated grid, so the LOD surface is watertight on its own, tracks
    /// deformation, and costs nothing until a far band actually selects it.
    /// </summary>
    public void RefreshLodMeshes()
    {
        if (VoxelMesh)
        {
            // Voxel mode (1eu): the stepped mesh has no TOPS-FIRST corner grid to sample, so the
            // children are decimated COLUMN samples of the voxel store (VoxelMesher renders ~1/4 /
            // ~1/9 the detail). Nothing to build when the store is missing.
            if (!_lodDirty || VoxelStore == null)
            {
                _lodDirty = false;
                return;
            }
            _lodDirty = false;
            _lod1Go = BuildVoxelLodChild(_lod1Go, ref _lod1Mf, "Lod1", 2, VoxelStore);
            _lod2Go = BuildVoxelLodChild(_lod2Go, ref _lod2Mf, "Lod2", 3, VoxelStore);
            return;
        }
        // (1hi.1) Low-poly mode skips the Lod1/Lod2 bands entirely: the root IS already the
        // decimated surface (e.g. 3 m facets — ~1/9 the 1 m mesh), so the children would duplicate
        // or exceed its density; ChunkLodManager.ApplyBand falls back to the root renderer when a
        // named detail is missing, and the far shell covers distance instead.
        if (_meshStep > 0)
        {
            _lodDirty = false;
            return;
        }
        if (!_lodDirty || _mf == null)
            return;
        _lodDirty = false;

        // 1ew: the coarse 31x31 corner lattice carried by the merged data. The old sampler walked
        // the merged vertex array on a fixed per-tile stride; refined (16-vertex) blocks break that.
        var corners = _merged.Corners;
        if (corners.Y == null)
            return;

        _lod1Go = BuildLodChild(_lod1Go, ref _lod1Mf, "Lod1", 2, corners);
        _lod2Go = BuildLodChild(_lod2Go, ref _lod2Mf, "Lod2", 3, corners);
    }

    private GameObject BuildLodChild(GameObject child, ref MeshFilter childMf, string name, int step,
        ChunkCornerGrid corners)
    {
        child = EnsureLodChild(child, ref childMf, name);
        if (childMf == null)
            return child;

        int cs = TerrainChunkCoord.ChunkSize;
        // Grid points every `step` tiles, inclusive of the far edge. `step` MUST divide cs (30) so
        // the last sample lands exactly on the chunk boundary — otherwise the grid stops short and
        // the decimated surface leaves a visible seam against the neighbour chunk.
        int axis = (cs / step) + 1;

        var positions = new Vector3[axis * axis];
        var uvs = new Vector2[axis * axis];
        var normals = new Vector3[axis * axis];
        var colors = new Color[axis * axis];

        for (int gz = 0, v = 0; gz < axis; gz++)
        {
            for (int gx = 0; gx < axis; gx++, v++)
            {
                // Sample the corner lattice at (gx*step, gz*step): node order is corner-grid
                // (gz * 31 + gx) order (1ew), so decimation is a plain axis-aligned stride.
                int s = gz * step * TerrainChunkCoord.CornerGridSize + gx * step;
                positions[v] = new Vector3(gx * step, corners.Y[s], gz * step);
                uvs[v] = corners.UV != null && s < corners.UV.Length ? corners.UV[s] : Vector2.zero;
                normals[v] = corners.Normals != null && s < corners.Normals.Length
                    ? corners.Normals[s] : Vector3.up;
                colors[v] = corners.Colors != null && s < corners.Colors.Length
                    ? corners.Colors[s] : Color.white;
            }
        }

        int[] indices = new int[(axis - 1) * (axis - 1) * 6];
        for (int gz = 0, t = 0; gz < axis - 1; gz++)
        {
            for (int gx = 0; gx < axis - 1; gx++)
            {
                int i00 = gz * axis + gx;
                int i10 = i00 + 1;   // +X (SE)
                int i01 = i00 + axis; // +Z (NW)
                int i11 = i01 + 1;   // NE
                indices[t++] = i00; indices[t++] = i10; indices[t++] = i11;
                indices[t++] = i00; indices[t++] = i11; indices[t++] = i01;
            }
        }

        // Same pooled-mesh discipline as the full chunk (1dv): upload into a reused instance.
        Mesh mesh = childMf.sharedMesh;
        if (mesh == null)
            mesh = ChunkMeshGenerator.AcquireChunkMesh(name);
        if (mesh.vertexCount != positions.Length)
            mesh.Clear();
        mesh.SetVertices(positions);
        mesh.SetTriangles(indices, 0);
        mesh.SetNormals(normals);
        mesh.SetUVs(0, uvs);
        mesh.SetColors(colors);
        mesh.RecalculateBounds();
        mesh.UploadMeshData(false);
        childMf.sharedMesh = mesh;

        var mr = child.GetComponent<MeshRenderer>();
        if (mr != null && _mr != null && _mr.sharedMaterial != null)
            mr.sharedMaterial = _mr.sharedMaterial;

        return child;
    }

    /// <summary>Create (or reacquire) a Lod child's GameObject/MeshFilter. Shared by the smooth
    /// corner-grid builder and the voxel column-sample builder (1eu).</summary>
    private GameObject EnsureLodChild(GameObject child, ref MeshFilter childMf, string name)
    {
        if (child == null)
        {
            child = new GameObject(name);
            child.transform.SetParent(transform, false);
            childMf = child.AddComponent<MeshFilter>();
            child.AddComponent<MeshRenderer>();
        }
        else if (childMf == null)
        {
            childMf = child.GetComponent<MeshFilter>();
        }
        return child;
    }

    /// <summary>
    /// Rebuilds one stepped-voxel Lod child (1eu): the decimated surface of <paramref name="vc"/> at
    /// <paramref name="step"/>-metre blocks. Each block (min(bx+step, cs) wide) collapses to ONE
    /// column top — the rounded mean of the block's real column tops — written into a coarse
    /// full-size store that VoxelMesher then renders: row-run merging collapses the equal blocks to a
    /// handful of quads, so the child carries ~1/4 (step 2) to ~1/9 (step 3) of the full mesh detail.
    /// The mean keeps the block flat, and on flat ground the boundary block equals the real column top
    /// exactly, so the LOD surface meets the real mesh level at the chunk rim.
    /// </summary>
    private GameObject BuildVoxelLodChild(GameObject child, ref MeshFilter childMf, string name, int step,
        VoxelChunkData vc)
    {
        child = EnsureLodChild(child, ref childMf, name);
        if (childMf == null)
            return child;

        int cs = TerrainChunkCoord.ChunkSize;
        var coarse = VoxelChunkData.Create(vc.Coord, vc.Seed);
        for (int bz = 0; bz < cs; bz += step)
        {
            int maxZ = Mathf.Min(bz + step, cs);
            for (int bx = 0; bx < cs; bx += step)
            {
                int maxX = Mathf.Min(bx + step, cs);
                int sum = 0;
                int n = 0;
                for (int z = bz; z < maxZ; z++)
                {
                    for (int x = bx; x < maxX; x++)
                    {
                        sum += vc.ColumnTop(x, z);
                        n++;
                    }
                }
                int top = Mathf.RoundToInt((float)sum / n);
                for (int z = bz; z < maxZ; z++)
                {
                    for (int x = bx; x < maxX; x++)
                        coarse.SetColumnTop(x, z, top);
                }
            }
        }

        Mesh mesh = childMf.sharedMesh;
        if (mesh == null)
            mesh = ChunkMeshGenerator.AcquireChunkMesh(name);
        ChunkMeshGenerator.UploadMerged(VoxelMesher.Build(coarse), mesh);
        childMf.sharedMesh = mesh;

        var mr = child.GetComponent<MeshRenderer>();
        if (mr != null && _mr != null && _mr.sharedMaterial != null)
            mr.sharedMaterial = _mr.sharedMaterial;

        return child;
    }

    /// <summary>True when the tile at local (lx, lz) currently renders as the 1ew refined 2x2 block
    /// (16 vertices) instead of the coarse 4-corner quad. RebuildChunkRegion compares this against
    /// the freshly built tile before Patching so a rebuild that changes a tile's split state falls
    /// back to a full chunk rebuild (the merged block table must not be re-skinned in place).</summary>
    public bool IsTileRefined(int lx, int lz)
    {
        if (_merged.TileVertexCount == null)
            return false;
        int idx = lz * TerrainChunkCoord.ChunkSize + lx;
        return idx >= 0 && idx < _merged.TileVertexCount.Length
            && _merged.TileVertexCount[idx] > ChunkData.VertexCount;
    }

    /// <summary>True while this chunk's props are visible (queued AND inside the prop ring).
    /// False both before the ring reaches it and while the ring keeps its props dormant (1du).</summary>
    public bool PropsOn => _propActive;

    /// <summary>True while this chunk still has prop tiles waiting to spawn.</summary>
    public bool PropsPending => PropsOn && _propRng != null && _propTiles != null && _propCursor < _propTiles.Length;

    /// <summary>
    /// Queue the chunk's props (trees/rocks) for incremental spawning. One deterministic Random
    /// stream per chunk (previously 900 per-tile Random allocations). When the chunk is coming
    /// back into the ring after a release (1du), the existing GameObjects are reactivated instead
    /// — a fully-streamed chunk skips the re-roll entirely (was: destroy + full deterministic
    /// respawn ~= ~2 GOs + re-roll of ~900 tiles); a partially-streamed one resumes from the
    /// cursor the release preserved. No-op when the props are already active.
    /// </summary>
    public void BeginProps(long seed)
    {
        if (_propActive)
            return;
        if (_propRng != null && _propTiles != null)
        {
            // Return visit (1du): reactivate this chunk's own props, stream-position intact.
            for (int i = 0; i < _props.Count; i++)
            {
                if (_props[i] != null)
                    _props[i].SetActive(true);
            }
            _propActive = true;
            return;
        }
        _propSeed = seed;
        int cs = TerrainChunkCoord.ChunkSize;
        _propRng = new System.Random(seed.GetHashCode() ^ (ChunkCoord.X * 73856093) ^ (ChunkCoord.Z * 19349663));
        if (_propTiles == null || _propTiles.Length != cs * cs)
            _propTiles = new int[cs * cs];
        int tileIdx = 0;
        for (int z = 0; z < cs; z++)
            for (int x = 0; x < cs; x++)
                _propTiles[tileIdx++] = z * cs + x;
        _propCursor = 0;
        _propActive = true;
    }

    /// <summary>
    /// Spawn props for up to <paramref name="budget"/> more tiles. Returns tiles consumed
    /// (caller shares a global budget across pending chunks). Deterministic across steps.
    /// </summary>
    public int StepProps(int budget)
    {
        if (_propRng == null || !_propActive || _propTiles == null) return 0;
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
    /// Hides this chunk's spawned props (keep-alive, 1du) when the prop ring moves past — was
    /// Destroy + full deterministic respawn on re-entry. The merged terrain mesh + collider are
    /// untouched, so the chunk stays rendered and collidable. The deterministic stream position
    /// (RNG/tiles/cursor) is preserved so a later BeginProps reactivates the same GameObjects and
    /// a partially-streamed chunk resumes exactly where StepProps stopped.
    /// </summary>
    public void ReleaseProps()
    {
        if (!_propActive)
            return;
        for (int i = 0; i < _props.Count; i++)
        {
            if (_props[i] != null)
                _props[i].SetActive(false);
        }
        _propActive = false;
    }

    public void Release()
    {
        // Full teardown — props are destroyed outright (dormant or visible; nothing is pooled
        // across releases, this is a chunk unload).
        for (int i = _props.Count - 1; i >= 0; i--)
        {
            if (_props[i] != null)
                Destroy(_props[i]);
        }
        _props.Clear();
        _propActive = false;
        _propRng = null;
        _propCursor = 0;
        _merged = default;
        _colliderActive = false;
        VoxelStore = null;

        // Return this chunk's pooled mesh to the shared freed-mesh pool (1dv) — a later chunk
        // reuses the same GPU buffer instead of allocating a fresh one.
        if (_mesh != null)
        {
            ChunkMeshGenerator.ReleaseChunkMesh(_mesh);
            _mesh = null;
        }
        // (1hi) The decimated collider mesh is its own pooled instance (smooth path): release it
        // here too; voxel mode never allocates one.
        if (_colliderMesh != null)
        {
            ChunkMeshGenerator.ReleaseChunkMesh(_colliderMesh);
            _colliderMesh = null;
        }
        // LOD children (1e6): meshes go back to the same pooled-mesh cache; the lightweight child
        // GameObjects are destroyed outright.
        if (_lod1Go != null)
        {
            if (_lod1Mf != null && _lod1Mf.sharedMesh != null)
                ChunkMeshGenerator.ReleaseChunkMesh(_lod1Mf.sharedMesh);
            Destroy(_lod1Go);
        }
        if (_lod2Go != null)
        {
            if (_lod2Mf != null && _lod2Mf.sharedMesh != null)
                ChunkMeshGenerator.ReleaseChunkMesh(_lod2Mf.sharedMesh);
            Destroy(_lod2Go);
        }
        _lod1Go = null;
        _lod2Go = null;
        _lod1Mf = null;
        _lod2Mf = null;
        _lodDirty = true;
        if (_mf != null)
            _mf.sharedMesh = null;
        if (_mc != null)
            _mc.sharedMesh = null;
    }
}
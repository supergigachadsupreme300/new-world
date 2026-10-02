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
    // (collider cooks + the broadphase bodies) is gated to what the gameplay uses. Since 1ex each
    // ring collider is 1800 tris (was 450), so the ring-7 square's full broadphase cost is ~405k
    // triangles rather than ~101k — a deliberate trade for not walking through visible craters.
    private bool _colliderActive;

    /// <summary>True while this chunk's MeshCollider is assigned (near the player or magic, 1dq).</summary>
    public bool HasCollider => _colliderActive;

    /// <summary>
    /// True while the chunk is DORMANT (hidden-but-retained, 1gc): the streamer demoted it out of the
    /// loaded ring but keeps its data/mesh/GameObject alive so re-entry wakes it instantly.
    /// ChunkDistanceCull skips dormant entries so its visibility sweep can never re-enable a hidden
    /// chunk during the timer-gated unregister gap.
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
    /// Assignment point for the chunk's MeshCollider (1hi). Smooth chunks cook a lattice sampled from
    /// the same world corners as the render mesh — every 1 m node since 1ex, or the chunk's OWN low-poly
    /// facet step (1hi.1 <see cref="ColliderStep"/>, so you stand exactly on the visible facets — 3 m if
    /// <c>WorldStreamer.LowPolyFacets</c> is on, which it is not by default since 1ia). 1ex raised this
    /// from every-2nd-node because the player has no separate ground raycast (CharacterController sweeps
    /// this collider directly), so at step 2 a carve centred on an odd coordinate had no sampled node in
    /// its footprint and you walked over a visible crater. The lattice shares the EXACT world corners the
    /// far shell and the neighbour chunks use, so the physics surface is seam-proof across chunks by
    /// construction. Voxel mode (already chunky 1 m columns) keeps the render mesh as its collider,
    /// unchanged from pre-1hi. <paramref name="md"/> carries worker-thread-built collider arrays on the
    /// merge path (0 main-thread build); enabling a collider later (1dq collider ring, 1gg exempt cooks)
    /// derives them here — 961 verts since 1ex, once per enable. Disabling nulls the collider (zero
    /// physics) but KEEPS the pooled collider mesh, so a re-enable re-cooks the same instance.
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

    /// <summary>Uploads the collider arrays into <see cref="_colliderMesh"/> (overwrite-only
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
    /// path re-derives the surface from <see cref="_merged.Corners"/> (PatchCornerGrid just
    /// re-stamped it), so the physics surface tracks every excavation — 961 verts since 1ex (was 256),
    /// main thread, safe per patch. The null→assign pair runs inside this single synchronous call, so no physics step
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
    /// Show/hide the chunk's visuals (the root merged mesh) without releasing anything
    /// (1gc dormant keep-ring): a dormant chunk keeps its pooled mesh and tile data so a wake is an
    /// instant re-show. Does NOT touch the collider (the streamer's collider ring owns
    /// that) or the props (the prop ring owns those). Since 1f6 the root mesh is the chunk's ONLY
    /// render surface — there are no detail children to toggle (see the 1f6 note on the class).
    /// </summary>
    public void SetVisualActive(bool active)
    {
        if (_mr != null)
            _mr.enabled = active;
    }

    // CPU-side copy of the merged chunk mesh arrays, kept so terrain deformation can patch only
    // the touched tiles' vertices without re-running a full 900-tile rebuild.
    private MergedChunkMeshData _merged;

    // One pooled Mesh for this chunk's whole life (1dv): acquired from the freed-mesh pool on the
    // first ApplyMerged, then re-uploaded in place on every rebuild — no new+Destroy churn and no
    // transient double GPU buffer. Returned to the pool on Release() for the next chunk to reuse.
    private Mesh _mesh;

    // (1hi) One pooled Mesh for this chunk's collider surface (smooth path only). The MeshCollider
    // cooks this ~961-vert / ~1800-tri 1 m lattice (every node of the 31x31 corner grid since 1ex)
    // instead of the full render surface. It is no longer a cheaper COOK (1ex raised it from 256/450 to
    // render-resolution to stop the player walking through visible craters) — what it still saves is the
    // render mesh's side walls and refined blocks, which physics has no use for. Lazily acquired on
    // first enable (a chunk that never enters the collider ring allocates nothing), retained across
    // en/disables, and returned to the pool on Release — the same pool/discipline as _mesh (1dv). Voxel
    // mode never uses it (its chunky 1 m render columns stay the collider). It is a SEPARATE input mesh,
    // so its vertex indices bear no relation to the render mesh's.
    private Mesh _colliderMesh;
    private const int ColliderDecimation = ChunkMeshGenerator.ChunkColliderDecimation;

    // (1hi.1) Coarse facet step this chunk's ROOT mesh was built at (0 = full-resolution 1 m per-tile
    // surface — the smooth look). Set from MergedChunkMeshData.LowPolyStep on every apply; drives
    // both the low-poly PatchRegion re-sample (the root is re-emitted from the restamped lattice
    // instead of a per-tile skim) and the collider decimation, so physics always rides the SAME step
    // as the visible facets.
    private int _meshStep;

    private int _buildStamp;

    /// <summary>Collider decimation step for THIS chunk (1hi.1): the low-poly root's own step when
    /// coarse (stand exactly on the visual facets), else the standard 2 m decimation.</summary>
    private int ColliderStep => _meshStep > 0 ? _meshStep : ColliderDecimation;

    // --- QA read-only accessors (1hj) ---
    // Exposed for diagnostics that compare a chunk's own corner lattice against its neighbours' — and
    // against the far cells at the rim — to measure the "gaps between chunks" report instead of
    // guessing at it. Strictly read-only: nothing here mutates chunk state, and every accessor is
    // null/NaN-safe so a diagnostic can walk a half-torn-down streamer. (1hx removed the F2/F3/F4
    // audit lanes; these accessors stay because they are the only safe way to ask these questions.)

    /// <summary>Coarse facet step this chunk's root mesh was built at (0 = full 1 m surface).</summary>
    public int MeshStep => _meshStep;

    /// <summary>
    /// Generator revision this chunk's mesh was built by (1i7), read from the build data at apply
    /// time exactly like <see cref="MeshStep"/>. Stays 0 for a chunk whose mesh predates the stamp.
    /// </summary>
    public int BuildStamp => _buildStamp;

    /// <summary>True when this chunk carries a 31x31 world-corner lattice (smooth / low-poly terrain
    /// chunks). Voxel chunks build their own stepped mesh and have none.</summary>
    public bool HasLattice => _merged.Corners.Y != null;

    /// <summary>
    /// Height (world metres) of one node of this chunk's 31x31 world-corner lattice, addressed by
    /// CHUNK-LOCAL lattice coords gx/gz in 0..30 — the same addressing <c>BuildCornerGrid</c> stamps,
    /// so node (30, k) is the chunk's east edge row and (k, 30) its north edge row. Returns NaN when
    /// the lattice is absent or the index is out of range, which the audit skips.
    /// </summary>
    public float LatticeY(int gx, int gz)
    {
        float[] y = _merged.Corners.Y;
        if (y == null)
            return float.NaN;
        int idx = gz * TerrainChunkCoord.CornerGridSize + gx;
        return idx >= 0 && idx < y.Length ? y[idx] : float.NaN;
    }

    /// <summary>The chunk's live root mesh (what its MeshFilter and MeshCollider point at), or null
    /// before the first apply / after release.</summary>
    public Mesh RootMesh => _mf != null ? _mf.sharedMesh : null;

    // 1f6: the two decimated detail children ("Lod1" = every 2nd corner, "Lod2" = every 3rd) that
    // used to live here are GONE, together with RefreshLodMeshes/BuildLodChild/BuildVoxelLodChild,
    // the LodDirty staleness flag and the NeedsLodDetail curvature gate. Each child was a DIFFERENT
    // surface from the root: a 2 m / 3 m resample of the corner lattice, drawn INSTEAD of the mesh
    // the player was standing on, so terrain visibly changed shape at 30 m and 60 m (1f3, 1f5) and
    // could cover the real surface where the coarse triangle spans convex ground. The root mesh is
    // now the chunk's only render output at every distance.

    /// <summary>
    /// True while this chunk renders a stepped voxel mesh (1et). Voxel chunks have no corner lattice
    /// of their own — <see cref="VoxelStore"/> backs their stepped mesh — so the collider
    /// (<see cref="RefreshCollider"/>) and <see cref="PatchRegion"/> branch on this flag instead of
    /// re-cooking a decimated lattice from surface heights that do not exist for them.
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
        _buildStamp = md.BuildStamp;
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
        // (1hi) Smooth chunks cook the 1 m collider lattice carried on md (worker-thread built; since 1ex
        // render-resolution, 961 verts — no longer a cheaper cook than the render mesh, but still skips
        // its side walls and refined blocks); voxel mode keeps the render mesh as its collider.
        RefreshCollider(buildCollider, md);
        _colliderActive = buildCollider;

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
        // coarse facet surface (100 quads / 121 verts at 3 m, the 1ia default; 25 at 6 m), so a patch cannot skim `count` vertices per
        // tile — it re-samples the whole root from the restamped lattice below (far cheaper than
        // the 1 m skim anyway). Dead since 1ia unless LowPolyFacets is turned back on: _meshStep
        // is 0 for every chunk again, so this branch never runs.
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

        // Keep the corner lattice in sync with the patch (1ew): it is what the collider re-cooks
        // from below and what the far shell and the F3 corner audit read, so it must track the edit.
        if (_merged.Corners.Y != null)
            ChunkMeshGenerator.PatchCornerGrid(_merged.Corners, region, cs,
                localMinX, localMinZ, w, h, seed);

        // Low-poly root (1hi.1): re-sample the WHOLE surface from the just-restamped lattice — ~121
        // quads at 3 m (the 1ia default), cheaper than the 1 m per-tile skim it replaces, and it
        // rebuilds the bounds too. Not reached by default since 1ia (LowPolyFacets is false).
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
        // the lattice). Smooth chunks re-skim the 1 m surface (961 verts since 1ex) so the physical
        // ground tracks the excavation at the render resolution; at the pre-1ex 2 m step it did not,
        // which was the walk-through-a-visible-crater bug 1ex fixed. Voxel chunks re-cook their
        // render mesh. Skipped for collider-less chunks (1dq).
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

        // (1gd) Any in-flight async seam rebuild snapshot taken before this patch is now stale.
        MeshRebuildStamp = NextMeshRebuildStamp();
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
        if (_mf != null)
            _mf.sharedMesh = null;
        if (_mc != null)
            _mc.sharedMesh = null;
    }
}

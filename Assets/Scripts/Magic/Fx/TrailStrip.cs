using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 1jq: the exhaust a magic projectile leaves behind it while it flies, as a single
/// <b>camera-facing quad strip</b> — one GameObject, one MeshRenderer, one draw call — instead of
/// the stream of small cubes 1jg used.
///
/// <para>
/// <b>Why this is cheaper, and by how much.</b> 1jg emitted one pooled cube every <see cref="Step"/>
/// metres, each surviving <see cref="Life"/> seconds. At the default 20 m/s that is ~23 LIVE CUBES per
/// flying projectile: 23 GameObjects, 23 MeshRenderers (so 23 draws) and 23 MonoBehaviours whose
/// <c>Update</c> ticks each frame. Worse, the pooling did not actually make that cheap —
/// <c>ObjectPooler.Return(go, delay)</c> does <c>go.AddComponent&lt;ReturnTimer&gt;()</c> on EVERY emit
/// and <c>ReturnTimer.Update</c> calls <c>Destroy(gameObject)</c> when it expires, so every voxel cost a
/// fresh native component create plus a deferred destroy per life. The strip replaces all of that with
/// a pre-sized vertex buffer rewritten in place: 1 GameObject, 1 MeshRenderer, 1 Update, and no
/// per-frame allocation at all.
/// </para>
///
/// <para>
/// <b>Where the emission is driven from, and why.</b> Deliberately NOT in
/// <c>MagicProjectileModelBuilder</c>, which is a one-shot shape factory shared by the static model
/// bench (a motionless pedestal) and by <c>SpellCaster.DecorateProjectile</c> (a turret bolt that never
/// flies) — a trail emitted there would hang a ribbon in mid-air on a pedestal that never moves. The
/// <c>Push</c> call sits inside <c>SpellEffect.Update</c>, behind its <c>if (!_launched) return;</c>
/// gate, so only a genuinely flying projectile trails and no extra spawn flag is needed. This is 1jg's
/// placement rule, kept intact across the rewrite.
/// </para>
///
/// <para>
/// <b>Colour.</b> <c>SpellLook.Edge</c>, the struct's two-tone member documented for "rim, trails,
/// shards", passed in ALREADY RESOLVED by the projectile (<c>SpellEffect.Initialize</c> resolves the
/// look once). So the trail is per-spell — authored profile, then id-hash, then school default — and
/// this file adds no second <c>SpellLook.Resolve</c> and derives no colour of its own (rule 13). It is
/// carried in the MESH's vertex colours, not in a material, which is why one shared material serves
/// every strip of every school.
/// </para>
/// </summary>
public sealed class TrailStrip : MonoBehaviour
{
    /// <summary>Metres of flight between points. Distance-gated, not time-gated, so a fast bolt and a
    /// slow one lay the same spacing instead of the slow one drawing a solid ribbon and the fast one a
    /// dotted line. Carried over unchanged from 1jg — this constant IS the gate, and
    /// <c>SpellEffect.Update</c> reads it.</summary>
    public const float Step = 0.3f;

    /// <summary>Seconds a point survives. Carried over unchanged from 1jg.</summary>
    public const float Life = 0.35f;

    /// <summary>Points held in the strip. DERIVED, not chosen: the fastest authored magic projectile is
    /// 22 m/s (<c>SkillCatalog.Magic.cs</c>, Continuous Fireball), so the authored
    /// <see cref="Life"/> is <c>22 x 0.35 / 0.3 = 25.7</c> segments. 29 points gives headroom, and when
    /// it fills the <b>oldest</b> point is dropped — the tail shortens, the arrays never grow.</summary>
    public const int MaxPoints = 29;

    /// <summary>Strip half-width at the head, i.e. the full strip is 0.10 m. Sized against 1jg's
    /// 0.05-0.09 m cubes: a solid strip of the same visual thickness, not a wide band.</summary>
    private const float HeadHalfWidth = 0.05f;

    /// <summary>Half-width at the oldest point. Zero, so the tail tapers to nothing instead of ending
    /// on a hard edge. This taper plus the alpha ramp is what makes it read as exhaust — 1jg got its
    /// gradient for free from voxels being recycled, which a single strip cannot borrow.</summary>
    private const float TailHalfWidth = 0f;

    /// <summary>Below this squared length a cross product is treated as degenerate. Used for the
    /// sighting-along-the-strip case, where the tangent and the camera forward are parallel and the
    /// side vector would come out as NaN and blow the whole strip off screen.</summary>
    private const float DegenerateSqr = 1e-8f;

    /// <summary>Slack added to the hand-set bounds so a strip is never culled a millimetre early.</summary>
    private const float BoundsSlack = 0.1f;

    /// <summary>Oldest point at index 0, newest at the end. A front-shifting array rather than a ring
    /// buffer: with <see cref="MaxPoints"/> = 29 and one push every 0.3 m, the copy is 29 elements three
    /// times a second, which is free — and it means "index 0 is always the oldest" is readable, which
    /// matters when there is no compiler to check it (rule 3).</summary>
    private readonly Vector3[] _pts = new Vector3[MaxPoints];
    private readonly float[] _age = new float[MaxPoints];
    private int _count;

    private Vector3[] _verts;
    private List<Color> _cols;
    private int[] _tris;
    private Mesh _mesh;
    private MeshRenderer _renderer;
    private Camera _cam;
    private Color _base;

    /// <summary>Last usable side vector, so a degenerate frame reuses the previous orientation rather
    /// than snapping to world-up and popping.</summary>
    private Vector3 _lastSide = Vector3.up;

    private static Material _sharedMaterial;

    /// <summary>Live quads. Reported by the 1jq Numpad8 lane.</summary>
    public int SegmentCount { get { return _count > 1 ? _count - 1 : 0; } }

    /// <summary>Vertices the strip's geometry actually REFERENCES, which is not the array length:
    /// <c>Rebuild</c> uploads the whole preallocated buffer and lets the unused tail go unreferenced
    /// (and, for indices, zeroed). Reported by the 1jq Numpad8 lane, so it has to be the number the
    /// strip really draws.
    /// <para>
    /// ZERO when there are no segments, not <c>_count * 2</c>: a one-point strip never reaches
    /// <c>Rebuild</c> at all, so its mesh holds nothing and reporting 2 would be a count of geometry
    /// that does not exist (rule 7: an absent measurement and a measurement of zero are different).
    /// </para></summary>
    public int VertexCount { get { return SegmentCount > 0 ? _count * 2 : 0; } }

    /// <summary>Triangles the strip really draws — <see cref="SegmentCount"/> * 2, one-sided counted
    /// once. The submesh itself holds <c>MaxPoints - 1</c> * 2 entries because the full index buffer is
    /// uploaded, and the remainder are zeroed degenerate triples that rasterise to nothing.</summary>
    public int TriangleCount { get { return SegmentCount * 2; } }

    /// <summary>Was this strip actually rendered? A count of components is not a visibility proof —
    /// this is the difference, and it is what catches bad mesh bounds or a missing camera.</summary>
    public bool WasDrawn { get { return _renderer != null && _renderer.isVisible; } }

    /// <summary>
    /// Build a strip at <paramref name="position"/>, coloured from an already-resolved
    /// <paramref name="look"/>.
    /// <para>
    /// Exactly ONE point is pushed. A strip grows as the bolt flies, so the first 0.3 m has nothing to
    /// draw — and 1jq's first draft pushed the same point twice to "guarantee a legal segment on the
    /// first Update". That guarantee was never needed and the comment claiming it was false:
    /// <c>Update</c> only destroys on <c>_count == 0</c>, so a one-point strip survives its first frames
    /// on the <c>_count &lt; 2</c> return. The duplicate point instead drew a zero-area quad and made
    /// the Numpad8 lane report 1 segment / 2 triangles for a strip with no geometry — a positive-looking
    /// number about nothing, which is the failure rule 7 exists to stop.
    /// </para>
    /// </summary>
    public static TrailStrip Spawn(Vector3 position, SpellLook look)
    {
        var go = new GameObject("TrailStrip");
        var strip = go.AddComponent<TrailStrip>();

        var mf = go.AddComponent<MeshFilter>();
        var mr = go.AddComponent<MeshRenderer>();

        Shader sh = Shader.Find("Sprites/Default");
        if (sh == null) sh = Shader.Find("Unlit/Color");
        if (sh != null) mr.sharedMaterial = SharedMaterial(sh);

        // Cosmetic only: a trail must never cast a shadow, and never receive one.
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;

        strip._mesh = new Mesh { name = "TrailStripMesh" };
        strip._mesh.MarkDynamic();
        mf.sharedMesh = strip._mesh;
        // Bounds start AT the spawn point, not at the default origin-sized zero box. A one-point strip
        // now lives for its first 0.3 m with an empty mesh, and a zero bounds centred on the world
        // origin is a real culling trap for that window.
        strip._mesh.bounds = new Bounds(position, Vector3.one * 0.01f);

        strip._verts = new Vector3[MaxPoints * 2];
        strip._cols = new List<Color>(MaxPoints * 2);
        strip._tris = new int[(MaxPoints - 1) * 6];

        // The `List<T>` constructor takes a CAPACITY, not a length: this list is born with Count == 0
        // and 58 slots reserved, and the indexer setter rejects any index >= Count. So it has to be
        // FILLED once, here - otherwise the very first per-vertex write in Rebuild throws
        // ArgumentOutOfRangeException, on the first frame the strip draws.
        //
        // This is the cost of moving from `Color32[]` to `List<Color>`: an array's Length is both its
        // capacity and its last valid index + 1, and a List's is not. Swapping the container to match
        // `SetColors`' overload silently transferred the addressing contract with it.
        //
        // The placeholder value is irrelevant, deliberately: the entries past the live segment count are
        // never referenced by any drawn triangle (the index tail is zeroed to degenerate triples), so
        // what they hold cannot be seen. They exist only to make the full-array upload legal.
        for (int i = 0; i < MaxPoints * 2; i++) strip._cols.Add(Color.clear);

        // Stays a float `Color` all the way through, deliberately. 1jq's first two versions stored a
        // `Color32` here and each was a compile error: `new Color32(look.Edge.r, ...)` is CS1503,
        // because `SpellLook.Edge` is a `Color` (four normalised FLOATS) while the Color32 CONSTRUCTOR
        // takes four bytes - and a `Color -> Color32` conversion existing (as an implicit OPERATOR, not
        // an overload of the constructor) does not make that signature legal. Keeping one type removes
        // the whole float->byte ladder. It also puts `SetColors(List<Color>)` on the same overload the
        // rest of this project actually uses (FarShell, VoxelMesher, ChunkMeshGenerator all hand it a
        // `List<Color>`), where `List<Color32>` appears nowhere in the tree.
        strip._base = look.Edge;
        strip._cam = Camera.main;

        strip.Push(position);
        return strip;
    }

    /// <summary>
    /// One shared material for every strip of every school. The colour lives in the mesh's vertex
    /// colours, which <c>Sprites/Default</c> multiplies in, so one material serves all of them — the
    /// strips differ per cast, the material does not.
    /// </summary>
    private static Material SharedMaterial(Shader sh)
    {
        if (_sharedMaterial == null)
        {
            _sharedMaterial = new Material(sh) { name = "TrailStripShared" };
            _sharedMaterial.hideFlags = HideFlags.DontSave;
            _sharedMaterial.color = Color.white;
        }
        return _sharedMaterial;
    }

    /// <summary>Record that the bolt has just been here. Called from the flight loop only.</summary>
    public void Push(Vector3 worldPosition)
    {
        if (_count >= MaxPoints)
        {
            _count = MaxPoints - 1;
            System.Array.Copy(_pts, 1, _pts, 0, _count);
            System.Array.Copy(_age, 1, _age, 0, _count);
        }
        _pts[_count] = worldPosition;
        _age[_count] = 0f;
        _count++;
    }

    private void Update()
    {
        float dt = Time.deltaTime;

        for (int i = 0; i < _count; i++) _age[i] += dt;

        int drop = 0;
        while (drop < _count && _age[drop] >= Life) drop++;
        if (drop > 0)
        {
            System.Array.Copy(_pts, drop, _pts, 0, _count - drop);
            System.Array.Copy(_age, drop, _age, 0, _count - drop);
            _count -= drop;
        }

        // Every point has aged out: nothing left to draw, and the strip is unparented so nothing else
        // is holding it. This is what lets SpellEffect destroy itself on impact with no hand-off.
        if (_count == 0)
        {
            Destroy(gameObject);
            return;
        }
        if (_count < 2) return;

        if (_cam == null) _cam = Camera.main;
        // No camera: keep the last good shape rather than write a guessed one. A strip frozen for a
        // frame is invisible; a strip built from a fabricated forward vector is wrong on screen.
        if (_cam == null) return;

        Rebuild(_cam.transform.forward);
    }

    /// <summary>
    /// Rewrite the whole vertex buffer for the current point list. Every point emits one cross-section
    /// of two vertices, so adjacent quads SHARE their boundary vertices and cannot tear apart.
    /// </summary>
    private void Rebuild(Vector3 camForward)
    {
        int segs = _count - 1;
        int tcount = segs * 6;
        var bounds = new Bounds(_pts[0], Vector3.zero);

        for (int i = 0; i < _count; i++)
        {
            // Tangent by the AVERAGE of the adjacent segment directions. A per-segment side vector
            // would give the shared vertex two different offsets and rip the strip open at every joint.
            Vector3 tangent;
            if (i == 0) tangent = _pts[1] - _pts[0];
            else if (i == _count - 1) tangent = _pts[_count - 1] - _pts[_count - 2];
            else tangent = (_pts[i] - _pts[i - 1]) + (_pts[i + 1] - _pts[i]);

            Vector3 side = Vector3.Cross(tangent, camForward);
            // Sighting along the strip: tangent and camForward are parallel and the cross is ~0.
            if (side.sqrMagnitude < DegenerateSqr) side = _lastSide;
            if (side.sqrMagnitude < DegenerateSqr) side = Vector3.up;
            side.Normalize();
            _lastSide = side;

            // 0 at the oldest point, 1 at the head.
            float t = i / (float)segs;
            float half = Mathf.Lerp(TailHalfWidth, HeadHalfWidth, t);
            Vector3 off = side * half;

            _verts[i * 2] = _pts[i] + off;
            _verts[i * 2 + 1] = _pts[i] - off;

            // Fade the authored alpha along the strip. One float write per vertex now, no byte
            // rounding and no clamp-to-0-255 step: the ramp is linear in float and the GPU quantises
            // it once, on upload.
            Color c = _base;
            c.a = Mathf.Clamp01(_base.a * t);
            _cols[i * 2] = c;
            _cols[i * 2 + 1] = c;

            bounds.Encapsulate(_pts[i]);
        }

        int w = 0;
        for (int s = 0; s < segs; s++)
        {
            int v0 = s * 2, v1 = s * 2 + 1, v2 = (s + 1) * 2, v3 = (s + 1) * 2 + 1;
            // Double-sided under Sprites/Default, so this winding is a convention, not a constraint.
            _tris[w++] = v0; _tris[w++] = v2; _tris[w++] = v1;
            _tris[w++] = v2; _tris[w++] = v3; _tris[w++] = v1;
        }

        // The whole index array is uploaded every rebuild (see the note below), so the entries past
        // this frame's segment count are leftovers from a LONGER strip and would still reference real
        // vertex slots — that is garbage geometry, not a degenerate no-op. Zeroing them collapses each
        // to three copies of vertex 0, i.e. zero area, which draws nothing.
        if (tcount < _tris.Length) System.Array.Clear(_tris, tcount, _tris.Length - tcount);

        // Full-array overloads, with NO MeshUpdateFlags — deliberately. 1jq's first version carried a
        // `MeshUpdateFlags` field to skip bounds recalculation and index validation, and it did not
        // compile (CS0246). This project runs Unity 6000.5.1f1, where ChunkMeshGenerator's upload is the
        // proven-working reference and it uses exactly these overloads with no flags at all. Matching the
        // codebase is both the version-safe choice and the one that can be verified by reading a file that
        // already ships. The optimisation it gave up is worth naming: these three calls DO recalculate
        // bounds, which is why the hand-set bounds below must come AFTER them rather than before — and it
        // costs a bounds pass over 58 vertices about three times a second, which is not a measurement.
        _mesh.SetVertices(_verts);
        _mesh.SetColors(_cols);
        _mesh.SetTriangles(_tris, 0);

        // Bounds are set BY HAND, and LAST. Skip this and the strip gets culled by a stale/zero bound and
        // vanishes — including right at the screen edge, which is exactly where it looks like a mesh bug
        // and not a culling bug.
        bounds.Expand(HeadHalfWidth * 2f + BoundsSlack);
        _mesh.bounds = bounds;
    }

    private void OnDestroy()
    {
        if (_mesh != null)
        {
            if (Application.isPlaying) Destroy(_mesh); else DestroyImmediate(_mesh);
        }
    }
}
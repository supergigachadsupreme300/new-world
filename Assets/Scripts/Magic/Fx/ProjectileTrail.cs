using UnityEngine;

/// <summary>
/// 1jg: the exhaust a magic projectile leaves behind it while it flies - a stream of small
/// school-coloured voxels left at the positions the bolt has already passed through.
/// <para>
/// Why it lives here and not in <c>MagicProjectileModelBuilder</c>: that builder is a ONE-SHOT
/// shape factory with no per-frame behaviour, and it is shared by the static model bench
/// (<c>NewWorldTestGround</c>'s spell band draws every castable spell as a motionless pedestal
/// model) and by <c>SpellCaster.DecorateProjectile</c> (a summoned turret's bolt, which never
/// flies through the world). A trail emitted there would hang a row of cubes in mid-air on a
/// pedestal that never moves. Instead the EMISSION is driven from the flight loop in
/// <c>SpellEffect.Update</c>, which only runs while a projectile is actually travelling - so
/// every real cast trails and nothing static does, with no extra component to pool or update.
/// </para>
/// <para>
/// The colour is <c>SpellLook.Edge</c>, the struct's two-tone member (the core hue pushed
/// brighter and cooler), documented for exactly this use - "rim, trails, shards" - and until now
/// had no reader. It arrives already resolved on the projectile (<c>SpellEffect.Initialize</c>
/// resolves the look once), so there is no second <c>SpellLook.Resolve</c> call and no colour
/// derived anywhere else.
/// </para>
/// <para>
/// (It is <c>Edge</c>, not "Secondary": two <c>SpellLook</c> doc comments called the field
/// Secondary for as long as it has existed, and nothing ever declared a member by that name - 1jg
/// found this by grepping the comment rather than the declarations. Those comments are fixed.)
/// </para>
/// </summary>
public static class ProjectileTrail
{
    /// <summary>Metres of flight between emitted voxels. Distance-gated, not time-gated, so a
    /// fast bolt and a slow one lay the same spacing instead of the slow one drawing a solid
    /// ribbon and the fast one a dotted line.</summary>
    public const float Step = 0.3f;

    /// <summary>Seconds a voxel survives. Kept short on purpose: the tail tapers because older
    /// voxels are about to be recycled, which is what gives the stream a comet-exhaust gradient
    /// without a per-voxel fade, an alpha ramp, or an Update of its own.</summary>
    public const float Life = 0.35f;

    /// <summary>Small enough to read as spray rather than as objects (a trail of 10 cm cubes is
    /// the "floating objects" failure 1gb was filed for, which is why the impact debris is
    /// chunk-sized and this is not).</summary>
    private static readonly float MinSize = 0.05f;
    private static readonly float MaxSize = 0.09f;

    private static GameObject _template;

    /// <summary>
    /// One static cube GO shared by every trail voxel, built once and kept inactive so its own
    /// transform/renderer cost is zero. Cloned from the same one per-instance mesh the terrain's
    /// excavation debris uses (<c>WorldStreamer.SharedDebrisCube</c>) rather than calling
    /// CreatePrimitive per emit, which would allocate a fresh cube mesh for every voxel.
    /// <para>
    /// It is a SEPARATE template from that one on purpose: <c>SharedDebrisCube</c> is private to
    /// the WorldStreamer partial, and duplicating a cube template across two files is the second
    /// spelling rule 8 warns about. The alternative - promoting the streamer's template to a
    /// shared owner - was judged a wider change than this task earns, so the duplication is
    /// deliberate and recorded here rather than silent.
    /// </para>
    /// </summary>
    private static GameObject Template
    {
        get
        {
            if (_template != null) return _template;
            _template = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _template.name = "TrailVoxelTemplate";
            // Collider dropped: a trail voxel is cosmetic and must never be hit by the
            // projectile's own raycasts, the ground probe, or the player's controller.
            // DestroyImmediate, not Destroy: Destroy is deferred to the end of the frame, and the
            // first trail voxel is usually emitted in the very frame the template is built - a
            // deferred Destroy would still leave the collider on it long enough for that clone to
            // inherit one, putting a collider on live trail geometry for a frame. Safe here
            // because this template is built at runtime and never rendered or collided into.
            Collider col = _template.GetComponent<Collider>();
            if (col != null) Object.DestroyImmediate(col);
            _template.SetActive(false);
            return _template;
        }
    }

    /// <summary>
    /// Leave one voxel at <paramref name="position"/>. Pooled (1e6's transient pool) when a
    /// pooler exists; the pooler owns the delayed recycle, so no voxel carries its own timer.
    /// </summary>
    public static void Emit(Vector3 position, Color color)
    {
        var pool = ObjectPooler.Instance;
        GameObject voxel = pool != null ? pool.Get(Template) : Object.Instantiate(Template);
        voxel.SetActive(true);
        voxel.name = "TrailVoxel";
        voxel.transform.position = position;
        // Random rotation + random size so the stream does not read as a mechanical dotted line of
        // identical cubes. Position is world-space and unparented: the voxel must stay where the
        // bolt was, which is the entire point of a trail.
        voxel.transform.rotation = Random.rotation;
        voxel.transform.localScale = Vector3.one * Random.Range(MinSize, MaxSize);
        var r = voxel.GetComponent<Renderer>();
        // Renderer.material returns a per-renderer instance that Unity CACHES on the pooled
        // object, so this allocates once per pooled voxel, not once per emit - which is why this
        // stays on the house idiom instead of reaching for a MaterialPropertyBlock (unused
        // anywhere in this project).
        if (r != null) r.material.color = color;

        if (pool != null) pool.Return(voxel, Life);
        else Object.Destroy(voxel, Life);
    }
}
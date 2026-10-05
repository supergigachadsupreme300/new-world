using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Pooled, per-spell impact flashes (1id) — the shared home for the <see cref="SpellImpactStyle"/>
/// families that <see cref="SpellLook"/> resolves.
///
/// <para><b>What replaced <c>SkillFx.ImpactSphere</c>, and why it had to be replaced.</b> Its only call
/// site (<c>SpellEffect.cs:313</c>) was reached by Projectile spells alone, and it allocated a fresh
/// primitive <i>and a fresh <c>Material</c></i> per impact. 1ih adds on-hit flashes to Zone (77),
/// Beam (11) and Vortex (8) spells that previously had none, so the per-tick rate rises by two
/// orders of magnitude; at 0.5 s tick intervals a single blizzard is 2 flashes/s, and a screen with
/// several of them plus a storm is where "one primitive per impact" stops being affordable.</para>
///
/// <para><b>Pooling follows the retired <c>SpellStorm.StrikeFlash</c> precedent</b> rather than
/// <c>ObjectPooler</c>: <c>SpawnTransient</c> keys its pool by <c>prefab.GetEntityId()</c> and
/// everything here is built at runtime from primitives, so there is no prefab to key on (THINKING.md
/// 1ib H38). <c>StrikeFlash</c> itself was deleted in 1ih — it was the last per-spell strike-only
/// flash, and it is now this one dispatcher — so it survives here as history, not as a live
/// reference.</para>
///
/// <para><b>Each instance owns its materials.</b> The fade writes <c>_mat.color</c> in place, so a
/// shared or cached material would corrupt every other live flash of the same colour — the reason
/// <c>SkillFx.SharedSpriteMaterial</c> carries that warning. A pooled instance is returned to the
/// pool only after its fade has finished, so its materials are safe to reuse in place.</para>
///
/// <para><b>Two guards, because the load changed.</b> <c>StrikeFlash</c>'s <c>PoolCap = 32</c> was
/// sized for 14 storm spells; ~101 spells now share this path, so the cap is raised
/// (<see cref="PoolCap"/>) AND a <see cref="PerFrameBudget"/> drops requests past a per-frame limit
/// rather than thrashing the pool. Over-budget requests are <i>dropped, not queued</i>: a queued
/// flash would arrive after the event that caused it, which is worse than no flash at all.</para>
///
/// <para>Read-only in the sense that matters: this class never touches terrain, damage or any
/// gameplay state. It draws.</para>
///
/// <para><b>1jb: this class owns the flash's LIFETIME; the shapes moved to
/// Models/Magic/MagicImpactModelBuilder.cs.</b> The eight per-style part builders used to be private
/// instance methods on the nested <c>ImpactFlash</c>, so the only way to find the geometry was to
/// know it was filed under the pool. They are now one static builder that returns what it built, and
/// <c>ImpactFlash</c> keeps the pool, the budget, the growth curve, the tumble and the fade.</para>
/// </summary>
public static class SpellImpactFx
{
    /// <summary>
    /// Max <i>retained idle</i> instances per style — the pool's own size, not a live-instance cap.
    /// Naming the two differently is how this class ended up with a comment that said "max live"
    /// while the code bounded the pool, which is the one quantity that does nothing on screen.
    /// A live instance is never capped; <see cref="PerFrameBudget"/> is what bounds live count.
    /// </summary>
    public const int PoolCap = 96;

    /// <summary>
    /// Max flashes spawned in any one frame across all styles. A storm with 14 strikes plus several
    /// ticking zones can ask for far more than looks right; capping keeps the cost bounded and the
    /// excess simply does not appear.
    /// </summary>
    public const int PerFrameBudget = 24;

    private const float DefaultLifetime = 0.42f;

    /// <summary>
    /// The growth curve's value at <c>t = 0</c>. Stated as a constant because <see cref="Play"/>
    /// and <see cref="Update"/> both need it, and they must agree: a freshly activated flash is
    /// rendered this frame, and whether its own <c>Update</c> also runs this frame depends on
    /// script execution order. Without seeding the scale here, an unlucky order draws one frame
    /// at full <c>1.0</c> instead of <c>0.22</c> — a visibly larger, briefly wrong flash.
    /// </summary>
    private const float StartScale = 0.22f;
    private const float GrowEnd = 0.38f;

    private static readonly Dictionary<SpellImpactStyle, List<ImpactFlash>> Pools
        = new Dictionary<SpellImpactStyle, List<ImpactFlash>>();

    private static int _budgetFrame = -1;
    private static int _spawnedThisFrame;

    /// <summary>Requests refused by the per-frame budget since launch (diagnostic, rule 7).</summary>
    public static int DroppedSinceLaunch { get; private set; }

    /// <summary>
    /// Play this spell's impact flash. The one entry point every strike path should call, so no
    /// caller re-derives the style (1ib's whole point).
    /// </summary>
    /// <param name="look">Resolved identity; supplies style, colours and scale.</param>
    /// <param name="radius">Ground/area radius the flash should read at.</param>
    /// <param name="scaleMul">Extra multiplier (overcharge size, storm stacking).</param>
    public static void Spawn(Vector3 worldPos, Vector3 upDir, in SpellLook look, float radius, float scaleMul = 1f)
    {
        if (look.Impact == SpellImpactStyle.Inherit)
            return;

        if (_budgetFrame != Time.frameCount)
        {
            _budgetFrame = Time.frameCount;
            _spawnedThisFrame = 0;
        }
        if (_spawnedThisFrame >= PerFrameBudget)
        {
            DroppedSinceLaunch++;
            return;
        }
        _spawnedThisFrame++;

        ImpactFlash flash = Acquire(look.Impact);
        if (flash == null)
            return;
        flash.Play(worldPos, upDir, look, Mathf.Max(0.5f, radius) * scaleMul, DefaultLifetime);
    }

    private static ImpactFlash Acquire(SpellImpactStyle style)
    {
        if (!Pools.TryGetValue(style, out var pool))
        {
            pool = new List<ImpactFlash>();
            Pools[style] = pool;
        }

        for (int i = 0; i < pool.Count; i++)
        {
            var f = pool[i];
            if (f == null)
            {
                pool.RemoveAt(i);
                i--;
                continue;
            }
            if (f.gameObject.activeSelf) continue;
            pool.RemoveAt(i);
            return f;
        }

        Shader shader = Shader.Find("Sprites/Default") ?? Shader.Find("Unlit/Color");
        if (shader == null) return null;
        var go = new GameObject("SpellImpact_" + style);
        var flash = go.AddComponent<ImpactFlash>();
        flash.Build(style, shader);
        return flash;
    }

    /// <summary>Hand a finished flash back to its style's pool. Called by the flash itself.</summary>
    private static void Recycle(ImpactFlash flash)
    {
        if (flash == null) return;
        if (!Pools.TryGetValue(flash.Style, out var pool)) return;
        if (pool.Count >= PoolCap)
        {
            Object.Destroy(flash.gameObject);
            return;
        }
        // Guard against a double-recycle: a flash already sitting in the pool would otherwise be
        // added twice and handed out to two different casts.
        if (pool.Contains(flash)) return;
        pool.Add(flash);
    }

    /// <summary>
    /// One pooled flash. Built once per style from a fixed set of primitives and afterwards only
    /// <i>shown</i> — so a replay is a transform write and a material colour, never a
    /// <c>CreatePrimitive</c>. Materials are per-instance (see class remarks).
    /// </summary>
    private sealed class ImpactFlash : MonoBehaviour
    {
        private float _age;
        private float _lifetime = 0.42f;
        private float _scale = 1f;
        private Vector3 _spin;

        // 1jb: ONE list of what the builder actually made. This used to be three parallel lists
        // (_materials/_parts/_spins) that the fade and tumble loops below walked by the same index —
        // an invariant nothing checked and no compiler enforced. The geometry now returns a single
        // Part per built piece (transform + material + spin flag together), so "these three lists must
        // stay index-aligned" is unrepresentable rather than merely correct. See
        // MagicImpactModelBuilder, which owns the shapes; this class owns the lifetime.
        private List<MagicImpactModelBuilder.Part> _parts = new List<MagicImpactModelBuilder.Part>();

        public SpellImpactStyle Style { get; private set; }

        /// <summary>Called immediately after AddComponent, while nothing has been shown yet, so the
        /// object is never seen half-built.</summary>
        public void Build(SpellImpactStyle style, Shader shader)
        {
            Style = style;
            // 1jb: the per-style geometry is MagicImpactModelBuilder's. Everything below is lifetime.
            _parts = MagicImpactModelBuilder.Build(transform, style, shader);

            gameObject.SetActive(false);
        }

        public void Play(Vector3 pos, Vector3 up, in SpellLook look, float radius, float lifetime)
        {
            transform.position = pos + up.normalized * 0.02f;
            transform.rotation = Quaternion.FromToRotation(Vector3.up, up.normalized);
            _scale = radius * Mathf.Max(0.5f, look.Scale);
            _lifetime = Mathf.Max(lifetime, 0.08f);
            _age = 0f;
            // Tempo drives tumble speed: a faster-tempo spell reads as more urgent without any
            // extra geometry.
            _spin = new Vector3(37f, 61f, 23f) * look.Tempo;

            // Seed the growth curve's t=0 value here, not in the first Update — see StartScale.
            transform.localScale = Vector3.one * (_scale * StartScale);

            // Two-tone: alternating parts take Edge, the rest take Core, so a family reads as one
            // spell family rather than one flat colour.
            for (int i = 0; i < _parts.Count; i++)
            {
                Material m = _parts[i].Mat;
                if (m == null) continue;
                Color c = ((i & 1) == 1) ? look.Edge : look.Core;
                c.a = 1f;
                m.color = c;
            }

            gameObject.SetActive(true);
        }

        private void Update()
        {
            _age += Time.deltaTime;
            float t = Mathf.Clamp01(_age / _lifetime);

            // Grow fast then hold — reads as an impact, not a slow inflate. StartScale/GrowEnd are
            // shared with Play, so the first drawn frame and the animated one cannot disagree.
            transform.localScale = Vector3.one * (_scale * Mathf.Lerp(StartScale, 1f, Mathf.SmoothStep(0f, GrowEnd, t)));

            if (t < 1f)
            {
                // Only the shard parts tumble; the ring/foot must stay flat on the ground.
                for (int i = 0; i < _parts.Count; i++)
                {
                    if (_parts[i].Spins) _parts[i].T.Rotate(_spin * Time.deltaTime, Space.Self);
                }
            }

            float alpha = 1f - t;
            for (int i = 0; i < _parts.Count; i++)
            {
                Material m = _parts[i].Mat;
                if (m == null) continue;
                Color c = m.color;
                c.a = alpha;
                m.color = c;
            }

            if (t >= 1f)
            {
                gameObject.SetActive(false);
                transform.localScale = Vector3.one;
                Recycle(this);
            }
        }
    }
}
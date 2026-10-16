using System.Collections;
using UnityEngine;

/// <summary>
/// Storm delivery (SpellDelivery.Storm): a persistent area that repeatedly strikes at points inside
/// its radius for the spell's duration. Each strike flashes an element-styled bolt/burst, deals a
/// burst of the spell's power inside a small radius, and applies its status through the shared
/// pipeline. Strikes land with a small random stagger so the storm reads as chaotic, not pinging.
/// <para><b>1kf: a Storm may also mount a persistent body</b> — the resolved
/// <see cref="SpellLook.StormBodyModel"/> gates it: only an authored profile grants
/// <see cref="StormBody.Funnel"/> (Firestorm alone), <see cref="StormBody.FrozenField"/>
/// (1l70: Deep Freeze) or <see cref="StormBody.VortexCircle"/> (1l100: Blizzard, wearing the
/// conflagration's model) — and 1l111 added <see cref="StormBody.Rain"/> (Downpour) and
/// <see cref="StormBody.Thunderhead"/> (Thunderhead, which also swaps Devastation's generic cloud
/// puff for a body of its own at its own altitude) — and that funnel spins its root 240 deg/s,
/// the same spin the pre-1kc conflagration's <c>SpellZone</c> ran while it pulled. The other 10
/// storms resolve <see cref="StormBody.None"/> and are byte-for-byte unchanged. Spin and build
/// share ONE predicate — the body's presence — so they cannot drift apart.</para>
/// </summary>
public class SpellStorm : MonoBehaviour
{
    public float Radius = 3f;
    public float Lifetime = 3f;
    public float TickInterval = 0.5f;
    public int StrikesPerTick = 2;
    public float StrikePowerMultiplier = 0.8f;

    /// <summary>1l77: how high above the area a <see cref="SpellData.StormCloud"/> storm (Devastation)
    /// mounts its dark cloud — the source every strike's bolt falls FROM. Visual only: the bolt is
    /// segments with no collider, so this is a picture height, not a projectile runway.</summary>
    public const float StormCloudHeight = 45f;

    /// <summary>1l111: how high a <see cref="StormBody.Rain"/> storm hangs its rain shelf — low on
    /// purpose, so the curtain reads as driving rain over the area rather than a ceiling the player
    /// cannot see. Visual only, like <see cref="StormCloudHeight"/>.</summary>
    public const float RainCloudHeight = 5f;

    /// <summary>1l111: how high a <see cref="StormBody.Thunderhead"/> thunderhead sits. Lower than
    /// <see cref="StormCloudHeight"/> because the thunderhead is a model worth looking at, while
    /// Devastation's puff is a horizon prop; the strike bolts fall from this altitude either way
    /// (see <c>_cloudY</c>).</summary>
    public const float ThunderheadHeight = 16f;

    /// <summary>1l111: the altitude the long strike bolts fall FROM — Devastation's 45 m cloud, or the
    /// Thunderhead body's own. Written once in <c>Initialize</c> from the resolved body and read by
    /// both the body mount and <c>SpawnStrikeFx</c>, so the cloud and the bolts it throws can never
    /// be authored against two different numbers (rule 8).</summary>
    private float _cloudY;

    /// <summary>1l77: the storm cloud's dark tint. Public so the QA bench mounts the SAME colour
    /// rather than re-spelling it (rule 13).</summary>
    public static readonly Color StormCloudTint = new Color(0.15f, 0.15f, 0.18f);

    private SpellCaster _caster;
    private SpellData _spell;
    private float _power;
    private Transform _casterRoot;
    private float _age;
    private float _tick;
    private Color _color;
    private DamageType _type;
    private bool _stormCloud;
    private readonly Collider[] _strikeBuffer = new Collider[128];

    /// <summary>1ie/1ih: the storm's resolved look, cached at Initialize.</summary>
    private SpellLook _look;

    public void Initialize(SpellCaster caster, SpellData spell, float power, float radiusMult = 1f)
    {
        _caster = caster;
        _spell = spell;
        _power = power;
        _casterRoot = caster != null ? caster.transform.root : null;
        _stormCloud = spell != null && spell.StormCloud;
        radiusMult = Mathf.Max(radiusMult, 0.01f);
        // 1ie: the storm's body colour and its spawn ring both come from the per-spell look, so a
        // lightning storm and a frost storm are told apart by their Core hue as well as by
        // their shape. _type is kept as-is — it still gates the bolt-vs-sphere FX branch.
        //
        // Resolved OUTSIDE the spell != null guard because RingFlash below reads _look.Scale
        // unconditionally. In the guard it would stay default(SpellLook), whose Scale is 0 —
        // so a spell-less storm spawned one ring at scale 0 (5x too small) while its body
        // used the real Radius. Zone/Beam/Tornado all seed the fallback for this reason;
        // read as a sibling rather than re-derived per class.
        _look = spell != null ? SpellLook.Resolve(spell) : SpellLook.Resolve(DamageType.Wind, ProjectileShape.Auto);
        _cloudY = _look.StormBodyModel == StormBody.Thunderhead ? ThunderheadHeight : StormCloudHeight;
        if (spell != null)
        {
            Radius = Mathf.Max(spell.Radius * radiusMult, 1.5f);
            if (spell.Duration > 0f) Lifetime = spell.Duration;
            if (spell.TickInterval > 0f) TickInterval = spell.TickInterval;
            _color = _look.Core;
            _type = spell.Type;
        }

        // 1kf: the authored funnel body mounts BEFORE the spawn flash, mirroring
        // SpellZone.Initialize's BuildVisual-then-RingFlash order, so the two persistent-area
        // deliveries read the same. Gated on the resolved axis — a spell-less storm's fallback
        // look resolves None, and Radius/_color are only real when spell != null anyway.
        if (_look.StormBodyModel == StormBody.Funnel)
            SpellZoneModelBuilder.BuildFunnel(transform, Radius, SkillFx.SharedSpriteMaterial(_color));

        // 1l70: Deep Freeze's frozen-field storm body — a frost disc + ice spikes that sit under the
        // storm's repeated freeze strikes. The SAME builder the 1l6z zone body used (one
        // implementation, two delivery gates); it deliberately does NOT spin — 240 deg/s is a wind,
        // and a frozen field has none.
        // 1l102: pass the storm's lifetime so the spikes grow up at the start and sink back down as
        // the storm ends ("needles would go up then go down when spell end"). Lifetime is set from the
        // spell above (spell.Duration), so the sink lands exactly when SpellStorm destroys the body.
        if (_look.StormBodyModel == StormBody.FrozenField)
            SpellZoneModelBuilder.BuildFrozenField(transform, Radius, _color, _look.Edge, Lifetime);

        // 1l100: Blizzard wears the conflagration's model (the user's "use the same model as
        // Conflagration"), mounted here as a STORM body rather than by switching Blizzard to
        // SpellDelivery.Vortex — rule 20: moving the delivery would have changed its resolution and
        // lost its per-strike chills. The SAME builder Conflagration's SpellZone calls, so one
        // implementation, three gates (Conflagration Zone, this axis, the QA bench).
        //
        // Colour: Conflagration passes SpellLook.HotCore (Fire's embers are yellow). That derivation
        // rotates a hue TOWARD yellow, so on Ice it would paint the rising rings a fire-yellow — a
        // second spelling of Fire on an ice storm. The disc still takes the spell's Core, but the
        // rising rings take _look.Edge (the spell's own core brightened/cooled, see EdgeFor), which
        // is the same palette-derived, non-drifting choice that keeps the body reading as ice.
        if (_look.StormBodyModel == StormBody.VortexCircle)
            SpellZoneModelBuilder.BuildConflagration(transform, Radius, _color, _look.Edge);

        // 1l111: Downpour's body — a low rain shelf hung with a falling curtain (the motion lives in
        // SpellZoneModelBuilder.RainFall, not here). Ground-level like the other area bodies, so it
        // sits under the strikes rather than over them, and it deliberately does NOT spin.
        if (_look.StormBodyModel == StormBody.Rain)
            SpellZoneModelBuilder.BuildRainCloud(transform, Radius, RainCloudHeight, StormCloudTint, _color);

        // 1l111: Thunderhead's body — a cumulonimbus at _cloudY, the altitude the strike bolts fall
        // from, so the cloud the player sees IS the cloud the bolts come out of. Size: the radius
        // drives the shelf span (0.85x, with a floor so a narrow storm still hangs a readable
        // thunderhead); the altitude is the shared constant, never the radius.
        if (_look.StormBodyModel == StormBody.Thunderhead)
            SpellZoneModelBuilder.BuildThunderhead(transform, Mathf.Max(Radius * 0.85f, 2.5f),
                _cloudY, StormCloudTint, _color);

        // 1l77: a StormCloud storm (Devastation, testing.md #8) mounts a dark cloud high above the
        // area; every strike then falls as a long jagged bolt from that cloud down to the strike
        // point, so the spell reads as a sky-fury instead of ground pings. The cloud is parented to
        // the storm's own transform, so it lives exactly as long as the storm and needs no lifetime
        // of its own. The dark tint + Renderer walk mirror RandomEventManager's rain cloud (the
        // proven local use of MapBuilder.BuildCloud — rule 3).
        // 1l111: a Thunderhead-bodied storm keeps the SKY-BOLT behaviour (it still passes
        // stormCloud: true) but draws its own voxel cloud at the same altitude instead of this puff —
        // two gates, one behaviour: this branch is the generic fallback, not the definition.
        if (_stormCloud && _look.StormBodyModel != StormBody.Thunderhead)
        {
            Vector3 cloudPos = transform.position + Vector3.up * _cloudY;
            var cloud = MapBuilder.BuildCloud(transform, cloudPos, 5f);
            foreach (var r in cloud.GetComponentsInChildren<Renderer>())
                r.material.color = StormCloudTint;
        }

        SkillFx.RingFlash(transform.position, Vector3.up, _color, Radius, 0.5f, _look.Scale);
    }

    private void Update()
    {
        if (_spell == null)
        {
            Destroy(gameObject);
            return;
        }
        _age += Time.deltaTime;
        // 1kf: spin only while the FUNNEL exists — the exact gate SpellZone.Update used
        // (PullSpeed > 0, i.e. the pulling-vortex funnels), so the pre-1kc conflagration model
        // reproduces down to its 240 deg/s turn. Strikes are position-based, so a yaw of the root
        // moves nothing they read (transform.position is unchanged by a self-rotation).
        // (1l100: Blizzard's VortexCircle body is deliberately NOT in this gate — its rising rings
        // carry their own spin, and the conflagration disc must not turn under them.)
        if (_look.StormBodyModel == StormBody.Funnel)
            transform.Rotate(0f, 240f * Time.deltaTime, 0f, Space.Self);

        if (_age >= Lifetime)
        {
            Destroy(gameObject);
            return;
        }

        _tick -= Time.deltaTime;
        if (_tick <= 0f)
        {
            _tick = TickInterval > 0f ? TickInterval : 0.5f;
            BurstStrikes();
        }
    }

    private void BurstStrikes()
    {
        int count = Mathf.Max(StrikesPerTick, 1);
        for (int i = 0; i < count; i++)
            StartCoroutine(StrikeDelayed(Random.Range(0f, 0.35f)));
    }

    private IEnumerator StrikeDelayed(float delay)
    {
        if (delay > 0f) yield return new WaitForSeconds(delay);
        if (this == null) yield break;

        Vector3 at = RandomStrikePoint();

        // Sky storms (summonFallingRock: Meteor Rain, Rockfall) send a big rock down to each strike
        // point; the strike's flash/damage/deform fire when it lands, so the storm reads as boulders
        // raining in rather than instant ground pings. Purely visual rock — no collider (1cx).
        // `_look` is already resolved above (:47), so the sky-rock style rides along with the tint
        // rather than being re-resolved here (1f7).
        if (_spell != null && _spell.SummonFallingRock)
        {
            float scale = Mathf.Max(_spell.Radius * 0.5f, 1.2f);
            Color tint = _color;
            SkillFx.FallRock(at, scale, tint, _look.SkyRock, () =>
            {
                if (this == null) return;
                SpawnStrikeFx(at);
                ResolveStrike(at);
                DeformGround(at);
            });
            yield break;
        }

        SpawnStrikeFx(at);
        ResolveStrike(at);
        DeformGround(at);
    }

    /// <summary>Earth storms (TerrainShape, e.g. Rockfall) dent the ground under each strike —
    /// a permanent pit where every boulder lands (§3.8). Only runs for spells that carry a
    /// terrain shape; other elements no-op in TerrainDeformer.</summary>
    private void DeformGround(Vector3 at)
    {
        if (_spell == null || _spell.TerrainShape == TerrainShape.None) return;
        Vector3 ground = at;
        if (Physics.Raycast(at + Vector3.up * 0.5f, Vector3.down, out RaycastHit hit, 10f))
            ground = hit.point;
        float strikeRadius = Mathf.Max(_spell.Radius * 0.55f, 1.2f);
        TerrainDeformer.Apply(ground, strikeRadius, _spell.TerrainShape);
    }

    /// <summary>Largely prefer striking near an enemy inside the area; otherwise a random point.</summary>
    private Vector3 RandomStrikePoint()
    {
        int count = Physics.OverlapSphereNonAlloc(transform.position, Radius, _strikeBuffer);
        float bestSqr = float.MaxValue;
        Transform best = null;
        for (int i = 0; i < count && i < 24; i++)
        {
            var col = _strikeBuffer[i];
            if (col == null) continue;
            Transform root = col.transform.root;
            if (root == _casterRoot) continue;
            if (root.CompareTag("Player") || root.CompareTag("Companion")) continue;
            if (!root.TryGetComponent<EnemyController>(out _) &&
                !root.TryGetComponent<BossController>(out _)) continue;

            float dSqr = (root.position - transform.position).sqrMagnitude;
            if (dSqr < bestSqr)
            {
                bestSqr = dSqr;
                best = root;
            }
        }
        if (best != null)
            return best.position;

        Vector2 r = Random.insideUnitCircle * Radius;
        return transform.position + new Vector3(r.x, 0.25f, r.y);
    }

    private void ResolveStrike(Vector3 at)
    {
        if (_caster == null || _spell == null) return;

        float strikeRadius = Mathf.Max(_spell.Radius * 0.55f, 1.2f);
        int count = Physics.OverlapSphereNonAlloc(at, strikeRadius, _strikeBuffer);
        for (int i = 0; i < count; i++)
        {
            var col = _strikeBuffer[i];
            if (col == null) continue;
            Transform root = col.transform.root;
            if (root == _casterRoot) continue;

            if (_spell.Heals && root.TryGetComponent<IHealable>(out _))
            {
                _caster.ResolveHeal(_spell, _power * StrikePowerMultiplier, col.gameObject);
                continue;
            }
            if (root.CompareTag("Player") || root.CompareTag("Companion")) continue;
            if (!col.gameObject.TryGetComponent<IDamageable>(out _)) continue;

            _caster.ResolveHitAt(col.gameObject, _spell, _power * StrikePowerMultiplier);
        }
    }

    private void SpawnStrikeFx(Vector3 at)
    {
        float groundY = transform.position.y;
        Vector3 ground = new Vector3(at.x, groundY, at.z);
        Color c = _color;

        if (_type == DamageType.Lightning)
        {
            if (_stormCloud)
            {
                // 1l77: Devastation's strike is a long multi-segment bolt from the mounted cloud down
                // to the strike point — the cloud is the spell's identity, so it does NOT draw the
                // crossed-bar flash the other lightning storms use. Pure visual (no collider); the
                // 0.5 s lifetime matches the SkyStrikeBolt one-shot (Rule 13: one bolt, one look).
                Vector3 cloud = transform.position + Vector3.up * _cloudY;
                SpellBoltModelBuilder.BuildBolt(null, cloud, at, SkillFx.SharedSpriteMaterial(c),
                    0.5f, segments: 12, width: 0.2f, jitterAmplitude: 0.7f);
            }
            else
            {
                // 1jd: the crossed bolt pair moved to SpellStormModelBuilder (Models/Magic), together with
                // the BoltFader that owns its lifetime — the fader has to travel with the shape it fades or
                // the bolts would stand in the world forever. The per-strike flash and ring below are NOT
                // part of that model: they are the shared impact families and they stay here.
                SpellStormModelBuilder.BuildLightningBolt(at, SkillFx.SharedSpriteMaterial(c));
            }
        }

        // 1ih: the per-strike flash is now the shared per-spell impact family instead of a
        // sphere-only StrikeFlash. 14 storm spells share it, which is what sized PoolCap at 96 —
        // StrikeFlash's 32 was sized for the same spells but is now retired, so its cap has no
        // remaining owner. The ring stays: a strike with a flash AND a ring is the strongest
        // on-screen read, and only the sphere is redundant.
        SpellImpactFx.Spawn(at, Vector3.up, _look, 1.6f);
        SkillFx.RingFlash(ground, Vector3.up, c, Random.Range(0.8f, 1.4f), 0.35f, _look.Scale);
    }
}

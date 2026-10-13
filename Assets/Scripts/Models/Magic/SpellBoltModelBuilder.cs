using UnityEngine;

/// <summary>
/// 1l73: the jagged "two points connected by a lightning stroke" body — the delivery visual the
/// Lightning school uses wherever a bolt must CONNECT two world points: a Chain Lightning jump
/// (1l76), a teleporting Leap Bolt (1l78), a single sky strike (1l74), a Storm cloud's strike
/// (1l77) and a caster-centered nova's downward bolts (1l79). One builder for the school so the
/// look cannot drift between those five callers.
/// <para>This is the same cube-chain technique <c>RandomEventManager.SpawnJaggedBolt</c> ships,
/// generalised from "vertical storm bolt, jitter hard-coded to X/Z" to an arbitrary from→to axis
/// and stated once instead of per caller. <c>SpellStormModelBuilder.BuildLightningBolt</c> is NOT
/// this builder: it draws two crossing BARS at a single point and has no path to connect two
/// points with.</para>
/// <para><b>Body-only, like the builder it shares the technique with.</b> Every collider is
/// destroyed and nothing ever queries a bolt — the delivery resolves its own damage points, and
/// the bolt is a picture with a lifetime, nothing more.</para>
/// <para>Materials are handed in as the caller's resolved core tint (via
/// <c>SkillFx.SharedSpriteMaterial(...)</c>) and assigned as SHARED, so all of a bolt's segments
/// batch as one draw — the same cube-chain batching the thunder-storm event uses.</para>
/// </summary>
public static class SpellBoltModelBuilder
{
    /// <summary>One-shot jagged bolt between two world points with a fixed life.
    /// <para>Returns the root (a child of <paramref name="parent"/> when given, else the world),
    /// so a caller may fade/scale it or park it — the QA bench parks pedestal bolts with a large
    /// lifetime. The root is destroyed after <paramref name="lifetime"/> when that is &gt; 0.</para></summary>
    public static GameObject BuildBolt(Transform parent, Vector3 from, Vector3 to,
        Material sharedMat, float lifetime, int segments = 10,
        float width = 0.16f, float jitterAmplitude = 0.45f)
    {
        var root = new GameObject("SpellBolt");
        if (parent != null) root.transform.SetParent(parent, false);
        root.transform.position = from;
        BoltStrip strip = CreateStrip(root.transform, segments, sharedMat, width, jitterAmplitude);
        strip.Rebuild(Vector3.zero, to - from);
        if (lifetime > 0f) Object.Destroy(root, lifetime);
        return root;
    }

    /// <summary>Fixed-count bolt for a live channel: the same cubes get re-shuffled by every
    /// <see cref="BoltStrip.Rebuild"/>, so a re-jittering beam costs transform writes, never a
    /// per-visit create/destroy churn (1l75). Cubes are laid out in the PARENT's local space along
    /// the from→to axis, so the caller owns where the parent sits and points.</summary>
    public static BoltStrip CreateStrip(Transform parent, int segments, Material sharedMat,
        float width = 0.16f, float jitterAmplitude = 0.45f)
    {
        var strip = new BoltStrip();
        strip.Init(parent, segments, sharedMat, width, jitterAmplitude);
        return strip;
    }
}

/// <summary>See <see cref="SpellBoltModelBuilder.CreateStrip"/>. Not a MonoBehaviour — it holds the
/// cube transforms a live channel re-aims through their parent.</summary>
public sealed class BoltStrip
{
    private Transform[] _cubes;
    private Material _sharedMat;
    private float _width;
    private float _jitter;

    public void Init(Transform parent, int segments, Material sharedMat, float width, float jitterAmplitude)
    {
        _sharedMat = sharedMat;
        _width = Mathf.Max(width, 0.03f);
        _jitter = Mathf.Max(jitterAmplitude, 0f);
        _cubes = new Transform[Mathf.Max(segments, 2)];
        for (int i = 0; i < _cubes.Length; i++)
        {
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = "BoltSeg";
            Collider col = cube.GetComponent<Collider>();
            if (col != null) Object.Destroy(col);
            cube.transform.SetParent(parent, false);
            var r = cube.GetComponent<MeshRenderer>();
            if (r != null && sharedMat != null) r.sharedMaterial = sharedMat;
            _cubes[i] = cube.transform;
        }
    }

    /// <summary>Re-jitter: lays the cubes along the local from→to axis, each segment rotated onto
    /// its OWN direction, endpoints exact, interior points kicked off the axis by a tapered random
    /// amount — the same taper the thunder event uses (<c>1f - t * 0.5f</c>), so the stroke reads
    /// chaotic at the head and settles toward the far end.</summary>
    public void Rebuild(Vector3 from, Vector3 to)
    {
        if (_cubes == null || _cubes.Length == 0) return;
        Vector3 axis = to - from;
        float len = axis.magnitude;
        Vector3 dir = len > 0.0001f ? axis / len : Vector3.up;
        // Perpendicular basis for the lateral kick, so any from→to (vertical OR horizontal) jitters
        // off the axis instead of only X/Z like the storm event's vertical-only bolt.
        Vector3 u = Vector3.Cross(dir, Mathf.Abs(dir.y) < 0.9f ? Vector3.up : Vector3.right).normalized;
        Vector3 v = Vector3.Cross(u, dir).normalized;

        Vector3 prev = from;
        for (int i = 0; i < _cubes.Length; i++)
        {
            float t = (float)(i + 1) / _cubes.Length;
            Vector3 next;
            if (i == _cubes.Length - 1)
            {
                next = to;
            }
            else
            {
                float kick = _jitter * (1f - t * 0.5f);
                Vector3 lateral = u * (Random.Range(-1f, 1f) * kick) + v * (Random.Range(-1f, 1f) * kick);
                next = Vector3.Lerp(from, to, t) + lateral;
            }

            Vector3 mid = (prev + next) * 0.5f;
            float segLen = Mathf.Max(Vector3.Distance(prev, next), 0.01f);
            Transform cube = _cubes[i];
            cube.localPosition = mid;
            cube.localScale = new Vector3(SegWidth(t, i), segLen, SegWidth(t, i));
            Vector3 segDir = (next - prev).normalized;
            if (segDir.sqrMagnitude > 0.0001f)
                cube.localRotation = AxisRotation(segDir);

            prev = next;
        }
    }

    /// <summary>Segment thickness: the first segment reads fat (the strike's head), later ones
    /// taper toward the far end so the bolt reads pointed.</summary>
    private float SegWidth(float t, int i)
    {
        if (i == 0) return _width * 1.6f;
        return _width * (1f - 0.25f * t);
    }

    /// <summary>Cube whose local Y is <paramref name="dir"/> (the length axis) — never degenerate,
    /// where the storm event's <c>Quaternion.LookRotation(Vector3.forward, dir)</c> breaks whenever
    /// dir is parallel to world Z, which any horizontal chain/leap bolt is.</summary>
    private static Quaternion AxisRotation(Vector3 dir)
    {
        Vector3 up = dir.sqrMagnitude > 0.0001f ? dir.normalized : Vector3.up;
        Vector3 fwd = Mathf.Abs(Vector3.Dot(up, Vector3.forward)) > 0.9f ? Vector3.right : Vector3.forward;
        return Quaternion.LookRotation(fwd, up);
    }
}
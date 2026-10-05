using UnityEngine;

/// <summary>
/// The storm spell's per-strike lightning bolt — the crackling crossed bar pair.
/// <para>Extracted from <c>Magic/Cast/SpellStorm.cs</c> in <b>1jd</b>: "storm bolt model" matched
/// nothing under <c>Models/</c> while the model shipped. The two bars, their names, their scales,
/// the 90° cross and the fade lifetime are unchanged.</para>
/// <para><b>Body-only, and the fader is what owns the piece.</b> Every collider is destroyed and
/// nothing ever queries a bolt — <c>SpellStorm.ResolveStrike</c> picks its own damage point, and the
/// ground deform is a data-layer carve, not a physics hit. So the bolt is a picture with a lifetime,
/// nothing more.</para>
/// </summary>
public static class SpellStormModelBuilder
{
    /// <summary>Crackling bolt column: two crossed tall thin bars, faded out by <c>BoltFader</c>.
    /// <para>The fader lives here with the shape it fades (1jd): it was a private nested class of
    /// <c>SpellStorm</c>, so moving the bars out would have left the animation behind and the bolts
    /// would have stood in the world forever. <c>Destroy</c> inside it is therefore
    /// <c>Object.Destroy</c> — the static-class rule 17 calls out, and the exact class of edit a diff
    /// cannot show because the line is textually identical.</para>
    /// <para>Returns false when no material is available, so the caller can skip the pair entirely —
    /// the same "no material, no primitive" guard the original had, kept at the same point in the
    /// call so the piece count per strike is unchanged.</para></summary>
    public static bool BuildLightningBolt(Vector3 at, Material sharedMat)
    {
        if (sharedMat == null) return false;

        GameObject bolt = GameObject.CreatePrimitive(PrimitiveType.Cube);
        bolt.name = "StormBoltA";
        DestroyCollider(bolt.transform);
        bolt.transform.position = at;
        bolt.transform.localScale = BoltScale;
        AssembleBolt(bolt, sharedMat);

        GameObject boltB = GameObject.CreatePrimitive(PrimitiveType.Cube);
        boltB.name = "StormBoltB";
        DestroyCollider(boltB.transform);
        boltB.transform.position = at;
        boltB.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);
        boltB.transform.localScale = BoltScale;
        AssembleBolt(boltB, sharedMat);
        return true;
    }

    /// <summary>The bars' scale, stated once. The fader re-derives its start scale from it, so the
    /// shape and the shrink-from-here can never be authored against two different numbers.</summary>
    public static readonly Vector3 BoltScale = new Vector3(0.1f, 3.2f, 0.1f);

    private static void AssembleBolt(GameObject bolt, Material sharedMat)
    {
        var r = bolt.GetComponent<MeshRenderer>();
        if (r != null && sharedMat != null)
            r.sharedMaterial = sharedMat;
        bolt.AddComponent<BoltFader>().Init(BoltScale, 0.25f);
    }

    private static void DestroyCollider(Transform t)
    {
        Collider col = t.GetComponent<Collider>();
        if (col != null) Object.Destroy(col);
    }

    /// <summary>Shrinks the lightning bars to nothing, then removes them (no lingering leak).
    /// The leak this guards against is real: the pre-1ir code forgot to destroy these bolts, so each
    /// strike left two permanent cubes in the world.</summary>
    private sealed class BoltFader : MonoBehaviour
    {
        private Vector3 _startScale;
        private float _lifetime = 0.25f;
        private float _age;

        public void Init(Vector3 startScale, float lifetime)
        {
            _startScale = startScale;
            _lifetime = lifetime;
        }

        private void Update()
        {
            _age += Time.deltaTime;
            float t = Mathf.Clamp01(_age / _lifetime);
            transform.localScale = _startScale * (1f - t);
            if (t >= 1f)
                Object.Destroy(gameObject);
        }
    }
}
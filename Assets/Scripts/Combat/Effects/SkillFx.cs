using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// One-shot, script-built combat FX for skill execution (visible feedback even when no
/// target is in range). Deliberately prefab-free so skills read without authored assets.
/// </summary>
public static class SkillFx
{
    private static Shader _spriteShader;
    private static readonly Dictionary<Color, Material> _spriteMats = new Dictionary<Color, Material>();

    /// <summary>
    /// Cached persistent material shared across FX renderers (zone/storm visuals), so persistent
    /// effects stop allocating a fresh Material per cast. Keyed by color, which caps the pool at
    /// the damage-palette size. Never mutate the returned material's color — fading FX (Faders,
    /// StrikeFlash) must keep their own per-face instance.
    /// </summary>
    public static Material SharedSpriteMaterial(Color color)
    {
        if (_spriteShader == null)
            _spriteShader = Shader.Find("Sprites/Default") ?? Shader.Find("Unlit/Color");
        if (_spriteShader == null) return null;
        if (_spriteMats.TryGetValue(color, out var mat) && mat != null)
            return mat;
        mat = new Material(_spriteShader) { color = color };
        _spriteMats[color] = mat;
        return mat;
    }
    /// <summary>
    /// Spawn a bright, forward-facing slash sheet at the strike origin and shrink it to
    /// nothing over <paramref name="lifetime"/> seconds. No collider, pure visual.
    /// </summary>
    public static void SlashFlash(Vector3 origin, Vector3 forward, float radius, float lifetime, Color color)
    {
        GameObject slice = GameObject.CreatePrimitive(PrimitiveType.Cube);
        slice.name = "SkillSlash";
        slice.transform.position = origin + forward * (radius * 0.5f);
        slice.transform.rotation = Quaternion.LookRotation(forward);
        slice.transform.localScale = new Vector3(radius * 1.3f, radius, 0.1f);

        Collider col = slice.GetComponent<Collider>();
        if (col != null)
            Object.Destroy(col);

        Shader shader = Shader.Find("Unlit/Color") ?? Shader.Find("Standard");
        Renderer renderer = slice.GetComponent<MeshRenderer>();
        if (renderer != null && shader != null)
        {
            var mat = new Material(shader) { color = color };
            renderer.material = mat;
        }

        slice.AddComponent<SlashFader>().Init(lifetime);
    }

    /// <summary>
    /// Spawn an expanding, fading flat ring (zone spells). A primitive cylinder flattened along
    /// <paramref name="upDir"/>, alpha-fading out. No collider, pure visual.
    /// </summary>
    public static void RingFlash(Vector3 worldPos, Vector3 upDir, Color color, float radius, float lifetime)
    {
        GameObject ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        ring.name = "FxRing";
        ring.transform.position = worldPos + upDir.normalized * 0.02f;
        ring.transform.rotation = Quaternion.FromToRotation(Vector3.up, upDir.normalized);

        Collider col = ring.GetComponent<Collider>();
        if (col != null)
            Object.Destroy(col);

        Shader shader = Shader.Find("Sprites/Default") ?? Shader.Find("Unlit/Color");
        Renderer renderer = ring.GetComponent<MeshRenderer>();
        if (renderer != null && shader != null)
            renderer.material = new Material(shader) { color = color };

        ring.AddComponent<RingFader>().Init(radius, lifetime);
    }

    /// <summary>
    /// Spawn an expanding, fading solid sphere at a magic projectile's impact point. The sphere
    /// grows from a quarter to full <paramref name="radius"/> ("explodes outward") while its
    /// per-face material fades to transparent — "as it explodes, transparency increases" — then
    /// removes itself. No collider, pure visual; each instance owns its material so the fade never
    /// races a shared cached one.
    /// </summary>
    public static void ImpactSphere(Vector3 worldPos, Color color, float radius, float lifetime = 0.45f)
    {
        GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        sphere.name = "FxImpactSphere";
        sphere.transform.position = worldPos;

        Collider col = sphere.GetComponent<Collider>();
        if (col != null)
            Object.Destroy(col);

        Shader shader = Shader.Find("Sprites/Default") ?? Shader.Find("Unlit/Color");
        Renderer renderer = sphere.GetComponent<MeshRenderer>();
        if (renderer != null && shader != null)
            renderer.material = new Material(shader) { color = color };

        sphere.AddComponent<ImpactSphereFader>().Init(radius, lifetime);
    }

    /// <summary>Grows the impact sphere to full radius while fading to transparent, then removes it.</summary>
    private sealed class ImpactSphereFader : MonoBehaviour
    {
        private float _radius;
        private float _age;
        private float _lifetime = 0.45f;
        private Material _mat;

        public void Init(float radius, float lifetime)
        {
            _radius = Mathf.Max(radius, 0.05f);
            _lifetime = Mathf.Max(lifetime, 0.05f);
        }

        private void Start()
        {
            var renderer = GetComponent<MeshRenderer>();
            _mat = renderer != null ? renderer.material : null;
        }

        private void Update()
        {
            _age += Time.deltaTime;
            float t = Mathf.Clamp01(_age / _lifetime);
            float s = Mathf.Lerp(0.25f, 1f, Mathf.SmoothStep(0f, 0.45f, t));
            transform.localScale = Vector3.one * (_radius * 2f * s);
            if (_mat != null)
            {
                Color c = _mat.color;
                c.a = 1f - t;
                _mat.color = c;
            }
            if (t >= 1f)
                Destroy(gameObject);
        }
    }

    /// <summary>Expands the ring to full radius while fading to transparent, then removes it.</summary>
    private sealed class RingFader : MonoBehaviour
    {
        private float _radius;
        private float _age;
        private float _lifetime = 0.35f;
        private Material _mat;

        public void Init(float radius, float lifetime)
        {
            _radius = radius;
            _lifetime = Mathf.Max(lifetime, 0.05f);
        }

        private void Start()
        {
            var renderer = GetComponent<MeshRenderer>();
            _mat = renderer != null ? renderer.material : null;
        }

        private void Update()
        {
            _age += Time.deltaTime;
            float t = Mathf.Clamp01(_age / _lifetime);
            float s = Mathf.Lerp(0.25f, 1f, Mathf.SmoothStep(0f, 0.45f, t));
            transform.localScale = new Vector3(_radius * 2f * s, 0.05f, _radius * 2f * s);
            if (_mat != null)
            {
                Color c = _mat.color;
                c.a = 1f - t;
                _mat.color = c;
            }
            if (t >= 1f)
                Destroy(gameObject);
        }
    }

    /// <summary>Shrinks the flash slice to zero scale, then removes it.</summary>
    private sealed class SlashFader : MonoBehaviour
    {
        private Vector3 _startScale;
        private float _age;
        private float _lifetime = 0.15f;

        public void Init(float lifetime)
        {
            _lifetime = Mathf.Max(lifetime, 0.05f);
        }

        private void Start()
        {
            _startScale = transform.localScale;
        }

        private void Update()
        {
            _age += Time.deltaTime;
            float t = Mathf.Clamp01(_age / _lifetime);
            transform.localScale = _startScale * (1f - t);
            if (t >= 1f)
                Destroy(gameObject);
        }
    }

    /// <summary>
    /// Summon a big rubble rock that drops from high above <paramref name="groundTarget"/> (the
    /// sky/rock spell family: Meteor, Asteroid, Earth Meteor as Zone; Meteor Rain / Rockfall as
    /// Storm; Comet as a burning projectile). The rock accelerates to the ground, then on landing
    /// fires <paramref name="onImpact"/> (the spell defers its burst/terrain to that moment),
    /// throws off small shards and a ring flash, and fades away. Purely visual — the rock and its
    /// shards carry NO collider, so it can never shove the terrain root or ragdoll foes (1cx) and
    /// never takes part in damage resolution (which stays on the spell's own pipeline).
    /// </summary>
    public static void FallRock(Vector3 groundTarget, float scale, Color tint, System.Action onImpact)
    {
        if (scale <= 0f) scale = 1f;

        var root = new GameObject("FxFallingRock").transform;
        root.position = groundTarget + Vector3.up * (Mathf.Max(scale, 1.5f) * 3f + 30f); // high above

        Shader shader = Shader.Find("Sprites/Default") ?? Shader.Find("Unlit/Color");
        if (shader == null)
        {
            DestroyRoot(root);
            return;
        }

        // Ragged boulder: a chunky core plus a few off-angle ridge cubes so it reads as a rock,
        // warm-tinted for fire variants; one shared material, colliders stripped.
        var rockMat = new Material(shader) { color = Color.Lerp(Color.gray, tint, 0.45f) };
        var core = CubeChild("RockCore", root);
        core.localScale = new Vector3(scale * 1.1f, scale * 0.9f, scale);
        Apply(core, rockMat);
        for (int i = 0; i < 4; i++)
        {
            var ridge = CubeChild("Ridge" + i, root);
            ridge.localPosition = new Vector3(
                UnityEngine.Random.Range(-scale, scale) * 0.42f,
                UnityEngine.Random.Range(-scale, scale) * 0.35f,
                UnityEngine.Random.Range(-scale, scale) * 0.42f);
            ridge.localRotation = UnityEngine.Random.rotation;
            ridge.localScale = Vector3.one * UnityEngine.Random.Range(scale * 0.35f, scale * 0.6f);
            Apply(ridge, rockMat);
        }

        root.gameObject.AddComponent<RockDrop>().Init(root, groundTarget, scale, tint, onImpact);
    }

    /// <summary>Primitive cube with its collider stripped, parented at local zero.</summary>
    private static Transform CubeChild(string name, Transform parent)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        Collider col = go.GetComponent<Collider>();
        if (col != null)
            Object.Destroy(col);
        go.transform.SetParent(parent, false);
        return go.transform;
    }

    private static void Apply(Transform t, Material mat)
    {
        var r = t.GetComponent<MeshRenderer>();
        if (r != null && mat != null)
            r.sharedMaterial = mat;
    }

    private static void DestroyRoot(Transform root)
    {
        if (root != null)
            Object.Destroy(root.gameObject);
    }

    /// <summary>Accelerates the summoned rock down to <see cref="_ground"/>, then fires the
    /// deferred spell impact, throws shard debris + a ring, and shrinks the rock away.</summary>
    private sealed class RockDrop : MonoBehaviour
    {
        private Transform _root;
        private Vector3 _ground;
        private float _scale;
        private Color _tint;
        private System.Action _onImpact;
        private float _speedY;
        private bool _impacted;
        private float _fadeT;
        private float _fadeStartScale;
        private Material _shardMat;
        private const float Gravity = 140f;
        private const float FadeTime = 0.3f;

        public void Init(Transform root, Vector3 groundTarget, float scale, Color tint, System.Action onImpact)
        {
            _root = root;
            _ground = groundTarget;
            _scale = scale;
            _tint = tint;
            _onImpact = onImpact;
            _fadeStartScale = root != null ? Mathf.Max(root.localScale.x, 0.01f) : 1f;
        }

        private void Update()
        {
            if (_root == null)
            {
                Destroy(gameObject);
                return;
            }

            if (!_impacted)
            {
                _speedY -= Gravity * Time.deltaTime;
                _root.position += Vector3.up * (_speedY * Time.deltaTime);
                if (_root.position.y <= _ground.y)
                    Land();
                return;
            }

            _fadeT += Time.deltaTime;
            float t = Mathf.Clamp01(_fadeT / FadeTime);
            _root.localScale = Vector3.one * Mathf.Lerp(_fadeStartScale, 0f, t);
            if (t >= 1f)
            {
                Destroy(_root.gameObject);
                Destroy(gameObject);
            }
        }

        private void Land()
        {
            _impacted = true;
            _root.position = new Vector3(_root.position.x, _ground.y, _root.position.z);
            OnImpactSafe();
            ThrowingShards(_root.position, _scale, _tint);
            SkillFx.RingFlash(new Vector3(_root.position.x, _ground.y + 0.02f, _root.position.z),
                Vector3.up, _tint, _scale * 2.2f, 0.35f);
        }

        private void OnImpactSafe()
        {
            try
            {
                _onImpact?.Invoke();
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning("FallRock impact callback threw: " + ex.Message);
            }
        }

        /// <summary>Small shards that fly outward from the landing and fade — pure visuals, no
        /// colliders, each with its own fading material so the shared rock material stays intact.</summary>
        private void ThrowingShards(Vector3 origin, float scale, Color tint)
        {
            if (_shardMat == null)
            {
                Shader shader = Shader.Find("Sprites/Default") ?? Shader.Find("Unlit/Color");
                if (shader != null)
                {
                    _shardMat = new Material(shader) { color = Color.Lerp(Color.gray, tint, 0.35f) };
                }
            }
            for (int i = 0; i < 5; i++)
            {
                var shard = CubeChild("FxShard", null);
                if (shard == null) continue;
                shard.position = origin + Vector3.up * UnityEngine.Random.Range(0f, scale * 0.3f);
                shard.localScale = Vector3.one * UnityEngine.Random.Range(scale * 0.12f, scale * 0.24f);
                Vector3 dir = (UnityEngine.Random.insideUnitSphere + Vector3.up * 0.7f).normalized;
                var r = shard.GetComponent<MeshRenderer>();
                if (r != null && _shardMat != null)
                    r.material = new Material(_shardMat); // per-shard instance so fading never races
                shard.gameObject.AddComponent<ShardFader>().Init(dir * UnityEngine.Random.Range(4f, 9f), 0.5f);
            }
        }
    }

    /// <summary>Tiny rock shard that flies out with velocity, fades, and removes itself.</summary>
    private sealed class ShardFader : MonoBehaviour
    {
        private Vector3 _vel;
        private float _age;
        private float _lifetime = 0.5f;
        private float _startScale;
        private Material _mat;

        public void Init(Vector3 velocity, float lifetime)
        {
            _vel = velocity;
            _lifetime = Mathf.Max(lifetime, 0.1f);
            _startScale = transform.localScale.x;
        }

        private void Update()
        {
            _age += Time.deltaTime;
            float t = Mathf.Clamp01(_age / _lifetime);
            transform.position += _vel * Time.deltaTime;
            _vel += Vector3.down * (9.8f * 4f) * Time.deltaTime; // light gravity
            transform.localScale = Vector3.one * (_startScale * (1f - t));

            if (_mat == null)
            {
                var r = GetComponent<MeshRenderer>();
                if (r != null) _mat = r.sharedMaterial;
            }
            if (_mat != null)
            {
                Color c = _mat.color;
                c.a = 1f - t;
                _mat.color = c;
            }
            if (t >= 1f)
                Destroy(gameObject);
        }
    }
}
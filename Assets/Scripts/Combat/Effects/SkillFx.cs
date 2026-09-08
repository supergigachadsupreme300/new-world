using UnityEngine;

/// <summary>
/// One-shot, script-built combat FX for skill execution (visible feedback even when no
/// target is in range). Deliberately prefab-free so skills read without authored assets.
/// </summary>
public static class SkillFx
{
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
}
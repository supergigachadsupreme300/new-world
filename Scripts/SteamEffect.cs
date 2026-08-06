using System.Collections;
using UnityEngine;

public class SteamEffect : MonoBehaviour
{
    public static void SpawnPuff(Vector3 position)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        go.name = "SteamPuff";
        go.transform.position = position;
        var col = go.GetComponent<Collider>();
        if (col != null)
            Destroy(col);
        go.transform.localScale = Vector3.one * 0.08f;
        var rend = go.GetComponent<Renderer>();
        if (rend != null)
            rend.material.color = new Color(1f, 1f, 1f, 0.85f);
        var puff = go.AddComponent<SteamPuff>();
        puff.Init(0.08f);
    }
}

public class SteamPuff : MonoBehaviour
{
    private float _size;
    private float _life;
    private float _elapsed;

    public void Init(float size)
    {
        _size = size;
        _life = 1.6f;
    }

    void Update()
    {
        _elapsed += Time.deltaTime;
        if (_elapsed >= _life)
        {
            Destroy(gameObject);
            return;
        }
        float t = _elapsed / _life;
        transform.position += Vector3.up * Time.deltaTime * 0.5f;
        transform.localScale = Vector3.one * Mathf.Lerp(_size, _size * 2.4f, t);
    }
}

public class SteamEmitter : MonoBehaviour
{
    private const float Interval = 1.4f;
    private readonly Vector3[] _puffOffsets =
    {
        new Vector3(0.45f, 1.75f, -2.4f),
        new Vector3(0.45f, 1.75f, -1.2f),
        new Vector3(0.45f, 1.75f, 0f),
        new Vector3(0.45f, 1.75f, 1.2f),
        new Vector3(0.45f, 1.75f, 2.4f),
    };
    private float _timer;
    private int _next;

    void Update()
    {
        if (GameManager.Instance == null || GameManager.Instance.GamePaused)
            return;
        if (CutsceneManager.Instance != null && CutsceneManager.Instance.IsActive)
            return;

        _timer += Time.deltaTime;
        if (_timer >= Interval)
        {
            _timer = 0f;
            SteamEffect.SpawnPuff(transform.TransformPoint(_puffOffsets[_next % _puffOffsets.Length]));
            _next++;
        }
    }
}

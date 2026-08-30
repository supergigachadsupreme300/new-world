using UnityEngine;

public class NightClubController : MonoBehaviour
{
    private Transform _door;
    private Collider _doorPanel;
    private Renderer _disco;
    private Renderer _neon;
    private Light[] _lights;
    private ClubDancer[] _dancers;
    private ClubDJAnimator _dj;
    private Renderer[] _floorTiles;
    private bool _night;
    private bool _initialized;

    void Start()
    {
        Initialize();
    }

    private void Initialize()
    {
        _door = transform.Find("ClubDoor");
        var panel = _door != null ? _door.Find("DoorPanel") : null;
        if (panel != null) _doorPanel = panel.GetComponent<Collider>();

        var discoGo = transform.Find("DiscoBall");
        if (discoGo != null) _disco = discoGo.GetComponent<Renderer>();

        var neonGo = transform.Find("NeonSign");
        if (neonGo != null) _neon = neonGo.GetComponent<Renderer>();

        _lights = GetComponentsInChildren<Light>(true);
        _dancers = GetComponentsInChildren<ClubDancer>(true);
        foreach (var d in _dancers)
        {
            if (d != null) d.IsDancing = true;
        }

        _dj = GetComponentInChildren<ClubDJAnimator>(true);

        var tiles = new System.Collections.Generic.List<Renderer>();
        foreach (Transform child in transform)
        {
            if (child.name == "Tile")
            {
                var r = child.GetComponent<Renderer>();
                if (r != null) tiles.Add(r);
            }
        }
        _floorTiles = tiles.ToArray();

        _initialized = true;
    }

    void Update()
    {
        if (!_initialized)
            Initialize();

        var gm = GameManager.Instance;
        bool night = gm == null || gm.TimeOfDay >= 18f || gm.TimeOfDay < 6f;
        if (night != _night)
        {
            _night = night;
            ApplyState();
        }
        if (_night)
            Disco();
        UpdateDoor();
    }

    public bool IsPlayerInside()
    {
        var p = GameManager.Instance?.Player;
        if (p == null) return false;
        Vector3 d = p.transform.position - transform.position;
        return Mathf.Abs(d.x) < 12f && Mathf.Abs(d.z) < 8f;
    }

    private void UpdateDoor()
    {
        if (_door == null)
            return;
        bool open = _night || IsPlayerInside();
        _door.localRotation = open ? Quaternion.Euler(0f, -90f, 0f) : Quaternion.identity;
        if (_doorPanel != null)
            _doorPanel.enabled = !open;
    }

    private void ApplyState()
    {
        if (_door != null)
            _door.localRotation = _night ? Quaternion.Euler(0f, -90f, 0f) : Quaternion.identity;
        if (_doorPanel != null)
            _doorPanel.enabled = !_night;

        if (_lights != null)
        {
            foreach (var l in _lights)
            {
                if (l != null) l.enabled = _night;
            }
        }
        if (_dancers != null)
        {
            foreach (var d in _dancers)
            {
                if (d != null) d.IsDancing = true;
            }
        }
        if (_dj != null) _dj.IsPlaying = true;
        if (_neon != null)
            _neon.material.color = _night ? new Color(1f, 0.3f, 0.9f) : new Color(0.16f, 0.13f, 0.22f);
    }

    private void Disco()
    {
        float t = Time.time * 0.8f;
        Color c = Color.HSVToRGB(t % 1f, 1f, 1f);
        if (_disco != null)
            _disco.material.color = c;

        if (_lights != null)
        {
            for (int i = 0; i < _lights.Length; i++)
            {
                var l = _lights[i];
                if (l == null) continue;
                if (l.name == "DiscoLight")
                {
                    l.color = c;
                }
                else if (l.name == "ClubLight")
                {
                    float phase = (float)i / Mathf.Max(1, _lights.Length);
                    l.color = Color.HSVToRGB((t + phase) % 1f, 0.9f, 1f);
                }
            }
        }

        if (_floorTiles != null)
        {
            for (int i = 0; i < _floorTiles.Length; i++)
            {
                if (_floorTiles[i] == null) continue;
                float phase = (float)i / Mathf.Max(1, _floorTiles.Length);
                _floorTiles[i].material.color = Color.HSVToRGB((t * 0.5f + phase) % 1f, 0.8f, 0.9f);
            }
        }
    }
}

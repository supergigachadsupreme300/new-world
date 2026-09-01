using System.Collections.Generic;
using UnityEngine;

// Phase 3D: a rideable horse. Spawned near the player's house; press E to mount
// and dismount (the trigger is disabled while riding so E is not re-triggered),
// press R to dismount anywhere. While riding the player moves faster and the
// horse gallops; the player's own body is hidden until dismount.
public class HorseMount : MonoBehaviour
{
    public static HorseMount Instance { get; private set; }

    public bool IsMounted { get; private set; }

    private PlayerController _player;
    private Transform _worldRoot;
    private BoxCollider _trigger;
    private readonly List<Renderer> _playerRenderers = new List<Renderer>();
    private readonly List<Transform> _legPivots = new List<Transform>();
    private float _gallopPhase;

    public static HorseMount Spawn(Vector3 position)
    {
        var go = new GameObject("HorseMount");
        go.transform.position = position;
        var mount = go.AddComponent<HorseMount>();
        mount.Build();
        return mount;
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private void Build()
    {
        _worldRoot = GameObject.Find("WorldRoot")?.transform;
        var model = HorseModelBuilder.BuildHorse(transform);
        _trigger = gameObject.AddComponent<BoxCollider>();
        _trigger.isTrigger = true;
        _trigger.center = new Vector3(0f, 0.8f, 0f);
        _trigger.size = new Vector3(1.6f, 1.8f, 2.8f);

        foreach (var n in new[] { "HipPivotL", "HipPivotBackL", "HipPivotR", "HipPivotBackR" })
        {
            var pivot = model.Find(n);
            if (pivot != null)
                _legPivots.Add(pivot);
        }
    }

    private void Update()
    {
        if (!IsMounted || _player == null)
            return;
        bool moving = _player.IsMoving;
        _gallopPhase += Time.deltaTime * (moving ? 14f : 3f);
        for (int i = 0; i < _legPivots.Count; i++)
        {
            float phase = _gallopPhase + (i % 2) * Mathf.PI;
            float swing = Mathf.Sin(phase) * (moving ? 28f : 4f);
            _legPivots[i].localRotation = Quaternion.Euler(swing, 0f, 0f);
        }
    }

    public void ToggleMount()
    {
        if (IsMounted)
            Dismount();
        else
            Mount();
    }

    private void Mount()
    {
        if (IsMounted)
            return;
        var player = GameManager.Instance?.Player;
        if (player == null)
            return;
        _player = player;
        HidePlayerBody();
        transform.SetParent(_player.transform, false);
        transform.localPosition = new Vector3(0f, -0.05f, 0f);
        transform.localRotation = Quaternion.identity;
        IsMounted = true;
        if (_trigger != null)
            _trigger.enabled = false;
        GameManager.Instance?.UIManager?.ShowMessage(
            Localization.T("Đã cưỡi ngựa. Ấn E hoặc R để xuống."), 2f);
    }

    public void Dismount()
    {
        if (!IsMounted)
            return;
        var player = _player;
        Vector3 drop = player != null
            ? player.transform.position + (-player.transform.forward * 1.2f)
            : transform.position;
        ShowPlayerBody();
        transform.SetParent(_worldRoot != null ? _worldRoot : null, true);
        transform.position = drop;
        transform.localRotation = Quaternion.identity;
        foreach (var pivot in _legPivots)
            pivot.localRotation = Quaternion.identity;
        IsMounted = false;
        if (_trigger != null)
            _trigger.enabled = true;
        _player = null;
        GameManager.Instance?.UIManager?.ShowMessage(Localization.T("Đã xuống ngựa."), 1.5f);
    }

    private void HidePlayerBody()
    {
        if (_player == null)
            return;
        _playerRenderers.Clear();
        _playerRenderers.AddRange(_player.GetComponentsInChildren<Renderer>(true));
        foreach (var r in _playerRenderers)
            r.enabled = false;
    }

    private void ShowPlayerBody()
    {
        foreach (var r in _playerRenderers)
        {
            if (r != null)
                r.enabled = true;
        }
        _playerRenderers.Clear();
    }
}
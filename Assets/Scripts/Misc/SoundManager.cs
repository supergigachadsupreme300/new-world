using System.Collections.Generic;
using UnityEngine;

public class SoundManager : MonoSingleton<SoundManager>
{
    [Header("Tool Sounds")]
    public AudioClip AxeClip;
    public AudioClip PickaxeClip;
    public AudioClip HoeClip;
    public AudioClip HammerClip;
    public AudioClip ScytheClip;
    public AudioClip PlantClip;

    [Header("World Sounds")]
    public AudioClip VendorTruckClip;

    [Header("Pitch Settings")]
    public float PitchMin = 0.55f;
    public float PitchMax = 1.5f;

    private readonly Dictionary<string, AudioClip> _clips = new Dictionary<string, AudioClip>();
    private readonly Dictionary<string, AudioClip> _overrides = new Dictionary<string, AudioClip>();
    private AudioSource _source;

    // 1e5: LoadSoundClips is called from Awake and from GameBootstrap; without a guard the 8
    // synchronous Resources.Load calls ran twice at boot.
    private bool _clipsLoaded;

    protected override void Awake()
    {
        base.Awake();
        _source = gameObject.AddComponent<AudioSource>();
        _source.playOnAwake = false;
        LoadSoundClips();
        RegisterOverrides();
    }

    public void LoadSoundClips()
    {
        if (_clipsLoaded) return;
        _clipsLoaded = true;
        var names = new[] {"pop", "axe", "pickaxe", "hoe", "sickle", "hammer", "bonk", "mexican_truck"};
        foreach (var name in names)
        {
            var clip = Resources.Load<AudioClip>($"Sounds/{name}");
            if (clip != null)
                _clips[name] = clip;
        }
    }

    private void RegisterOverrides()
    {
        _overrides.Clear();
        if (AxeClip != null)         _overrides["axe"] = AxeClip;
        if (PickaxeClip != null)     _overrides["pickaxe"] = PickaxeClip;
        if (HoeClip != null)         _overrides["hoe"] = HoeClip;
        if (HammerClip != null)      _overrides["hammer"] = HammerClip;
        if (ScytheClip != null)      _overrides["sickle"] = ScytheClip;
        if (PlantClip != null)       _overrides["pop"] = PlantClip;
        if (VendorTruckClip != null) _overrides["mexican_truck"] = VendorTruckClip;
    }

    public void Play(string name, float pitch = 1f)
    {
        if (!_overrides.TryGetValue(name, out var clip) || clip == null)
            _clips.TryGetValue(name, out clip);
        if (clip == null) return;

        _source.pitch = (name == "mexican_truck") ? 1f : Random.Range(PitchMin, PitchMax);
        _source.PlayOneShot(clip);
    }
}

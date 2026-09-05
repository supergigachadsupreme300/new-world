using UnityEngine;

/// <summary>
/// Phase 9 (Task: Audio system): self-contained ambient / SFX / music manager that runs on a
/// single audio rig (always-on <see cref="AudioListener"/> + two sources). Composes
/// <see cref="GameManager"/> (player transform, day/night <see cref="GameManager.TimeOfDay"/>)
/// and a footprint-free biome bank. No external audio package; audio clips are wired in-editor.
/// </summary>
public sealed class AudioManager : MonoBehaviour
{
    public enum Biome
    {
        Forest,
        Ocean,
        Town,
        Cave,
        Default
    }

    public static AudioManager Instance { get; private set; }

    [Header("Clips (wired in-editor)")]
    public AudioClip ForestAmbient;
    public AudioClip OceanAmbient;
    public AudioClip TownAmbient;
    public AudioClip CaveAmbient;

    public AudioClip MusicDay;
    public AudioClip MusicCombat;
    public AudioClip MusicRest;

    [Header("Behaviour")]
    [Tooltip("Base volume for ambient loops.")]
    public float AmbientVolume = 0.25f;
    [Tooltip("Crossfade seconds between ambient/music changes.")]
    public float FadeSeconds = 1.0f;
    [Tooltip("Day/night ambient reducer at night (multiplier).")]
    public float NightGainMin = 0.4f;

    private AudioSource _ambientA;
    private AudioSource _ambientB;
    private AudioSource _musicA;
    private AudioSource _musicB;
    private AudioSource _sfx;

    private bool _ambientMain = true;
    private bool _musicMain = true;
    private float _musicTarget;
    private Biome _current;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        BuildRig();
    }

    private void BuildRig()
    {
        // Only add a listener if the scene has none; duplicating the Main Camera's
        // listener trips the "2 audio listeners" warning and breaks spatialization.
        if (Object.FindAnyObjectByType<AudioListener>() == null)
            gameObject.AddComponent<AudioListener>();
        _ambientA = MakeSource("AmbientA", true);
        _ambientB = MakeSource("AmbientB", true);
        _musicA = MakeSource("MusicA", true);
        _musicB = MakeSource("MusicB", true);
        _sfx = MakeSource("SFX", false);
    }

    private AudioSource MakeSource(string name, bool loop)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        var src = go.AddComponent<AudioSource>();
        src.loop = loop;
        src.spatialBlend = loop ? 0f : 1f;
        src.playOnAwake = false;
        return src;
    }

    private void Update()
    {
        // Keep the rig parented to the camera/listener each frame.
        var cam = Camera.main;
        if (cam != null)
            transform.position = cam.transform.position;
        else if (GameManager.Instance?.Player != null)
            transform.position = GameManager.Instance.Player.transform.position;

        float ambientTarget = AmbientVolume;
        float t = GameManager.Instance != null ? GameManager.Instance.TimeOfDay : 12f;
        if (t > 19f || t < 5f)
            ambientTarget *= NightGainMin; // quiet ambient at night

        Crossfade(_ambientA, _ambientB, ambientTarget, ref _ambientMain);
        Crossfade(_musicA, _musicB, _musicTarget, ref _musicMain);
    }

    /// <summary>Blend a two-source crossfade toward the target volume each frame.</summary>
    private void Crossfade(AudioSource a, AudioSource b, float target, ref bool main)
    {
        if (main)
        {
            a.volume = Mathf.MoveTowards(a.volume, target, FadeSeconds * Time.deltaTime);
            b.volume = Mathf.MoveTowards(b.volume, 0f, FadeSeconds * Time.deltaTime);
            if (b.volume <= 0.001f && b.isPlaying) b.Stop();
        }
        else
        {
            b.volume = Mathf.MoveTowards(b.volume, target, FadeSeconds * Time.deltaTime);
            a.volume = Mathf.MoveTowards(a.volume, 0f, FadeSeconds * Time.deltaTime);
            if (a.volume <= 0.001f && a.isPlaying) a.Stop();
        }
    }

    /// <summary>Switch the ambient loop to a biome's clip (crossfades automatically).</summary>
    public void SetBiome(Biome biome)
    {
        if (_current == biome) return;
        _current = biome;
        AudioClip clip = AmbientFor(biome);
        if (clip == null)
        {
            _current = Biome.Default;
            return;
        }
        AudioSource next = _ambientMain ? _ambientB : _ambientA;
        AudioSource prev = _ambientMain ? _ambientA : _ambientB;
        next.clip = clip;
        next.volume = 0f;
        next.Play();
        _ambientMain = !_ambientMain; // next becomes the primary; the other fades out via Crossfade
        prev.volume = Mathf.Max(0.01f, prev.volume); // keep alive so the fade-out plays
    }

    private AudioClip AmbientFor(Biome biome)
    {
        switch (biome)
        {
            case Biome.Forest: return ForestAmbient;
            case Biome.Ocean: return OceanAmbient;
            case Biome.Town: return TownAmbient;
            case Biome.Cave: return CaveAmbient;
            default: return ForestAmbient;
        }
    }

    /// <summary>Play a one-shot spatialized SFX at position.</summary>
    public void PlaySFX(AudioClip clip, Vector3 position, float volume = 1f)
    {
        if (clip == null) return;
        _sfx.transform.position = position;
        _sfx.volume = volume;
        _sfx.PlayOneShot(clip);
    }

    /// <summary>Play on the listener (non-spatialized UI/clip).</summary>
    public void PlayUI(AudioClip clip, float volume = 0.8f)
    {
        if (clip == null) return;
        _sfx.spatialBlend = 0f;
        _sfx.volume = volume;
        _sfx.PlayOneShot(clip);
        _sfx.spatialBlend = 1f;
    }

    /// <summary>Switch/start a crossfading music track.</summary>
    public void PlayMusic(AudioClip clip)
    {
        if (clip == null) return;
        AudioSource next = _musicMain ? _musicB : _musicA;
        AudioSource prev = _musicMain ? _musicA : _musicB;
        next.clip = clip;
        next.volume = 0f;
        next.Play();
        _musicTarget = MusicVolume;
        _musicMain = !_musicMain;
        prev.volume = Mathf.Max(0.01f, prev.volume);
    }

    public float MusicVolume = 0.35f;
}
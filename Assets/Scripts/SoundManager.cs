using System.Collections.Generic;
using UnityEngine;

public class SoundManager : MonoBehaviour
{
    public static SoundManager Instance { get; private set; }
    private readonly Dictionary<string, AudioClip> _clips = new Dictionary<string, AudioClip>();
    private AudioSource _source;

    public void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        _source = gameObject.AddComponent<AudioSource>();
        _source.playOnAwake = false;
    }

    public void LoadSoundClips()
    {
        var names = new[] {"mexican_truck", "pop", "tree", "axe", "pickaxe", "gun", "hoe", "sword", "hammer", "sickle", "bonk"};
        foreach (var name in names)
        {
            var clip = Resources.Load<AudioClip>($"Sounds/{name}");
            if (clip != null)
                _clips[name] = clip;
        }
    }

    public void Play(string name, float pitch = 1f)
    {
        if (!_clips.TryGetValue(name, out var clip) || clip == null)
            return;

        _source.pitch = pitch;
        _source.PlayOneShot(clip);
    }
}

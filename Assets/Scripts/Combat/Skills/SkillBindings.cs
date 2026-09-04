using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Binds skill ids to keyboard hotkeys (Phase 10). When a key is pressed (PC only, new Input
/// System) the mapped castable skill is executed via <see cref="SkillProfile.Execute"/>.
/// Supports a "capture next key" flow driven by the Character Info menu: while a capture is in
/// progress, <see cref="TryCapture"/>/<see cref="Update"/> reads the first pressed key and calls
/// <see cref="OnKeyCaptured"/>.
/// </summary>
public sealed class SkillBindings : MonoBehaviour
{
    [Tooltip("True while a hotkey capture is armed (menu-driven).")]
    public bool Capturing;

    private readonly Dictionary<Key, string> _bindings = new Dictionary<Key, string>();
    private string _captureTarget;

    /// <summary>Fires with the captured Key when a capture completes.</summary>
    public System.Action<Key> OnKeyCaptured;

    /// <summary>Bind <paramref name="leave"/> to a skill id.</summary>
    public void Bind(Key key, string skillId)
    {
        if (string.IsNullOrEmpty(skillId)) return;
        _bindings[key] = skillId;
    }

    /// <summary>Clear the current binding for a key.</summary>
    public void Unbind(Key key) => _bindings.Remove(key);

    /// <summary>The skill id bound to a key, or null.</summary>
    public string BoundSkill(Key key)
    {
        _bindings.TryGetValue(key, out var id);
        return id;
    }

    /// <summary>Begin capturing the next pressed key for <paramref name="skillId"/>.</summary>
    public void BeginCapture(string skillId)
    {
        Capturing = true;
        _captureTarget = skillId;
    }

    /// <summary>Cancel an in-progress capture.</summary>
    public void CancelCapture()
    {
        Capturing = false;
        _captureTarget = null;
    }

    private void Update()
    {
        var kb = Keyboard.current;
        if (kb == null) return;

        // Honor a capture first: bind the next pressed key.
        if (Capturing)
        {
            foreach (Key key in System.Enum.GetValues(typeof(Key)))
            {
                if (key == Key.None) continue;
                if (kb[key].wasPressedThisFrame)
                {
                    if (!string.IsNullOrEmpty(_captureTarget))
                    {
                        _bindings[key] = _captureTarget;
                        OnKeyCaptured?.Invoke(key);
                    }
                    Capturing = false;
                    _captureTarget = null;
                    return;
                }
            }
        }

        // Execute bound castable skills.
        var profile = GetComponent<SkillProfile>();
        if (profile == null) return;
        foreach (var pair in _bindings)
        {
            if (kb[pair.Key].wasPressedThisFrame)
                profile.Execute(pair.Value);
        }
    }
}
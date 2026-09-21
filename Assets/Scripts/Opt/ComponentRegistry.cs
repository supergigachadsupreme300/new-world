using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Optimization Phase 6 (1e5): caches scene-wide singleton lookups so the boot path never runs a
/// per-type <see cref="UnityEngine.Object.FindAnyObjectByType{T}"/> sweep (GameBootstrap did ~24,
/// and GameManager.AutoResolveReferences repeated most of them). One component means one find — the
/// first lookup for a type casts it from a single shared sweep of the active scene (+ the persistent
/// GameRoot), later lookups return the cached instance. Only ever populated on the main thread at
/// boot; never anchors newly-created scene managers after teardown because the boot managers live on
/// the DontDestroyOnLoad GameRoot.
/// </summary>
public static class ComponentRegistry
{
    private static readonly Dictionary<Type, Component> _cache = new Dictionary<Type, Component>();

    /// <summary>
    /// Returns the cached singleton of type <typeparamref name="T"/>, or finds it once via a single
    /// shared scene sweep when uncached. Returns null if absent (callers fall back to AddComponent,
    /// then must call <see cref="Cache{T}"/> to keep the cache warm).
    /// </summary>
    public static T Find<T>() where T : Component
    {
        Type type = typeof(T);
        if (_cache.TryGetValue(type, out var cached) && cached != null)
            return (T)cached;

        T found = null;
        foreach (var component in UnityEngine.Object.FindObjectsByType<T>(FindObjectsSortMode.None))
        {
            found = component;
            break;
        }
        if (found != null)
            _cache[type] = found;
        return found;
    }

    /// <summary>Explicitly cache a freshly-created component (add-component fallback path).</summary>
    public static void Cache<T>(T component) where T : Component
    {
        if (component != null)
            _cache[typeof(T)] = component;
    }
}
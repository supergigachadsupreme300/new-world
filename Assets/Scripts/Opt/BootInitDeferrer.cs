using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Optimization Phase 6 (1e5): runs a fixed queue of boot initializer batches across successive
/// frames (one batch per frame), so the non-critical manager setup that GameBootstrap used to run
/// synchronously before the first rendered frame now trickles in behind the critical path (player
/// spawn, ground chunk, streaming focus). Semantics are unchanged — every call still runs, in the
/// original order, just a frame or two later. Boot-critical work stays in GameBootstrap itself.
/// </summary>
public sealed class BootInitDeferrer : MonoBehaviour
{
    private Action[] _batches;

    /// <summary>
    /// Queue the deferred initializer batches. Each array entry runs on its own frame; the order
    /// inside a batch is preserved. The first batch runs during Start (frame 1, after every
    /// GameRoot manager's Start — the deferrer is added last), the rest in the following frames.
    /// </summary>
    public void Queue(Action[] batches)
    {
        _batches = batches;
    }

    private void Start()
    {
        StartCoroutine(RunDeferred());
    }

    private IEnumerator RunDeferred()
    {
        if (_batches != null)
        {
            for (int i = 0; i < _batches.Length; i++)
            {
                if (_batches[i] != null)
                    _batches[i]();
                yield return null;
            }
        }
        // The deferrer lives ON the GameRoot (added last by GameBootstrap), so this destroys only
        // the component — never the root that hosts every manager.
        Destroy(this);
    }
}
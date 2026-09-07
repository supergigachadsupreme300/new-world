using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Animates the equipped weapon rigs between their held (drawn) and stowed (sheathed) poses
/// when the player toggles combat mode. Moving a rig from the hand to a body anchor (waist
/// scabbard / back carry) and back is done as a smooth ease-in-out lerp of the rig's local
/// transform, so the weapon reads as being drawn/sheathed rather than popping.
///
/// Attach to the player root. Rigs are seeded from the <see cref="CombatController"/> hands by
/// <see cref="WeaponRigBuilder.ApplyPose"/>. While a transition is busy, combat input is gated
/// (weapon is "in transition") and re-triggers are ignored.
/// </summary>
public class WeaponStowAnimator : MonoBehaviour
{
    /// <summary>Seconds for a single draw/stow transition.</summary>
    public float Duration = 0.4f;

    private readonly List<Entry> _entries = new List<Entry>();
    private bool _busy;
    private bool _drawn = true;
    private Coroutine _active;

    private class Entry
    {
        public Transform Rig;
        public Transform DrawParent;
        public Vector3 DrawPos;
        public Quaternion DrawRot;
        public float DrawScale;
        public Transform StowParent;
        public Vector3 StowPos;
        public Quaternion StowRot;
        public float StowScale;
    }

    private class Move
    {
        public Entry Entry;
        public Vector3 StartLocalPos;
        public Quaternion StartLocalRot;
        public float StartScale;
    }

    /// <summary>True while a draw/stow transition is playing.</summary>
    public bool IsBusy => _busy;

    /// <summary>True if the rigs are currently drawn (in hand).</summary>
    public bool IsDrawn => _drawn;

    /// <summary>The rigid-bodied parent under which stowed weapon anchors are created.</summary>
    public Transform AnchorParent;

    /// <summary>
    /// Register (or refresh) an entry for a rig. Pass the hand transform it should live under
    /// when drawn. <paramref name="drawScale"/> / <paramref name="stowScale"/> are the desired
    /// WORLD scales for each pose (the rig's local scale is derived from them per parent — the
    /// hand blocks are non-uniformly scaled, so a fixed local scale would distort the weapon).
    /// </summary>
    public void Register(Transform rig, Transform drawParent, Vector3 drawPos, Quaternion drawRot,
        Transform stowParent, Vector3 stowPos, Quaternion stowRot, float drawScale, float stowScale)
    {
        for (int i = 0; i < _entries.Count; i++)
        {
            if (_entries[i].Rig == rig)
            {
                Entry e = _entries[i];
                e.DrawParent = drawParent;
                e.DrawPos = drawPos;
                e.DrawRot = drawRot;
                e.DrawScale = drawScale;
                e.StowParent = stowParent;
                e.StowPos = stowPos;
                e.StowRot = stowRot;
                e.StowScale = stowScale;
                return;
            }
        }
        _entries.Add(new Entry
        {
            Rig = rig,
            DrawParent = drawParent,
            DrawPos = drawPos,
            DrawRot = drawRot,
            DrawScale = drawScale,
            StowParent = stowParent,
            StowPos = stowPos,
            StowRot = stowRot,
            StowScale = stowScale
        });
    }

    /// <summary>Drop an entry for a rig (e.g. its weapon was re-equipped/removed).</summary>
    public void Forget(Transform rig)
    {
        for (int i = _entries.Count - 1; i >= 0; i--)
        {
            if (_entries[i].Rig == rig)
            {
                _entries.RemoveAt(i);
                return;
            }
        }
    }

    /// <summary>
    /// Remove entries whose rig was destroyed (e.g. re-equipped/cleared). Called after registering
    /// the current hands so stale rigs never linger into a transition.
    /// </summary>
    public void Prune()
    {
        for (int i = _entries.Count - 1; i >= 0; i--)
        {
            if (_entries[i].Rig == null)
                _entries.RemoveAt(i);
        }
    }

    /// <summary>
    /// Begin a gradual transition of all registered rigs to the requested pose. If the rigs are
    /// already in that pose (or no entries exist) nothing animates. Assumes the target parents
    /// already exist.
    /// </summary>
    public void SetPose(bool drawn)
    {
        if (_entries.Count == 0 || drawn == _drawn) return;
        _drawn = drawn;
        if (_active != null)
            StopCoroutine(_active);
        _active = StartCoroutine(Transition(drawn));
    }

    /// <summary>Instantly place all rigs (used on model rebuild — no in-flight lerp).</summary>
    public void Snap(bool drawn)
    {
        _drawn = drawn;
        if (_busy)
        {
            if (_active != null) StopCoroutine(_active);
            _active = null;
            _busy = false;
        }
        foreach (var e in _entries)
            Apply(e, drawn, 1f);
    }

    private IEnumerator Transition(bool drawn)
    {
        _busy = true;

        // Convert each rig's CURRENT world pose into the target parent's local frame so the lerp
        // is seamless: at k=0 the rig sits exactly where it was (no re-parent pop), at k=1 it sits
        // in its end pose.
        var moves = new List<Move>();
        foreach (var e in _entries)
        {
            var target = drawn ? e.DrawParent : e.StowParent;
            if (target == null || e.Rig == null) continue;
            moves.Add(new Move
            {
                Entry = e,
                StartLocalPos = target.InverseTransformPoint(e.Rig.position),
                StartLocalRot = Quaternion.Inverse(target.rotation) * e.Rig.rotation,
                StartScale = e.Rig.lossyScale.x
            });
        }

        if (moves.Count == 0)
        {
            _busy = false;
            yield break;
        }

        float t = 0f;
        while (t < Duration)
        {
            t += Time.deltaTime;
            float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / Duration));
            for (int i = 0; i < moves.Count; i++)
            {
                var m = moves[i];
                var e = m.Entry;
                if (e.Rig == null) continue;
                var target = drawn ? e.DrawParent : e.StowParent;
                if (target == null) continue;
                e.Rig.SetParent(target, false);
                e.Rig.localPosition = Vector3.Lerp(m.StartLocalPos, drawn ? e.DrawPos : e.StowPos, k);
                e.Rig.localRotation = Quaternion.Slerp(m.StartLocalRot, drawn ? e.DrawRot : e.StowRot, k);
                float ws = Mathf.Lerp(m.StartScale, drawn ? e.DrawScale : e.StowScale, k);
                e.Rig.localScale = WeaponRigBuilder.ScaleForWorld(e.Rig.parent, ws);
            }
            yield return null;
        }

        foreach (var e in _entries)
            Apply(e, drawn, 1f);
        _active = null;
        _busy = false;
    }

    private static void Apply(Entry e, bool drawn, float k)
    {
        if (e.Rig == null) return;
        e.Rig.SetParent(drawn ? e.DrawParent : e.StowParent, false);
        e.Rig.localPosition = Vector3.Lerp(e.Rig.localPosition, drawn ? e.DrawPos : e.StowPos, k);
        e.Rig.localRotation = Quaternion.Slerp(e.Rig.localRotation, drawn ? e.DrawRot : e.StowRot, k);
        e.Rig.localScale = WeaponRigBuilder.ScaleForWorld(e.Rig.parent, drawn ? e.DrawScale : e.StowScale);
    }
}

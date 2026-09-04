using System.Collections.Generic;
using UnityEngine;

public class SittableSeat : MonoBehaviour
{
    private static readonly string[] Keywords = { "chair", "sofa", "bench", "stool", "seat", "booth", "throne", "pew", "couch" };

    public static readonly List<SittableSeat> All = new List<SittableSeat>();

    public Vector3 WorldAnchor
    {
        get
        {
            Vector3 p = transform.position;
            if (transform.lossyScale.y >= 0.001f)
                p.y += transform.lossyScale.y * 0.5f;
            return p;
        }
    }

    public Vector3 Facing => transform.forward;

    private void OnEnable()
    {
        if (!All.Contains(this))
            All.Add(this);
    }

    private void OnDisable()
    {
        All.Remove(this);
    }

    public static void Register(Transform root)
    {
        if (root == null)
            return;
        foreach (Transform child in root)
            RegisterRecursive(child);
    }

    private static void RegisterRecursive(Transform t)
    {
        if (t == null)
            return;
        if (IsSeatName(t.name) && !IsBackRest(t.name) && t.GetComponent<Renderer>() != null)
        {
            if (t.GetComponent<SittableSeat>() == null)
                t.gameObject.AddComponent<SittableSeat>();
        }
        foreach (Transform c in t)
            RegisterRecursive(c);
    }

    public static void Attach(Transform t)
    {
        if (t == null)
            return;
        if (t.GetComponent<SittableSeat>() == null)
            t.gameObject.AddComponent<SittableSeat>();
    }

    private static bool IsSeatName(string name)
    {
        if (string.IsNullOrEmpty(name))
            return false;
        for (int i = 0; i < Keywords.Length; i++)
        {
            if (name.IndexOf(Keywords[i], System.StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
        }
        return false;
    }

    private static bool IsBackRest(string name)
    {
        return name != null && name.IndexOf("Back", System.StringComparison.OrdinalIgnoreCase) >= 0;
    }

    public static void PruneNull()
    {
        for (int i = All.Count - 1; i >= 0; i--)
        {
            if (All[i] == null)
                All.RemoveAt(i);
        }
    }

    public static SittableSeat FindNearest(Vector3 position, float maxDistance)
    {
        SittableSeat best = null;
        float bestSqr = maxDistance * maxDistance;
        for (int i = 0; i < All.Count; i++)
        {
            var s = All[i];
            if (s == null)
                continue;
            Vector3 p = s.WorldAnchor;
            p.y = position.y;
            float sqr = (p - position).sqrMagnitude;
            if (sqr < bestSqr)
            {
                bestSqr = sqr;
                best = s;
            }
        }
        return best;
    }
}
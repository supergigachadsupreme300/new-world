using System;
using UnityEngine;

/// <summary>
/// A standalone fast-travel node (planning Task 5.3 "Fast travel network (bonfires, signs)").
/// Wraps a <see cref="FastTravelSign"/> marker plus a small generated bonfire/sign visual so the
/// existing fast-travel menu (which auto-discovers FastTravelSign components) lists this node.
/// </summary>
public class FastTravelNode : MonoBehaviour
{
    public POIDefinition Definition;
    public FastTravelSign TravelSign { get; private set; }

    public static FastTravelNode Build(Transform parent, POIDefinition poi, int index)
    {
        var root = new GameObject("FastTravel_" + poi.Id);
        root.transform.SetParent(parent);
        root.transform.position = poi.LocalPosition;
        var node = root.AddComponent<FastTravelNode>();
        node.Definition = poi;
        node.TravelSign = root.AddComponent<FastTravelSign>();
        node.TravelSign.Index = index;
        node.TravelSign.Label = poi.DisplayName;
        node.BuildVisual();
        return node;
    }

    private void BuildVisual()
    {
        var trigger = gameObject.AddComponent<BoxCollider>();
        trigger.isTrigger = true;
        trigger.center = new Vector3(0f, 1.0f, 0f);
        trigger.size = new Vector3(1.8f, 2.0f, 1.8f);

        BuildCube("Post", transform, new Vector3(0.14f, 1.5f, 0.14f), new Vector3(0f, 0.7f, 0f),
            new Color(0.55f, 0.4f, 0.22f));
        BuildCube("Board", transform, new Vector3(1.1f, 0.45f, 0.09f), new Vector3(0f, 1.6f, 0f),
            new Color(0.68f, 0.52f, 0.3f));

        for (int i = 0; i < 3; i++)
        {
            float angle = (i / 3f) * 360f;
            Vector3 off = new Vector3(Mathf.Cos(angle * Mathf.Deg2Rad), 0f, Mathf.Sin(angle * Mathf.Deg2Rad)) * 0.5f;
            BuildCube("Log" + i, transform, new Vector3(0.25f, 0.18f, 0.25f), new Vector3(off.x, 0.25f, off.z),
                new Color(0.3f, 0.2f, 0.12f));
        }
        BuildCube("Fire", transform, new Vector3(0.4f, 0.5f, 0.4f), new Vector3(0f, 0.1f, 0f),
            new Color(1f, 0.6f, 0.15f));
    }

    private static void BuildCube(string name, Transform parent, Vector3 scale, Vector3 position, Color color)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localScale = scale;
        go.transform.localPosition = position;
        go.GetComponent<MeshRenderer>().material.color = color;
        Destroy(go.GetComponent<Collider>());
    }
}
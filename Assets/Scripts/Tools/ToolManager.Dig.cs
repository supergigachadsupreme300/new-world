/// Shovel/pickaxe dig tools: cosmetic excavation puffs (stroke logic lives in UseSelectedItem).
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using TMPro;
using static CountryLife.Helpers.PickupVisualHelper;

public partial class ToolManager
{
    /// <summary>A few tiny tinted shards scattered out of an excavation stroke, so a dig has a
    /// light, readable feedback puff (dirt-brown for the shovel, stone-gray for the pickaxe).
    /// Only cosmetic: the shards are unparented, gravity-driven and self-destruct in ~1 s.</summary>
    private void SpawnDigPuff(Vector3 point, Color color)
    {
        for (int i = 0; i < 4; i++)
        {
            var shard = GameObject.CreatePrimitive(PrimitiveType.Cube);
            shard.name = "DigPuff";
            shard.transform.position = point + Random.insideUnitSphere * 0.15f;
            shard.transform.localScale = Vector3.one * (0.03f + Random.value * 0.04f);
            var body = shard.AddComponent<Rigidbody>();
            body.linearVelocity = (Vector3.up * 0.6f) + Random.insideUnitSphere * 1.2f;
            shard.GetComponent<Renderer>().material = new Material(Shader.Find("Universal Render Pipeline/Lit"))
            {
                color = color
            };
            Destroy(shard, 1f);
        }
    }
}
using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A town point of interest (planning Task 5.3, game-design §7.2 "Towns (NPCs, shops,
/// crafting)"). Composes the existing interaction contracts:
///   • a <see cref="FastTravelSign"/> (+ trigger) lets the fast-travel menu list the town;
///   • shop NPC markers named "GroceryShopNPC"/"VendorNPC" open the existing vendor UI;
///   • crafting-station markers named per <see cref="CraftingManager.StationCategories"/>
///     (crafting_stove / preserve_jar / brewing_kettle) resolve through the player interaction;
///   • a <see cref="LootContainer"/> governs a small guaranteed chest.
/// Generated cube stand-ins stand in until real town art drops in.
/// </summary>
public class Town : MonoBehaviour
{
    [Header("Identity")]
    public POIDefinition Definition;

    [Tooltip("Shop NPC marker names (opened by the player's existing vendor interaction).")]
    public string[] ShopMarkers = { "GroceryShopNPC", "VendorNPC" };
    [Tooltip("Crafting station collider names resolved by CraftingManager.")]
    public string[] StationMarkers = { "crafting_stove", "preserve_jar", "brewing_kettle" };

    public FastTravelSign TravelSign { get; private set; }
    public GameObject ShopRoot { get; private set; }
    public LootContainer Chest { get; private set; }

    /// <summary>Builds the town world object at <paramref name="worldPosition"/> under parent.</summary>
    public static Town Build(Transform parent, Vector3 worldPosition, POIDefinition poi)
    {
        var root = new GameObject("Town_" + poi.Id);
        root.transform.SetParent(parent);
        root.transform.position = worldPosition;
        var town = root.AddComponent<Town>();
        town.Definition = poi;
        town.PlaceMarkers(root.transform);
        return town;
    }

    private void PlaceMarkers(Transform root)
    {
        Vector3 origin = Vector3.zero;

        // Fast travel sign (index assigned by the POI generator; label from display name).
        var signGo = new GameObject("FastTravelSign");
        signGo.transform.SetParent(root, false);
        signGo.transform.localPosition = origin + new Vector3(0f, 0f, Definition.Radius * 0.8f);
        TravelSign = signGo.AddComponent<FastTravelSign>();
        TravelSign.Label = Definition.DisplayName;
        var col = signGo.AddComponent<BoxCollider>();
        col.isTrigger = true;
        col.center = new Vector3(0f, 0.9f, 0f);
        col.size = new Vector3(1.2f, 1.6f, 0.5f);
        BuildMarkerCube("SignPost", signGo.transform, new Vector3(0.12f, 1.3f, 0.12f), new Vector3(0f, 0.65f, 0f),
            new Color(0.55f, 0.4f, 0.22f));

        // Shop NPC marker that opens the existing vendor UI.
        ShopRoot = new GameObject(ShopMarkers.Length > 0 ? ShopMarkers[0] : "VendorNPC");
        ShopRoot.transform.SetParent(root, false);
        ShopRoot.transform.localPosition = origin + new Vector3(Definition.Radius * 0.6f, 0.5f, 0f);
        var shopCol = ShopRoot.AddComponent<BoxCollider>();
        shopCol.isTrigger = true;
        shopCol.size = new Vector3(1.2f, 2.0f, 1.2f);
        BuildMarkerCube("ShopVendor", ShopRoot.transform, new Vector3(0.6f, 1.2f, 0.6f), new Vector3(0f, 1f, 0f),
            new Color(0.85f, 0.6f, 0.2f));

        // Crafting stations (one of each category, named so CraftingManager resolves them).
        for (int i = 0; i < StationMarkers.Length; i++)
        {
            var station = new GameObject(StationMarkers[i]);
            station.transform.SetParent(root, false);
            station.transform.localPosition = origin + new Vector3((i - 1) * 1.6f, 0.3f, -Definition.Radius * 0.7f);
            var scol = station.AddComponent<BoxCollider>();
            scol.isTrigger = true;
            scol.size = new Vector3(1.0f, 1.0f, 1.0f);
            BuildMarkerCube("Station" + i, station.transform, new Vector3(0.8f, 0.7f, 0.8f), new Vector3(0f, 0.55f, 0f),
                new Color(0.5f, 0.45f, 0.55f));
        }

        // Guaranteed chest (hidden loot per §7.2).
        var chestGo = new GameObject("TownChest");
        chestGo.transform.SetParent(root, false);
        chestGo.transform.localPosition = origin + new Vector3(-Definition.Radius * 0.6f, 0.6f, Definition.Radius * 0.3f);
        Chest = chestGo.AddComponent<LootContainer>();
        Chest.GuaranteedItemId = "healing_potion";
        Chest.GuaranteedCount = 2;
        Chest.RequiresInteract = false;

        // Simple town plaza floor.
        BuildMarkerCube("Plaza", root, new Vector3(Definition.Radius * 1.4f, 0.12f, Definition.Radius * 1.4f),
            origin + new Vector3(0f, -0.06f, 0f), new Color(0.72f, 0.68f, 0.58f));
    }

    private static void BuildMarkerCube(string name, Transform parent, Vector3 scale, Vector3 position, Color color)
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
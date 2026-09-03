using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Places data-driven open-world NPCs (planning Task 6.4). Builds an <see cref="NpcController"/>
/// (with a synthetic <see cref="NpcDefinition"/>) at a world position, so towns and player-home
/// areas can host vendor/quest/follower NPCs without hand-authored CountryLife scripts.
/// </summary>
public class WorldNpcPlacer : MonoBehaviour
{
    public Vector3 Anchor = Vector3.zero;
    public bool AutoPlaceOnStart = true;

    private readonly List<NpcController> _npcs = new List<NpcController>();
    public IReadOnlyList<NpcController> Npcs => _npcs;

    private void Start()
    {
        if (AutoPlaceOnStart)
            PlaceDefaults();
    }

    /// <summary>Place a single NPC at a world position with a synthetic definition.</summary>
    public NpcController Place(string id, string displayName, NpcRoleKind role,
        NpcShopMode shopMode, Vector3 worldPosition, string friendshipId = null)
    {
        var go = new GameObject("Npc_" + id);
        go.transform.SetParent(transform);
        go.transform.position = worldPosition;

        var def = ScriptableObject.CreateInstance<NpcDefinition>();
        def.name = "NpcDef_" + id;
        def.Id = id;
        def.DisplayName = displayName;
        def.Role = role;
        def.ShopMode = shopMode;
        def.GreetingLines = new[] { "Hello, traveller." };

        var npc = go.AddComponent<NpcController>();
        npc.Definition = def;
        npc.FriendshipId = friendshipId;
        npc.BuildNpc(worldPosition);
        _npcs.Add(npc);
        return npc;
    }

    /// <summary>Place a starter set of NPCs (a vendor, a quest giver, a follower).</summary>
    public void PlaceDefaults()
    {
        Place("vendor_1", "Merchant", NpcRoleKind.Vendor, NpcShopMode.Tools,
            Anchor + new Vector3(4f, 0f, 2f), "fishshop");
        Place("quest_1", "Hermit", NpcRoleKind.QuestGiver, NpcShopMode.Vendor,
            Anchor + new Vector3(-3f, 0f, -2f));
        Place("companion_1", "Traveler", NpcRoleKind.Follower, NpcShopMode.Vendor,
            Anchor + new Vector3(1f, 0f, -4f));
    }
}
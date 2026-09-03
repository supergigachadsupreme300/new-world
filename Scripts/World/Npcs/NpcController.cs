using UnityEngine;

/// <summary>
/// A data-driven world NPC (planning Task 6.4 "adapt NPC scripts for shop/companion/quest
/// roles"). Composes the existing contracts: a Vendor role opens the shipped
/// <see cref="VendorShopManager"/> in the configured mode; a QuestGiver advances the quest
/// tracker + grants a daily friendship-talk; a Follower toggles a simplified follow AI that
/// moves toward the player. Place the NPC in towns via <see cref="WorldNpcPlacer"/> or by hand.
/// </summary>
public class NpcController : MonoBehaviour
{
    [Header("Identity")]
    public NpcDefinition Definition;
    [Tooltip("Friendship id (e.g. 'chef'/'fishshop') for the simplified friendship adapter.")]
    public string FriendshipId;

    [Header("Behaviour")]
    [Tooltip("Follow distance kept for a Follower role.")]
    public float FollowDistance = 2.2f;
    [Tooltip("Follow speed units/sec.")]
    public float FollowSpeed = 3.5f;

    private bool _following;
    private VendorShopManager _shop;
    private float _bobT;

    /// <summary>True while the NPC's shop panel is open.</summary>
    public bool IsTalking { get; private set; }

    private void Start()
    {
        _shop = Object.FindObjectOfType<VendorShopManager>();
    }

    private void Update()
    {
        if (Definition == null) return;
        if (Definition.Role == NpcRoleKind.Follower && _following && GameManager.Instance != null)
        {
            var player = GameManager.Instance.Player;
            if (player != null)
            {
                Vector3 target = player.transform.position;
                var pos = transform.position;
                float dist = Vector3.Distance(pos, target);
                if (dist > FollowDistance + 0.4f)
                {
                    Vector3 dir = (target - pos).normalized;
                    transform.position = Vector3.MoveTowards(pos, target - dir * FollowDistance,
                        FollowSpeed * Time.deltaTime);
                }
            }
        }
        else if (Definition.Role != NpcRoleKind.Follower)
        {
            // Gentle idle bob so NPCs feel alive.
            _bobT += Time.deltaTime;
            transform.localPosition = new Vector3(transform.localPosition.x, 0.15f + Mathf.Sin(_bobT * 1.5f) * 0.05f, transform.localPosition.z);
        }
    }

    /// <summary>Player interaction entry point (wired to the NPC trigger).</summary>
    public void Interact()
    {
        if (Definition == null) return;
        switch (Definition.Role)
        {
            case NpcRoleKind.Vendor: OpenVendor(); break;
            case NpcRoleKind.QuestGiver: InteractQuest(); break;
            case NpcRoleKind.Follower: ToggleFollow(); break;
        }
    }

    private void OpenVendor()
    {
        if (_shop == null) _shop = Object.FindObjectOfType<VendorShopManager>();
        if (_shop == null)
        {
            GameManager.Instance?.UIManager?.ShowMessage("Vendor unavailable.", 1.5f);
            return;
        }
        IsTalking = true;
        switch (Definition.ShopMode)
        {
            case NpcShopMode.Tools: _shop.OpenTools(); break;
            case NpcShopMode.Convenience: _shop.OpenConvenience(); break;
            case NpcShopMode.Grocery: _shop.OpenGrocery(); break;
            case NpcShopMode.Fishing: _shop.OpenFishing(); break;
            case NpcShopMode.Restaurant: _shop.OpenRestaurant(); break;
            case NpcShopMode.Cafe: _shop.OpenCafe(); break;
            default: _shop.Open(); break;
        }
    }

    private void InteractQuest()
    {
        FellowshipGrant();
        if (!string.IsNullOrEmpty(Definition.QuestObjective))
        {
            QuestManager.Instance?.AddProgress(Definition.QuestObjective, 1);
            GameManager.Instance?.UIManager?.ShowMessage(
                string.IsNullOrEmpty(Definition.DisplayName) ? "Quest progress." : Definition.DisplayName, 1.5f);
        }
    }

    private void ToggleFollow()
    {
        _following = !_following;
        GameManager.Instance?.UIManager?.ShowMessage(
            _following ? "Follower active." : "Follower dismissed.", 1.5f);
    }

    private void FellowshipGrant()
    {
        if (!string.IsNullOrEmpty(FriendshipId))
            FriendshipSimplified.GrantTalk(FriendshipId);
    }

    /// <summary>Build the NPC's interaction trigger + body. Called by WorldNpcPlacer on load.</summary>
    public void BuildNpc(Vector3 home)
    {
        var col = gameObject.AddComponent<SphereCollider>();
        col.isTrigger = true;
        col.radius = 1.4f;

        var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        body.name = "NpcBody";
        body.transform.SetParent(transform, false);
        body.transform.localScale = new Vector3(0.5f, 1.1f, 0.5f);
        body.transform.localPosition = new Vector3(0f, 1.1f, 0f);
        Destroy(body.GetComponent<Collider>());
        body.GetComponent<Renderer>().material.color = NpcBodyColor();
    }

    private Color NpcBodyColor()
    {
        switch (Definition != null ? Definition.Role : NpcRoleKind.Vendor)
        {
            case NpcRoleKind.Vendor: return new Color(0.85f, 0.6f, 0.2f);
            case NpcRoleKind.QuestGiver: return new Color(0.3f, 0.55f, 0.8f);
            default: return new Color(0.4f, 0.75f, 0.45f);
        }
    }
}
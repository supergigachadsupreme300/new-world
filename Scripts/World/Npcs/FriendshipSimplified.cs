using UnityEngine;

/// <summary>
/// Simplified friendship adapter (planning Task 6.4 "Friendship system simplified"). Rather
/// than a parallel system, it exposes a small helper surface over the shipped
/// <see cref="FriendshipManager"/>: a 0..5 heart level per NPC (by friendship id), a 10%
/// shop discount at level 2+, and one-line "liked item" checks. Open-world NPCs can hand the
/// manager's id (e.g. "chef"/"fishshop") to reuse existing heart-points + gift mechanics.
/// </summary>
public static class FriendshipSimplified
{
    /// <summary>Heart level 0..5 for a friendship id (delegates to FriendshipManager).</summary>
    public static int HeartLevel(string friendshipId)
    {
        if (string.IsNullOrEmpty(friendshipId)) return 0;
        var fm = FriendshipManager.Instance;
        return fm != null ? fm.HeartLevel(friendshipId) : 0;
    }

    /// <summary>Shop discount multiplier at heart level 2+ (0.9), else 1.</summary>
    public static float DiscountFor(string friendshipId)
    {
        if (string.IsNullOrEmpty(friendshipId)) return 1f;
        var fm = FriendshipManager.Instance;
        return fm != null ? fm.ShopDiscountFor(friendshipId) : 1f;
    }

    /// <summary>Whether the given item is a liked gift for the friendship id.</summary>
    public static bool IsLikedGift(string friendshipId, string itemId)
    {
        if (string.IsNullOrEmpty(friendshipId)) return false;
        var fm = FriendshipManager.Instance;
        return fm != null && fm.IsItemLiked(friendshipId, itemId);
    }

    /// <summary>On first daily talk with the manager's NPC, advances the heart track.</summary>
    public static bool GrantTalk(string friendshipId)
    {
        if (string.IsNullOrEmpty(friendshipId)) return false;
        var fm = FriendshipManager.Instance;
        return fm != null && fm.GrantTalk(friendshipId);
    }
}
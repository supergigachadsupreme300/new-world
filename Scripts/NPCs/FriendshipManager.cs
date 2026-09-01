using UnityEngine;
using System;
using System.Collections.Generic;

// Central friendship system for 4 NPCs (Monk / Librarian / Chef / FishShop).
// Each NPC tracks 0..50 heart-points; heart level = floor(points/10), capped 0..5.
// Talk once/day +1 point; give one liked gift a day for +2 points (other gifts +1).
public class FriendshipManager : MonoSingleton<FriendshipManager>
{
    public const int PointsPerHeart = 10;
    public const int MaxPoints = 50;

    private const int _npcCount = 4;
    private readonly string[] _npcIds = { "monk", "librarian", "chef", "fishshop" };

    private readonly float[] _points = new float[_npcCount];
    private readonly int[] _talkedDay = new int[_npcCount];
    private readonly int[] _giftDay = new int[_npcCount];
    private readonly bool[] _grantedL3 = new bool[_npcCount];
    private readonly bool[] _grantedL5 = new bool[_npcCount];

    public string[] NpcIds => _npcIds;

    public int IndexOf(string id)
    {
        for (int i = 0; i < _npcCount; i++)
            if (_npcIds[i] == id) return i;
        return -1;
    }

    public void Initialize()
    {
        for (int i = 0; i < _npcCount; i++)
            _points[i] = Mathf.Clamp(_points[i], 0f, MaxPoints);
    }

    private int CurrentDay => GameManager.Instance != null ? GameManager.Instance.CurrentDay : -1;

    public float HeartPoints(string id)
    {
        int i = IndexOf(id);
        return i < 0 ? 0f : _points[i];
    }

    public int HeartLevel(string id)
    {
        int i = IndexOf(id);
        return i < 0 ? 0 : Mathf.FloorToInt(_points[i] / PointsPerHeart);
    }

    public float PointsToNextHeart(string id)
    {
        int lv = HeartLevel(id);
        if (lv >= 5) return 0f;
        return (lv + 1) * PointsPerHeart - HeartPoints(id);
    }

    public bool HasTalkedToday(string id)
    {
        int i = IndexOf(id);
        return i >= 0 && _talkedDay[i] == CurrentDay;
    }

    public bool HasGivenGiftToday(string id)
    {
        int i = IndexOf(id);
        return i >= 0 && _giftDay[i] == CurrentDay;
    }

    // Called once per player interaction. +1 point the first time each day.
    public bool GrantTalk(string id)
    {
        int i = IndexOf(id);
        if (i < 0 || _talkedDay[i] == CurrentDay) return false;
        _talkedDay[i] = CurrentDay;
        AddPoints(i, 1f);
        return true;
    }

    // Removes one gift item from the player's inventory and grants points.
    // Returns true on success (gift accepted and removed).
    public bool GiveGift(string id, string itemType)
    {
        int i = IndexOf(id);
        if (i < 0) return false;
        var tm = ToolManager.Instance;
        if (tm == null || tm.CountItem(itemType) <= 0) return false;

        bool alreadyGifted = _giftDay[i] == CurrentDay;
        tm.RemoveItemAmount(itemType, 1);
        _giftDay[i] = CurrentDay;
        AddPoints(i, alreadyGifted ? 0f : (IsItemLiked(id, itemType) ? 2f : 1f));
        return true;
    }

    public bool IsItemLiked(string id, string itemType)
    {
        var liked = LikedItemsFor(id);
        if (liked == null) return false;
        return Array.IndexOf(liked, itemType) >= 0;
    }

    public static string[] LikedItemsFor(string id)
    {
        switch (id)
        {
            case "monk":
                return new[] { "rice", "tu_gao", "honey", "xoi_gac", "trai_cay_kho" };
            case "librarian":
                return new[] { "cafe_den", "tra_da", "trai_cay_kho", "honey", "nuoc_dau" };
            case "chef":
                return new[] { "tomato", "pumpkin", "onion", "carrot", "strawberry", "mut_ca_rot", "dua_chua", "mi_hao_hao", "banh_mi", "tuong_ot" };
            case "fishshop":
                return new[] { "fish_carp", "fish_salmon", "fish_tuna", "fish_pufferfish", "ruou_gao" };
            default:
                return null;
        }
    }

    public static string[] AllGiftable()
    {
        return new[]
        {
            "carrot", "tomato", "strawberry", "pumpkin", "onion", "rice", "wheat", "corn", "potato", "sugarcane",
            "fish_carp", "fish_salmon", "fish_tuna", "fish_pufferfish",
            "honey", "trai_cay_kho", "mut_ca_rot", "dua_chua", "xoi_gac", "ruou_gao", "tu_gao",
            "cafe_den", "tra_da", "nuoc_dau", "soda", "keo", "tuong_ot", "banh_mi", "mi_hao_hao", "com_trang"
        };
    }

    // 10% shop discount at heart level 2+ (used by Chef + FishShop shops).
    public float ShopDiscountFor(string id)
    {
        return HeartLevel(id) >= 2 ? 0.9f : 1f;
    }

    private void AddPoints(int i, float amount)
    {
        if (amount <= 0f) return;
        int before = Mathf.FloorToInt(_points[i] / PointsPerHeart);
        _points[i] = Mathf.Clamp(_points[i] + amount, 0f, MaxPoints);
        int after = Mathf.FloorToInt(_points[i] / PointsPerHeart);

        if (after > before)
            GrantHeartReward(i, after);
    }

    private void GrantHeartReward(int i, int newLevel)
    {
        string id = _npcIds[i];
        var ui = GameManager.Instance?.UIManager;

        if (newLevel == 2)
        {
            if (ui != null)
                ui.ShowMessage(Localization.F("Tình bạn lên {0} tim với {1}!", HeartEmoji(2), NpcDisplayName(id)), 3f);
        }
        else if (newLevel == 3 && !_grantedL3[i])
        {
            _grantedL3[i] = true;
            GrantLevel3Reward(i);
        }
        else if (newLevel >= 5 && !_grantedL5[i])
        {
            _grantedL5[i] = true;
            if (ui != null)
                ui.ShowMessage(Localization.F("Tình bạn đạt 5 tim với {0}!", NpcDisplayName(id)), 3f);
        }
        else if (ui != null)
        {
            ui.ShowMessage(Localization.F("Tình bạn lên {0} tim với {1}!", HeartEmoji(newLevel), NpcDisplayName(id)), 3f);
        }
    }

    private void GrantLevel3Reward(int i)
    {
        var tm = ToolManager.Instance;
        var ui = GameManager.Instance?.UIManager;
        if (tm == null) return;

        string id = _npcIds[i];
        string item = null;
        int amount = 1;
        string flavor = "";

        switch (id)
        {
            case "monk":
                item = "tu_gao"; flavor = "Nhà Sư tặng con một túi gạo vì đã thân thiết!";
                break;
            case "librarian":
                item = "cafe_den"; flavor = "Thủ Thư tặng con một ly cà phê sách!";
                break;
            case "chef":
                item = "com_ga"; flavor = "Đầu Bếp tặng con một phần Cơm Gà ngon nhất quán!";
                break;
            case "fishshop":
                item = "fishing_bait"; amount = 2; flavor = "Người bán cá tặng con 2 mồi câu vì tin nhau!";
                break;
        }

        if (item != null && tm.CanHoldItem(item))
        {
            tm.AddItem(item, amount);
            if (ui != null)
                ui.ShowMessage(Localization.T(flavor), 3f);
        }
        else if (ui != null)
        {
            ui.ShowMessage(Localization.F("Tình bạn lên 3 tim với {0}!", NpcDisplayName(id)), 3f);
        }
    }

    public static string HeartEmoji(int level)
    {
        int filled = Mathf.Clamp(level, 0, 5);
        string s = "";
        for (int i = 0; i < filled; i++) s += "♥";
        for (int i = filled; i < 5; i++) s += "♡";
        return s;
    }

    public static string NpcDisplayName(string id)
    {
        switch (id)
        {
            case "monk": return Localization.T("Nhà Sư");
            case "librarian": return Localization.T("Thủ Thư");
            case "chef": return Localization.T("Đầu Bếp");
            case "fishshop": return Localization.T("Người Bán Câu Cá");
            default: return id;
        }
    }

    // === Save / load ===
    public string SerializeState()
    {
        var data = new FriendshipSaveData();
        for (int i = 0; i < _npcCount; i++)
        {
            data.points[i] = _points[i];
            data.talkedDay[i] = _talkedDay[i];
            data.giftDay[i] = _giftDay[i];
            data.grantedL3[i] = _grantedL3[i];
            data.grantedL5[i] = _grantedL5[i];
        }
        return JsonUtility.ToJson(data);
    }

    public void DeserializeState(string json)
    {
        if (string.IsNullOrEmpty(json)) return;
        try
        {
            var data = JsonUtility.FromJson<FriendshipSaveData>(json);
            if (data == null) return;
            for (int i = 0; i < _npcCount; i++)
            {
                _points[i] = Mathf.Clamp(data.points[i], 0f, MaxPoints);
                _talkedDay[i] = data.talkedDay[i];
                _giftDay[i] = data.giftDay[i];
                _grantedL3[i] = data.grantedL3[i];
                _grantedL5[i] = data.grantedL5[i];
            }
        }
        catch
        {
            // ignore malformed save
        }
    }

    [Serializable]
    public class FriendshipSaveData
    {
        public float[] points = new float[_npcCount];
        public int[] talkedDay = new int[_npcCount];
        public int[] giftDay = new int[_npcCount];
        public bool[] grantedL3 = new bool[_npcCount];
        public bool[] grantedL5 = new bool[_npcCount];
    }
}

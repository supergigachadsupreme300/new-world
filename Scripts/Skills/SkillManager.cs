using UnityEngine;
using System;

public class SkillManager : MonoSingleton<SkillManager>
{
    public enum Track { Farming = 0, Fishing = 1 }

    public const int MaxLevel = 5;

    private int[] _level = { 1, 1 };
    private float[] _xp = { 0f, 0f };

    public int Level(Track t) => _level[(int)t];
    public float XP(Track t) => _xp[(int)t];

    public float XPToNext(Track t)
    {
        int lv = _level[(int)t];
        if (lv >= MaxLevel) return 0f;
        return 100f + (lv - 1) * 50f;
    }

    public float XPNormalized(Track t)
    {
        float need = XPToNext(t);
        return need > 0f ? _xp[(int)t] / need : 1f;
    }

    public void Initialize()
    {
        for (int i = 0; i < 2; i++)
        {
            _level[i] = Mathf.Clamp(_level[i], 1, MaxLevel);
            _xp[i] = Mathf.Max(0f, _xp[i]);
        }
    }

    public void AddXP(Track t, float amount)
    {
        if (amount <= 0f) return;
        int idx = (int)t;
        _xp[idx] += amount;

        bool leveled = false;
        while (_level[idx] < MaxLevel && _xp[idx] >= XPToNext(t))
        {
            _xp[idx] -= XPToNext(t);
            _level[idx]++;
            leveled = true;
        }
        if (_level[idx] >= MaxLevel)
            _xp[idx] = 0f;

        if (leveled)
        {
            string msg = t == Track.Farming
                ? Localization.F("Cấp độ Canh Tác lên {0}! Bạn thu hoạch và hồi sức hiệu quả hơn.", _level[idx])
                : Localization.F("Cấp độ Câu Cá lên {0}! Cá khó thoát và kéo nhanh hơn.", _level[idx]);
            GameManager.Instance?.UIManager?.ShowMessage(msg, 3f);
        }
    }

    // === Passive stat modifiers ===
    public const float FishingXpPerCatch = 15f;

    public float FarmingXPFor(string cropType)
    {
        if (cropType == null) return 10f;
        switch (cropType)
        {
            case "wheat": return 8f;
            case "rice": return 10f;
            case "carrot": return 9f;
            case "tomato": return 10f;
            case "onion": return 10f;
            case "pumpkin": return 13f;
            case "strawberry": return 13f;
            case "potato": return 12f;
            case "corn": return 12f;
            case "sugarcane": return 11f;
            default: return 11f;
        }
    }

    public float FarmingStaminaEfficiency()
    {
        // higher farming level = cheaper farming stamina cost (up to -24%)
        float costMul = 1f - 0.06f * Mathf.Max(0, _level[(int)Track.Farming] - 1);
        return Mathf.Max(0.76f, costMul);
    }

    public float FishingFlopMultiplier()
    {
        // -10% flop chance per fishing level beyond 1 (min 50%)
        float mul = 1f - 0.1f * Mathf.Max(0, _level[(int)Track.Fishing] - 1);
        return Mathf.Max(0.5f, mul);
    }

    public float FishingReelMultiplier()
    {
        // +6% reel gain per fishing level beyond 1
        return 1f + 0.06f * Mathf.Max(0, _level[(int)Track.Fishing] - 1);
    }

    public string SerializeState()
    {
        var data = new SkillSaveData
        {
            farmingLevel = _level[0],
            farmingXp = _xp[0],
            fishingLevel = _level[1],
            fishingXp = _xp[1]
        };
        return JsonUtility.ToJson(data);
    }

    public void DeserializeState(string json)
    {
        if (string.IsNullOrEmpty(json)) return;
        var data = JsonUtility.FromJson<SkillSaveData>(json);
        if (data == null) return;
        _level[(int)Track.Farming] = data.farmingLevel;
        _xp[(int)Track.Farming] = data.farmingXp;
        _level[(int)Track.Fishing] = data.fishingLevel;
        _xp[(int)Track.Fishing] = data.fishingXp;
    }

    [Serializable]
    public class SkillSaveData
    {
        public int farmingLevel = 1;
        public float farmingXp;
        public int fishingLevel = 1;
        public float fishingXp;
    }
}

using UnityEngine;
using System;

// Persistent fishing-rod progression for Phase 3A.
// RodLevel 0-3; each upgrade boosts reeling and cuts flop.
public class FishingProgression : MonoSingleton<FishingProgression>
{
    public const int MaxRodLevel = 3;

    private int _rodLevel;

    public int RodLevel => _rodLevel;

    public void Initialize()
    {
        _rodLevel = Mathf.Clamp(_rodLevel, 0, MaxRodLevel);
    }

    // Truer to plan: reel gain rises and flop falls as the rod improves.
    public float RodReelMultiplier()
    {
        return 1f + 0.05f * _rodLevel;
    }

    public float RodFlopMultiplier()
    {
        return Mathf.Max(0.7f, 1f - 0.08f * _rodLevel);
    }

    // Attempts to upgrade to the given new level (must be exactly current+1).
    // Returns a localized result message (empty on failure with no spend).
    public string TryUpgrade(int requiredNewLevel)
    {
        if (_rodLevel + 1 == requiredNewLevel)
        {
            _rodLevel = Mathf.Clamp(requiredNewLevel, 0, MaxRodLevel);
            return Localization.F("Cần Câu đã nâng lên Cấp {0}!", _rodLevel);
        }
        if (_rodLevel + 1 < requiredNewLevel)
            return Localization.F("Nâng cấp này yêu cầu cần câu Cấp {0} trước.", requiredNewLevel - 1);
        return Localization.T("Cần câu đã ở cấp tối đa của bậc này.");
    }

    public string SerializeState()
    {
        return JsonUtility.ToJson(new FishingProgressionSaveData { rodLevel = _rodLevel });
    }

    public void DeserializeState(string json)
    {
        if (string.IsNullOrEmpty(json)) return;
        try
        {
            var data = JsonUtility.FromJson<FishingProgressionSaveData>(json);
            if (data == null) return;
            _rodLevel = Mathf.Clamp(data.rodLevel, 0, MaxRodLevel);
        }
        catch
        {
            // ignore malformed save
        }
    }

    [Serializable]
    public class FishingProgressionSaveData
    {
        public int rodLevel;
    }
}

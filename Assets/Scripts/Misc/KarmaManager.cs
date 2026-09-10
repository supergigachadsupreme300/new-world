using UnityEngine;

public class KarmaManager : MonoSingleton<KarmaManager>
{
public float CurrentKarma { get; private set; }
    public float MaxKarma { get; private set; }

    /// <summary>Multipliers fed by <see cref="ReligionManager"/> for the Buddhist faith perks.</summary>
    public float GainMultiplier = 1f;
    public float RegenMultiplier = 1f;
    public float MaxKarmaGainMultiplier = 1f;

    private const float REGEN_PER_GAME_HOUR = 1f / 24f;
    private float _regenAccumulator;

    public void Initialize(float maxKarma = 5f, float currentKarma = -1f)
    {
        MaxKarma = maxKarma;
        CurrentKarma = currentKarma < 0f ? MaxKarma : currentKarma;
    }
    public void AddKarma(float amount)
    {
        CurrentKarma = Mathf.Min(CurrentKarma + amount * Mathf.Max(0f, GainMultiplier), MaxKarma);
    }
    public void AddMaxKarma(float amount)
    {
        float scaled = amount * Mathf.Max(0f, MaxKarmaGainMultiplier);
        MaxKarma += scaled;
        CurrentKarma = Mathf.Min(CurrentKarma + scaled, MaxKarma);
    }
    public bool ConsumeKarma(float amount)
    {
        if (CurrentKarma < amount)
            return false;
        CurrentKarma -= amount;
        return true;
    }
    public void RegenKarma(float realDeltaTime)
    {
        var gm = GameManager.Instance;
        if (gm == null || gm.GamePaused) return;

        float timeSpeed = gm.TimeSpeed;
        float gameHoursElapsed = timeSpeed * realDeltaTime;
        _regenAccumulator += gameHoursElapsed * REGEN_PER_GAME_HOUR;

        if (_regenAccumulator >= 1f)
        {
            float toAdd = Mathf.Floor(_regenAccumulator);
            _regenAccumulator -= toAdd;
            CurrentKarma = Mathf.Min(CurrentKarma + toAdd * Mathf.Max(0f, RegenMultiplier), MaxKarma);
        }
    }
    public float GetKarmaNormalized()
    {
        return MaxKarma > 0f ? CurrentKarma / MaxKarma : 0f;
    }
    public KarmaSaveData GetSaveData()
    {
        return new KarmaSaveData { maxKarma = MaxKarma, currentKarma = CurrentKarma };
    }
    public void LoadSaveData(KarmaSaveData data)
    {
        if (data == null) return;
        MaxKarma = data.maxKarma > 0f ? data.maxKarma : 5f;
        CurrentKarma = Mathf.Clamp(data.currentKarma, 0f, MaxKarma);
    }

    [System.Serializable]
    public class KarmaSaveData
    {
        public float maxKarma;
        public float currentKarma;
    }
}

using UnityEngine;

public class KarmaManager : MonoSingleton<KarmaManager>
{
public float CurrentKarma { get; private set; }
    public float MaxKarma { get; private set; }

    private const float REGEN_PER_GAME_HOUR = 1f;
    private float _regenAccumulator;

    public void Initialize(float maxKarma = 5f, float currentKarma = -1f)
    {
        MaxKarma = maxKarma;
        CurrentKarma = currentKarma < 0f ? MaxKarma : currentKarma;
    }
    public void AddKarma(float amount)
    {
        CurrentKarma = Mathf.Min(CurrentKarma + amount, MaxKarma);
    }
    public void AddMaxKarma(float amount)
    {
        MaxKarma += amount;
        CurrentKarma = Mathf.Min(CurrentKarma + amount, MaxKarma);
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
            CurrentKarma = Mathf.Min(CurrentKarma + toAdd, MaxKarma);
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

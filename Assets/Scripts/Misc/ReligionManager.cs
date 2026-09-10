using UnityEngine;

/// <summary>
/// Three-faith religion system (PLAN.md Batch 20). The player belongs to at most ONE faith at a
/// time; worshipping at a holy place (monk - pagoda / taoist shrine / church) raises that faith's
/// devotion (0..<see cref="MaxDevotion"/>) and converts the shared Faith stat into religion-specific
/// passive perks. Switching faiths applies a devotion penalty and is limited to once per game day.
/// Only the currently-followed faith's perks are live - every getter returns the identity (1f) for
/// the other faiths so gameplay hooks can multiply harmlessly. The Faith stat itself is untouched
/// (level-ups / gear / races / permanent stat effects feed it as before).
/// </summary>
public class ReligionManager : MonoSingleton<ReligionManager>
{
    public enum ReligionFaith
    {
        None = 0,
        Taoism = 1,
        Buddhism = 2,
        Church = 3
    }

    public const int MaxDevotion = 10;

    /// <summary>Devotion lost (percent) when abandoning the current faith.</summary>
    private const float SwitchPenalty = 0.3f;

    public ReligionFaith CurrentFaith { get; private set; } = ReligionFaith.None;
    public int LastSwitchDay { get; private set; } = -1;

    private readonly int[] _devotion = new int[4];
    private readonly int[] _lastWorshipDay = new int[4];
    private int _churchBlessedDay = -1;

    private float _cachedFaith = -1f;

    public void Initialize()
    {
        CurrentFaith = ReligionFaith.None;
        LastSwitchDay = -1;
        _churchBlessedDay = -1;
        for (int i = 0; i < 4; i++)
        {
            _devotion[i] = 0;
            _lastWorshipDay[i] = -1;
        }
        RefreshBlessings();
    }

    // ── State queries ─────────────────────────────────────────────────────

    public bool IsCurrentFaith(ReligionFaith faith) => CurrentFaith == faith;

    public int GetDevotion(ReligionFaith faith) => _devotion[(int)faith];

    public bool HasWorshippedToday(ReligionFaith faith)
    {
        return GameManager.Instance != null && _lastWorshipDay[(int)faith] == GameManager.Instance.CurrentDay;
    }

    public bool CanSwitchToday()
    {
        var gm = GameManager.Instance;
        return gm == null || LastSwitchDay != gm.CurrentDay;
    }

    /// <summary>The Faith stat total from <see cref="PlayerStats"/> (cached per day).</summary>
    public float FaithValue
    {
        get
        {
            var gm = GameManager.Instance;
            int day = gm != null ? gm.CurrentDay : -1;
            if (_cachedFaith < 0f || day != _cachedFaithDay)
            {
                _cachedFaithDay = day;
                var stats = gm != null && gm.Player != null ? gm.Player.GetComponent<PlayerStats>() : null;
                _cachedFaith = stats != null ? stats.GetTotal(StatType.Faith) : 0f;
            }
            return _cachedFaith;
        }
    }
    private int _cachedFaithDay = -1;

    /// <summary>Words the player last worshipped at their CURRENT faith's holy place.</summary>
    public bool HasDailyBlessingToday
    {
        get
        {
            if (CurrentFaith == ReligionFaith.None)
                return false;
            return HasWorshippedToday(CurrentFaith);
        }
    }

    /// <summary>Church "holy water" blessing (full heal + heal power) is active today.</summary>
    public bool ChurchHolyWaterBlessed => _churchBlessedDay != -1
        && GameManager.Instance != null
        && _churchBlessedDay == GameManager.Instance.CurrentDay
        && CurrentFaith == ReligionFaith.Church;

    // ── Worship / switching ───────────────────────────────────────────────

    /// <summary>
    /// Worship at a holy place. Returns false when the offering was already given today at this
    /// place (or the faith is None). Joins the faith on the first worship; worshipping at a
    /// different faith's place converts the player (devotion penalty, once/day).
    /// </summary>
    public bool Worship(ReligionFaith faith, out bool switched)
    {
        switched = false;
        if (faith == ReligionFaith.None)
            return false;
        var gm = GameManager.Instance;
        int day = gm != null ? gm.CurrentDay : 1;
        if (_lastWorshipDay[(int)faith] == day)
            return false;

        if (CurrentFaith == ReligionFaith.None)
        {
            CurrentFaith = faith;
            switched = true;
        }
        else if (CurrentFaith != faith)
        {
            if (LastSwitchDay == day)
                return false;   // switch once per day; come back tomorrow
            ApplySwitch(faith);
            switched = true;
        }

        _lastWorshipDay[(int)faith] = day;
        _devotion[(int)faith] = Mathf.Min(MaxDevotion, _devotion[(int)faith] + 1);

        if (faith == ReligionFaith.Church)
            _churchBlessedDay = day;

        RefreshBlessings();
        return true;
    }

    /// <summary>Convert to another faith from the Religion tab (never switch to None/current).</summary>
    public bool SwitchFaith(ReligionFaith faith)
    {
        if (faith == ReligionFaith.None || faith == CurrentFaith)
            return false;
        if (CurrentFaith == ReligionFaith.None)
        {
            CurrentFaith = faith;
            RefreshBlessings();
            return true;
        }
        if (!CanSwitchToday())
            return false;
        ApplySwitch(faith);
        RefreshBlessings();
        return true;
    }

    private void ApplySwitch(ReligionFaith newFaith)
    {
        int old = (int)CurrentFaith;
        if (old >= 0 && old < _devotion.Length)
            _devotion[old] = Mathf.RoundToInt(_devotion[old] * (1f - SwitchPenalty));
        CurrentFaith = newFaith;
        var gm = GameManager.Instance;
        LastSwitchDay = gm != null ? gm.CurrentDay : 1;
        _cachedFaith = -1f;
    }

    /// <summary>Recalculate derived multipliers; resets the player's daily stamina blessing slot.</summary>
    public void OnDayChanged()
    {
        GameManager gm = GameManager.Instance;
        if (gm != null && gm.CurrentDay != _lastRefreshDay)
            _lastRefreshDay = gm.CurrentDay;
        StatsRefresh();
    }
    private int _lastRefreshDay = -1;
    private void StatsRefresh() { _cachedFaith = -1f; RefreshBlessings(); }

    public void RefreshBlessings()
    {
        var gm = GameManager.Instance;
        var player = gm != null ? gm.Player : null;
        if (player == null)
            return;
        float mult = 1f;
        if (CurrentFaith == ReligionFaith.Taoism)
            mult = 1f + TaoistStaminaPassive;
        bool blessed = HasDailyBlessingToday;
        if (blessed && (CurrentFaith == ReligionFaith.Taoism || CurrentFaith == ReligionFaith.Buddhism))
            mult += 1f;   // daily "qi"/monk blessing: stamina recovers x2 all day
        player.StaminaRegenMultiplier = Mathf.Min(mult, 3f);
    }

    // ── Faith perk getters (identity = 1f when the faith is not current) ──

    private float Dev(ReligionFaith f) => CurrentFaith == f ? _devotion[(int)f] : 0f;
    private float Faith => FaithValue;

    // Taoism: qi (stamina), longevity passes through harvesting luck, talisman damage vs demons.
    public float TaoistStaminaPassive => CurrentFaith == ReligionFaith.Taoism
        ? Mathf.Clamp(0.25f * Dev(ReligionFaith.Taoism) + 0.02f * Faith, 0f, 2f) : 0f;

    public float TaoistHarvestMult => CurrentFaith == ReligionFaith.Taoism
        ? 1f + Mathf.Clamp(0.03f * Dev(ReligionFaith.Taoism) + 0.005f * Faith, 0f, 0.6f) : 1f;

    public float TaoistDemonDamageMult => CurrentFaith == ReligionFaith.Taoism
        ? 1f + Mathf.Clamp(0.05f * Dev(ReligionFaith.Taoism) + 0.01f * Faith, 0f, 1f) : 1f;

    // Buddhism: karma economy + protection.
    public float BuddhistKarmaGainMult => CurrentFaith == ReligionFaith.Buddhism
        ? 1f + Mathf.Clamp(0.1f * Dev(ReligionFaith.Buddhism) + 0.01f * Faith, 0f, 2f) : 1f;

    public float BuddhistKarmaRegenMult => CurrentFaith == ReligionFaith.Buddhism
        ? 1f + Mathf.Clamp(0.1f * Dev(ReligionFaith.Buddhism) + 0.02f * Faith, 0f, 2f) : 1f;

    public float BuddhistMaxKarmaGainMult => CurrentFaith == ReligionFaith.Buddhism
        ? 1f + 0.1f * Dev(ReligionFaith.Buddhism) : 1f;

    public float BuddhistRosaryCost => CurrentFaith == ReligionFaith.Buddhism
        ? Mathf.Max(0.5f, 1f - 0.05f * Dev(ReligionFaith.Buddhism)) : 1f;

    public float BuddhistDemonTakenMult => CurrentFaith == ReligionFaith.Buddhism
        ? 1f - Mathf.Clamp(0.02f * Dev(ReligionFaith.Buddhism) + 0.002f * Faith, 0f, 0.4f) : 1f;

    // Church: holy water / exorcism / salvation.
    public float ChurchHolyDamageMult => CurrentFaith == ReligionFaith.Church
        ? 1f + Mathf.Clamp(0.05f * Dev(ReligionFaith.Church) + 0.02f * Faith, 0f, 2f) : 1f;

    public float ChurchHealPowerMult => CurrentFaith == ReligionFaith.Church
        ? 1f + Mathf.Clamp(0.03f * Dev(ReligionFaith.Church) + 0.02f * Faith, 0f, 1.5f) : 1f;

    public float ChurchBuffDurationMult => CurrentFaith == ReligionFaith.Church
        ? 1f + Mathf.Clamp(0.02f * Dev(ReligionFaith.Church) + 0.01f * Faith, 0f, 1f) : 1f;

    /// <summary>Church "holy water" blessing: healing output raised +25% while blessed today.</summary>
    public float ChurchBlessedHealMult => ChurchHolyWaterBlessed ? 1.25f : 1f;

    public float ChurchDemonTakenMult => CurrentFaith == ReligionFaith.Church
        ? 1f - Mathf.Clamp(0.02f * Dev(ReligionFaith.Church) + 0.002f * Faith, 0f, 0.4f) : 1f;

    // ── Save data ─────────────────────────────────────────────────────────

    [System.Serializable]
    public class ReligionSaveData
    {
        public int current;
        public int[] devotion;
        public int[] lastWorshipDay;
        public int lastSwitchDay;
    }

    public ReligionSaveData GetSaveData()
    {
        return new ReligionSaveData
        {
            current = (int)CurrentFaith,
            devotion = (int[])_devotion.Clone(),
            lastWorshipDay = (int[])_lastWorshipDay.Clone(),
            lastSwitchDay = LastSwitchDay
        };
    }

    public void LoadSaveData(ReligionSaveData data)
    {
        if (data == null)
        {
            Initialize();
            return;
        }
        CurrentFaith = EnumWithin(data.current) ? (ReligionFaith)data.current : ReligionFaith.None;
        LastSwitchDay = data.lastSwitchDay;
        for (int i = 0; i < 4; i++)
        {
            _devotion[i] = data.devotion != null && i < data.devotion.Length ? Mathf.Clamp(data.devotion[i], 0, MaxDevotion) : 0;
        }
        var gm = GameManager.Instance;
        for (int i = 0; i < 4; i++)
        {
            _lastWorshipDay[i] = data.lastWorshipDay != null && i < data.lastWorshipDay.Length ? data.lastWorshipDay[i] : -1;
        }
        _churchBlessedDay = _lastWorshipDay[(int)ReligionFaith.Church];
        RefreshBlessings();
    }

    private static bool EnumWithin(int v) => v >= (int)ReligionFaith.None && v <= (int)ReligionFaith.Church;
}
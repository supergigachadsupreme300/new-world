using System.Collections.Generic;
using UnityEngine;

public class QuestManager : MonoBehaviour
{
    public static QuestManager Instance { get; private set; }

    private readonly List<QuestSave> _quests = new List<QuestSave>();
    private int _lastDay;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void Update()
    {
        if (GameManager.Instance == null || !GameManager.Instance.InGame || GameManager.Instance.GamePaused)
            return;

        bool changed = CheckTimedQuests();
        changed |= CheckDayChange();
        if (changed)
            UpdateQuestUI();
    }

    public void InitializeQuests()
    {
        if (_quests.Count > 0)
            return;

        _lastDay = GameManager.Instance != null ? GameManager.Instance.CurrentDay : 1;

        AddStoryQuestsForDay(_lastDay);
        GenerateDailyQuests(_lastDay);
        UpdateQuestUI();
    }

    private bool CheckDayChange()
    {
        if (GameManager.Instance == null)
            return false;

        int currentDay = GameManager.Instance.CurrentDay;
        if (currentDay == _lastDay)
            return false;

        _lastDay = currentDay;
        AddStoryQuestsForDay(currentDay);
        RefreshDailyQuests(currentDay);
        return true;
    }

    private bool CheckTimedQuests()
    {
        if (GameManager.Instance == null)
            return false;

        bool changed = false;
        float now = Time.time;

        for (int i = 0; i < _quests.Count; i++)
        {
            var q = _quests[i];
            if (q.Completed || q.Failed || q.TimeLimit <= 0f)
                continue;

            float elapsed = now - q.TimeStarted;
            if (elapsed >= q.TimeLimit)
            {
                q.Failed = true;
                changed = true;
                GameManager.Instance?.UIManager?.ShowMessage($"{q.Name} thất bại!", 2f);
            }
        }

        return changed;
    }

    private void AddStoryQuestsForDay(int day)
    {
        if (day >= 1) AddIfMissing(CreateStoryQuest("Chào Hỏi Hàng Xóm", "greet", 2, 0, "Nói chuyện với Buffalo và Jessica để làm quen với hàng xóm.", 1));
        if (day >= 3) AddIfMissing(CreateStoryQuest("Mùa Thu Đầu Tiên", "wheat", 50, 150, "Thu hoạch 50 lúa mì để trở thành nông dân thực thụ.", 3));
        if (day >= 5) AddIfMissing(CreateStoryQuest("Bảo Vệ Đất", "enemies", 10, 300, "Diệt 10 kẻ thù để bảo vệ nông trại.", 5));
        if (day >= 8) AddIfMissing(CreateStoryQuest("Bàn Tay Xanh", "wheat", 150, 400, "Thu hoạch 150 lúa mì để chứng minh tài năng.", 8));
        if (day >= 10) AddIfMissing(CreateStoryQuest("Xây Dựng Đế Chế", "money_earned", 50000, 750, "Kiếm 50.000 vàng bằng cách bán nông sản.", 10));
        if (day >= 12) AddIfMissing(CreateStoryQuest("Thợ Săn Quái Vật", "enemies", 30, 600, "Diệt 30 kẻ thù để очистить vùng đất.", 12));
        if (day >= 15) AddIfMissing(CreateStoryQuest("Trận Đấu Cuối Cùng", "enemies", 50, 1500, "Diệt 50 kẻ thù — trận chiến sinh tử!", 15));
        if (day >= 18) AddIfMissing(CreateStoryQuest("Tỷ Phú", "money_earned", 200000, 3000, "Kiếm 200.000 vàng để trở thành tỷ phú.", 18));
    }

    private void AddIfMissing(QuestSave quest)
    {
        foreach (var q in _quests)
        {
            if (q.Name == quest.Name)
                return;
        }
        _quests.Add(quest);
    }

    private void GenerateDailyQuests(int day)
    {
        var pool = new List<QuestSave>
        {
            CreateDailyQuest("Thu Hoạch Nhanh", "wheat", 25, 100, "Thu hoạch 25 lúa mì hôm nay."),
            CreateDailyQuest("Mùa Màng Bội Thu", "wheat", 60, 200, "Thu hoạch 60 lúa mì hôm nay."),
            CreateDailyQuest("Tiêu Diệt Sâu Bệnh", "enemies", 5, 150, "Diệt 5 quái vật hôm nay."),
            CreateDailyQuest("Săn Quái", "enemies", 15, 350, "Diệt 15 quái vật hôm nay."),
            CreateDailyQuest("Kiếm Thêm", "money_earned", 5000, 120, "Kiếm 5.000 vàng hôm nay."),
            CreateDailyQuest("Thu Nhập Lớn", "money_earned", 15000, 300, "Kiếm 15.000 vàng hôm nay."),
            CreateDailyQuest("Chuỗi Lúa Mì", "wheat", 100, 250, "Thu hoạch 100 lúa mì hôm nay."),
            CreateDailyQuest("Diệt Sạch", "enemies", 25, 500, "Diệt 25 quái vật hôm nay."),
        };

        int pickCount = Mathf.Min(2, pool.Count);
        for (int i = 0; i < pickCount; i++)
        {
            int idx = Random.Range(i, pool.Count);
            var temp = pool[i];
            pool[i] = pool[idx];
            pool[idx] = temp;
        }

        for (int i = 0; i < pickCount; i++)
        {
            var q = pool[i];
            q.Name = $"[Hàng Ngày] {q.Name}";
            _quests.Add(q);
        }
    }

    private void RefreshDailyQuests(int day)
    {
        _quests.RemoveAll(q => q.QuestType == "daily");
        GenerateDailyQuests(day);
    }

    public void AddProgress(string target, int amount)
    {
        if (string.IsNullOrEmpty(target))
            return;

        bool anyJustCompleted = false;
        foreach (var quest in _quests)
        {
            if (quest.Completed || quest.Failed)
                continue;

            if (quest.QuestType == "timed" && quest.TimeLimit > 0f)
            {
                float elapsed = Time.time - quest.TimeStarted;
                if (elapsed >= quest.TimeLimit)
                {
                    quest.Failed = true;
                    continue;
                }
            }

            if (quest.Target == target)
            {
                quest.Progress += amount;
                if (quest.Progress >= quest.Count)
                {
                    quest.Progress = quest.Count;
                    quest.Completed = true;
                    anyJustCompleted = true;
                }
            }
        }

        UpdateQuestUI();

        if (anyJustCompleted)
            AwardCompleted();
    }

    private void AwardCompleted()
    {
        long total = 0;
        foreach (var q in _quests)
        {
            if (q.Completed && !q.RewardClaimed)
            {
                total += q.RewardMoney;
                q.RewardClaimed = true;
            }
        }
        if (total > 0 && GameManager.Instance?.Player != null)
        {
            GameManager.Instance.Player.Money += total;
            var msg = $"Nhiệm vụ hoàn thành! Nhận {total}g!";
            GameManager.Instance?.UIManager?.ShowMessage(msg, 3f);
        }
    }

    public void LoadQuestSaves(List<QuestSave> saved)
    {
        if (saved == null || saved.Count == 0)
            return;

        _quests.Clear();
        foreach (var q in saved)
            _quests.Add(q);
        if (GameManager.Instance != null)
            _lastDay = GameManager.Instance.CurrentDay;
        UpdateQuestUI();
    }

    public List<QuestSave> GetQuestSaves()
    {
        return new List<QuestSave>(_quests);
    }

    private QuestSave CreateStoryQuest(string name, string target, int count, int reward, string description, int requiredDay)
    {
        return new QuestSave
        {
            Name = name,
            Target = target,
            Count = count,
            Progress = 0,
            RewardMoney = reward,
            RewardClaimed = false,
            Completed = false,
            QuestType = "story",
            TimeLimit = 0f,
            TimeStarted = 0f,
            RequiredDay = requiredDay,
            Description = description,
            Failed = false
        };
    }

    private QuestSave CreateDailyQuest(string name, string target, int count, int reward, string description)
    {
        return new QuestSave
        {
            Name = name,
            Target = target,
            Count = count,
            Progress = 0,
            RewardMoney = reward,
            RewardClaimed = false,
            Completed = false,
            QuestType = "daily",
            TimeLimit = 0f,
            TimeStarted = 0f,
            RequiredDay = 0,
            Description = description,
            Failed = false
        };
    }

    public QuestSave CreateTimedQuest(string name, string target, int count, int reward, float timeLimit, string description)
    {
        return new QuestSave
        {
            Name = $"[Giới Hạn] {name}",
            Target = target,
            Count = count,
            Progress = 0,
            RewardMoney = reward,
            RewardClaimed = false,
            Completed = false,
            QuestType = "timed",
            TimeLimit = timeLimit,
            TimeStarted = Time.time,
            RequiredDay = 0,
            Description = description,
            Failed = false
        };
    }

    public void AddTimedQuest(QuestSave timedQuest)
    {
        AddIfMissing(timedQuest);
        UpdateQuestUI();
    }

    public void AddStoryQuest(string name, string target, int count, int reward, string description)
    {
        AddIfMissing(CreateStoryQuest(name, target, count, reward, description, 0));
    }

    public bool IsComplete(string target)
    {
        foreach (var q in _quests)
            if (q.Target == target && q.Completed)
                return true;
        return false;
    }

    public bool IsNamedQuestComplete(string questName)
    {
        foreach (var q in _quests)
            if (q.Name == questName && q.Completed)
                return true;
        return false;
    }

    public int GetProgress(string target)
    {
        foreach (var q in _quests)
            if (q.Target == target)
                return q.Progress;
        return 0;
    }

    public int GetNamedQuestProgress(string questName)
    {
        foreach (var q in _quests)
            if (q.Name == questName)
                return q.Progress;
        return 0;
    }

    private void UpdateQuestUI()
    {
        if (GameManager.Instance == null || GameManager.Instance.UIManager == null)
            return;

        string hud = "";
        string panel = "";

        var storyQuests = new List<QuestSave>();
        var dailyQuests = new List<QuestSave>();
        var timedQuests = new List<QuestSave>();

        foreach (var q in _quests)
        {
            if (q.QuestType == "story") storyQuests.Add(q);
            else if (q.QuestType == "daily") dailyQuests.Add(q);
            else if (q.QuestType == "timed") timedQuests.Add(q);
            else storyQuests.Add(q);
        }

        if (storyQuests.Count > 0)
        {
            panel += "--- Nhiệm Vụ Cốt Truyện ---\n";
            foreach (var q in storyQuests)
            {
                string status = GetQuestStatusString(q);
                panel += $"{q.Name}: {status}\n";
                if (!string.IsNullOrEmpty(q.Description))
                    panel += $"  {q.Description}\n";
                if (hud.Length < 200)
                    hud += $"{q.Name}: {status}\n";
            }
        }

        if (dailyQuests.Count > 0)
        {
            panel += "\n--- Nhiệm Vụ Hàng Ngày ---\n";
            foreach (var q in dailyQuests)
            {
                string status = GetQuestStatusString(q);
                panel += $"{q.Name}: {status}\n";
                if (!string.IsNullOrEmpty(q.Description))
                    panel += $"  {q.Description}\n";
                if (hud.Length < 200)
                    hud += $"{q.Name}: {status}\n";
            }
        }

        if (timedQuests.Count > 0)
        {
            panel += "\n--- Nhiệm Vụ Giới Hạn ---\n";
            foreach (var q in timedQuests)
            {
                string status = GetQuestStatusString(q);
                panel += $"{q.Name}: {status}\n";
                if (!string.IsNullOrEmpty(q.Description))
                    panel += $"  {q.Description}\n";
                if (hud.Length < 200)
                    hud += $"{q.Name}: {status}\n";
            }
        }

        hud = hud.TrimEnd('\n');
        panel = panel.TrimEnd('\n');

        GameManager.Instance.UIManager.UpdateQuestHud(hud);
        GameManager.Instance.UIManager.UpdateQuestPanelText(panel);
    }

    private string GetQuestStatusString(QuestSave q)
    {
        if (q.Failed)
            return "THẤT BẠI";
        if (q.Completed)
            return "HOÀN THÀNH";

        if (q.QuestType == "timed" && q.TimeLimit > 0f)
        {
            float elapsed = Time.time - q.TimeStarted;
            float remaining = Mathf.Max(0f, q.TimeLimit - elapsed);
            int mins = Mathf.FloorToInt(remaining / 60f);
            int secs = Mathf.FloorToInt(remaining % 60f);
            return $"{q.Progress}/{q.Count} [{mins}:{secs:D2}]";
        }

        return $"{q.Progress}/{q.Count}";
    }

    [System.Serializable]
    public class QuestSave
    {
        public string Name;
        public string Target;
        public int Count;
        public int Progress;
        public int RewardMoney;
        public bool RewardClaimed;
        public bool Completed;
        public string QuestType;
        public float TimeLimit;
        public float TimeStarted;
        public int RequiredDay;
        public string Description;
        public bool Failed;
    }
}

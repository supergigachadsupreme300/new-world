using System.Collections.Generic;
using UnityEngine;

public class QuestManager : MonoBehaviour
{
    public static QuestManager Instance { get; private set; }

    private readonly List<QuestSave> _quests = new List<QuestSave>();
    private int _focusedIndex;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    public void InitializeQuests()
    {
        if (_quests.Count > 0)
            return;

        _quests.Add(CreateQuest("Harvest wheat", "wheat", 100, 250));
        _quests.Add(CreateQuest("Slay monsters", "enemies", 30, 500));
        _quests.Add(CreateQuest("Earn coins", "money_earned", 100000, 1000));
        _focusedIndex = 0;
        UpdateQuestUI();
    }

    public void AddProgress(string target, int amount)
    {
        if (string.IsNullOrEmpty(target))
            return;

        foreach (var quest in _quests)
        {
            if (quest.Completed)
                continue;

            if (quest.Target == target || quest.Target == "money_earned")
            {
                quest.Progress += amount;
                if (quest.Progress >= quest.Count)
                {
                    quest.Progress = quest.Count;
                    quest.Completed = true;
                }
            }
        }

        UpdateQuestUI();

        if (AllQuestsCompleted())
        {
            GameManager.Instance?.RequestHappyEnding();
        }
    }

    public void LoadQuestSave(QuestSave saved)
    {
        if (saved == null)
            return;

        _quests.Clear();
        _quests.Add(saved);
        _focusedIndex = 0;
        UpdateQuestUI();
    }

    public QuestSave GetQuestSave()
    {
        if (_quests.Count == 0)
            return null;
        return _quests[_focusedIndex];
    }

    private bool AllQuestsCompleted()
    {
        foreach (var quest in _quests)
        {
            if (!quest.Completed)
                return false;
        }
        return _quests.Count > 0;
    }

    private QuestSave CreateQuest(string name, string target, int count, int reward)
    {
        return new QuestSave
        {
            Name = name,
            Target = target,
            Count = count,
            Progress = 0,
            RewardMoney = reward,
            Completed = false
        };
    }

    private void UpdateQuestUI()
    {
        if (GameManager.Instance != null && GameManager.Instance.UIManager != null)
        {
            var quest = _quests.Count > 0 ? _quests[_focusedIndex] : null;
            if (quest != null)
                GameManager.Instance.UIManager.UpdateQuestText(quest.Name, quest.Progress, quest.Count);
        }
    }

    [System.Serializable]
    public class QuestSave
    {
        public string Name;
        public string Target;
        public int Count;
        public int Progress;
        public int RewardMoney;
        public bool Completed;
    }
}

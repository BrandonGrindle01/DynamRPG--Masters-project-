using System.Collections.Generic;
using UnityEngine;

public class KeyAuthorQuest : MonoBehaviour
{
    public static KeyAuthorQuest Instance;

    [Header("Authored Quests")]
    public List<KeyQuestSO> questList;

    private int currentIndex = 0;
    private KeyQuestInstance currentQuest;

    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        StartNextKeyQuest();
    }

    public void StartNextKeyQuest()
    {
        if (currentIndex >= questList.Count)
        {
            Debug.Log("All key quests complete.");
            return;
        }

        currentQuest = new KeyQuestInstance(questList[currentIndex]);
        Debug.Log($"[KeyQuest] Started: {currentQuest.data.questName}");
        currentIndex++;
    }

    public void IncrementKillQuest(GameObject killed)
    {
        if (currentQuest == null || currentQuest.data.questType != QuestType.Kill) return;

        if (currentQuest.data.requiresAllTargetsDead && currentQuest.data.targets.Length > 0)
        {
            foreach (var target in currentQuest.data.targets)
            {
                if (target == killed)
                {
                    currentQuest.currentProgress++;
                    break;
                }
            }
        }
        else
        {
            currentQuest.currentProgress++;
        }

        if (currentQuest.currentProgress >= currentQuest.data.targetAmount)
        {
            CompleteCurrentQuest();
        }
    }

    private void CompleteCurrentQuest()
    {
        currentQuest.isCompleted = true;
        Debug.Log($"[KeyQuest] Completed: {currentQuest.data.questName}");
        StartNextKeyQuest();
    }

    public void ReportKill(GameObject killed)
    {
        if (currentQuest == null || currentQuest.data.questType != QuestType.Kill) return;

        if (currentQuest.data.requiresAllTargetsDead)
        {
            if (System.Array.Exists(currentQuest.data.targets, t => t.name == killed.name))
            {
                currentQuest.IncrementProgress();
            }
        }
        else
        {
            currentQuest.IncrementProgress();
        }
    }

    public void ReportCollect(ItemData collected)
    {
        if (currentQuest == null || currentQuest.data.questType != QuestType.Collect) return;

        if (collected == currentQuest.data.requiredItem)
        {
            currentQuest.IncrementProgress();
        }
    }

    public void ReportExplore(Vector3 location, float radius = 10f)
    {
        if (currentQuest == null || currentQuest.data.questType != QuestType.Explore) return;

        if (Vector3.Distance(location, currentQuest.data.questLocation) <= radius)
        {
            currentQuest.IncrementProgress();
        }
    }

    public void ReportSteal(GameObject item)
    {
        if (currentQuest == null || currentQuest.data.questType != QuestType.Steal) return;

        if (System.Array.Exists(currentQuest.data.targets, t => t.name == item.name))
        {
            currentQuest.IncrementProgress();
        }
    }

    public KeyQuestInstance GetCurrentQuest() => currentQuest;
}

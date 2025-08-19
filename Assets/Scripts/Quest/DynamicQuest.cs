using UnityEngine;
using System;
using System.Collections.Generic;

[Serializable]
public class DynamicQuest
{
    public string questId;
    public string questName;
    public QuestType type;
    public QuestStatus status = QuestStatus.Inactive;

    [Header("Context")]
    public GameObject questGiver;
    public string contextTag;

    [Header("Targets")]
    public GameObject targetObject;
    public string targetItemName;
    public Vector3 targetPosition;
    public int requiredCount = 1;
    public int currentCount = 0;

    [Header("Rules")]
    public bool requireStealth = false;
    public float timeLimit = -1f;
    public float startedAt = -1f;

    [Header("Carry / Deliver")]
    public ItemData deliverItem;

    [Header("Area / Kill Filters")]
    public float areaRadius = 8f;
    public float zoneAlphaOverride = .3f;
    public float scatterFactor = 0.8f;
    public float deliverRadiusOverride = 2f;
    public string killRequiredTag;

    [Header("Collect Setup")]
    public ItemData collectItem;

    [Header("Rewards")]
    public int goldReward = 25;
    public List<ItemData> itemRewards = new();

    [Header("Text")]
    public string introText;
    public string successText;
    public string failText;

    public bool IsTimedOut => timeLimit > 0 && startedAt > 0 && Time.time - startedAt > timeLimit;
    public bool IsComplete => status == QuestStatus.Completed;
    [NonSerialized] public bool runtimeSetupDone;

    public void StartQuest()
    {
        status = QuestStatus.Active;
        startedAt = Time.time;
        PlayerStatsTracker.Instance?.ResetQuestTimer();
    }

    public void MarkProgress(int delta = 1)
    {
        if (status != QuestStatus.Active) return;
        currentCount += delta;
        if (currentCount >= requiredCount) Complete();
    }

    public void Complete()
    {
        if (status == QuestStatus.Completed) return;
        status = QuestStatus.Completed;
    }

    public void Fail()
    {
        if (status == QuestStatus.Completed) return;
        status = QuestStatus.Failed;
    }
}
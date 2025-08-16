using System;
using UnityEngine;

public static class QuestService
{
    public static event Action<DynamicQuestInstance> OnDynamicAssigned;
    public static event Action<KeyQuestInstance> OnKeyAssigned;
    public static event Action<string, int, int> OnProgress;
    public static event Action<string> OnCompleted;

    public static KeyQuestInstance CurrentKey { get; private set; }
    public static DynamicQuestInstance CurrentDynamic { get; private set; }

    private static string _currentQuestId => CurrentDynamic ? CurrentDynamic.questName : (CurrentKey != null ? CurrentKey.data.questName : null);

    public static void SetKey(KeyQuestInstance key)
    {
        CurrentKey = key;
        CurrentDynamic = null;
        OnKeyAssigned?.Invoke(key);
        // Map marker for key target (optional; add if you want)
        // MapService.AddSimple($"key:{key.data.questName}:marker", key.data.questLocation, MapIconType.QuestTarget);
    }

    public static void AssignDynamic(DynamicQuestInstance q)
    {
        CurrentDynamic = q;
        OnDynamicAssigned?.Invoke(q);

        if (q.questGiver)
        {
            MapService.AddSimple($"q:{q.questName}:giver", q.questGiver.transform.position, MapIconType.QuestGiver);
        }
        if (q.currentTarget) // may be null for Explore until trigger spawns
        {
            MapService.AddSimple($"q:{q.questName}:target", q.currentTarget.transform.position, MapIconType.QuestTarget);
        }
    }

    private static void ClearDynamicMarkers(DynamicQuestInstance q)
    {
        MapService.Remove($"q:{q.questName}:giver");
        MapService.Remove($"q:{q.questName}:target");
    }

    public static void ReportKill(GameObject killed)
    {
        if (!CurrentDynamic || CurrentDynamic.questType != DynamicQuestType.Kill) return;
        CurrentDynamic.RegisterProgress();
        OnProgress?.Invoke(CurrentDynamic.questName, CurrentDynamic.currentProgress, CurrentDynamic.template.baseTargetAmount);
        CheckDynamicComplete();
    }

    public static void ReportCollect(ItemData item)
    {
        if (!CurrentDynamic) return;
        if (CurrentDynamic.questType == DynamicQuestType.Collect && item == CurrentDynamic.template.requiredItem)
        {
            CurrentDynamic.RegisterProgress();
            OnProgress?.Invoke(CurrentDynamic.questName, CurrentDynamic.currentProgress, CurrentDynamic.template.baseTargetAmount);
            CheckDynamicComplete();
        }
    }

    public static void ReportExplore(Transform trigger)
    {
        if (!CurrentDynamic) return;
        if (CurrentDynamic.questType == DynamicQuestType.Explore)
        {
            CurrentDynamic.RegisterProgress();
            OnProgress?.Invoke(CurrentDynamic.questName, CurrentDynamic.currentProgress, CurrentDynamic.template.baseTargetAmount);
            CheckDynamicComplete();
        }
    }

    public static void ReportSteal(GameObject itemOrOwner)
    {
        if (!CurrentDynamic) return;
        if (CurrentDynamic.questType == DynamicQuestType.Steal)
        {
            CurrentDynamic.RegisterProgress();
            OnProgress?.Invoke(CurrentDynamic.questName, CurrentDynamic.currentProgress, CurrentDynamic.template.baseTargetAmount);
            CheckDynamicComplete();
        }
    }

    private static void CheckDynamicComplete()
    {
        if (CurrentDynamic && CurrentDynamic.isCompleted)
        {
            OnCompleted?.Invoke(CurrentDynamic.questName);
            ClearDynamicMarkers(CurrentDynamic);
            DynamicQuestGenerator.Instance?.GenerateNextDynamicQuest();
        }
    }
}
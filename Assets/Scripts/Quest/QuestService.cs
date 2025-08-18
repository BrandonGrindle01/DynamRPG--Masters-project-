using UnityEngine;
using System;
using System.Collections.Generic;
using System.Linq;

public static class QuestService
{
    public static event Action<DynamicQuest> OnAssigned;
    public static event Action<DynamicQuest> OnUpdated;
    public static event Action<DynamicQuest> OnCompleted;
    public static event Action<DynamicQuest> OnTurnedIn;

    public static event Action OnPendingChanged;

    private static readonly List<DynamicQuest> _active = new();
    public static IReadOnlyList<DynamicQuest> Active => _active;

    private static DynamicQuest _current;
    public static bool ShowAssignmentPopup = false;

    private static DynamicQuest _pendingOffer;
    private static GameObject _pendingGiver;

    public static bool SameNPC(GameObject a, GameObject b)
    {
        if (!a || !b) return false;
        if (a == b) return true;

        return false;
    }

    public static void ClearPendingOffer()
    {
        _pendingOffer = null;
        _pendingGiver = null;
        QuestIndicatorCoordinator.Instance?.ClearFocus();
        OnPendingChanged?.Invoke();
    }

    public static void SetPendingOffer(DynamicQuest q)
    {
        var old = _pendingGiver;

        _pendingOffer = q;
        _pendingGiver = q?.questGiver;

        if (old && old != _pendingGiver)
            QuestIndicatorCoordinator.Instance?.HideOn(old);

        if (_pendingGiver)
            QuestIndicatorCoordinator.Instance?.FocusOn(_pendingGiver, showQuestion: false);

        OnPendingChanged?.Invoke();
    }

    public static bool HasPendingFor(GameObject npc)
    {
        return _pendingOffer != null && (_pendingGiver == null || _pendingGiver == npc);
    }

    public static DynamicQuest GetPending() => _pendingOffer;

    public static bool AcceptPendingOffer(GameObject npc)
    {
        if (_pendingOffer == null) return false;
        if (_pendingGiver != null && npc != _pendingGiver) return false;

        var accepted = _pendingOffer;
        var giver = _pendingGiver ?? npc;
        if (accepted.questGiver == null) accepted.questGiver = giver;
        _pendingOffer = null;
        _pendingGiver = null;

        AssignDynamic(accepted);

        QuestIndicatorCoordinator.Instance?.HideOn(giver);

        OnPendingChanged?.Invoke();
        return true;
    }

    public static void DeclinePendingOffer(GameObject npc)
    {
        if (_pendingOffer == null) return;
        if (_pendingGiver != null && npc != _pendingGiver) return;

        _pendingOffer = null;
        _pendingGiver = null;
        QuestIndicatorCoordinator.Instance?.ClearFocus();
        OnPendingChanged?.Invoke();
    }

    private static readonly Dictionary<string, List<GameObject>> _rt = new();

    public static void RegisterRuntimeObject(string questId, GameObject go)
    {
        if (string.IsNullOrEmpty(questId) || !go) return;
        if (!_rt.TryGetValue(questId, out var list))
        {
            list = new List<GameObject>();
            _rt[questId] = list;
        }
        list.Add(go);
    }

    public static void CleanupRuntime(string questId)
    {
        if (string.IsNullOrEmpty(questId)) return;
        if (_rt.TryGetValue(questId, out var list))
        {
            foreach (var go in list) if (go) GameObject.Destroy(go);
            _rt.Remove(questId);
        }
    }

    public static void AssignDynamic(DynamicQuest quest)
    {
        if (quest == null) return;
        quest.questId = string.IsNullOrEmpty(quest.questId) ? System.Guid.NewGuid().ToString("N") : quest.questId;
        quest.StartQuest();
        _active.Add(quest);
        _current = quest;
        if (quest.targetObject)
        {
            MapService.AddSimple($"q_{quest.questId}_target",
                quest.targetObject.transform.position,
                MapIconType.QuestTarget);
        }
        else if (quest.targetPosition != Vector3.zero)
        {
            MapService.AddSimple($"q_{quest.questId}_target",
                quest.targetPosition,
                MapIconType.QuestTarget);
        }

        switch (quest.type)
        {
            case QuestType.Explore:
                DynamicQuestGenerator.Instance?.SetupExploreSearchRuntime(quest);
                break;
            case QuestType.Deliver:
                if (quest.deliverItem) InventoryManager.Instance?.AddItem(quest.deliverItem, 1);
                DynamicQuestGenerator.Instance?.SetupDeliverRuntime(quest);
                break;
            case QuestType.Collect:
                DynamicQuestGenerator.Instance?.SetupCollectRuntime(quest);
                break;
            case QuestType.Kill:
                DynamicQuestGenerator.Instance?.SetupKillRuntime(quest);
                break;
        }

        if (ShowAssignmentPopup && quest.questGiver)
        {
            DialogueService.BeginOneLiner(
                DialogueService.CleanName(quest.questGiver.name),
                $"Quest: {quest.questName}", null, 3f, true);
        }
        OnAssigned?.Invoke(quest);
    }

    public static DynamicQuest GetCurrent()
    {
        if (_current != null && _active.Contains(_current))
            return _current;

        return _active.Count > 0 ? _active[_active.Count - 1] : null;
    }

    public static bool TryTurnIn(GameObject npc)
    {
        var q = GetCurrent();


        if (q == null || q.status != QuestStatus.Completed)
        {
            DialogueService.BeginOneLiner("", "You haven't completed the task.", null, 2f, true);
            return false;
        }

        if (q.questGiver && npc && !SameNPC(q.questGiver, npc))
        {
            DialogueService.BeginOneLiner("", "Turn this in to the quest giver.", null, 2f, true);
            return false;
        }

        if (q.type == QuestType.Collect && !string.IsNullOrEmpty(q.targetItemName))
        {
            var inv = InventoryManager.Instance;
            if (inv != null)
            {
                int toRemove = q.requiredCount;
                for (int i = inv.inventory.Count - 1; i >= 0 && toRemove > 0; i--)
                {
                    var slot = inv.inventory[i];
                    if (slot.item != null && slot.item.itemName == q.targetItemName)
                    {
                        int take = Mathf.Min(slot.quantity, toRemove);
                        inv.RemoveItem(slot.item, take);
                        toRemove -= take;
                    }
                }
            }
        }

        if (q.goldReward > 0) InventoryManager.Instance?.AddGold(q.goldReward);
        if (q.itemRewards != null) foreach (var it in q.itemRewards) InventoryManager.Instance?.AddItem(it, 1);

        MapService.Remove($"q_{q.questId}_target");
        CleanupRuntime(q.questId);

        OnTurnedIn?.Invoke(q);
        DialogueService.End();

        _active.Remove(q);
        if (_current == q) _current = null;

        QuestIndicatorCoordinator.Instance?.ClearFocus();
        KeyQuestManager.Instance?.NotifyDynamicQuestFinished();

        var giverName = DialogueService.CleanName(npc ? npc.name : "NPC");
        var line = string.IsNullOrEmpty(q.successText) ? $"Completed: {q.questName}. +{q.goldReward}g" : $"{q.successText} +{q.goldReward}g";
        DialogueService.BeginOneLiner(giverName, line, null, 2f, true);

        return true;
    }

    public static void ReportKill(GameObject victim)
    {
        var q = GetCurrent();

        if (q == null || q.status != QuestStatus.Active) return;
        if (q.type != QuestType.Kill) return;
        if (victim == null) return;
        
        if (q.targetObject)
        {
            if (q.targetObject == victim)
            {
                q.MarkProgress(1);
                OnUpdated?.Invoke(q);
                if (q.IsComplete)
                {
                    MapService.SetActive($"q_{q.questId}_target", false);
                    OnCompleted?.Invoke(q);
                    DialogueService.BeginOneLiner("", $"Objective complete: {q.questName}", null, 2f, true);
                }
            }
            return;
        }

        bool tagOk = string.IsNullOrEmpty(q.killRequiredTag) || victim.CompareTag(q.killRequiredTag);
        bool areaOk = q.areaRadius <= 0f ||
                      Vector3.Distance(victim.transform.position, q.targetPosition) <= q.areaRadius;

        if (tagOk && areaOk)
        {
            q.MarkProgress(1);
            OnUpdated?.Invoke(q);
            if (q.IsComplete)
            {
                MapService.SetActive($"q_{q.questId}_target", false);
                OnCompleted?.Invoke(q);
                DialogueService.BeginOneLiner("", $"Objective complete: {q.questName}", null, 2f, true);
            }
        }
    }

    public static void ReportSteal(GameObject itemObject)
    {
        var q = GetCurrent();
        if (q == null || q.status != QuestStatus.Active) return;
        if (q.type != QuestType.Steal && q.type != QuestType.Collect) return;

        if (q.type == QuestType.Steal)
        {
            var col = itemObject ? itemObject.GetComponent<ItemCollection>() : null;
            if (col && (string.IsNullOrEmpty(q.targetItemName) || (col.itemData && col.itemData.itemName == q.targetItemName)))
            {
                q.MarkProgress(1);
                OnUpdated?.Invoke(q);
                if (q.IsComplete) OnCompleted?.Invoke(q);
            }
        }
    }

    public static void ReportCollect(ItemData item)
    {
        var q = GetCurrent();
        if (q == null || q.status != QuestStatus.Active) return;
        if (q.type != QuestType.Collect) return;
        if (item == null) return;

        if (string.IsNullOrEmpty(q.targetItemName) || item.itemName == q.targetItemName)
        {

            q.MarkProgress(1);
            OnUpdated?.Invoke(q);
            if (q.IsComplete)
            {
                MapService.SetActive($"q_{q.questId}_target", false);
                OnCompleted?.Invoke(q);
                CleanupRuntime(q.questId);
            }
        }
    }

    public static void ReportEnteredLocation(string id)
    {
        var q = GetCurrent();
        if (q == null || q.status != QuestStatus.Active) return;
        if (q.type != QuestType.Explore) return;

        if (id == $"q_{q.questId}_target" || id == $"q_{q.questId}_token")
        {
            q.MarkProgress(1);
            MapService.SetActive($"q_{q.questId}_target", false);
            OnCompleted?.Invoke(q);
            CleanupRuntime(q.questId);
        }
    }

    public static void ReportDelivered(string questId)
    {
        var q = GetCurrent();
        if (q == null || q.status != QuestStatus.Active) return;
        if (q.type != QuestType.Deliver) return;
        if (q.questId != questId) return;

        q.MarkProgress(1);
        MapService.SetActive($"q_{q.questId}_target", false);
        OnCompleted?.Invoke(q);
        CleanupRuntime(q.questId);
    }

    public static bool ReportKeyTalkedTo(GameObject npc)
    {
        var km = KeyQuestManager.Instance;
        if (!km) return false;
        return km.TryCompleteByTalkingTo(npc);
    }

    public static void ReportKeyEnemyKilled(GameObject enemy)
    {
        var km = KeyQuestManager.Instance; if (!km) return;
        km.NotifyEnemyKilled(enemy);
    }

    public static void ReportKeyReachedLocation(Vector3 playerPos)
    {
        var km = KeyQuestManager.Instance; if (!km) return;
        km.TryCompleteByReach(playerPos);
    }

    public static bool TryTurnInKey(GameObject npc)
    {
        var km = KeyQuestManager.Instance; if (!km) return false;
        return km.TryTurnInCurrentKey(npc);
    }

    private static bool _keyEventsHooked;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void HookKeyQuestEvents()
    {
        if (_keyEventsHooked) return;
        _keyEventsHooked = true;

        KeyQuestManager.OnKeyQuestAvailable += HandleKeyAvailable;
        KeyQuestManager.OnKeyQuestObjectiveComplete += HandleKeyObjectiveComplete;
        KeyQuestManager.OnKeyQuestTurnedIn += HandleKeyTurnedIn;
        KeyQuestManager.OnKeyQuestCompleted += HandleKeyCompleted;
    }

    private static void HandleKeyAvailable(KeyQuestSO k)
    {
        if (k == null) return;
        RemoveKeyMarkers(k);

        switch (k.completionType)
        {
            case KeyQuestSO.KeyQuestCompletionType.TalkToGiver:
                if (k.questGiver)
                {
                    MapService.Remove(KeyGiverId(k));
                    MapService.AddSimple(KeyGiverId(k), k.questGiver.transform.position, MapIconType.QuestTarget);
                }
                break;

            case KeyQuestSO.KeyQuestCompletionType.KillTarget:
                if (k.targetEnemy)
                {
                    MapService.Remove(KeyTargetId(k));
                    MapService.AddSimple(KeyTargetId(k), k.targetEnemy.transform.position, MapIconType.QuestTarget);
                }
                else if (k.questGiver)
                {
                    MapService.Remove(KeyGiverId(k));
                    MapService.AddSimple(KeyGiverId(k), k.questGiver.transform.position, MapIconType.QuestTarget);
                }
                break;

            case KeyQuestSO.KeyQuestCompletionType.ReachLocation:
                if (k.targetLocation)
                {
                    MapService.Remove(KeyTargetId(k));
                    MapService.AddSimple(KeyTargetId(k), k.targetLocation.position, MapIconType.QuestTarget);
                }
                break;
        }
    }

    private static void HandleKeyObjectiveComplete(KeyQuestSO k)
    {
        if (k == null) return;

        MapService.SetActive(KeyTargetId(k), false);

        if (k.requiresTurnIn && k.questGiver)
        {
            MapService.Remove(KeyGiverId(k));
            MapService.AddSimple(KeyGiverId(k), k.questGiver.transform.position, MapIconType.QuestTarget);
        }
    }

    private static void HandleKeyTurnedIn(KeyQuestSO k) { RemoveKeyMarkers(k); }
    private static void HandleKeyCompleted(KeyQuestSO k) { RemoveKeyMarkers(k); }

    private static void RemoveKeyMarkers(KeyQuestSO k)
    {
        if (k == null) return;
        MapService.Remove(KeyTargetId(k));
        MapService.Remove(KeyGiverId(k));
    }

    private static string KeyTargetId(KeyQuestSO k) => $"key_{k.id}_target";
    private static string KeyGiverId(KeyQuestSO k) => $"key_{k.id}_giver";

    public static void NotifyPlayerCommittedCrime()
    {
        var km = KeyQuestManager.Instance;
        if (km && km.currentIndex == 0 && km.IsKeyAvailable && !km.IsKeyObjectiveComplete)
            return;

        ReassignPendingToBandit();
    }


    private static void ReassignPendingToBandit()
    {
        if (_pendingOffer == null) return;
        if (_pendingGiver && _pendingGiver.CompareTag("Bandit")) return;

        var w = WorldTags.Instance;
        var bandit = w ? w.GetQuestGiver(true) : null;
        if (_pendingGiver) QuestIndicatorCoordinator.Instance?.HideOn(_pendingGiver);

        if (bandit == null)
        {
            ClearPendingOffer();
            OnPendingChanged?.Invoke();
            return;
        }

        _pendingGiver = bandit;
        _pendingOffer.questGiver = bandit;

        QuestIndicatorCoordinator.Instance?.FocusOn(bandit, showQuestion: false);
        OnPendingChanged?.Invoke();
    }
}
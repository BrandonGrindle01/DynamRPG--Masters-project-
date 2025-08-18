using UnityEngine;

public class KeyQuestManager : MonoBehaviour
{
    public static KeyQuestManager Instance { get; private set; }

    [Header("Story Beats in Order")]
    public KeyQuestSO[] keyQuests;

    [Header("Runtime State")]
    public int currentIndex = 0;
    public int dynamicsLeftToGate = 0;

    public static event System.Action<KeyQuestSO, int> OnKeyQuestBridgeState;
    public static event System.Action<KeyQuestSO> OnKeyQuestAvailable;
    public static event System.Action<KeyQuestSO> OnKeyQuestCompleted;

    public static event System.Action<KeyQuestSO> OnKeyQuestObjectiveComplete;
    public static event System.Action<KeyQuestSO> OnKeyQuestTurnedIn;
    public static event System.Action<KeyQuestSO> OnAllKeyQuestsCompleted;

    private bool _booted;
    private bool keyObjectiveComplete;
    private GameObject _forceNextOfferGiver;

    public KeyQuestSO CurrentKey => (currentIndex >= 0 && currentIndex < keyQuests.Length) ? keyQuests[currentIndex] : null;
    public KeyQuestSO Current => CurrentKey;
    public int BridgesLeft => dynamicsLeftToGate;
    public bool IsKeyAvailable => dynamicsLeftToGate <= 0;
    public bool IsKeyObjectiveComplete => keyObjectiveComplete;

    private void Awake()
    {
        if (Instance && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private static bool SameNPC(GameObject a, GameObject b)
    {
        if (!a || !b) return false;
        return a.transform.root.gameObject == b.transform.root.gameObject;
    }

    private bool CanStartKey(KeyQuestSO k)
    {
        if (!k) return false;
        var w = WorldTags.Instance;
        if (w == null) return true;
        if (k.requiredWorldFlags != null)
        {
            foreach (var f in k.requiredWorldFlags)
            {
                bool mustHave = !string.IsNullOrEmpty(f) && !f.StartsWith("!");
                string tag = mustHave ? f : f.TrimStart('!');
                bool has = w.HasFlag(tag);
                if (mustHave && !has) return false;
                if (!mustHave && has) return false;
            }
        }
        return true;
    }

    private void ResolveKeySceneRefs(KeyQuestSO k)
    {
        if (!k) return;

        k.questGiver = string.IsNullOrWhiteSpace(k.questGiverRefId)
            ? null
            : SceneRef.Find(k.questGiverRefId)?.gameObject;

        k.targetEnemy = string.IsNullOrWhiteSpace(k.targetEnemyRefId)
            ? null
            : SceneRef.Find(k.targetEnemyRefId)?.gameObject;

        k.targetLocation = string.IsNullOrWhiteSpace(k.targetLocationRefId)
            ? null
            : SceneRef.Find(k.targetLocationRefId)?.transform;
    }

    public void StartKeyIfReady()
    {
        if (_booted) return;
        var k = CurrentKey;
        if (!k) return;
        if (!CanStartKey(k)) return;

        _booted = true;
        keyObjectiveComplete = false;

        if (k.setFlagsOnStart != null)
            foreach (var f in k.setFlagsOnStart) WorldTags.Instance?.SetFlag(f);

        if (currentIndex == 0)
        {
            dynamicsLeftToGate = 0;
            OnKeyQuestBridgeState?.Invoke(k, 0);
            OnKeyQuestAvailable?.Invoke(k);
            return;
        }

        dynamicsLeftToGate = Random.Range(k.minDynamicBetween, k.maxDynamicBetween + 1);
        if (dynamicsLeftToGate <= 0) dynamicsLeftToGate = 1;

        OnKeyQuestBridgeState?.Invoke(k, dynamicsLeftToGate);

        if (dynamicsLeftToGate <= 0)
        {
            OnKeyQuestAvailable?.Invoke(k);
        }
        else
        {
            GameObject forced = _forceNextOfferGiver;
            var w = WorldTags.Instance;
            if (!forced && w && w.IsPlayerCriminal())
                forced = w.GetQuestGiver(criminal: true);

            OfferNextBridge(forced);
            _forceNextOfferGiver = null;
        }
    }


    public void OfferNextBridge(GameObject forcedOfferGiver = null)
    {
        var k = CurrentKey;
        var ctx = (k != null) ? k.contextTag : null;

        var offer = DynamicQuestGenerator.Instance?.GenerateNextDynamicQuest(
            ctx,
            assign: false,
            forcedGiver: forcedOfferGiver
        );

        if (offer != null)
            QuestService.SetPendingOffer(offer);
    }

    public void NotifyDynamicQuestFinished()
    {
        if (dynamicsLeftToGate > 0)
        {
            dynamicsLeftToGate--;

            OnKeyQuestBridgeState?.Invoke(CurrentKey, dynamicsLeftToGate);

            if (dynamicsLeftToGate <= 0)
            {
                OnKeyQuestAvailable?.Invoke(CurrentKey);
            }
            else
            {
                OfferNextBridge();
            }
        }
    }

    public void CompleteCurrentKey()
    {
        var k = CurrentKey; if (!k) return;
        bool FinalQuest = (currentIndex == keyQuests.Length - 1);
        if (k.setFlagsOnComplete != null)
            foreach (var f in k.setFlagsOnComplete) WorldTags.Instance?.SetFlag(f);

        if (FinalQuest)
        {
            DialogueService.BeginOneLiner(
                "System",
                "Artefact complete, please move on to the questionnaire when ready.",
                null, 8f, true);

            OnAllKeyQuestsCompleted?.Invoke(k);
        }

        OnKeyQuestCompleted?.Invoke(k);
        currentIndex++;
        _booted = false;
        StartKeyIfReady();
    }

    private void SetKeyObjectiveComplete()
    {
        if (keyObjectiveComplete) return;
        keyObjectiveComplete = true;

        var k = CurrentKey;
        OnKeyQuestObjectiveComplete?.Invoke(k);

        if (k != null && !k.requiresTurnIn)
        {
            CompleteCurrentKey();
        }
    }

    public bool TryCompleteByTalkingTo(GameObject npc)
    {
        var k = CurrentKey;
        if (!k || !IsKeyAvailable) return false;
        if (k.completionType != KeyQuestSO.KeyQuestCompletionType.TalkToGiver) return false;
        if (k.questGiver && !SameNPC(k.questGiver, npc)) return false;

        SetKeyObjectiveComplete();
        return true;
    }


    public void NotifyEnemyKilled(GameObject enemy)
    {
        var k = CurrentKey; if (!k || !IsKeyAvailable) return;
        if (k.completionType != KeyQuestSO.KeyQuestCompletionType.KillTarget) return;
        if (!enemy) return;
        if (k.targetEnemy == null && !string.IsNullOrWhiteSpace(k.targetEnemyRefId))
        {
            var srNow = SceneRef.Find(k.targetEnemyRefId);
            if (srNow) k.targetEnemy = srNow.gameObject;
        }
        if (k.targetEnemy)
        {
            if (enemy.transform.root == k.targetEnemy.transform.root)
            {
                SetKeyObjectiveComplete();
            }
            return;
        }

        var enemySr = enemy.GetComponent<SceneRef>();
        if (enemySr && !string.IsNullOrWhiteSpace(k.targetEnemyRefId) && enemySr.id == k.targetEnemyRefId)
        {
            SetKeyObjectiveComplete();
            return;
        }

    }

    public void TryCompleteByReach(Vector3 pos)
    {
        var k = CurrentKey; if (!k || !IsKeyAvailable) return;
        if (k.completionType != KeyQuestSO.KeyQuestCompletionType.ReachLocation || !k.targetLocation) return;

        if (Vector3.Distance(pos, k.targetLocation.position) <= k.reachRadius)
            SetKeyObjectiveComplete();
    }

    public bool TryTurnInCurrentKey(GameObject npc)
    {
        var k = CurrentKey; if (!k || !IsKeyAvailable) return false;
        if (k.requiresTurnIn && !keyObjectiveComplete) return false;
        if (k.questGiver && !SameNPC(k.questGiver, npc)) return false;

        _forceNextOfferGiver = ResolveNextBridgeGiverFrom(k);
        OnKeyQuestTurnedIn?.Invoke(k);
        CompleteCurrentKey();
        return true;
    }
    private GameObject ResolveNextBridgeGiverFrom(KeyQuestSO k)
    {
        if (k == null) return null;

        switch (k.nextBridgeGiverRule)
        {
            case KeyQuestSO.NextBridgeGiverRule.UseThisKeyGiver:
                return k.questGiver;

            case KeyQuestSO.NextBridgeGiverRule.UseSceneRefId:
                if (!string.IsNullOrWhiteSpace(k.nextBridgeGiverRefId))
                {
                    var sr = SceneRef.Find(k.nextBridgeGiverRefId);
                    if (sr) return sr.gameObject;
                }
                break;
        }
        return null;
    }

    public void RequestDynamicTowardsNext()
    {
        var next = (currentIndex + 1 < keyQuests.Length) ? keyQuests[currentIndex + 1] : null;
        var ctx = next ? next.contextTag : null;
        DynamicQuestGenerator.Instance?.GenerateNextDynamicQuest(ctx, assign: true, forcedGiver: null);
    }
}
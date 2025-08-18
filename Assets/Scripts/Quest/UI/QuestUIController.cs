using UnityEngine;
using TMPro;
using System.Collections;

[DefaultExecutionOrder(1000)]
public class QuestUIController : MonoBehaviour
{
    [Header("UI Refs")]
    [SerializeField] private GameObject rootPanel;
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text descriptionText;
    [SerializeField] private TMP_Text progressText;
    [SerializeField] private TMP_Text timerText;

    [Header("Indicator Behaviour")]
    [Tooltip("If true, pin the beacon/icon to the key quest giver even before bridges are done.")]
    [SerializeField] private bool pinKeyGiverAlways = true;

    private KeyQuestSO currentKey;
    private int bridgesLeft;
    private bool keyAvailable;
    private bool showingFinalMessage = false;

    private Coroutine focusCo;

    private DynamicQuest current;

    private Coroutine timerCo;

    private void OnEnable()
    {
        QuestService.OnAssigned += HandleAssigned;
        QuestService.OnUpdated += HandleUpdated;
        QuestService.OnCompleted += HandleCompleted;
        QuestService.OnTurnedIn += HandleTurnedIn;
        QuestService.OnPendingChanged += HandlePendingChanged;

        KeyQuestManager.OnKeyQuestBridgeState += HandleKeyBridge;
        KeyQuestManager.OnKeyQuestAvailable += HandleKeyAvailable;
        KeyQuestManager.OnKeyQuestCompleted += HandleKeyCompleted;
        KeyQuestManager.OnAllKeyQuestsCompleted += HandleAllKeysComplete;

        var km = KeyQuestManager.Instance;
        if (km != null)
        {
            km.StartKeyIfReady();
            currentKey = km.Current;
            bridgesLeft = km.BridgesLeft;
            keyAvailable = km.IsKeyAvailable;
        }

        current = QuestService.GetCurrent();
        if (current == null && QuestService.Active != null && QuestService.Active.Count > 0)
            current = QuestService.Active[QuestService.Active.Count - 1];

        UpdateUI();
        if (rootPanel) rootPanel.SetActive(currentKey != null || current != null);
        if (current != null) StartTimerIfNeeded();
    }

    private void OnDisable()
    {
        QuestService.OnAssigned -= HandleAssigned;
        QuestService.OnUpdated -= HandleUpdated;
        QuestService.OnCompleted -= HandleCompleted;
        QuestService.OnTurnedIn -= HandleTurnedIn;
        QuestService.OnPendingChanged -= HandlePendingChanged;

        KeyQuestManager.OnKeyQuestBridgeState -= HandleKeyBridge;
        KeyQuestManager.OnKeyQuestAvailable -= HandleKeyAvailable;
        KeyQuestManager.OnKeyQuestCompleted -= HandleKeyCompleted;
        KeyQuestManager.OnAllKeyQuestsCompleted -= HandleAllKeysComplete;

        StopTimer();
        FocusIndicatorTo(null, false);
    }

    private void HandleKeyBridge(KeyQuestSO key, int left)
    {
        currentKey = key;
        bridgesLeft = left;
        keyAvailable = (left <= 0);
        if (current == null) ShowKeyUI();
    }

    private void HandleKeyAvailable(KeyQuestSO key)
    {
        currentKey = key;
        keyAvailable = true;
        if (current == null) ShowKeyUI();
    }

    private void HandleKeyCompleted(KeyQuestSO key)
    {
        if (showingFinalMessage) return;

        if (current == null && currentKey == key)
        {
            currentKey = null;
            bridgesLeft = 0;
            keyAvailable = false;
            if (rootPanel) rootPanel.SetActive(false);
            FocusIndicatorTo(null, false);
        }
    }

    private void HandleAllKeysComplete(KeyQuestSO key)
    {
        showingFinalMessage = true;

        current = null;
        currentKey = null;
        bridgesLeft = 0;
        keyAvailable = false;

        if (rootPanel) rootPanel.SetActive(true);
        if (titleText) titleText.text = "Artefact complete";
        if (descriptionText) descriptionText.text =
            "Please move on to the attached questionnaire or explore more if you please.";
        if (progressText) progressText.text = string.Empty;
        if (timerText) timerText.text = string.Empty;

        FocusIndicatorTo(null, false);
    }

    private void HandlePendingChanged()
    {
        if (current == null) UpdateUI();
    }

    private void HandleAssigned(DynamicQuest q)
    {
        current = q;
        if (rootPanel) rootPanel.SetActive(true);
        UpdateUI();
        StartTimerIfNeeded();
    }

    private void HandleUpdated(DynamicQuest q)
    {
        if (q != current) return;
        UpdateUI();
    }

    private void HandleCompleted(DynamicQuest q)
    {
        if (q != current) return;
        UpdateUI();
        StopTimer();
    }

    private void HandleTurnedIn(DynamicQuest q)
    {
        if (q != current) return;
        current = null;
        StopTimer();
        ShowKeyUI();
    }

    private void UpdateUI()
    {
        if (showingFinalMessage)
        {
            if (rootPanel && !rootPanel.activeSelf) rootPanel.SetActive(true);
            return;
        }

        var pending = QuestService.GetPending();
        if (current == null && pending != null)
        {
            ShowPendingOfferUI(pending);
            return;
        }

        if (current != null)
        {
            if (rootPanel) rootPanel.SetActive(true);
            if (titleText) titleText.text = current.questName;
            if (descriptionText) descriptionText.text = BuildDescription(current);
            if (progressText) progressText.text = BuildProgress(current);
            if (timerText) timerText.text = BuildTimer(current);

            if (current.IsComplete)
                FocusIndicatorTo(current.questGiver, true);
            else
                FocusIndicatorTo(null, false);

            return;
        }

        ShowKeyUI();
    }

    private void ShowKeyUI()
    {
        if (currentKey == null)
        {
            if (rootPanel) rootPanel.SetActive(false);
            FocusIndicatorTo(null, false);
            return;
        }

        if (rootPanel) rootPanel.SetActive(true);
        if (titleText) titleText.text = currentKey.title;

        string keyDesc = string.IsNullOrWhiteSpace(currentKey.description)
            ? "Main objective"
            : currentKey.description;

        if (descriptionText) descriptionText.text = keyDesc;
        if (progressText) progressText.text = keyAvailable
            ? "Objective available."
            : $"Complete {bridgesLeft} task(s) to proceed.";
        if (timerText) timerText.text = string.Empty;

        var giver = (pinKeyGiverAlways || keyAvailable) ? currentKey.questGiver : null;
        FocusIndicatorTo(giver, false);
    }

    private string GiverName(DynamicQuest q)
    {
        if (q != null && q.questGiver != null)
            return DialogueService.CleanName(q.questGiver.name);
        return "the quest giver";
    }

    private string BuildDescription(DynamicQuest q)
    {
        if (q.IsComplete)
            return $"Objective complete. Return to {GiverName(q)} to turn it in.";

        if (!string.IsNullOrEmpty(q.introText)) return q.introText;
        switch (q.type)
        {
            case QuestType.Kill:
                return q.targetObject ? $"Eliminate {DialogueService.CleanName(q.targetObject.name)}." : "Eliminate the threat.";
            case QuestType.Steal:
                return q.requireStealth ? $"Steal {(string.IsNullOrEmpty(q.targetItemName) ? "valuables" : q.targetItemName)} without being seen."
                                        : $"Steal {(string.IsNullOrEmpty(q.targetItemName) ? "valuables" : q.targetItemName)}.";
            case QuestType.Collect:
                return $"Collect {q.requiredCount} {(string.IsNullOrEmpty(q.targetItemName) ? "materials" : q.targetItemName)}.";
            case QuestType.Explore:
                return "Travel to the marked location.";
            case QuestType.Defend:
                return "Defend the target until the danger passes.";
            case QuestType.Deliver:
                return "Deliver the item to the destination.";
            default:
                return "Complete the objective.";
        }
    }

    private string BuildProgress(DynamicQuest q)
    {
        if (q.IsComplete)
            return $"Ready to turn in > {GiverName(q)}";

        int cur = Mathf.Clamp(q.currentCount, 0, q.requiredCount);
        switch (q.type)
        {
            case QuestType.Kill: return $"Targets: {cur}/{q.requiredCount}";
            case QuestType.Steal: return $"Stolen: {cur}/{q.requiredCount}";
            case QuestType.Collect: return $"Collected: {cur}/{q.requiredCount}";
            case QuestType.Explore: return cur >= q.requiredCount ? "Location reached." : "Reach the marker.";
            default: return $"Progress: {cur}/{q.requiredCount}";
        }
    }

    private string BuildTimer(DynamicQuest q)
    {
        if (q.timeLimit <= 0f || q.startedAt <= 0f) return string.Empty;
        float remaining = Mathf.Max(0f, q.timeLimit - (Time.time - q.startedAt));
        int m = Mathf.FloorToInt(remaining / 60f);
        int s = Mathf.FloorToInt(remaining % 60f);
        return $"{m:00}:{s:00}";
    }

    private void StartTimerIfNeeded()
    {
        if (timerText == null) return;
        if (current == null || current.timeLimit <= 0f) { timerText.text = string.Empty; return; }
        StopTimer();
        timerCo = StartCoroutine(TimerRoutine());
    }

    private void StopTimer()
    {
        if (timerCo != null) { StopCoroutine(timerCo); timerCo = null; }
        if (timerText) timerText.text = string.Empty;
    }

    private IEnumerator TimerRoutine()
    {
        while (current != null && current.status == QuestStatus.Active && current.timeLimit > 0f)
        {
            if (timerText) timerText.text = BuildTimer(current);
            yield return new WaitForSeconds(0.2f);
        }
    }

    private void FocusIndicatorTo(GameObject giver, bool showQuestion)
    {
        if (focusCo != null) { StopCoroutine(focusCo); focusCo = null; }
        focusCo = StartCoroutine(FocusIndicatorRoutine(giver, showQuestion));
    }

    private IEnumerator FocusIndicatorRoutine(GameObject giver, bool showQuestion)
    {
        float waited = 0f;
        while ((QuestIndicatorCoordinator.Instance == null ||
               (giver != null && !giver.activeInHierarchy)) && waited < 2f)
        {
            waited += Time.unscaledDeltaTime;
            yield return null;
        }

        var coord = QuestIndicatorCoordinator.Instance;
        if (coord == null) yield break;

        GameObject target = giver;
        if (target == null && current != null) target = current.questGiver;
        if (target == null && currentKey != null) target = currentKey.questGiver;

        if (target == null)
        {
            coord.ClearFocus();
            focusCo = null;
            yield break;
        }

        coord.FocusOn(target, showQuestion);
        focusCo = null;

        coord.FocusOn(target, showQuestion);
        focusCo = null;
    }

    private void ShowPendingOfferUI(DynamicQuest offer)
    {
        if (offer == null)
        {
            ShowKeyUI();
            return;
        }

        if (rootPanel) rootPanel.SetActive(true);

        string giverName = offer.questGiver ? DialogueService.CleanName(offer.questGiver.name) : "an NPC";
        if (titleText) titleText.text = $"{giverName} needs help";
        if (descriptionText) descriptionText.text = $"Head to {giverName} to find out what they need.";
        if (progressText) progressText.text = "";
        if (timerText) timerText.text = "";

        FocusIndicatorTo(offer.questGiver, showQuestion: false);
    }
}
using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;

public class QuestIndicatorCoordinator : MonoBehaviour
{
    public static QuestIndicatorCoordinator Instance { get; private set; }

    [Header("Default Prefabs to use when adding indicators at runtime")]
    public GameObject exclamationPrefab;
    public GameObject questionPrefab;

    [Header("Beacon Defaults")]
    public QuestBeaconParamsAsset beaconParamsAsset;
    public bool defaultSpawnBeacon = true;
    public float defaultBeamHeight = 20f;
    public float defaultBeamRadius = 0.05f;
    [Range(0.05f, 1f)] public float defaultBeamAlpha = 1f;

    private QuestGiverIndicator currentInd;
    private GameObject currentGiver;

    private void Awake()
    {
        if (Instance && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void Start()
    {
        StartCoroutine(DelayedStartupSync());

        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene s, LoadSceneMode m)
    {
        StartCoroutine(DelayedStartupSync());
    }

    private IEnumerator DelayedStartupSync()
    {
        yield return null;
        yield return null;
        TrySyncNow();
    }

    private void TrySyncNow()
    {
        if (HasActiveDynamic()) return;

        var km = KeyQuestManager.Instance;
        if (km == null) return;

        var key = km.CurrentKey;
        if (key == null) return;

        if (key.questGiver == null && !string.IsNullOrWhiteSpace(key.questGiverRefId))
        {
            var sr = SceneRef.Find(key.questGiverRefId);
            if (sr) key.questGiver = sr.gameObject;
        }

        if (key.questGiver != null)
            FocusOn(key.questGiver, showQuestion: false);
    }

    private void OnEnable()
    {
        QuestService.OnAssigned += HandleAssigned;
        QuestService.OnUpdated += HandleUpdated;
        QuestService.OnCompleted += HandleCompleted;
        QuestService.OnTurnedIn += HandleTurnedIn;

        KeyQuestManager.OnKeyQuestAvailable += HandleKeyAvailable;
        KeyQuestManager.OnKeyQuestBridgeState += HandleKeyBridgeState;
    }

    private void OnDisable()
    {
        QuestService.OnAssigned -= HandleAssigned;
        QuestService.OnUpdated -= HandleUpdated;
        QuestService.OnCompleted -= HandleCompleted;
        QuestService.OnTurnedIn -= HandleTurnedIn;

        KeyQuestManager.OnKeyQuestAvailable -= HandleKeyAvailable;
        KeyQuestManager.OnKeyQuestBridgeState -= HandleKeyBridgeState;
    }

    public void FocusOn(GameObject giver, bool showQuestion)
    {
        if (giver == currentGiver && currentInd != null)
        {
            if (showQuestion) currentInd.ShowQuestion(); else currentInd.ShowExclamation();
            return;
        }

        if (currentInd != null) currentInd.HideAll();

        currentInd = null;
        currentGiver = giver;
        if (giver == null) return;

        var ind = giver.GetComponent<QuestGiverIndicator>();
        if (!ind) ind = giver.AddComponent<QuestGiverIndicator>();

        if (!ind.exclamationPrefab && exclamationPrefab) ind.exclamationPrefab = exclamationPrefab;
        if (!ind.questionPrefab && questionPrefab) ind.questionPrefab = questionPrefab;

        ind.spawnBeacon = defaultSpawnBeacon;
        ind.beamHeight = defaultBeamHeight;
        ind.beamRadius = defaultBeamRadius;
        ind.beamAlpha = defaultBeamAlpha;
        ind.AssignBeaconParams(beaconParamsAsset);
        if (showQuestion) ind.ShowQuestion(); else ind.ShowExclamation();

        currentInd = ind;
    }

    public void HideOn(GameObject giver)
    {
        if (giver == null) return;
        var ind = giver.GetComponent<QuestGiverIndicator>();
        if (ind) ind.HideAll();

        if (giver == currentGiver) { currentInd = null; currentGiver = null; }
    }

    public void ClearFocus()
    {
        if (currentInd != null) currentInd.HideAll();
        currentInd = null;
        currentGiver = null;
    }


    private void HandleAssigned(DynamicQuest q)
    {
        if (q != null && q.questGiver != null) HideOn(q.questGiver);
    }

    private void HandleUpdated(DynamicQuest q)
    {
    }

    private void HandleCompleted(DynamicQuest q)
    {
        if (q != null && q.questGiver != null) FocusOn(q.questGiver, showQuestion: true);
    }

    private void HandleTurnedIn(DynamicQuest q)
    {

        if (q != null && q.questGiver != null) HideOn(q.questGiver);
    }

    private void HandleKeyAvailable(KeyQuestSO key)
    {
        if (HasActiveDynamic()) return;
        if (key != null && key.questGiver == null && !string.IsNullOrWhiteSpace(key.questGiverRefId))
        {
            var sr = SceneRef.Find(key.questGiverRefId);
            if (sr) key.questGiver = sr.gameObject;
        }
        if (key != null && key.questGiver != null)
            FocusOn(key.questGiver, showQuestion: false);
    }

    private void HandleKeyBridgeState(KeyQuestSO key, int bridgesLeft)
    {
        if (HasActiveDynamic()) return;

        if (bridgesLeft <= 0 && key != null)
        {
            if (key.questGiver == null && !string.IsNullOrWhiteSpace(key.questGiverRefId))
            {
                var sr = SceneRef.Find(key.questGiverRefId);
                if (sr) key.questGiver = sr.gameObject;
            }
            if (key.questGiver != null)
                FocusOn(key.questGiver, showQuestion: false);
        }
        else
        {
            ClearFocus();
        }
    }

    private static bool HasActiveDynamic()
    {
        var list = QuestService.Active;
        return list != null && list.Count > 0;
    }
}
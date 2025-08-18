using System.Collections.Generic;
using UnityEngine;

public class RuntimeDevConsole : MonoBehaviour
{
    // Last type pick + normalized weights used
    public static readonly Dictionary<QuestType, float> lastWeights = new();
    public static QuestType lastPick = QuestType.Explore;

    // Why a generation returned null / was redirected, etc.
    public static string lastSkipOrRedirect = "";

    // Last generated quest (preview or assigned)
    public static DynamicQuest lastGenerated;

    // Dialogue + key quest context
    public static string lastDialogueOwner = "";
    public static string lastDialogueNode = "";
    public static string lastDialogueAction = "";

    // Pending/current snapshots
    public static DynamicQuest lastPending => QuestService.GetPending();
    public static DynamicQuest lastCurrent => QuestService.GetCurrent();

    // Append last few log lines (including build)
    private static readonly Queue<string> _log = new Queue<string>();
    public static IEnumerable<string> recentLogLines => _log;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void HookLogs()
    {
        Application.logMessageReceived -= HandleLog;
        Application.logMessageReceived += HandleLog;
    }

    private static void HandleLog(string condition, string stackTrace, LogType type)
    {
        if (_log.Count > 30) _log.Dequeue();
        _log.Enqueue($"[{type}] {condition}");
    }

    public static void SetWeights(Dictionary<QuestType, float> normalized)
    {
        lastWeights.Clear();
        foreach (var kv in normalized) lastWeights[kv.Key] = kv.Value;
    }
}

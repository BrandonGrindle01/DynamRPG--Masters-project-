using System.Linq;
using UnityEngine;

public class QuestDebugHUD : MonoBehaviour
{
    public KeyCode toggleKey = KeyCode.F9;
    public bool visible = false;

    private Rect _rect = new Rect(10, 10, 440, 520);
    private Vector2 _scroll;

    private void Update()
    {
        if (Input.GetKeyDown(toggleKey)) visible = !visible;
    }

    private GUIStyle _hdr, _mono;
    private void EnsureStyles()
    {
        if (_hdr == null)
        {
            _hdr = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold };
            _mono = new GUIStyle(GUI.skin.label) { font = Resources.GetBuiltinResource<Font>("Lucida Console.ttf"), fontSize = 11 };
        }
    }

    private string Name(GameObject go) => go ? go.name.Replace("(Clone)", "") : "—";

    private void OnGUI()
    {
        if (!visible) return;
        EnsureStyles();

        _rect = GUILayout.Window(920131, _rect, DrawWindow, "Quest Debug");
    }

    private void DrawWindow(int id)
    {
        _scroll = GUILayout.BeginScrollView(_scroll, GUILayout.Height(_rect.height - 32));

        var pend = QuestDiagnostics.lastPending;
        var curr = QuestDiagnostics.lastCurrent;

        GUILayout.Label("Dialogue", _hdr);
        GUILayout.Label($"Owner: {QuestDiagnostics.lastDialogueOwner}");
        GUILayout.Label($"Node:  {QuestDiagnostics.lastDialogueNode}");
        GUILayout.Label($"Action:{QuestDiagnostics.lastDialogueAction}");
        GUILayout.Space(6);

        GUILayout.Label("Key Quest", _hdr);
        var km = KeyQuestManager.Instance;
        if (km && km.CurrentKey)
        {
            GUILayout.Label($"Current: {km.CurrentKey.title}");
            GUILayout.Label($"Available: {km.IsKeyAvailable}  ObjectiveDone: {km.IsKeyObjectiveComplete}");
            GUILayout.Label($"BridgesLeft: {km.BridgesLeft}");
        }
        else GUILayout.Label("None");

        GUILayout.Space(6);
        GUILayout.Label("Dynamic (Pending / Current)", _hdr);
        if (pend != null)
        {
            GUILayout.Label($"Pending: {pend.type} '{pend.questName}' @ {pend.targetPosition}");
            GUILayout.Label($"Giver: {Name(pend.questGiver)}  ReqCount:{pend.requiredCount}");
        }
        else GUILayout.Label("Pending: —");

        if (curr != null)
        {
            GUILayout.Label($"Current: {curr.type} '{curr.questName}' [{curr.status}]");
            GUILayout.Label($"Giver: {Name(curr.questGiver)}  Pos:{curr.targetPosition}  Radius:{curr.areaRadius:0.0}");
        }
        else GUILayout.Label("Current: —");

        GUILayout.Space(6);
        GUILayout.Label("Picker", _hdr);
        if (QuestDiagnostics.lastWeights.Count > 0)
        {
            foreach (var kv in QuestDiagnostics.lastWeights.OrderByDescending(k => k.Value))
                GUILayout.Label($"{kv.Key,-8} : {kv.Value:0.00}", _mono);
            GUILayout.Label($"Pick   : {QuestDiagnostics.lastPick}", _mono);
        }
        else GUILayout.Label("No weight data yet.");

        if (!string.IsNullOrEmpty(QuestDiagnostics.lastSkipOrRedirect))
        {
            GUILayout.Space(4);
            GUILayout.Label($"Note: {QuestDiagnostics.lastSkipOrRedirect}");
        }

        GUILayout.Space(6);
        GUILayout.Label("Recent Log (last 30)", _hdr);
        foreach (var line in QuestDiagnostics.recentLogLines)
            GUILayout.Label(line, _mono);

        GUILayout.EndScrollView();
        GUI.DragWindow();
    }
}
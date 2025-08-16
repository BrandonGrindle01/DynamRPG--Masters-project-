using UnityEngine;

public class QuestKillReporter : MonoBehaviour
{
    private void OnDestroy()
    {
        if (Application.isPlaying)
            QuestService.ReportKill(gameObject);
    }
}


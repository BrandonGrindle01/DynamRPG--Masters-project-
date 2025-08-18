using System.Collections;
using UnityEngine;

[DefaultExecutionOrder(9999)]
public class QuestBootstrap : MonoBehaviour
{
    private IEnumerator Start()
    {
        yield return null;

        yield return new WaitUntil(() =>
            KeyQuestManager.Instance != null &&
            DynamicQuestGenerator.Instance != null &&
            WorldTags.Instance != null);

        KeyQuestRuntimeBinder.BindAll(KeyQuestManager.Instance.keyQuests);

        KeyQuestManager.Instance.StartKeyIfReady();
    }
}
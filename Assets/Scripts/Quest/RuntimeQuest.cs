using UnityEngine;

public class KeyQuestInstance
{
    public KeyQuestSO data;
    public int currentProgress;
    public bool isCompleted;

    public KeyQuestInstance(KeyQuestSO questData)
    {
        data = questData;
        currentProgress = 0;
        isCompleted = false;
    }

    public void IncrementProgress()
    {
        if (isCompleted) return;

        currentProgress++;
        if (currentProgress >= data.targetAmount)
        {
            isCompleted = true;
            Debug.Log($"[KeyQuest] Completed: {data.questName}");
            KeyAuthorQuest.Instance.StartNextKeyQuest();
        }
    }


}

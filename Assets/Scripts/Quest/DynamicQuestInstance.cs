using UnityEngine;
using System;

public class DynamicQuestInstance : MonoBehaviour
{
    public DynamicQuestTemplateSO template;

    [Header("Runtime Quest Data")]
    public string questName;
    [TextArea] public string description;
    public Transform questLocation;
    public GameObject questGiver;
    public GameObject currentTarget;

    public DynamicQuestType questType;

    public int currentProgress = 0;
    public bool isCompleted = false;

    public event Action<DynamicQuestInstance> OnQuestCompleted;

    public void InitializeFromTemplate(DynamicQuestTemplateSO questTemplate, Transform location, GameObject giver)
    {
        template = questTemplate;
        questName = template.templateName;
        description = template.templateDescription;
        questLocation = location;
        questGiver = giver;
        currentProgress = 0;
        isCompleted = false;

        questType = template.questType;

        if (template.targetPrefab != null)
        {
            currentTarget = Instantiate(template.targetPrefab, questLocation);
        }
    }

    public void RegisterProgress()
    {
        currentProgress++;
        if (currentProgress >= template.baseTargetAmount)
        {
            CompleteQuest();
        }
    }

    private void CompleteQuest()
    {
        isCompleted = true;
        Debug.Log("[DynamicQuest] Completed: " + questName);
        OnQuestCompleted?.Invoke(this);
    }
}


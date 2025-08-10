using System.Collections.Generic;
using UnityEngine;

public class DynamicQuestGenerator : MonoBehaviour
{
    public static DynamicQuestGenerator Instance;

    [Header("Quest Templates")]
    public List<DynamicQuestTemplateSO> questTemplates;

    [Header("Current Quest Chain")]
    public int dynamicQuestsPerKey = 3;
    private int dynamicQuestIndex = 0;
    private KeyQuestInstance previousKeyQuest;
    private KeyQuestInstance nextKeyQuest;
    private DynamicQuestInstance currentDynamicQuest;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    public void StartDynamicQuestChain(KeyQuestInstance lastKey, KeyQuestInstance upcomingKey)
    {
        dynamicQuestIndex = 0;
        previousKeyQuest = lastKey;
        nextKeyQuest = upcomingKey;

        GenerateNextDynamicQuest();
    }

    public void GenerateNextDynamicQuest()
    {
        if (dynamicQuestIndex >= dynamicQuestsPerKey)
        {
            Debug.Log("Dynamic quest chain complete. Proceed to next key quest.");
            return;
        }

        DynamicQuestTemplateSO selectedTemplate = SelectTemplateBasedOnPlayer();

        if (selectedTemplate == null)
        {
            Debug.LogWarning("No suitable quest template found.");
            return;
        }

        // Build instance
        DynamicQuestInstance newQuest = new DynamicQuestInstance();
        newQuest.template = selectedTemplate;
        newQuest.questName = selectedTemplate.templateName + " (Instance)";
        newQuest.description = selectedTemplate.templateDescription;

        // Special handling for first and last in chain
        if (dynamicQuestIndex == 0 && previousKeyQuest != null)
        {
            newQuest.description += $"\nFollow-up to: {previousKeyQuest.data.questName}";
        }
        else if (dynamicQuestIndex == dynamicQuestsPerKey - 1 && nextKeyQuest != null)
        {
            newQuest.description += $"\nLeads into: {nextKeyQuest.data.questName}";
        }

        AssignQuestGiver(ref newQuest);
        AssignQuestLocation(ref newQuest);

        currentDynamicQuest = newQuest;
        dynamicQuestIndex++;

        Debug.Log($"[DynamicQuest] Generated: {newQuest.questName}");
        PlayerStatsTracker.Instance.ResetQuestTimer();
    }

    private DynamicQuestTemplateSO SelectTemplateBasedOnPlayer()
    {
        var stats = PlayerStatsTracker.Instance;
        var filteredTemplates = new List<DynamicQuestTemplateSO>();

        foreach (var template in questTemplates)
        {
            if (template.restrictToCriminals && !WorldTags.Instance.IsPlayerCriminal()) continue;
            if (template.mustAvoidTowns && WorldTags.Instance.IsPlayerCriminal()) continue;

            // Match style
            switch (template.questType)
            {
                case DynamicQuestType.Explore:
                    if (stats.distanceTraveled > 500f || stats.timeSinceLastQuest > 60f)
                        filteredTemplates.Add(template);
                    break;
                case DynamicQuestType.Steal:
                    if (stats.crimesCommitted > 2)
                        filteredTemplates.Add(template);
                    break;
                case DynamicQuestType.Kill:
                    if (stats.enemiesKilled > 5)
                        filteredTemplates.Add(template);
                    break;
                case DynamicQuestType.Collect:
                    if (stats.secretsFound > 0 || stats.fightsAvoided > 3)
                        filteredTemplates.Add(template);
                    break;
                default:
                    filteredTemplates.Add(template);
                    break;
            }
        }

        if (filteredTemplates.Count == 0) return null;
        return filteredTemplates[Random.Range(0, filteredTemplates.Count)];
    }

    private void AssignQuestGiver(ref DynamicQuestInstance quest)
    {
        if (WorldTags.Instance.IsPlayerCriminal())
        {
            quest.questGiver = WorldTags.Instance.GetQuestGiver(true);
        }
        else
        {
            quest.questGiver = WorldTags.Instance.GetQuestGiver();
        }
    }

    private void AssignQuestLocation(ref DynamicQuestInstance quest)
    {
        switch (quest.questType)
        {
            case DynamicQuestType.Explore:
                quest.questLocation = WorldTags.Instance.GetRemoteLocation();
                break;
            case DynamicQuestType.Steal:
                quest.questLocation = WorldTags.Instance.GetTownLocation();
                break;
            case DynamicQuestType.Kill:
                quest.questLocation = WorldTags.Instance.GetBanditCamp();
                break;
            case DynamicQuestType.Collect:
                quest.questLocation = WorldTags.Instance.GetSecretLocation();
                break;
            default:
                quest.questLocation = null;
                break;
        }
    }

    public DynamicQuestInstance GetCurrentQuest() => currentDynamicQuest;
}

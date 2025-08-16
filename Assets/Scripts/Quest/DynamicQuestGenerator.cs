using System.Collections.Generic;
using UnityEngine;

public class DynamicQuestGenerator : MonoBehaviour
{
    private enum Persona { Explorer, Fighter, Criminal, Collector, Neutral }

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

    private Persona GetPersona()
    {
        var s = PlayerStatsTracker.Instance;
        if (WorldTags.Instance.IsPlayerCriminal()) return Persona.Criminal;
        if (s.enemiesKilled > 5) return Persona.Fighter;
        if (s.distanceTraveled > 500f || s.timeSinceLastQuest > 60f) return Persona.Explorer;
        if (s.secretsFound > 0 || s.fightsAvoided > 3) return Persona.Collector;
        return Persona.Neutral;
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

        bool isFinal = (dynamicQuestIndex == dynamicQuestsPerKey - 1);

        DynamicQuestTemplateSO selectedTemplate = isFinal
            ? SelectBridgeTemplate()
            : SelectTemplateBasedOnPlayer();

        if (!selectedTemplate)
        {
            Debug.LogWarning("No suitable quest template found.");
            return;
        }

        GameObject giver = PickGiverForTemplate(selectedTemplate);
        Transform loc = PickLocationForTemplate(selectedTemplate);
        if (!loc)
        {
            var anchorGO = new GameObject("QuestAnchor");
            anchorGO.transform.position = StarterAssets.FirstPersonController.instance.transform.position + new Vector3(0, 0, 25f);
            loc = anchorGO.transform;
        }

        var host = new GameObject($"DQ_{selectedTemplate.templateName}_{dynamicQuestIndex + 1}");
        var newQuest = host.AddComponent<DynamicQuestInstance>();

        newQuest.InitializeFromTemplate(selectedTemplate, loc, giver);

        if (dynamicQuestIndex == 0 && previousKeyQuest != null)
        {
            newQuest.description += $"\nFollow-up to: {previousKeyQuest.data.questName}";
        }
        else if (isFinal && nextKeyQuest != null)
        {
            newQuest.description += $"\nLeads into: {nextKeyQuest.data.questName}";
        }

        if ((selectedTemplate.questType == DynamicQuestType.Kill ||
             selectedTemplate.questType == DynamicQuestType.Assassinate) && newQuest.currentTarget)
        {
            if (!newQuest.currentTarget.GetComponent<QuestKillReporter>())
                newQuest.currentTarget.AddComponent<QuestKillReporter>();
        }
        if (selectedTemplate.questType == DynamicQuestType.Explore && newQuest.questLocation)
        {
            var trigGO = new GameObject($"ExploreTrigger_{newQuest.questName}");
            trigGO.transform.position = newQuest.questLocation.position;
            trigGO.AddComponent<QuestExploreTrigger>().radius = 8f;
        }

        currentDynamicQuest = newQuest;
        dynamicQuestIndex++;

        QuestService.AssignDynamic(newQuest);

        Debug.Log($"[DynamicQuest] Generated: {newQuest.questName}");
        PlayerStatsTracker.Instance.ResetQuestTimer();
    }

    private GameObject PickGiverForTemplate(DynamicQuestTemplateSO t)
    {
        bool criminal = WorldTags.Instance.IsPlayerCriminal();

        if (!string.IsNullOrEmpty(t.requiredGiverTag))
        {
            var match = WorldTags.Instance.potentialQuestGivers
                .FindAll(g => g && g.CompareTag(t.requiredGiverTag));
            if (match.Count > 0) return match[Random.Range(0, match.Count)];
        }

        return WorldTags.Instance.GetQuestGiver(criminal) ?? WorldTags.Instance.GetQuestGiver(!criminal);
    }

    private Transform PickLocationForTemplate(DynamicQuestTemplateSO t)
    {
        Transform picked = null;

        if (t.requiresRemoteLocation) picked = WorldTags.Instance.GetRemoteLocation();
        else if (!string.IsNullOrEmpty(t.requiredWorldTag))
        {
            switch (t.requiredWorldTag.ToLower())
            {
                case "town": picked = WorldTags.Instance.GetTownLocation(); break;
                case "remote": picked = WorldTags.Instance.GetRemoteLocation(); break;
                case "secret": picked = WorldTags.Instance.GetSecretLocation(); break;
                case "bandit": picked = WorldTags.Instance.GetBanditCamp(); break;
            }
        }

        if (!picked) picked = WorldTags.Instance.GetTownLocation()
                     ?? WorldTags.Instance.GetRemoteLocation()
                     ?? WorldTags.Instance.GetBanditCamp()
                     ?? WorldTags.Instance.GetSecretLocation();

        return picked;
    }

    private DynamicQuestTemplateSO SelectBridgeTemplate()
    {
        var persona = GetPersona();
        bool criminal = WorldTags.Instance.IsPlayerCriminal();
        string nextName = (nextKeyQuest != null) ? nextKeyQuest.data.questName : "";

        foreach (var t in questTemplates)
        {
            if (criminal && !t.restrictToCriminals) continue;
            if (t.mustAvoidTowns && !criminal) continue;

            //bridging logic
            if (!string.IsNullOrEmpty(nextName) && nextName.ToLower().Contains("mayor"))
            {
                if (criminal && t.templateName.Contains("Steal Mayor Reserve")) return t;
                if (!criminal && persona == Persona.Explorer && t.templateName.Contains("Find Fine Wine")) return t;
                if (!criminal && persona == Persona.Fighter && t.templateName.Contains("Defend Carriage")) return t;
            }
        }

        return SelectTemplateBasedOnPlayer();
    }
    private DynamicQuestTemplateSO SelectTemplateBasedOnPlayer()
    {
        var persona = GetPersona();
        bool criminal = WorldTags.Instance.IsPlayerCriminal();

        var candidates = new List<DynamicQuestTemplateSO>();

        foreach (var t in questTemplates)
        {
            if (criminal && t.mustAvoidTowns) continue;
            if (!criminal && t.restrictToCriminals) continue;

            bool ok = true;
            switch (t.personalityRequirement)
            {
                case QuestPersonalityTag.Aggressive: ok = (persona == Persona.Fighter); break;
                case QuestPersonalityTag.Stealthy: ok = (persona == Persona.Criminal); break;
                case QuestPersonalityTag.Explorer: ok = (persona == Persona.Explorer); break;
                case QuestPersonalityTag.Criminal: ok = (persona == Persona.Criminal); break;
                case QuestPersonalityTag.Neutral: ok = true; break;
            }
            if (!ok) continue;

            candidates.Add(t);
        }

        if (candidates.Count == 0) return null;
        return candidates[Random.Range(0, candidates.Count)];
    }
    public DynamicQuestInstance GetCurrentQuest() => currentDynamicQuest;
}

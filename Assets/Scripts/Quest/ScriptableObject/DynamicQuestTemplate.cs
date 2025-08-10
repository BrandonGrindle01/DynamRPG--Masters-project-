using UnityEngine;

public enum DynamicQuestType { Kill, Steal, Explore, Escort, Collect, Assassinate, Defend, Retrieve }
public enum QuestPersonalityTag { Aggressive, Stealthy, Explorer, Criminal, Neutral }

[CreateAssetMenu(fileName = "NewDynamicQuestTemplate", menuName = "Quests/Dynamic Quest Template")]
public class DynamicQuestTemplateSO : ScriptableObject
{
    [Header("Template Details")]
    public string templateName;
    [TextArea] public string templateDescription;

    public DynamicQuestType questType;
    public QuestPersonalityTag personalityRequirement;

    [Header("Access Restrictions")]
    public bool restrictToCriminals = false;
    public bool mustAvoidTowns = false;

    [Header("Required Prefabs & Assets")]
    public GameObject targetPrefab;             
    public GameObject questGiverPrefab;         

    [Header("Location & Context")]
    public string requiredWorldTag;             
    public bool requiresRemoteLocation;

    [Header("Logic Parameters")]
    public int baseTargetAmount = 1;
    public float difficultyWeight = 1.0f;      

    [Header("Reward Settings")]
    public int goldReward = 50;
    public ItemData[] possibleItemRewards;
}


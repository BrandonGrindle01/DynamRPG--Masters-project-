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
    public ItemData requiredItem;

    [Header("Location & Context")]
    public string requiredWorldTag;             
    public bool requiresRemoteLocation;

    [Header("Logic Parameters")]
    public int baseTargetAmount = 1;
    public float difficultyWeight = 1.0f;      

    [Header("Reward Settings")]
    public int goldReward = 50;
    public ItemData[] possibleItemRewards;

    [Header("Placement Rules")]
    public float minDistanceFromPlayer = 40f;
    public float maxDistanceFromPlayer = 300f;
    public string requiredGiverTag;

    [Header("Text Tokens")]
    public string titleFormat = "{verb} {target} for {giver}";
    public string descriptionFormat = "{flavor}\nLocation: {placeName}";
    public string[] flavorLines;
    public string verb = "Retrieve";
    public string targetNoun = "Supplies";
}


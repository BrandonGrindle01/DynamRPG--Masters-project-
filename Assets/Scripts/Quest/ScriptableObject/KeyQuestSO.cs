using UnityEngine;

public enum QuestType { Kill, Collect, Explore, Steal }

[CreateAssetMenu(fileName = "NewKeyQuest", menuName = "Quests/Key Quest")]
public class KeyQuestSO : ScriptableObject
{
    [Header("Quest Name")]
    public string questName;
    [TextArea] public string description;

    [Header("Quest Logic")]
    public QuestType questType;
    [System.NonSerialized] public QuestLocation runtimeLocation;
    public int targetAmount = 1;
    public GameObject[] targets;
    public bool requiresAllTargetsDead = false;

    public ItemData requiredItem;

    [Header("Quest giver")]
    [System.NonSerialized] public GameObject questGiver;
    public Sprite questIcon;

    [Header("Reward Settings")]
    public int goldReward = 50;
    public ItemData[] possibleItemRewards;
}

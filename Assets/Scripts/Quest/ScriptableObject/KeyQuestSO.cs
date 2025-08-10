using UnityEngine;

public enum QuestType { Kill, Collect, Explore, Steal }

[CreateAssetMenu(fileName = "NewKeyQuest", menuName = "Quests/Key Quest")]
public class KeyQuestSO : ScriptableObject
{
    public string questName;
    [TextArea] public string description;

    public QuestType questType;
    public Vector3 questLocation;

    public int targetAmount = 1; 
    public GameObject[] targets; 
    public bool requiresAllTargetsDead = false;

    public GameObject questGiver; 
    public Sprite questIcon;

    public ItemData requiredItem;
}

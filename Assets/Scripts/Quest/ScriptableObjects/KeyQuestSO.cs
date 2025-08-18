using UnityEngine;

[CreateAssetMenu(menuName = "Quests/Key Quest")]
public class KeyQuestSO : ScriptableObject
{
    public string id = "meet-guard";
    public string title = "Meet the town guard";
    [TextArea] public string description;

    [Header("Prereqs & Effects")]
    public string[] requiredWorldFlags;
    public string[] setFlagsOnStart;
    public string[] setFlagsOnComplete;

    [Header("Bridge settings")]
    public int minDynamicBetween = 1;
    public int maxDynamicBetween = 2;

    [Header("Optional quest giver for this key quest")]
    public string questGiverRefId;
    [HideInInspector] public GameObject questGiver;

    [Header("Context for generator")]
    public string contextTag;

    public enum KeyQuestCompletionType { TalkToGiver, KillTarget, ReachLocation }

    [Header("Completion settings")]
    public KeyQuestCompletionType completionType = KeyQuestCompletionType.TalkToGiver;

    [Tooltip("If KillTarget, you can leave null to accept any 'boss' kill reported.")]
    public string targetEnemyRefId;
    [HideInInspector] public GameObject targetEnemy;

    [Tooltip("If ReachLocation, player must get within reachRadius of this.")]
    public string targetLocationRefId;
    [HideInInspector] public Transform targetLocation;

    public float reachRadius = 3f;

    [Tooltip("Key quests require turning in at the quest giver to advance.")]
    public bool requiresTurnIn = true;

    public enum NextBridgeGiverRule
    {
        Default,
        UseThisKeyGiver,
        UseSceneRefId
    }

    [Header("After this key quest is turned in...")]
    public NextBridgeGiverRule nextBridgeGiverRule = NextBridgeGiverRule.Default;

    [Tooltip("Used only when nextBridgeGiverRule = UseSceneRefId ")]
    public string nextBridgeGiverRefId;
}
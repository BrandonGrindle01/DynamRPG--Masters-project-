// KeyQuestSceneBindings.cs
using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class KeyQuestBinding
{
    public KeyQuestSO quest;
    public QuestLocation location;
    public GameObject questgiver;
}

public class KeyQuestBindings : MonoBehaviour
{
    public List<KeyQuestBinding> bindings = new();

    private void Awake()
    {
        foreach (var b in bindings)
        {
            if (b.quest)
            {
                b.quest.runtimeLocation = b.location;
                b.quest.questGiver = b.questgiver;
            }
        }
    }
}
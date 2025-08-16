using System;
using System.Collections.Generic;
using UnityEngine;

public enum DialogueAction { None, OpenShop, OfferQuest, Close, AcceptQuest, TurnInQuest }

[Serializable]
public class DialogueChoice
{
    public string label;
    public string nextNodeId;
    public DialogueAction action = DialogueAction.None;
}

[Serializable]
public class DialogueNode
{
    public string id = "start";
    [TextArea] public string line;
    public List<DialogueChoice> choices = new List<DialogueChoice>();
}

[CreateAssetMenu(fileName = "Dialogue", menuName = "Dialogue/Definition")]
public class DialogueDefinition : ScriptableObject
{
    public string npcName = "NPC";
    public string startNodeId = "start";
    public List<DialogueNode> nodes = new List<DialogueNode>();

    public DialogueNode FindNode(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;
        for (int i = 0; i < nodes.Count; i++)
            if (nodes[i] != null && nodes[i].id == id) return nodes[i];
        return null;
    }
}
using System;
using System.Collections.Generic;
using UnityEngine;

public enum DialogueAction
{
    None,
    OpenShop,
    OfferQuest,
    AcceptQuest,
    TurnInQuest,
    ReportKeyTalkedTo,
    TurnInKey,
    HelpKeyAndOffer,
    Close
}

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

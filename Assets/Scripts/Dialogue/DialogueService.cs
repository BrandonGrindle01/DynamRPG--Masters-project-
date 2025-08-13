using System;
using UnityEngine;

public static class DialogueService
{
    public static event Action<DialogueDefinition, DialogueNode> OnOpen;
    public static event Action<DialogueDefinition, DialogueNode> OnAdvance;
    public static event Action OnClose;

    public static DialogueDefinition CurrentDef { get; private set; }
    public static DialogueNode CurrentNode { get; private set; }
    public static DialogueComponent CurrentOwner { get; private set; }

    public static void Begin(DialogueDefinition def, DialogueComponent owner = null, string startId = null)
    {
        bool wasActive = CurrentDef != null;

        CurrentDef = def;
        CurrentOwner = owner;
        CurrentNode = def?.FindNode(startId ?? def?.startNodeId);

        if (CurrentDef == null || CurrentNode == null)
        {
            End();
            return;
        }

        if (wasActive)
            OnAdvance?.Invoke(CurrentDef, CurrentNode);
        else
            OnOpen?.Invoke(CurrentDef, CurrentNode);
    }

    public static void Choose(DialogueChoice choice)
    {
        if (CurrentDef == null || CurrentNode == null || choice == null) return;

        switch (choice.action)
        {
            case DialogueAction.OpenShop:
                OnClose?.Invoke();
                if (CurrentOwner && !CurrentOwner.TryOpenShop())
                {
                    BeginOneLiner(CurrentOwner.NpcDisplayName, CurrentOwner.RefusalTextOrDefault(), CurrentOwner);
                    return;
                }
                CurrentDef = null;
                CurrentNode = null;
                CurrentOwner = null;
                return;

            case DialogueAction.OfferQuest:
                CurrentOwner?.OfferQuest();
                return;

            case DialogueAction.Close:
                End();
                return;
        }

        if (string.IsNullOrWhiteSpace(choice.nextNodeId)) { End(); return; }

        var next = CurrentDef.FindNode(choice.nextNodeId);
        if (next == null) { End(); return; }

        CurrentNode = next;
        OnAdvance?.Invoke(CurrentDef, CurrentNode);
    }

    public static void End()
    {
        CurrentDef = null;
        CurrentNode = null;
        CurrentOwner = null;
        OnClose?.Invoke();
    }

    public static void BeginOneLiner(string npcName, string line, DialogueComponent owner = null)
    {
        var tmp = ScriptableObject.CreateInstance<DialogueDefinition>();
        tmp.npcName = npcName;
        tmp.startNodeId = "start";
        tmp.nodes.Add(new DialogueNode
        {
            id = "start",
            line = line,
            choices = new System.Collections.Generic.List<DialogueChoice> {
                new DialogueChoice { label = "OK", action = DialogueAction.Close }
            }
        });
        Begin(tmp, owner, "start");
    }
}
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

    public static float AutoCloseAt { get; private set; } = -1f;
    public static bool IsAutoClosing => AutoCloseAt > 0f;

    public static void Begin(DialogueDefinition def, DialogueComponent owner = null, string startId = null, float autoCloseSeconds = -1f)
    {
        bool wasActive = CurrentDef != null;

        CurrentDef = def;
        CurrentOwner = owner;
        CurrentNode = def?.FindNode(startId ?? def?.startNodeId);

        if (autoCloseSeconds >= 0f)
            AutoCloseAt = Time.unscaledTime + autoCloseSeconds;
        else
            AutoCloseAt = -1f;

        if (CurrentDef == null || CurrentNode == null)
        {
            End();
            return;
        }

        if (wasActive) OnAdvance?.Invoke(CurrentDef, CurrentNode);
        else OnOpen?.Invoke(CurrentDef, CurrentNode);
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
                    BeginOneLiner(CurrentOwner.NpcDisplayName, CurrentOwner.RefusalTextOrDefault(), CurrentOwner, 3f, true);
                }
                CurrentDef = null; 
                CurrentNode = null; 
                CurrentOwner = null;
                return;

            case DialogueAction.OfferQuest:
                CurrentOwner?.OfferQuest();
                return;
            case DialogueAction.AcceptQuest:
                DialogueService.OnClose?.Invoke();
                CurrentOwner?.OfferQuest();
                CurrentDef = null; CurrentNode = null; CurrentOwner = null;
                return;

            case DialogueAction.TurnInQuest:
                DialogueService.OnClose?.Invoke();
                CurrentDef = null; CurrentNode = null; CurrentOwner = null;
                return;
            case DialogueAction.Close:
                End();
                return;
        }

        if (string.IsNullOrWhiteSpace(choice.nextNodeId)) { End(); return; }

        var next = CurrentDef.FindNode(choice.nextNodeId);
        if (next == null) { End(); return; }

        CurrentNode = next;
        AutoCloseAt = -1f;
        OnAdvance?.Invoke(CurrentDef, CurrentNode);
    }

    public static void End()
    {
        CurrentDef = null;
        CurrentNode = null;
        CurrentOwner = null;
        AutoCloseAt = -1f;
        OnClose?.Invoke();
    }

    public static void BeginOneLiner(string npcName, string line, DialogueComponent owner = null, float autoCloseSeconds = -1f, bool noButton = false)
    {
        var tmp = ScriptableObject.CreateInstance<DialogueDefinition>();
        tmp.npcName = npcName;
        tmp.startNodeId = "start";

        var node = new DialogueNode { id = "start", line = line };

        if (!noButton && autoCloseSeconds < 0f)
        {
            node.choices.Add(new DialogueChoice { label = "OK", action = DialogueAction.Close });
        }

        tmp.nodes.Add(node);
        Begin(tmp, owner, "start", autoCloseSeconds);
    }

    public static string CleanName(string raw) => string.IsNullOrEmpty(raw) ? "NPC" : raw.Replace("(Clone)", "").Trim();
}
using System.Linq;
using UnityEngine;

public static class DialogueQuestBuilder
{
    private const string N_OFFER = "auto_offer";
    private const string N_ACCEPT = "auto_accept";
    private const string N_TURNIN = "auto_turnin";
    private const string N_KEYTALK = "auto_key_talk";
    private const string N_KEYTURN = "auto_key_turnin";

    private const string L_OFFER = "What fo you need?";
    private const string L_ACCEPT = "I'll take it.";
    private const string L_TURNIN = "Here's the job done.";
    private const string L_SHOP = "Open shop";
    private const string L_KEY_TALK = "I'm here to meet you.";
    private const string L_KEY_TURN = "Report back.";

    public static DialogueDefinition BuildForNPC(DialogueDefinition baseDef, DialogueComponent owner)
    {
        var def = (baseDef != null) ? DeepClone(baseDef) : CreateEmpty(owner);

        var start = def.FindNode(def.startNodeId);
        if (start == null)
        {
            start = new DialogueNode { id = def.startNodeId, line = "" };
            def.nodes.Add(start);
        }
        EnsureNode(def, N_OFFER);
        EnsureNode(def, N_ACCEPT);
        EnsureNode(def, N_TURNIN);
        EnsureNode(def, N_KEYTALK);
        EnsureNode(def, N_KEYTURN);

        var npc = owner ? owner.gameObject : null;
        var trader = owner ? owner.GetComponent<Trader>() : null;
        var dqCurrent = QuestService.GetCurrent();
        bool canTurnInDynamicHere = dqCurrent != null &&
                            dqCurrent.status == QuestStatus.Completed &&
                            QuestService.SameNPC(dqCurrent.questGiver, npc);

        bool hasPendingHere = QuestService.HasPendingFor(npc);

        bool isCriminal = WorldTags.Instance && WorldTags.Instance.IsPlayerCriminal();
        bool isBanditNPC = npc && npc.CompareTag("Bandit");
        bool isTownNPC = npc && npc.CompareTag("Town");

        var km = KeyQuestManager.Instance;
        var key = km ? km.CurrentKey : null;
        bool keyAvail = km && km.IsKeyAvailable;
        bool keyObjDone = km && km.IsKeyObjectiveComplete;

        bool isKeyGiver = false;
        if (key != null && npc != null && !string.IsNullOrEmpty(key.questGiverRefId))
        {
            var sr = npc.GetComponent<SceneRef>();
            if (sr && sr.id == key.questGiverRefId) isKeyGiver = true;
        }

        start.choices.RemoveAll(c =>
            c != null && (
                c.label == L_OFFER|| c.label == L_ACCEPT || c.label == L_TURNIN ||
                c.label == L_SHOP || c.label == L_KEY_TALK || c.label == L_KEY_TURN ||
                c.action == DialogueAction.HelpKeyAndOffer ||
                c.action == DialogueAction.OpenShop ||
                c.action == DialogueAction.AcceptQuest ||
                c.action == DialogueAction.TurnInQuest ||
                c.action == DialogueAction.ReportKeyTalkedTo ||
                c.action == DialogueAction.TurnInKey
            )
        );

        if (key != null && isKeyGiver &&
            keyAvail && !keyObjDone &&
            key.completionType == KeyQuestSO.KeyQuestCompletionType.TalkToGiver)
        {
            var who = DialogueService.CleanName(npc ? npc.name : "NPC");
            start.line = $"{who}: hey, how can i help you";

            var helpLabel = $"Help {who}";
            AddChoiceUnique(start, helpLabel, DialogueAction.HelpKeyAndOffer, N_OFFER);

            AddChoiceUnique(start, "Close", DialogueAction.Close, null);

            FillOfferNode(def, N_OFFER, npc);
            return def;
        }

        FillOfferNode(def, N_OFFER, npc);
        FillAcceptNode(def, N_ACCEPT, npc);
        FillTurnInNode(def, N_TURNIN, npc); 
        FillKeyTalkNode(def, N_KEYTALK, key, keyAvail, isKeyGiver);
        FillKeyTurnInNode(def, N_KEYTURN, key, keyAvail, keyObjDone, isKeyGiver);

        if (trader != null)
            AddChoiceUnique(start, L_SHOP, DialogueAction.OpenShop);

        if (canTurnInDynamicHere)
            AddChoiceUnique(start, L_TURNIN, DialogueAction.TurnInQuest, N_TURNIN);

        if (hasPendingHere)
            AddChoiceUnique(start, L_OFFER, DialogueAction.OfferQuest, N_ACCEPT);

        if (key != null && isKeyGiver && keyAvail && keyObjDone && key.requiresTurnIn)
            AddChoiceUnique(start, L_KEY_TURN, DialogueAction.TurnInKey, N_KEYTURN);

        AddChoiceUnique(start, "Close", DialogueAction.Close, null);

        return def;
    }

    private static void FillOfferNode(DialogueDefinition def, string id, GameObject npc)
    {
        var node = EnsureNode(def, id);
        var pending = QuestService.GetPending();

        if (pending != null && pending.questGiver == npc)
        {
            string giver = DialogueService.CleanName(npc ? npc.name : "NPC");
            string reward = pending.goldReward > 0 ? $"\nReward: +{pending.goldReward}g" : "";
            node.line = $"{giver}: {ShortDesc(pending)}{reward}";

            EnsureChoice(node, "I'll take it.", DialogueAction.AcceptQuest, null);
            EnsureChoice(node, "Maybe later.", DialogueAction.Close, null);
        }
        else
        {
            node.line = "others need your help, please act quickly";
            EnsureChoice(node, "OK", DialogueAction.Close, null);
        }
    }

    private static void FillAcceptNode(DialogueDefinition def, string id, GameObject npc)
    {
        var node = EnsureNode(def, id);
        var cur = QuestService.Active.LastOrDefault();
        if (cur != null && cur.questGiver == npc)
        {
            node.line = $"Quest accepted: {cur.questName}\n{ShortDesc(cur)}";
        }
        else
        {
            node.line = "Alright, noted.";
        }
        EnsureChoice(node, "OK", DialogueAction.Close, null);
    }

    private static void FillTurnInNode(DialogueDefinition def, string id, GameObject npc)
    {
        var node = EnsureNode(def, id);

        var cur = QuestService.GetCurrent();
        bool canTurnInHere =
            cur != null &&
            cur.status == QuestStatus.Completed;
        if (canTurnInHere)
        {
            string reward = (cur.goldReward > 0) ? $"\nReward: +{cur.goldReward}g" : "";
            node.line = $"Thanks for handling “{cur.questName}”.{reward}";
        }
        else
        {
            node.line = "You haven’t completed the task yet.";
        }
        EnsureChoice(node, "OK", DialogueAction.Close, null);
    }

    private static void FillKeyTalkNode(DialogueDefinition def, string id, KeyQuestSO key, bool keyAvail, bool isKeyGiver)
    {
        var node = EnsureNode(def, id);
        if (key != null && keyAvail && isKeyGiver && key.completionType == KeyQuestSO.KeyQuestCompletionType.TalkToGiver)
        {
            node.line = $"Good. {(!string.IsNullOrWhiteSpace(key.description) ? key.description : "Let’s proceed.")}";
        }
        else node.line = "…";
        EnsureChoice(node, "OK", DialogueAction.Close, null);
    }

    private static void FillKeyTurnInNode(DialogueDefinition def, string id, KeyQuestSO key, bool keyAvail, bool keyObjDone, bool isKeyGiver)
    {
        var node = EnsureNode(def, id);
        if (key != null && keyAvail && keyObjDone && isKeyGiver)
        {
            node.line = $"Well done on “{key.title}”.";
        }
        else node.line = "…";
        EnsureChoice(node, "OK", DialogueAction.Close, null);
    }

    private static DialogueDefinition CreateEmpty(DialogueComponent owner)
    {
        var def = ScriptableObject.CreateInstance<DialogueDefinition>();
        def.npcName = owner ? DialogueService.CleanName(owner.NpcDisplayName) : "NPC";
        def.startNodeId = "start";
        def.nodes.Add(new DialogueNode { id = "start", line = "" });
        return def;
    }

    private static DialogueDefinition DeepClone(DialogueDefinition src)
    {
        var def = ScriptableObject.CreateInstance<DialogueDefinition>();
        def.npcName = src.npcName;
        def.startNodeId = src.startNodeId;

        for (int i = 0; i < src.nodes.Count; i++)
        {
            var n = src.nodes[i];
            if (n == null) continue;
            var nn = new DialogueNode { id = n.id, line = n.line };
            for (int c = 0; c < n.choices.Count; c++)
            {
                var ch = n.choices[c];
                if (ch == null) continue;
                nn.choices.Add(new DialogueChoice
                {
                    label = ch.label,
                    nextNodeId = ch.nextNodeId,
                    action = ch.action
                });
            }
            def.nodes.Add(nn);
        }
        return def;
    }

    private static DialogueNode EnsureNode(DialogueDefinition def, string id)
    {
        var n = def.FindNode(id);
        if (n != null) return n;
        n = new DialogueNode { id = id, line = "" };
        def.nodes.Add(n);
        return n;
    }

    private static void AddChoiceUnique(DialogueNode node, string label, DialogueAction action, string nextId = null)
    {
        if (node.choices.Any(c => c != null && (c.action == action || c.label == label))) return;
        node.choices.Add(new DialogueChoice { label = label, action = action, nextNodeId = nextId });
    }

    private static void EnsureChoice(DialogueNode node, string label, DialogueAction action, string nextId = null)
    {
        var found = node.choices.FirstOrDefault(c => c != null && (c.action == action || c.label == label));
        if (found != null)
        {
            if (nextId != null) found.nextNodeId = nextId;
            return;
        }
        node.choices.Add(new DialogueChoice { label = label, action = action, nextNodeId = nextId });
    }

    private static string ShortDesc(DynamicQuest q)
    {
        if (!string.IsNullOrEmpty(q.introText)) return q.introText;
        switch (q.type)
        {
            case QuestType.Kill:
                return q.targetObject ? $"i need you to Eliminate {DialogueService.CleanName(q.targetObject.name)}."
                                      : "i need you to Eliminate the threat.";
            case QuestType.Steal:
                return string.IsNullOrEmpty(q.targetItemName)
                    ? "ive heard theres some valuables easily accessable nearby."
                    : $"i need you to Steal {q.targetItemName} for me.";
            case QuestType.Collect:
                return string.IsNullOrEmpty(q.targetItemName)
                    ? $"could you Collect {q.requiredCount} items for me."
                    : $"i need {q.requiredCount} of {q.targetItemName}.";
            case QuestType.Explore:
                return "i heard theres some wierd stuff going on nearby, ive marked the spot on your map, can you investigate?";
            default:
                return "Complete the objective.";
        }
    }

    private static string BuildQuestOfferLine(DynamicQuest q)
    {
        string name = string.IsNullOrEmpty(q.questName) ? "A job" : q.questName;
        string desc = ShortDesc(q);
        string reward = q.goldReward > 0 ? $"\nReward: +{q.goldReward}g" : "";
        return $"{name}\n{desc}{reward}";
    }
}

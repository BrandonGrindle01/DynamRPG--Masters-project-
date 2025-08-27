using UnityEngine;

public class DialogueComponent : MonoBehaviour
{
    [Header("Template (asset)")]
    public DialogueDefinition definition;          

    public string npcDisplayNameOverride = "";
    public bool autoStartOnInteract = true;
    [TextArea] public string refusalText = "Not now.";

    [Header("Talk Gates")]
    public bool refuseIfCriminal = false;
    public bool requireCriminal = false;
    public bool refuseIfBannedForTheft = false;
    public bool autoReportKeyTalkOnOpen = true;

    public string NpcDisplayName => string.IsNullOrEmpty(npcDisplayNameOverride) ? gameObject.name : npcDisplayNameOverride;
    public string RefusalTextOrDefault() => string.IsNullOrEmpty(refusalText) ? "Not now." : refusalText;

    public bool TryOpenShop()
    {
        var fpc = StarterAssets.FirstPersonController.instance;
        var trader = GetComponent<Trader>();
        if (!trader || fpc == null) return false;
        return trader.TryOpenShop(fpc);
    }

    public bool TryStartDialogue(string startId = null)
    {
        bool criminal = WorldTags.Instance && WorldTags.Instance.IsPlayerCriminal();
        var trader = GetComponent<Trader>();
        var qCurrent = QuestService.GetCurrent();
        bool canTurnInDynamicHere = qCurrent != null &&
                                    qCurrent.status == QuestStatus.Completed &&
                                    QuestService.SameNPC(qCurrent.questGiver, gameObject);
        bool hasPendingHere = QuestService.HasPendingFor(gameObject);

        var km = KeyQuestManager.Instance;
        bool canTurnInKeyHere =
            km && km.IsKeyAvailable && km.IsKeyObjectiveComplete &&
            km.CurrentKey && km.CurrentKey.requiresTurnIn &&
            km.CurrentKey.questGiver == gameObject;

        bool canAdvanceKeyTalkHere =
            km && km.IsKeyAvailable && !km.IsKeyObjectiveComplete &&
            km.CurrentKey &&
            km.CurrentKey.completionType == KeyQuestSO.KeyQuestCompletionType.TalkToGiver &&
            km.CurrentKey.questGiver == gameObject;

        bool allowServiceInteraction = hasPendingHere || canTurnInDynamicHere || canTurnInKeyHere || canAdvanceKeyTalkHere;

        if (refuseIfBannedForTheft && trader && trader.IsBannedForTheft && !allowServiceInteraction)
        { DialogueService.BeginOneLiner(NpcDisplayName, RefusalTextOrDefault(), this, 3f, true); return false; }

        if (!allowServiceInteraction)
        {
            if (requireCriminal && !criminal)
            { DialogueService.BeginOneLiner(NpcDisplayName, RefusalTextOrDefault(), this, 3f, true); return false; }

            if (refuseIfCriminal && criminal)
            { DialogueService.BeginOneLiner(NpcDisplayName, RefusalTextOrDefault(), this, 3f, true); return false; }
        }

        if (!definition) return false;
        var built = DialogueQuestBuilder.BuildForNPC(definition, this);
        if (string.IsNullOrEmpty(built.npcName))
            built.npcName = DialogueService.CleanName(NpcDisplayName);

        DialogueService.Begin(built, this, startId);
        return true;
    }

    public void OfferQuest()
    {
        var npc = gameObject;

        if (QuestService.HasPendingFor(npc))
        {
            var builtExisting = DialogueQuestBuilder.BuildForNPC(definition, this);
            DialogueService.Begin(builtExisting, this, "auto_offer");
            return;
        }

        var existing = QuestService.GetPending();
        if (existing != null && existing.questGiver != npc)
        {
            existing.questGiver = npc;
            QuestService.SetPendingOffer(existing);

            var builtRetarget = DialogueQuestBuilder.BuildForNPC(definition, this);
            DialogueService.Begin(builtRetarget, this, "auto_offer");
            return;
        }

        var ctx = KeyQuestManager.Instance ? KeyQuestManager.Instance.Current?.contextTag : null;
        var preview = DynamicQuestGenerator.Instance?.GenerateNextDynamicQuest(ctx, assign: false, forcedGiver: npc);
        if (preview == null)
        { DialogueService.BeginOneLiner(NpcDisplayName, "No work right now.", this, 3f, true); return; }

        preview.questGiver = npc;
        QuestService.SetPendingOffer(preview);

        var built = DialogueQuestBuilder.BuildForNPC(definition, this);
        DialogueService.Begin(built, this, "auto_offer");
    }
}
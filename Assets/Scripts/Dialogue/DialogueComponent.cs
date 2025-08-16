using UnityEngine;

public class DialogueComponent : MonoBehaviour
{
    public DialogueDefinition definition;
    public string npcDisplayNameOverride = "";
    public bool autoStartOnInteract = true;
    [TextArea] public string refusalText = "Not now.";

    [Header("Talk Gates")]
    public bool refuseIfCriminal = false;
    public bool requireCriminal = false;
    public bool refuseIfBannedForTheft = false;

    public string NpcDisplayName => string.IsNullOrEmpty(npcDisplayNameOverride) ? gameObject.name : npcDisplayNameOverride;
    public string RefusalTextOrDefault() => string.IsNullOrEmpty(refusalText) ? "Not now." : refusalText;

    public bool TryOpenShop()
    {
        Debug.Log("trying to open shop");
        var fpc = StarterAssets.FirstPersonController.instance;
        var trader = GetComponent<Trader>();
        if (!trader || fpc == null) return false;
        bool opened = trader.TryOpenShop(fpc);
        return opened;
    }

    public bool TryStartDialogue(string startId = null)
    {
        bool criminal = WorldTags.Instance && WorldTags.Instance.IsPlayerCriminal();
        var trader = GetComponent<Trader>();

        if (refuseIfBannedForTheft && trader && trader.IsBannedForTheft)
        {
            DialogueService.BeginOneLiner(NpcDisplayName, RefusalTextOrDefault(), this, 3f, true);
            return false;
        }

        if (requireCriminal && !criminal)
        {
            DialogueService.BeginOneLiner(NpcDisplayName, RefusalTextOrDefault(), this, 3f, true);
            return false;
        }

        if (refuseIfCriminal && criminal)
        {
            DialogueService.BeginOneLiner(NpcDisplayName, RefusalTextOrDefault(), this, 3f, true);
            return false;
        }

        if (definition == null) return false;
        if (string.IsNullOrEmpty(definition.npcName))
            definition.npcName = DialogueService.CleanName(NpcDisplayName);

        DialogueService.Begin(definition, this, startId);
        return true;
    }
    public void OfferQuest()
    {
        bool criminal = WorldTags.Instance && WorldTags.Instance.IsPlayerCriminal();
        var trader = GetComponent<Trader>();

        if (trader && trader.IsBannedForTheft)
        { DialogueService.BeginOneLiner(NpcDisplayName, "Not serving thieves.", this, 3f, true); return; }

        if (trader && trader.traderType == TraderType.Bandit && !criminal)
        { DialogueService.BeginOneLiner(NpcDisplayName, "We only deal with wanted folk.", this, 3f, true); return; }

        if (!trader || (trader.traderType != TraderType.Bandit && criminal))
        { DialogueService.BeginOneLiner(NpcDisplayName, "We don’t deal with criminals.", this, 3f, true); return; }

        var gen = DynamicQuestGenerator.Instance;
        if (gen == null)
        { DialogueService.BeginOneLiner(NpcDisplayName, "No work right now.", this, 3f, true); return; }

        gen.GenerateNextDynamicQuest();
        var dq = gen.GetCurrentQuest();
        if (dq == null)
        { DialogueService.BeginOneLiner(NpcDisplayName, "No work right now.", this, 3f, true); return; }

        dq.questGiver = this.gameObject;

        QuestService.AssignDynamic(dq);
        DialogueService.BeginOneLiner(NpcDisplayName, $"Quest accepted: {dq.questName}", this, 2f, true);
    }
}
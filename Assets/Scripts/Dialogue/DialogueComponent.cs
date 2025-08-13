using UnityEngine;

public class DialogueComponent : MonoBehaviour
{
    public DialogueDefinition definition;
    public string npcDisplayNameOverride = "";
    public bool autoStartOnInteract = true;
    [TextArea] public string refusalText = "Not now.";

    public string NpcDisplayName => string.IsNullOrEmpty(npcDisplayNameOverride) ? gameObject.name : npcDisplayNameOverride;
    public string RefusalTextOrDefault() => string.IsNullOrEmpty(refusalText) ? "Not now." : refusalText;

    public bool TryOpenShop()
    {
        var fpc = StarterAssets.FirstPersonController.instance;
        var trader = GetComponent<Trader>();
        if (!trader || fpc == null) return false;
        bool opened = trader.TryOpenShop(fpc);
        return opened;
    }

    public bool TryStartDialogue(string startId = null)
    {
        if (definition == null) return false;
        DialogueService.Begin(definition, this, startId);
        return true;
    }

    public void OfferQuest()
    {
        // Hook quest creation/accept flow here.
        // For now just a friendly line.
        DialogueService.BeginOneLiner(NpcDisplayName, "Come back tomorrow for work.", this);
    }
}
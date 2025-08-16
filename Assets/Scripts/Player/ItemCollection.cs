using UnityEngine;
using System;

public class ItemCollection : MonoBehaviour
{
    public ItemData itemData;
    public int amount = 1;

    [Header("Ownership")]
    public bool isOwned = false;            
    public string ownerName = "NPC";        
    public bool wasStolen = false;

    public static Action<string> OnStolenFromOwner;

    public void MarkAsStolen()
    {
        wasStolen = true;
        Debug.Log($"Player stole {itemData.itemName} from {ownerName}");
        PlayerStatsTracker.Instance.RegisterCrime();
        QuestService.ReportSteal(gameObject);
        OnStolenFromOwner?.Invoke(ownerName);
    }
}

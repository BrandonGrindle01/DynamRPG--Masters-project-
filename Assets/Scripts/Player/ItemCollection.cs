using UnityEngine;

public class ItemCollection : MonoBehaviour
{
    public ItemData itemData;
    public int amount = 1;

    [Header("Ownership")]
    public bool isOwned = false;            
    public string ownerName = "NPC";        
    public bool wasStolen = false;          

    public void MarkAsStolen()
    {
        wasStolen = true;
        Debug.Log($"Player stole {itemData.itemName} from {ownerName}");
    }
}

using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class ItemController : MonoBehaviour
{
    private ItemData itemData;

    [SerializeField] private TextMeshProUGUI itemNameText;
    [SerializeField] private Image itemIcon;
    [SerializeField] private TextMeshProUGUI quantityText;

    public void addItem(ItemData newItem)
    {
        itemData = newItem;

        if (itemNameText != null)
            itemNameText.text = itemData.itemName;

        if (itemIcon != null)
            itemIcon.sprite = itemData.itemSprite;

        if (itemData.stackable)
            quantityText?.SetText(""); // Set externally via InventoryManager if needed
        else
            quantityText?.SetText(""); // Equipment has no quantity
    }

    // Called when player clicks on this item
    public void OnClick()
    {
        Debug.Log("Clicked on item: " + itemData.itemName);

        switch (itemData.itemType)
        {   
            case ItemData.ItemType.Consumable:
                //PlayerStats.Instance.Heal(itemData.healAmount);
                InventoryManager.Instance.RemoveItem(itemData, 1);
                break;

            case ItemData.ItemType.Equipable:
                //EquipmentManager.Instance.Equip(itemData);
                break;

            case ItemData.ItemType.Quest:
                Debug.Log("Quest item — can't be used.");
                break;

            default:
                Debug.Log("Unhandled item type.");
                break;
        }

        InventoryManager.Instance.RefreshUI();
    }
}

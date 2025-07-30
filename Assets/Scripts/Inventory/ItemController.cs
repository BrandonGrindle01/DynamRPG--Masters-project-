using UnityEngine;
using TMPro;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class ItemController : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    private InventorySlot slot;
    private ItemData itemData;

    [SerializeField] private TextMeshProUGUI itemNameText;
    [SerializeField] private Image itemIcon;
    [SerializeField] private TextMeshProUGUI quantityText;
    [SerializeField] private Image backgroundImage;

    private Color normalColor = new Color(0.8f, 0.8f, 0.8f, 1f);
    private Color hoverColor = new Color(1f, 1f, 0.5f, 1f);          
    private Color equippedColor = new Color(0.5f, 1f, 0.5f, 1f);

    public void addItem(InventorySlot newSlot)
    {
        slot = newSlot;
        itemData = slot.item;

        if (itemNameText != null)
            itemNameText.text = itemData.itemName;

        if (itemIcon != null)
            itemIcon.sprite = itemData.itemSprite;

        if (itemData.stackable && slot.quantity > 1)
            quantityText?.SetText(slot.quantity.ToString());
        else
            quantityText?.SetText("");


        UpdateBackgroundColor();
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (!slot.isEquipped)
        {
            backgroundImage.color = hoverColor;
        }      
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        UpdateBackgroundColor();
    }
    // Called when player clicks on this item
    public void OnClick()
    {
        switch (itemData.itemType)
        {   
            case ItemData.ItemType.Consumable:
                //PlayerStats.Instance.Heal(itemData.healAmount);
                Debug.Log("consuming for " + itemData.healAmount + " hp");
                InventoryManager.Instance.RemoveItem(itemData, 1);
                break;

            case ItemData.ItemType.Equipable:
                slot.isEquipped = !slot.isEquipped;
                Debug.Log("is equipt? = " + slot.isEquipped);
                UpdateBackgroundColor();
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

    private void UpdateBackgroundColor()
    {
        Debug.Log(slot.isEquipped);
        backgroundImage.color = slot.isEquipped ? equippedColor : normalColor;
    }
}

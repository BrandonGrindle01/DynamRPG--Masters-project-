using UnityEngine;
using TMPro;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using StarterAssets;

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
            case ItemData.ItemType.Equipable:
                bool alreadyEquipped = InventoryManager.Instance.IsEquipped(itemData);

                if (alreadyEquipped)
                {
                    InventoryManager.Instance.UnequipItem(itemData.equipSlot);
                    slot.isEquipped = false;
                }
                else
                {
                    bool slotOccupied = InventoryManager.Instance.IsSlotOccupied(itemData.equipSlot);

                    if (!slotOccupied)
                    {
                        InventoryManager.Instance.EquipItem(itemData);
                        slot.isEquipped = true;
                    }
                    else
                    {
                        Debug.Log("Can't equip — slot already occupied.");
                        slot.isEquipped = false;
                    }
                }

                InventoryManager.Instance.RefreshUI();
                break;

            case ItemData.ItemType.Consumable:
                FirstPersonController.instance.Heal(itemData.healAmount);
                InventoryManager.Instance.RemoveItem(itemData, 1);
                break;

            case ItemData.ItemType.Quest:
                Debug.Log("Quest item — cannot be used.");
                break;

            case ItemData.ItemType.Tool:
                break;
            case ItemData.ItemType.Material:
                Debug.Log($"Item '{itemData.itemName}' is of type '{itemData.itemType}' and currently has no use on click.");
                break;

            default:
                Debug.LogWarning("Unhandled item type.");
                break;
        }
    }


    private void UpdateBackgroundColor()
    {
        //Debug.Log(slot.isEquipped);
        backgroundImage.color = slot.isEquipped ? equippedColor : normalColor;
    }
}

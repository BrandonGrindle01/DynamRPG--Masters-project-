using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System.Linq;

public class InventoryManager : MonoBehaviour
{
    public static InventoryManager Instance;

    [Header("Inventory")]
    public List<InventorySlot> inventory = new List<InventorySlot>();

    [Header("UI")]
    public Transform ItemContent;
    public GameObject ItemContainer;

    [HideInInspector] public List<ItemController> ItemControllerList = new List<ItemController>();

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    public void AddItem(ItemData item, int amount = 1)
    {
        if (item.stackable)
        {
            InventorySlot existingSlot = inventory.FirstOrDefault(i => i.item == item);
            if (existingSlot != null)
            {
                existingSlot.quantity = Mathf.Min(existingSlot.quantity + amount, item.maxStack);
                RefreshUI();
                return;
            }
        }

        inventory.Add(new InventorySlot(item, amount));
        RefreshUI();
    }

    public void RemoveItem(ItemData item, int amount = 1)
    {
        InventorySlot slot = inventory.FirstOrDefault(i => i.item == item);
        if (slot != null)
        {
            slot.quantity -= amount;
            if (slot.quantity <= 0)
            {
                inventory.Remove(slot);
            }

            RefreshUI();
        }
    }

    public bool HasQuestItem(ItemData.ItemType type)
    {
        return inventory.Any(slot => slot.item.itemType == type);
    }

    public void RefreshUI()
    {
        ClearList();

        foreach (var slot in inventory)
        {
            GameObject obj = Instantiate(ItemContainer, ItemContent);
            var itemName = obj.transform.Find("ItemName").GetComponent<TextMeshProUGUI>();
            var itemIcon = obj.transform.Find("ItemIcon").GetComponent<Image>();
            var itemQuantity = obj.transform.Find("Quantity")?.GetComponent<TextMeshProUGUI>();

            itemName.text = slot.item.itemName;
            itemIcon.sprite = slot.item.itemSprite;

            if (slot.item.stackable && slot.quantity > 1 && slot.item.itemType != ItemData.ItemType.Equipable)
            {
                itemQuantity.text = slot.quantity.ToString();
                itemQuantity.gameObject.SetActive(true);
            }
            else if (itemQuantity != null)
            {
                itemQuantity.text = "";
                itemQuantity.gameObject.SetActive(false);
            }

            // Assign to UI controller
            ItemController controller = obj.GetComponent<ItemController>();
            if (controller != null)
            {
                controller.addItem(slot.item);
                ItemControllerList.Add(controller);
            }
        }
    }

    public void ClearList()
    {
        foreach (Transform child in ItemContent)
        {
            Destroy(child.gameObject);
        }

        ItemControllerList.Clear();
    }

    public void ClearInventory()
    {
        inventory = inventory
            .Where(slot => slot.item.itemType == ItemData.ItemType.Equipable)
            .ToList();

        RefreshUI();
    }
}
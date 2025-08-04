using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System.Linq;
using StarterAssets;
using static ItemData;

public class InventoryManager : MonoBehaviour
{
    public static InventoryManager Instance;

    [Header("Inventory")]
    public List<InventorySlot> inventory = new List<InventorySlot>();
    private Dictionary<ItemData.EquipmentSlot, ItemData> equippedItems = new();

    private GameObject currentWeaponObject;
    private GameObject[] currentArmorObjects = new GameObject[4];

    [Header("UI")]
    public Transform ItemContent;
    public GameObject ItemContainer;

    [HideInInspector] public List<ItemController> ItemControllerList = new List<ItemController>();


    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        RefreshUI();
    }

    public void AddItem(ItemData item, int amount = 1)
    {
        if (item == null)
        {
            Debug.LogWarning("Tried to add a null item to inventory!");
            return;
        }

        if (item.stackable)
        {
            int remainingAmount = amount;
            foreach (InventorySlot slot in inventory.Where(s => s.item == item && s.quantity < item.maxStack))
            {
                int spaceLeft = item.maxStack - slot.quantity;
                int addAmount = Mathf.Min(spaceLeft, remainingAmount);

                slot.quantity += addAmount;
                remainingAmount -= addAmount;

                if (remainingAmount <= 0)
                    break;
            }
            while (remainingAmount > 0)
            {
                int stackAmount = Mathf.Min(remainingAmount, item.maxStack);
                inventory.Add(new InventorySlot(item, stackAmount));
                remainingAmount -= stackAmount;
            }
        }
        else
        {
            for (int i = 0; i < amount; i++)
            {
                inventory.Add(new InventorySlot(item, 1));
            }
        }

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

    public bool IsEquipped(ItemData item) =>
    equippedItems.ContainsKey(item.equipSlot) && equippedItems[item.equipSlot] == item;

    public void EquipItem(ItemData item)
    {
        if (item == null || item.itemType != ItemData.ItemType.Equipable)
            return;

        // Check if something is already equipped in this slot
        if (equippedItems.TryGetValue(item.equipSlot, out var existingItem))
        {
            // Already equipped — do nothing if it's not the same item
            if (existingItem != item)
            {
                Debug.Log("Slot already occupied. Unequip first to equip a new item.");
                return;
            }
        }

        // Equip the item
        equippedItems[item.equipSlot] = item;
        FirstPersonController.instance.ApplyEquipStats(item);

        if (item.worldPrefab != null)
        {
            if (item.equipSlot == EquipmentSlot.Weapon)
            {
                EquipVisual(ref currentWeaponObject, item.worldPrefab, FirstPersonController.instance.weaponHolder);
            }
            else
            {
                int index = (int)item.equipSlot - 1;
                if (index >= 0 && index < FirstPersonController.instance.armorHolder.Length)
                {
                    EquipVisual(ref currentArmorObjects[index], item.worldPrefab, FirstPersonController.instance.armorHolder[index]);
                }
            }
        }
    }

    private void EquipVisual(ref GameObject currentObj, GameObject prefab, Transform holder)
    {
        if (currentObj != null)
            Destroy(currentObj);

        currentObj = Instantiate(prefab, holder);
    }

    public void UnequipItem(EquipmentSlot slot)
    {
        equippedItems.Remove(slot);

        if (slot == EquipmentSlot.Weapon)
        {
            Destroy(currentWeaponObject);
            currentWeaponObject = null;
        }
        else
        {
            int index = (int)slot - 1;
            if (index >= 0 && index < currentArmorObjects.Length)
            {
                Destroy(currentArmorObjects[index]);
                currentArmorObjects[index] = null;
            }
        }
    }

    public bool HasQuestItem(ItemData.ItemType type)
    {
        return inventory.Any(slot => slot.item.itemType == type);
    }

    public bool IsSlotOccupied(ItemData.EquipmentSlot slot)
    {
        return equippedItems.ContainsKey(slot);
    }

    public void RefreshUI()
    {
        ClearList();

        foreach (var slot in inventory)
        {
            GameObject obj = Instantiate(ItemContainer, ItemContent);
            var itemName = obj.transform.Find("Itm_name").GetComponent<TextMeshProUGUI>();
            var itemIcon = obj.transform.Find("Itm_icon").GetComponent<Image>();
            var quantity_tab = obj.transform.Find("Itm_quantity")?.gameObject;
            var itemQuantity = obj.transform.Find("Itm_quantity").GetComponentInChildren<TextMeshProUGUI>();
            if(itemQuantity == null)
            {
                Debug.Log("summing aint right here");
            }
            itemName.text = slot.item.itemName;
            itemIcon.sprite = slot.item.itemSprite;

            if (slot.item.stackable && slot.quantity > 1 && slot.item.itemType != ItemData.ItemType.Equipable)
            {
                //itemQuantity.text = slot.quantity.ToString();
                itemQuantity.gameObject.SetActive(true);
                quantity_tab.gameObject.SetActive(true);
            }
            else if (itemQuantity != null)
            {
                //itemQuantity.text = "";
                itemQuantity.gameObject.SetActive(false);
                quantity_tab.gameObject.SetActive(false);
            }

            // Assign to UI controller
            ItemController controller = obj.GetComponent<ItemController>();
            if (controller != null)
            {
                controller.addItem(slot);
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
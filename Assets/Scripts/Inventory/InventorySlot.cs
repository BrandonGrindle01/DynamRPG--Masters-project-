using System;
using UnityEngine;

[Serializable]
public class InventorySlot
{
    public ItemData item;
    public int quantity;
    public bool isEquipped;

    public InventorySlot(ItemData item, int quantity)
    {
        this.item = item;
        this.quantity = quantity;
    }
}

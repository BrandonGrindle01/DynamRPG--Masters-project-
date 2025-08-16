using UnityEngine;
using System.Collections.Generic;

public enum TraderType { Bandit, Blacksmith, Apothecary, TavernKeeper, Farmer }

[System.Serializable]
public class ShopItem
{
    public ItemData item;
    public int price = 10;
    public int initialQty = 1;
}

public class Trader : MonoBehaviour
{
    [Header("Identity")]
    public TraderType traderType;
    public string refusalText = "Not now.";

    [Header("Stock")]
    public List<ShopItem> stock = new();
    [Range(0f, 1f)] public float sellbackMultiplier = 0.5f;

    public string LastRefusalText { get; private set; }
    private Dictionary<ItemData, int> qty = new();

    [Header("Farmer Settings")]
    public string ownerNameForTheftBan = "";
    private bool bannedForTheft = false;
    public bool IsBannedForTheft => bannedForTheft;

    private void Awake()
    {
        foreach (var s in stock)
        {
            if (!s.item) continue;
            qty[s.item] = Mathf.Max(0, s.initialQty);
        }

        ItemCollection.OnStolenFromOwner += OnStolenFromOwner;
    }

    private void OnDestroy()
    {
        ItemCollection.OnStolenFromOwner -= OnStolenFromOwner;
    }

    private void OnStolenFromOwner(string owner)
    {
        Debug.Log("Comparing " + owner + " with " + ownerNameForTheftBan);
        if (traderType == TraderType.Farmer && !string.IsNullOrEmpty(ownerNameForTheftBan))
        {
            if (owner == ownerNameForTheftBan) bannedForTheft = true;
        }
    }

    public bool TryOpenShop(StarterAssets.FirstPersonController player)
    {
        LastRefusalText = "";
        if (!CanServePlayer())
        {
            LastRefusalText = refusalText;
            return false;
        }

        ShopService.Begin(this);
        return true;
    }

    private bool CanServePlayer()
    {
        bool isCriminal = WorldTags.Instance && WorldTags.Instance.IsPlayerCriminal();

        switch (traderType)
        {
            case TraderType.Bandit:
                if (!isCriminal) { refusalText = "What do you want? Get outta here."; return false; }
                return true;

            case TraderType.Blacksmith:
                if (isCriminal) { refusalText = "We don’t serve your kind here."; return false; }
                return true;
            case TraderType.Apothecary:
                if (isCriminal) { refusalText = "We don’t serve your kind here."; return false; }
                return true;
            case TraderType.TavernKeeper:
                if (isCriminal) { refusalText = "We don’t serve your kind here."; return false; }
                return true;

            case TraderType.Farmer:
                if (bannedForTheft) { refusalText = "You stole from me. GET LOST BEFORE I CALL THE GUARDS!"; return false; }
                return true;
        }
        return true;
    }

    public IReadOnlyDictionary<ItemData, int> Quantities => qty;

    public bool Buy(ItemData item)
    {
        if (!qty.TryGetValue(item, out int have) || have <= 0) return false;
        int price = GetPrice(item);

        if (!InventoryManager.Instance.SpendGold(price)) return false;

        qty[item] = have - 1;
        InventoryManager.Instance.AddItem(item, 1);
        return true;
    }

    public bool Sell(ItemData item)
    {
        var invSlot = InventoryManager.Instance.inventory
            .Find(s => s.item == item && s.quantity > 0);
        if (invSlot == null) return false;

        if (item.itemType == ItemData.ItemType.Equipable &&
            item.equipSlot == ItemData.EquipmentSlot.Weapon)
        {
            bool hasAnother = InventoryManager.Instance.HasOtherWeapon(item);
            if (!hasAnother)
            {
                Debug.Log("Cannot sell your last weapon.");
                return false;
            }
        }

        int price = Mathf.Max(1, Mathf.RoundToInt(GetPrice(item) * sellbackMultiplier));

        InventoryManager.Instance.RemoveItem(item, 1);
        InventoryManager.Instance.AddGold(price);

        if (!qty.ContainsKey(item)) qty[item] = 0;
        qty[item] += 1;

        return true;
    }

    public int GetPrice(ItemData item)
    {
        var entry = stock.Find(s => s.item == item);
        if (entry != null) return Mathf.Max(1, entry.price);
        return item ? Mathf.Max(1, item.basePrice) : 10;
    }
}
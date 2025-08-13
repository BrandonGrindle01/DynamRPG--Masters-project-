using System.Linq;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ShopUIController : MonoBehaviour
{
    [Header("Root / Visibility")]
    [SerializeField] private GameObject panelRoot;

    [Header("Buy (Top)")]
    [SerializeField] private Transform buyContent;
    [SerializeField] private GameObject ShopItemPrefab;

    [Header("Sell (Bottom)")]
    [SerializeField] private Transform sellContent;

    [Header("gold")]
    [SerializeField] private TextMeshProUGUI goldText;

    private void OnEnable()
    {
        ShopService.OnShopOpened += ShopOpened;
        ShopService.OnShopClosed += ShopClosed;
        ShopService.OnBought += _ => RefreshAll();
        ShopService.OnSold += _ => RefreshAll();

        if (ShopService.Current != null) ShopOpened(ShopService.Current);
    }

    private void OnDisable()
    {
        ShopService.OnShopOpened -= ShopOpened;
        ShopService.OnShopClosed -= ShopClosed;
        ShopService.OnBought -= _ => RefreshAll();
        ShopService.OnSold -= _ => RefreshAll();
    }

    private void ShopOpened(Trader trader)
    {
        if (panelRoot) panelRoot.SetActive(true);
        RefreshAll();

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private void ShopClosed()
    {
        if (panelRoot) panelRoot.SetActive(false);

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void RefreshAll()
    {
        RefreshGold();
        RefreshBuyList();
        RefreshSellList();
    }

    private void RefreshGold()
    {
        if (goldText) goldText.text = InventoryManager.Instance.GetGold().ToString();
    }

    private void RefreshBuyList()
    {
        ClearChildren(buyContent);

        var stock = ShopService.Stock();
        if (stock == null) return;

        foreach (var kv in stock)
        {
            var item = kv.Key;
            int qty = kv.Value;
            if (item == null || qty <= 0) continue;

            var go = Instantiate(ShopItemPrefab, buyContent);
            ShopItem(go, item, qty, ShopService.Price(item), isBuyRow: true);
        }
    }

    private void RefreshSellList()
    {
        ClearChildren(sellContent);

        foreach (var slot in InventoryManager.Instance.inventory)
        {
            var item = slot.item;
            if (item == null) continue;
            if (slot.quantity <= 0) continue;
            if (item.itemType == ItemData.ItemType.Quest) continue;

            int basePrice = ShopService.Price(item);
            var trader = ShopService.Current;
            int sellPrice = Mathf.Max(1, Mathf.RoundToInt(basePrice * (trader ? trader.sellbackMultiplier : 0.5f)));

            var go = Instantiate(ShopItemPrefab, sellContent);

            int shownQty = item.stackable ? slot.quantity : 1;
            ShopItem(go, item, shownQty, sellPrice, isBuyRow: false);
        }
    }

    private void ShopItem(GameObject ShopITM, ItemData item, int qty, int price, bool isBuyRow)
    {
        var btn = ShopITM.GetComponent<Button>();
        var icon = ShopITM.transform.Find("Image")?.GetComponent<Image>();
        var priceT = ShopITM.transform.Find("Value")?.GetComponentInChildren<TextMeshProUGUI>();
        var qtyT = ShopITM.transform.Find("AMNT")?.GetComponentInChildren<TextMeshProUGUI>();

        if (icon) icon.sprite = item.itemSprite;
        if (priceT) priceT.text = price.ToString();
        if (qtyT) qtyT.text = (qty > 1) ? ("x" + qty) : "";

        btn.onClick.RemoveAllListeners();

        if (isBuyRow)
        {
            btn.onClick.AddListener(() =>
            {
                bool ok = ShopService.Buy(item);
                if (!ok) FlashButton(btn);
            });
        }
        else
        {
            btn.onClick.AddListener(() =>
            {
                bool ok = ShopService.Sell(item);
                if (!ok) FlashButton(btn);
            });
        }
    }

    private void ClearChildren(Transform parent)
    {
        if (!parent) return;
        for (int i = parent.childCount - 1; i >= 0; i--)
            Destroy(parent.GetChild(i).gameObject);
    }

    private void FlashButton(Button b)
    {
        Debug.Log("Transaction failed (not enough gold, no stock, or no item to sell).");
    }

    public void CloseShop()
    {
        ShopService.End();
    }
}


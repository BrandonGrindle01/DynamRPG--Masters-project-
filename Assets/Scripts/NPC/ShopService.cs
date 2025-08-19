using UnityEngine;
using System;
using System.Collections.Generic;

public static class ShopService
{
    public static event Action<Trader> OnShopOpened;
    public static event Action OnShopClosed;
    public static event Action<ItemData> OnBought;
    public static event Action<ItemData> OnSold;

    public static Trader Current { get; private set; }

    public static void Begin(Trader trader)
    {
        Current = trader;
        OnShopOpened?.Invoke(trader);
    }

    public static void End()
    {
        Current = null;
        OnShopClosed?.Invoke();
    }

    public static bool Buy(ItemData item)
    {
        if (Current == null || item == null) return false;
        bool ok = Current.Buy(item);
        if (ok) OnBought?.Invoke(item);
        return ok;
    }

    public static bool Sell(ItemData item)
    {
        if (Current == null || item == null) return false;
        bool ok = Current.Sell(item);
        if (ok) OnSold?.Invoke(item);
        return ok;
    }

    public static IReadOnlyDictionary<ItemData, int> Stock()
    {
        return Current != null ? Current.Quantities : null;
    }

    public static int Price(ItemData item) => Current ? Current.GetPrice(item) : 0;
}
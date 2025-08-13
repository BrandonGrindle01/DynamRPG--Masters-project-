using UnityEngine;

[CreateAssetMenu(menuName = "Inventory/Item")]
public class ItemData : ScriptableObject
{
    public string itemName;
    public ItemType itemType;
    public Sprite itemSprite;
    public GameObject worldPrefab;

    [TextArea] public string description;

    public bool stackable = true;
    public int maxStack = 99;

    public int healAmount;

    [Header("equipment only")]
    public EquipmentSlot equipSlot;
    public int damage;
    public int armorBonus;

    [Header("Shop")]
    public int basePrice = 10;
    public bool allowSell = true;
    public bool isStarter = false;

    public enum ItemType { Consumable, Equipable, Quest, Tool, Material }
    public enum EquipmentSlot { Weapon, Helmet, Chestplate, Leggings, Boots }
}


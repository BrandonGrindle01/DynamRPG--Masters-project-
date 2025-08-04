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

    public enum ItemType { Consumable, Equipable, Quest, Tool, Material }
    public enum EquipmentSlot { Weapon, Helmet, Chestplate, Leggings, Boots }
}


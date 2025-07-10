using UnityEngine;

[CreateAssetMenu(fileName = "New Inventory Item", menuName = "Inventory/Item")]
public class InventoryItem : ScriptableObject
{
    public string itemName;
    public Sprite icon;
    public string description;
    public ItemType itemType;

    public enum ItemType
    {
        Key,
        Lever,
        SpecialTool
    }
}

using System.Collections.Generic;
using UnityEngine;

public class InventorySystem : MonoBehaviour
{
    public static InventorySystem Instance { get; private set; }

    [SerializeField] private int maxSlots = 5;
    private List<InventoryItem> items = new List<InventoryItem>();

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public bool AddItem(InventoryItem item)
    {
        // Eliminar la verificación de duplicados que impedía agregar múltiples llaves
        if (items.Count >= maxSlots)
        {
            Debug.Log("Inventario lleno!");
            return false;
        }

        items.Add(item);
        Debug.Log($"Añadido: {item.itemName} (Total: {items.Count}/{maxSlots})");
        InventoryUIController.Instance?.UpdateInventory(items);
        return true;
    }

    public bool RemoveItem(InventoryItem item)
    {
        if (items.Remove(item))
        {
            Debug.Log($"Removido: {item.itemName}");
            InventoryUIController.Instance?.UpdateInventory(items);
            return true;
        }
        return false;
    }

    public bool HasItem(InventoryItem item)
    {
        return items.Contains(item);
    }

    public bool HasItemOfType(InventoryItem.ItemType type)
    {
        foreach (var item in items)
        {
            if (item.itemType == type) return true;
        }
        return false;
    }

    public List<InventoryItem> GetAllItems()
    {
        return new List<InventoryItem>(items);
    }
}

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class InventoryUIController : MonoBehaviour
{
    public static InventoryUIController Instance;

    [Header("UI References")]
    [SerializeField] private Transform inventoryPanel;
    [SerializeField] private GameObject inventorySlotPrefab;
    [SerializeField] private GameObject inventoryWindow;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        UpdateInventory(InventorySystem.Instance.GetAllItems());
    }

    public void UpdateInventory(List<InventoryItem> items)
    {
        Debug.Log($"Actualizando UI - Items: {items.Count}");

        foreach (Transform child in inventoryPanel)
        {
            Destroy(child.gameObject);
        }

        foreach (var item in items)
        {
            Debug.Log($"Mostrando ítem: {item.itemName}");
            GameObject slot = Instantiate(inventorySlotPrefab, inventoryPanel);
            slot.GetComponentInChildren<Image>().sprite = item.icon;
        }
    }
}

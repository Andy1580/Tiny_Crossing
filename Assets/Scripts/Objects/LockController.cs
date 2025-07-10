using UnityEngine;

public class LockController : MonoBehaviour
{
    [Header("Lock Settings")]
    public InventoryItem requiredKey;
    public GameObject objectToUnlock; // Puerta, cofre, etc.

    public void TryUnlock()
    {
        if (InventorySystem.Instance.HasItem(requiredKey))
        {
            Unlock();
        }
        else
        {
            Debug.Log("¡Necesitas la llave correcta!");
        }
    }

    private void Unlock()
    {
        Debug.Log("¡Desbloqueado!");
        InventorySystem.Instance.RemoveItem(requiredKey);

        if (objectToUnlock != null)
        {
            objectToUnlock.SetActive(false); // O activar animación
        }

        Destroy(gameObject);
    }

    private void OnMouseDown()
    {
        TryUnlock();
    }
}

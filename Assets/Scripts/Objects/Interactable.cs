using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Interactable : MonoBehaviour
{
    public enum InteractableType { Obstacle, Weapon, InventoryItem }
    public InteractableType interactableType;

    [Header("Physics Settings")]
    public bool useGravity = true;

    private Rigidbody2D rb;
    private Collider2D col;
    public bool CanInteract { get; private set; } = true;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        col = GetComponent<Collider2D>();
    }

    public void DisablePhysics()
    {
        if (rb != null)
        {
            rb.velocity = Vector2.zero;
            rb.isKinematic = true;
        }
    }

    public void EnablePhysics()
    {
        if (rb != null)
        {
            rb.isKinematic = false;
            if (useGravity) rb.gravityScale = 1;
        }
    }

    public void MarkAsUsed()
    {
        CanInteract = false;
    }

    public void AddToInventory()
    {
        // Lógica para agregar al inventario
        gameObject.SetActive(false);
        MarkAsUsed();
    }
}

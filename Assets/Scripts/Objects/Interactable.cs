using UnityEngine;

public class Interactable : MonoBehaviour
{
    public enum InteractableType { Obstacle, Weapon, InventoryItem }
    public InteractableType interactableType;

    // Nuevo enum para tipos de armas
    public enum WeaponType { None, FlySwatter, Bat, Wrench }
    public WeaponType weaponType = WeaponType.None;

    [Header("Physics Settings")]
    public bool useGravity = true;

    private Rigidbody2D rb;
    private Collider2D col;
    public bool CanInteract { get; private set; } = true;

    [Header("Weapon Settings")]
    public bool isHoldingByHand = false;
    public bool isAttachedToTiny = false;
    public Transform tinyWeaponAnchor;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        col = GetComponent<Collider2D>();
    }

    void Update()
    {
        if (isAttachedToTiny && tinyWeaponAnchor != null)
        {
            transform.position = tinyWeaponAnchor.position;
            transform.rotation = tinyWeaponAnchor.rotation;
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (interactableType != InteractableType.Weapon) return;
        if (!isHoldingByHand) return;

        TinyController tiny = other.GetComponent<TinyController>();
        if (tiny != null && tiny.weaponAnchorPoint != null)
        {
            AttachToTiny(tiny.weaponAnchorPoint);

            //InteractiveHand hand = FindObjectOfType<InteractiveHand>();
            InteractiveHand hand = Object.FindFirstObjectByType<InteractiveHand>();
            if (hand != null)
            {
                hand.HandleWeaponHitTiny(this, other.ClosestPoint(transform.position));
            }
        }
    }

    public void AttachToTiny(Transform anchorPoint)
    {
        isAttachedToTiny = true;
        tinyWeaponAnchor = anchorPoint;

        if (rb != null)
        {
            rb.simulated = false;
        }
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
        gameObject.SetActive(false);
        MarkAsUsed();
    }
}

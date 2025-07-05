using UnityEngine;

public class InteractiveHand : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float moveSpeed = 15f;
    [SerializeField] private float grabRadius = 0.5f;

    [Header("Timers")]
    [SerializeField] private float obstacleHoldTime = 5f;
    [SerializeField] private float weaponHoldTime = 3f;

    [Header("Layers")]
    [SerializeField] private LayerMask interactableLayer;
    [SerializeField] private LayerMask groundLayer;

    [Header("Tiny Reference")]
    [SerializeField] private TinyController tiny;

    private Vector3 handOffsetFromTiny;
    private bool isAttachedToTiny = false;

    private Camera mainCamera;
    private GameObject grabbedObject;
    private Vector2 objectGrabOffset;
    private float currentHoldTimer;
    private bool isHoldingObject;

    void Start()
    {
        mainCamera = Camera.main;
        transform.position = new Vector3(transform.position.x, transform.position.y, 0);
    }

    void Update()
    {
        HandleMovement();
        HandleInteraction();
        UpdateTimers();
        ConstrainToViewport();
        HandleTinyAttachment();
    }

    void HandleMovement()
    {
        Vector3 mousePos = mainCamera.ScreenToWorldPoint(Input.mousePosition);
        mousePos.z = 0;
        transform.position = Vector3.Lerp(transform.position, mousePos, moveSpeed * Time.deltaTime);
    }

    void HandleInteraction()
    {
        if (Input.GetMouseButtonDown(0))
        {
            TryGrabObject();
        }

        if (Input.GetMouseButtonUp(0) && grabbedObject != null)
        {
            ReleaseObject();
        }

        if (grabbedObject != null)
        {
            grabbedObject.transform.position = (Vector2)transform.position + objectGrabOffset;
        }
    }

    void TryGrabObject()
    {
        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, grabRadius, interactableLayer);

        if (hits.Length > 0)
        {
            foreach (Collider2D hit in hits)
            {
                Interactable interactable = hit.GetComponent<Interactable>();

                if (interactable != null && interactable.CanInteract)
                {
                    grabbedObject = hit.gameObject;
                    objectGrabOffset = (Vector2)grabbedObject.transform.position - (Vector2)transform.position;

                    switch (interactable.interactableType)
                    {
                        case Interactable.InteractableType.Obstacle:
                            currentHoldTimer = obstacleHoldTime;
                            isHoldingObject = true;
                            interactable.DisablePhysics();
                            break;

                        case Interactable.InteractableType.Weapon:
                            currentHoldTimer = weaponHoldTime;
                            isHoldingObject = true;
                            interactable.isHoldingByHand = true;
                            break;

                        case Interactable.InteractableType.InventoryItem:
                            interactable.AddToInventory();
                            grabbedObject = null;
                            break;
                    }
                    break;
                }
            }
        }
    }

    void ReleaseObject()
    {
        if (grabbedObject == null) return;

        Interactable interactable = grabbedObject.GetComponent<Interactable>();
        if (interactable != null)
        {
            if (interactable.interactableType == Interactable.InteractableType.Obstacle)
            {
                interactable.EnablePhysics();
                interactable.MarkAsUsed();
            }
            else if (interactable.interactableType == Interactable.InteractableType.Weapon)
            {
                interactable.isHoldingByHand = false;
            }
        }

        grabbedObject = null;
        isHoldingObject = false;
    }

    void UpdateTimers()
    {
        if (!isHoldingObject || grabbedObject == null) return;

        currentHoldTimer -= Time.deltaTime;

        if (currentHoldTimer <= 0)
        {
            Interactable interactable = grabbedObject.GetComponent<Interactable>();

            if (interactable.interactableType == Interactable.InteractableType.Obstacle)
            {
                interactable.EnablePhysics();
                interactable.MarkAsUsed();
            }
            else if (interactable.interactableType == Interactable.InteractableType.Weapon)
            {
                Destroy(grabbedObject);
            }

            grabbedObject = null;
            isHoldingObject = false;
        }
    }

    void ConstrainToViewport()
    {
        Vector3 viewportPos = mainCamera.WorldToViewportPoint(transform.position);
        viewportPos.x = Mathf.Clamp(viewportPos.x, 0.05f, 0.95f);
        viewportPos.y = Mathf.Clamp(viewportPos.y, 0.05f, 0.95f);
        transform.position = mainCamera.ViewportToWorldPoint(viewportPos);
    }

    public void HandleWeaponHitTiny(Interactable weapon, Vector3 hitPoint)
    {
        grabbedObject = null;
        isHoldingObject = false;

        handOffsetFromTiny = transform.position - hitPoint;
        isAttachedToTiny = true;

        weapon.isHoldingByHand = false;
    }

    void HandleTinyAttachment()
    {
        if (isAttachedToTiny && tiny != null)
        {
            transform.position = tiny.transform.position + handOffsetFromTiny;
        }
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, grabRadius);
    }
}

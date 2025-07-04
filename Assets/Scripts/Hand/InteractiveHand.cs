using System.Collections;
using System.Collections.Generic;
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

    private Camera mainCamera;
    private GameObject grabbedObject;
    private Vector2 objectGrabOffset;
    private float currentHoldTimer;
    private bool isHoldingObject;

    void Start()
    {
        mainCamera = Camera.main;
        // Asegurar que la mano está en Z=0 (mismo que la cámara)
        transform.position = new Vector3(transform.position.x, transform.position.y, 0);
    }

    void Update()
    {
        HandleMovement();
        HandleInteraction();
        UpdateTimers();
        ConstrainToViewport();
    }

    void HandleMovement()
    {
        // Movimiento constante con el ratón sin necesidad de click
        Vector3 mousePos = mainCamera.ScreenToWorldPoint(Input.mousePosition);
        mousePos.z = 0; // Importante: mantener Z=0
        transform.position = Vector3.Lerp(transform.position, mousePos, moveSpeed * Time.deltaTime);
    }

    void HandleInteraction()
    {
        // Iniciar agarre al presionar el botón del ratón
        if (Input.GetMouseButtonDown(0))
        {
            TryGrabObject();
        }

        // Soltar objeto al levantar el botón del ratón
        if (Input.GetMouseButtonUp(0) && grabbedObject != null)
        {
            ReleaseObject();
        }

        // Mover objeto agarrado
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

                    // Configurar según tipo
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
                            break;

                        case Interactable.InteractableType.InventoryItem:
                            interactable.AddToInventory();
                            grabbedObject = null; // No mantenemos agarrado
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
        if (interactable.interactableType == Interactable.InteractableType.Obstacle)
        {
            interactable.EnablePhysics();
            interactable.MarkAsUsed();
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
                // SOLO activamos física sin reposicionar
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

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, grabRadius);
    }
}

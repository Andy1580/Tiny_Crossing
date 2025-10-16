using System.Runtime.CompilerServices;
using UnityEngine;

public class InteractiveHand : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float moveSpeed = 15f;
    [SerializeField] private float grabRadius = 0.5f;

    // --- Mecanismo ---
    private bool isAttachedToMechanism = false;
    [SerializeField] private TrapMechanism attachedMechanism;
    private Vector3 handOffsetFromMechanism = new Vector3(0f, 0.4f, 0f);

    // --- Control del tiempo entre clics de mecanismo ---
    [SerializeField] private float mechanismClickDelay = 0.6f; // segundos de espera mínima
    private float mechanismClickTimer = 0f;

    [Header("Timers")]
    [SerializeField] private float obstacleHoldTime = 5f;
    [SerializeField] private float weaponHoldTime = 3f;

    [Header("Layers")]
    [SerializeField] private LayerMask interactableLayer;
    [SerializeField] private LayerMask groundLayer;

    [Header("Tiny Reference")]
    [SerializeField] private TinyController tiny;

    [Header("Power Bar")]
    public PowerBarController powerBar;

    private Vector3 handOffsetFromTiny;
    private bool isAttachedToTiny = false;

    private Camera mainCamera;
    private GameObject grabbedObject;
    private Vector2 objectGrabOffset;
    private float currentHoldTimer;
    private bool isHoldingObject;
    private bool isMechanismAwaitingConfirm = false;
    private TinySpeechBubble tinyBubble;

    void Start()
    {
        mainCamera = Camera.main;
        transform.position = new Vector3(transform.position.x, transform.position.y, 0);
        mechanismClickTimer = 0f;
    }

    void Update()
    {
        if (!isAttachedToTiny)
        {
            HandleMovement();
        }

        HandleInteraction();
        // Contador para evitar activaciones instantáneas
        if (mechanismClickTimer > 0f)
            mechanismClickTimer -= Time.deltaTime;

        // --- Clic global para confirmar mecanismo ---
        if (isMechanismAwaitingConfirm && Input.GetMouseButtonDown(0) && mechanismClickTimer <= 0f)
        {
            if (attachedMechanism != null && attachedMechanism.IsPreActivating())
            {
                Debug.Log("Clic global detectado (confirmación remota).");
                attachedMechanism.TryActivate();
                ReleaseFromMechanism();

                isMechanismAwaitingConfirm = false;
                attachedMechanism = null;
            }
        }

        UpdateTimers();
        ConstrainToViewport();
        HandleTinyAttachment();
        HandleMechanismAttachment();

        // Limpieza de referencia si el mecanismo desaparece o se destruye
        if (isMechanismAwaitingConfirm && attachedMechanism == null)
        {
            isMechanismAwaitingConfirm = false;
            isAttachedToMechanism = false;
            Debug.LogWarning("Mecanismo perdido o destruido: referencia limpiada.");
        }

        // Evitar interacciones si ya estamos anclados a Tiny o a un mecanismo
        if (isAttachedToTiny || isAttachedToMechanism)
        {
            return; // previene clics adicionales mientras está anclada
        }
    }

    void HandleMovement()
    {
        Vector3 mousePos = mainCamera.ScreenToWorldPoint(Input.mousePosition);
        mousePos.z = 0;
        transform.position = Vector3.Lerp(transform.position, mousePos, moveSpeed * Time.deltaTime);
    }

    void HandleInteraction()
    {
        if (Input.GetMouseButtonDown(0) && grabbedObject == null)
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
        if (hits.Length == 0) return;

        Collider2D hit = hits[0];
        Interactable interactable = hit.GetComponent<Interactable>();

        if (interactable != null && interactable.CanInteract)
        {
            // IMPORTANTE:
            // Solo asignamos grabbedObject para tipos que realmente se pueden mover
            if (interactable.interactableType != Interactable.InteractableType.Mechanism)
            {
                grabbedObject = hit.gameObject;
                objectGrabOffset = (Vector2)grabbedObject.transform.position - (Vector2)transform.position;
            }

            switch (interactable.interactableType)
            {
                case Interactable.InteractableType.InventoryItem:
                    if (interactable.inventoryItem != null)
                    {
                        interactable.AddToInventory();
                        grabbedObject = null;
                    }
                    break;

                case Interactable.InteractableType.Obstacle:
                    currentHoldTimer = obstacleHoldTime;
                    isHoldingObject = true;
                    interactable.DisablePhysics();
                    break;

                case Interactable.InteractableType.MidObstacle:
                    currentHoldTimer = obstacleHoldTime;
                    isHoldingObject = true;
                    interactable.DisablePhysics();
                    break;

                case Interactable.InteractableType.Weapon:
                    currentHoldTimer = weaponHoldTime;
                    isHoldingObject = true;
                    interactable.isHoldingByHand = true;
                    if(tinyBubble == null)
                    {
                        tinyBubble = GameObject.FindFirstObjectByType<TinySpeechBubble>();
                    }
                    if(tinyBubble != null)
                    {
                        tinyBubble.InterruptWithWeapon(interactable.weaponType);
                    }
                    break;

                case Interactable.InteractableType.Mechanism:
                    TrapMechanism mechanism = hit.GetComponent<TrapMechanism>();
                    if (mechanism == null) break;

                    // Primer clic sobre un nuevo mecanismo
                    if (!isMechanismAwaitingConfirm)
                    {
                        // Si no tiene palanca
                        if (!InventorySystem.Instance.HasItemOfType(InventoryItem.ItemType.Lever))
                        {
                            mechanism.TryActivate(); // solo flash rojo
                            Debug.Log("Sin palanca: solo feedback visual.");
                            break;
                        }

                        // Guardar referencia y entrar en preactivación
                        attachedMechanism = mechanism;
                        isMechanismAwaitingConfirm = true;
                        mechanismClickTimer = mechanismClickDelay; // empieza cooldown

                        AttachToMechanism(mechanism);
                        mechanism.TryActivate();
                        Debug.Log("Entrando en preactivación del mecanismo (esperando confirmación).");
                    }
                    else
                    {
                        // Solo permitir la confirmación si ya pasó el tiempo mínimo
                        if (mechanismClickTimer <= 0f && attachedMechanism != null && attachedMechanism.IsPreActivating())
                        {
                            attachedMechanism.TryActivate();
                            ReleaseFromMechanism();
                            Debug.Log("Confirmación completada (segundo clic).");
                        }
                        else
                        {
                            Debug.Log("Clic ignorado: espera a que pase el delay visual.");
                            break;
                        }

                        isMechanismAwaitingConfirm = false;
                        attachedMechanism = null;
                    }
                    break;
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
            else if (interactable.interactableType == Interactable.InteractableType.MidObstacle)
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
            else if (interactable.interactableType == Interactable.InteractableType.MidObstacle)
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

        if (tiny != null)
        {
            TinyController tinyController = tiny.GetComponent<TinyController>();
            if (tinyController != null && tinyController.IsInvincible())
            {
                Debug.Log("Tiny es invencible: el arma se destruye sin efecto");
                Destroy(weapon.gameObject);
                isAttachedToTiny = false;
                return;
            }
        }

        powerBar.StartPowerBar(weapon.weaponType, (powerValue) =>
        {
            ApplyWeaponEffect(weapon, powerValue);
            powerBar.Hide();
        });
    }

    void HandleTinyAttachment()
    {
        if (isAttachedToTiny && tiny != null)
        {
            transform.position = tiny.transform.position + handOffsetFromTiny;
        }
    }

    private void HandleMechanismAttachment()
    {
        if (isAttachedToMechanism && attachedMechanism != null)
        {
            transform.position = attachedMechanism.transform.position + handOffsetFromMechanism;
        }
    }

    public void AttachToMechanism(TrapMechanism mechanism)
    {
        isAttachedToMechanism = true;
        attachedMechanism = mechanism;

        SpriteRenderer sr = GetComponentInChildren<SpriteRenderer>();
        if (sr != null)
        {
            Color c = sr.color; c.a = 0f; sr.color = c;
        }
    }

    public void ReleaseFromMechanism()
    {
        isAttachedToMechanism = false;
        attachedMechanism = null;

        SpriteRenderer sr = GetComponentInChildren<SpriteRenderer>();
        if (sr != null)
        {
            Color c = sr.color; c.a = 1f; sr.color = c;
        }
    }

    void ApplyWeaponEffect(Interactable weapon, float powerValue)
    {
        TinyController tinyController = tiny.GetComponent<TinyController>();
        if (tinyController == null) return;

        switch (weapon.weaponType)
        {
            case Interactable.WeaponType.FlySwatter:
                tinyController.ApplyFlySwatterEffect(powerValue);
                break;
            case Interactable.WeaponType.Bat:
                tinyController.ApplyBatEffect(powerValue);
                break;
            case Interactable.WeaponType.Wrench:
                tinyController.ApplyWrenchEffect(powerValue);
                break;
        }

        isAttachedToTiny = false;
        Destroy(weapon.gameObject);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, grabRadius);
    }
}

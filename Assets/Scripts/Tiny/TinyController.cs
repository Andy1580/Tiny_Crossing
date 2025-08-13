using System.Collections;
using UnityEngine;
using UnityEngine.UIElements;

public class TinyController : MonoBehaviour
{
    [Header("Weapon Position Reference")]
    public Transform weaponAnchorPoint;

    [Header("Movement Settings")]
    public float normalSpeed = 3f;
    public float slowSpeed = 1f;
    public float stunDuration = 2f;
    public enum StartingDirection { Right, Left }
    [Tooltip("Dirección inicial de movimiento")]
    public StartingDirection startDirection = StartingDirection.Right;

    [Header("Jump Settings")]
    [SerializeField] private float jumpForce = 8f;
    [SerializeField] private float groundCheckDistance = 0.2f;
    [SerializeField] private LayerMask groundLayer;

    [Header("Gap Detection")]
    [SerializeField] private float gapDetectionDistance = 1.5f;
    [SerializeField] private Vector2 gapCheckSize = new Vector2(0.8f, 0.1f);
    [SerializeField] private LayerMask gapTriggerLayer;
    private bool shouldJumpGap = false;

    [Header("Visual Settings")]
    [SerializeField] private Transform spriteTransform;
    [SerializeField] private float rotationSpeed = 10f;

    [Header("Debug Controls")]
    [Tooltip("Click para cambiar dirección en Play Mode")]
    [SerializeField] private bool _changeDirection;

    public bool isGrounded;
    private float currentSpeed;
    private bool isStunned = false;
    private bool isReversed;
    private bool isAlive = true;
    private Vector2 currentDirection;
    private bool isFacingRight = true;
    private Rigidbody2D rb;

    private float stuckTimer = 0f;
    private const float stuckCheckInterval = 0.3f;
    private const float stuckThreshold = 0.05f;
    private Vector2 lastPosition;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        rb.gravityScale = 3f; // Valor del código antiguo
        rb.freezeRotation = true;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

        currentSpeed = normalSpeed;
        InitializeDirection();
    }

    void Update()
    {
        if (!isAlive) return;

        CheckGrounded();
        DetectGaps();

        if (!isStunned)
        {
            HandleMovement();
            HandleSpriteRotation();
            HandleGapJump();
        }
    }

    #region JUMP
    private void DetectGaps()
    {
        if (!isGrounded) return;

        Vector2 direction = isFacingRight ? Vector2.right : Vector2.left;
        Vector2 origin = (Vector2)transform.position + direction * 0.5f;

        // Detección con BoxCast (preciso para huecos)
        RaycastHit2D hit = Physics2D.BoxCast(
            origin,
            gapCheckSize,
            0f,
            direction,
            gapDetectionDistance,
            gapTriggerLayer
        );

        shouldJumpGap = hit.collider != null && hit.collider.CompareTag("GapTrigger");

        // Debug visual
        Debug.DrawRay(origin, direction * gapDetectionDistance, shouldJumpGap ? Color.yellow : Color.white);
    }

    private void HandleGapJump()
    {
        if (shouldJumpGap && isGrounded)
        {
            TryJump();
            shouldJumpGap = false;
            Debug.Log("¡Saltando hueco detectado!");
        }
    }

    void CheckGrounded()
    {
        Vector2 boxSize = new Vector2(0.8f, 0.1f);
        Vector2 boxCenter = (Vector2)transform.position + Vector2.down * 0.6f;

        isGrounded = Physics2D.BoxCast(boxCenter, boxSize, 0f, Vector2.down, 0.1f, groundLayer);

        // Debug visual
        Debug.DrawRay(boxCenter, Vector2.down * 0.1f, isGrounded ? Color.green : Color.red);
    }

    public void TryJump()
    {
        if (isGrounded)
        {
            rb.velocity = new Vector2(rb.velocity.x, 0); // Resetear velocidad Y
            rb.AddForce(Vector2.up * jumpForce, ForceMode2D.Impulse);
            Debug.Log("Tiny saltó! Fuerza: " + jumpForce);
        }
        else
        {
            Debug.Log("Tiny no puede saltar: No está en el suelo");
        }
    }
    #endregion JUMP

    #region MOVEMENT
    void InitializeDirection()
    {
        currentDirection = startDirection == StartingDirection.Right ? Vector2.right : Vector2.left;
        isFacingRight = startDirection == StartingDirection.Right;
        UpdateSpriteRotation();
    }


    void HandleMovement()
    {
        if (!isGrounded) return;

        float effectiveDirection = isReversed ? -currentDirection.x : currentDirection.x;

        // Usar velocidad física en lugar de Translate
        rb.velocity = new Vector2(effectiveDirection * currentSpeed, rb.velocity.y);

        // Sistema anti-atascos del código antiguo
        CheckStuckLogic();

        Debug.DrawRay(transform.position, currentDirection * 0.5f, Color.red); // Debug: dirección actual
    }

    void HandleSpriteRotation()
    {
        float targetRotation = 0f;

        if (isReversed)
        {
            targetRotation = currentDirection.x > 0 ? 180f : 0f;
        }
        else
        {
            targetRotation = currentDirection.x > 0 ? 0f : 180f;
        }

        Quaternion newRotation = Quaternion.Euler(0, targetRotation, 0);
        spriteTransform.rotation = Quaternion.Lerp(
            spriteTransform.rotation,
            newRotation,
            rotationSpeed * Time.deltaTime
        );
    }

    private void OnValidate()
    {
        if (Application.isPlaying && _changeDirection)
        {
            _changeDirection = false;
            ToggleDirection();
        }
    }

    // Método público para pruebas (puedes llamarlo desde otros scripts o eventos de Unity)
    public void ToggleDirection()
    {
        currentDirection *= -1;
        isFacingRight = !isFacingRight;
        UpdateSpriteRotation();
        Debug.Log($"Dirección cambiada a: {(isFacingRight ? "Derecha" : "Izquierda")}");
    }

    void CheckStuckLogic()
    {
        stuckTimer += Time.deltaTime;

        if (stuckTimer >= stuckCheckInterval)
        {
            float moved = Vector2.Distance(transform.position, lastPosition);

            if (moved < stuckThreshold && isGrounded)
            {
                Debug.Log("Tiny atascado. Aplicando mini-salto.");
                rb.velocity = new Vector2(rb.velocity.x, jumpForce * 0.3f);
            }

            lastPosition = transform.position;
            stuckTimer = 0f;
        }
    }
    #endregion MOVEMENT

    // ===== EFECTOS DE ARMAS (MANTENIDOS CON DEBUGS) =====
    private float GetEffectDuration(float power)
    {
        if (power < 0.4f) return 1f;       // Rango bajo
        if (power < 0.8f) return 2.5f;     // Rango medio
        return 4f;                         // Rango alto
    }

    public void ApplyFlySwatterEffect(float power)
    {
        Debug.Log($"FlySwatter aplicado! Poder: {power}. Inversión de dirección.");
        isReversed = true;

        float effectDuration = GetEffectDuration(power);
        Invoke("ResetDirection", effectDuration);
    }

    public void ApplyBatEffect(float power)
    {
        Debug.Log($"Bat aplicado! Poder: {power}. Reducción de velocidad.");
        currentSpeed = Mathf.Lerp(normalSpeed, slowSpeed, power);

        float effectDuration = GetEffectDuration(power);
        Invoke("ResetSpeed", effectDuration);

        if (power >= 0.8f)
        {
            Debug.Log("Golpe crítico con bate!");
            Die();
        }
    }

    public void ApplyWrenchEffect(float power)
    {
        Debug.Log($"Wrench aplicado! Poder: {power}. Aturdimiento.");
        isStunned = true;

        float effectDuration = GetEffectDuration(power);
        Invoke("EndStun", effectDuration);

        if (power >= 0.8f)
        {
            Debug.Log("Golpe crítico con llave inglesa!");
            Die();
        }
    }

    void ResetDirection()
    {
        Debug.Log("Dirección de Tiny restaurada");
        isReversed = false;
    }

    void ResetSpeed()
    {
        Debug.Log("Velocidad de Tiny restaurada a " + normalSpeed);
        currentSpeed = normalSpeed;
    }

    void EndStun()
    {
        Debug.Log("Tiny ya no está aturdido");
        isStunned = false;
    }

    void Die()
    {
        Debug.Log("Tiny ha muerto!");
        isAlive = false;
        // Aquí tu lógica para reiniciar nivel o mostrar Game Over
    }

    // Método auxiliar para actualizar rotación
    void UpdateSpriteRotation()
    {
        if (spriteTransform == null) return;

        float targetRotation = (isReversed ^ !isFacingRight) ? 180f : 0f;
        spriteTransform.rotation = Quaternion.Euler(0, targetRotation, 0);
    }
}

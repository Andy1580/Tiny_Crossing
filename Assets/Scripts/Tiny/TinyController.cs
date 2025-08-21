using System.Collections.Generic;
using System.Linq;
using UnityEngine;

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

    [Header("Obstacle Detection")]
    [SerializeField] private float obstacleDetectionRange = 1.5f;
    [SerializeField] private Vector2 obstacleOverlapSize = new Vector2(0.8f, 0.8f);
    [SerializeField] private LayerMask interactableLayer; // Asigna la layer "Interactable" en el Inspector
    private bool isObstacleBlocking = false;

    [Header("Obstacle Reaction")]
    [SerializeField] private float obstacleStopTime = 2f; // Tiempo que espera tras chocar
    private float obstacleStopTimer = 0f;
    private bool isWaitingAfterObstacle = false;

    [Header("Mid Obstacle Jump")]
    [SerializeField] private float midObstacleJumpForce = 6f;
    [SerializeField] private float midObstacleCheckHeight = 0.5f;

    [Header("Goal Settings")]
    [SerializeField] private float goalReachedDistance = 0.5f; // Distancia para considerar que llegó
    [SerializeField] private float goalSlowDownDistance = 2f; // Distancia para comenzar a frenar
    [SerializeField] private Transform goal;
    private bool hasReachedGoal = false;

    [Header("Platform Planning")]
    [SerializeField] private float lookAheadDistance = 10f; // Rango para escanear plataformas
    private bool goalIsElevated = false;
    private List<Vector2> platformPositions = new List<Vector2>();

    [Header("Multi-Platform Jump")]
    [SerializeField] private float minGapWidth = 0.5f; // Ancho mínimo de hueco para evitar caída
    [SerializeField] private float platformSequenceLookAhead = 8f; // Rango extendido para planificación
    [SerializeField] private float minHeightToJump = 2f; // Altura mínima para considerar salto
    [SerializeField] private float maxJumpHeight = 4f; // Máximo que Tiny puede saltar
    [SerializeField] private float platformForwardOffset = 2f; // Margen para saltar ANTES de la plataforma
    private bool isOnPlatformSequence = false;
    private Vector2? nextPlatformTarget = null;
    private bool shouldJumpToPlatform = false;
    private float initialGroundHeight;

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

    #region CORE
    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        rb.gravityScale = 3f; // Valor del código antiguo
        rb.freezeRotation = true;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

        currentSpeed = normalSpeed;
        initialGroundHeight = transform.position.y;
        InitializeDirection();
    }

    void Update()
    {
        if (!isAlive || hasReachedGoal) return; // No hacer nada si llegó a la meta

        CheckGrounded();
        ScanForPlatforms();
        CheckGoalProximity(); // Escaneo temprano cada frame
        if (isOnPlatformSequence && nextPlatformTarget.HasValue)
        {
            Debug.DrawLine(transform.position, nextPlatformTarget.Value, Color.magenta);
            Debug.Log("Secuencia de plataformas detectada. Objetivo: " + nextPlatformTarget.Value);
        }
        DetectGaps();
        DetectObstacles();
        CheckPlatformsAndGoal();
        Vector2 direction = isFacingRight ? Vector2.right : Vector2.left;
        Debug.DrawRay(transform.position + (Vector3)(direction * 0.5f), Vector2.down * 1f, Color.blue);

        if (!isStunned && !isWaitingAfterObstacle)
        {
            if (shouldJumpToPlatform)
            {
                // El salto ya se ejecutó en CheckPlatformsAndGoal()
                // No mover para evitar conflicto con la física del salto
            }
            else if (isWaitingAfterObstacle)
            {
                obstacleStopTimer -= Time.deltaTime;
                if (obstacleStopTimer <= 0f)
                {
                    isWaitingAfterObstacle = false;
                    ToggleDirection();
                }
            }
            else
            {
                HandleMovement(); // Movimiento normal
            }
        }

        HandleSpriteRotation();
        HandleGapJump();

        if (goal != null)
        {
            Debug.DrawLine(transform.position, goal.position, goalIsElevated ? Color.red : Color.yellow);
        }

        Debug.Log($"shouldJumpToPlatform: {shouldJumpToPlatform} | isGrounded: {isGrounded}");

        if (goal != null)
        {
            // Círculo de distancia de llegada
            Debug.DrawLine(goal.position - Vector3.right * goalReachedDistance,
                           goal.position + Vector3.right * goalReachedDistance,
                           hasReachedGoal ? Color.green : Color.yellow);

            // Círculo de distancia de frenado
            Debug.DrawLine(goal.position - Vector3.right * goalSlowDownDistance,
                           goal.position + Vector3.right * goalSlowDownDistance,
                           Color.blue);
        }
    }
    #endregion CORE

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
            //Debug.Log("¡Saltando hueco detectado!");
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

    public void TryJump(float customForce = 0f)
    {
        if (isGrounded)
        {
            float force = customForce > 0 ? customForce : jumpForce;
            rb.velocity = new Vector2(rb.velocity.x, 0);
            rb.AddForce(Vector2.up * force, ForceMode2D.Impulse);
            //Debug.Log("Tiny saltó! Fuerza: " + jumpForce);
        }
        else
        {
            Debug.Log("Tiny no puede saltar: No está en el suelo");
        }
    }

    private float CalculateJumpForce(float height)
    {
        // Fórmula física simplificada: v = sqrt(2 * g * h)
        float gravity = Mathf.Abs(Physics2D.gravity.y) * rb.gravityScale;
        return Mathf.Sqrt(2 * gravity * height) * 1.1f; // +10% de margen
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
        if (!isGrounded || hasReachedGoal) return;

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

    // Método auxiliar para actualizar rotación
    void UpdateSpriteRotation()
    {
        if (spriteTransform == null) return;

        float targetRotation = (isReversed ^ !isFacingRight) ? 180f : 0f;
        spriteTransform.rotation = Quaternion.Euler(0, targetRotation, 0);
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

    #region PLATFORM DETECTION
    private void ScanForPlatforms()
    {
        platformPositions.Clear();

        // DETECCIÓN MEJORADA - Usar el máximo entre altura actual e inicial
        float referenceHeight = Mathf.Max(transform.position.y, initialGroundHeight);
        goalIsElevated = goal != null && goal.position.y > referenceHeight + minHeightToJump;

        if (!goalIsElevated)
        {
            Debug.Log("Meta NO elevada. Altura meta: " + goal.position.y + " vs Referencia: " + (referenceHeight + minHeightToJump));
            return;
        }

        Debug.Log("Meta elevada detectada. Saltando secuencia...");

        // Escaneo desde el suelo hacia adelante (sistema anterior)
        Vector2 scanOrigin = (Vector2)transform.position + Vector2.up * 0.1f;
        float scanDirection = isFacingRight ? 1f : -1f;

        for (float x = 0; x <= lookAheadDistance; x += 0.5f)
        {
            Vector2 checkPos = scanOrigin + new Vector2(x * scanDirection, 0);

            // Raycast hacia ABAJO para encontrar plataformas
            RaycastHit2D hit = Physics2D.Raycast(
                checkPos + Vector2.up * maxJumpHeight,
                Vector2.down,
                maxJumpHeight + 1f,
                groundLayer
            );

            if (hit.collider != null && hit.point.y > checkPos.y + minHeightToJump)
            {
                platformPositions.Add(hit.point);
                Debug.DrawLine(checkPos, hit.point, Color.green, 0.1f);
            }
        }
    }

    private bool CheckPlatformSequence()
    {
        // Verificar si hay plataformas consecutivas hacia la meta
        for (int i = 0; i < platformPositions.Count - 1; i++)
        {
            float gapWidth = Mathf.Abs(platformPositions[i + 1].x - platformPositions[i].x);
            if (gapWidth > minGapWidth) // Hay un hueco significativo
            {
                nextPlatformTarget = platformPositions[i + 1];
                return true;
            }
        }
        return false;
    }

    private void CheckPlatformsAndGoal()
    {
        /*
        shouldJumpToPlatform = false; // Resetear cada frame

        if (goal == null) return;

        Vector2 direction = isFacingRight ? Vector2.right : Vector2.left;
        Vector2 origin = (Vector2)transform.position + direction * platformForwardOffset;

        // 1. Calcular diferencia de altura con la meta
        float goalHeightDiff = goal.position.y - transform.position.y;

        // 2. Solo saltar si la meta está arriba y alcanzable
        if (goalHeightDiff > minHeightToJump && goalHeightDiff <= maxJumpHeight)
        {
            RaycastHit2D[] hits = Physics2D.RaycastAll(
                origin,
                Vector2.up,
                Mathf.Clamp(goalHeightDiff, minHeightToJump, maxJumpHeight),
                groundLayer
            );

            // 5. Saltar la plataforma más baja que lleve a la meta
            foreach (RaycastHit2D hit in hits.OrderBy(h => h.point.y))
            {
                float platformHeight = hit.point.y - transform.position.y;
                if (platformHeight > minHeightToJump && isGrounded)
                {
                    shouldJumpToPlatform = true; // Activar el salto
                    TryJump(CalculateJumpForce(platformHeight));
                    break;
                }
            }
        }

        // Debug visual
        Debug.DrawRay(origin, Vector2.up * maxJumpHeight, Color.cyan);
        */
        shouldJumpToPlatform = false;
        if (!goalIsElevated || platformPositions.Count == 0) return;

        Vector2 currentPos = transform.position;

        // 1. PRIMERO: Verificar si hay un hueco adelante que requiera salto anticipado
        CheckForGapAhead(currentPos);

        // 2. SEGUNDO: Lógica original de salto a plataformas
        Vector2? nearestPlatform = null;
        float minDistance = float.MaxValue;

        foreach (var platform in platformPositions)
        {
            float distance = Mathf.Abs(platform.x - currentPos.x);
            bool inCorrectDirection = (isFacingRight && platform.x > currentPos.x) ||
                                    (!isFacingRight && platform.x < currentPos.x);

            if (inCorrectDirection && distance < minDistance && distance < 2f)
            {
                minDistance = distance;
                nearestPlatform = platform;
            }
        }

        if (nearestPlatform.HasValue && isGrounded)
        {
            float heightDiff = nearestPlatform.Value.y - currentPos.y;
            if (heightDiff <= maxJumpHeight && heightDiff >= minHeightToJump)
            {
                shouldJumpToPlatform = true;
                TryJump(CalculateJumpForce(heightDiff));
                Debug.Log($"Saltando a plataforma ({nearestPlatform.Value.x}, {nearestPlatform.Value.y})");
            }
        }
    }

    private void CheckForGapAhead(Vector2 currentPos)
    {
        // Detectar huecos entre plataformas
        float checkDistance = 0.5f; // Distancia para detectar huecos
        Vector2 direction = isFacingRight ? Vector2.right : Vector2.left;

        // Verificar si hay suelo adelante
        RaycastHit2D groundAhead = Physics2D.Raycast(
            currentPos + direction * 0.5f,
            Vector2.down,
            1f,
            groundLayer
        );

        // Si NO hay suelo adelante pero SÍ hay una plataforma después
        if (groundAhead.collider == null)
        {
            foreach (var platform in platformPositions)
            {
                float platformDistance = Mathf.Abs(platform.x - currentPos.x);
                if (platformDistance > checkDistance && platformDistance < 4f)
                {
                    // Saltar anticipadamente para evitar caer
                    float heightDiff = platform.y - currentPos.y;
                    if (heightDiff <= maxJumpHeight && isGrounded)
                    {
                        shouldJumpToPlatform = true;
                        TryJump(CalculateJumpForce(heightDiff));
                        Debug.Log("Saltando anticipadamente para evitar hueco");
                        break;
                    }
                }
            }
        }
    }

    private float GetNextPlatformHeight()
    {
        foreach (var platform in platformPositions.OrderBy(p => p.x))
        {
            if ((isFacingRight && platform.x > transform.position.x) ||
                (!isFacingRight && platform.x < transform.position.x))
            {
                return platform.y;
            }
        }
        return 0f;
    }
    #endregion PLATFORM DETECTION

    #region OBSTACLE DETECTION
    private void DetectObstacles()
    {
        Vector2 direction = isFacingRight ? Vector2.right : Vector2.left;
        Vector2 origin = (Vector2)transform.position + direction * 0.5f;

        // 1. OverlapBox en la layer "Interactable"
        Collider2D[] hits = Physics2D.OverlapBoxAll(
            origin + direction * (obstacleDetectionRange * 0.5f),
            obstacleOverlapSize,
            0f,
            interactableLayer
        );

        bool obstacleFound = false;
        foreach (Collider2D hit in hits)
        {
            if (hit.isTrigger) continue;

            Interactable interactable = hit.GetComponent<Interactable>();
            if (interactable == null) continue;

            // ------- Reacción a Obstáculos Normales -------
            if (interactable.interactableType == Interactable.InteractableType.Obstacle)
            {
                obstacleFound = true;
                break;
            }
            // ------- Reacción a MidObstacle (salto) -------
            else if (interactable.interactableType == Interactable.InteractableType.MidObstacle && isGrounded)
            {
                TryJump(midObstacleJumpForce); // Salto con fuerza ajustada
                Debug.Log("¡Saltando MidObstacle!");
            }
        }

        // 2. Lógica de reacción (igual que antes)
        if (obstacleFound && !isWaitingAfterObstacle)
        {
            isObstacleBlocking = true;
            isWaitingAfterObstacle = true;
            obstacleStopTimer = obstacleStopTime;
            rb.velocity = Vector2.zero;
            Debug.Log("Obstáculo detectado (sin empujar)");
        }
        else if (!obstacleFound)
        {
            isObstacleBlocking = false;
        }

        // Debug visual
        Debug.DrawLine(origin, origin + direction * obstacleDetectionRange, obstacleFound ? Color.red : Color.green);
    }
    #endregion OBSTACLE DETECTION

    #region WEAPON EFFECTS
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
    #endregion WEAPON EFFECTS

    private void CheckGoalProximity()
    {
        if (goal == null || hasReachedGoal) return;

        float distanceToGoal = Vector2.Distance(transform.position, goal.position);

        // Debug de distancia
        Debug.Log($"Distancia a meta: {distanceToGoal}");

        if (distanceToGoal <= goalReachedDistance)
        {
            // Llegó a la meta
            hasReachedGoal = true;
            rb.velocity = Vector2.zero;
            Debug.Log("¡Tiny llegó a la meta!");
        }
        else if (distanceToGoal <= goalSlowDownDistance)
        {
            // Frenar al acercarse a la meta
            float speedFactor = Mathf.Clamp01(distanceToGoal / goalSlowDownDistance);
            currentSpeed = normalSpeed * speedFactor;
        }
        else
        {
            currentSpeed = normalSpeed;
        }
    }

    void Die()
    {
        Debug.Log("Tiny ha muerto!");
        isAlive = false;
        // Aquí tu lógica para reiniciar nivel o mostrar Game Over
    }

}

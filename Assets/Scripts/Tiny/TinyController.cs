using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using static Interactable;

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

    [Header("Speed Modifiers")]
    private float speedDebuffFactor = 1f;     // bate
    private float goalProximityFactor = 1f;   // meta

    [Header("Jump Settings")]
    [SerializeField] private float jumpForce = 8f;
    [SerializeField] private float groundCheckDistance = 0.2f;
    [SerializeField] private LayerMask groundLayer;

    // === Trap jump queue ===
    [SerializeField] private float trapJumpGraceWindow = 0.15f; // ventana de gracia (seg)
    private float trapJumpTimer = 0f;
    private float queuedTrapJumpForce = 0f;

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

    [Header("Checkpoint System")]
    private CheckPoint currentCheckpoint;
    private Vector3 initialSpawnPosition;

    [Header("PowerUp Core")]
    [SerializeField] private float powerUpDetectionRange = 3f;
    [SerializeField] private LayerMask powerUpLayer;
    private List<Interactable> detectedPowerUps = new List<Interactable>();
    private Interactable currentTargetPowerUp = null;
    private bool isInvisible = false;
    private bool isInvincible = false;

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

    // --- Ruta por hints ---
    private JumpHints activeHint = null;
    private float navTargetX = 0f;
    private bool movingToHint = false;
    private bool jumpingViaHint = false;
    private bool awaitingHintLanding = false;
    private float postLandingDelay = 0.3f;
    private float landingTimer = 0f;
    private bool hintJumpQueued = false;

    private Coroutine invincibilityRoutine;
    private bool isSearchingAlternateRoute = false;
    private Vector2? retreatTarget = null;
    private float retreatDirX = 0f;          // +1 derecha, -1 izquierda (dirección de RETORNO fija)
    private float retreatFallbackTimer = 0f;  // pequeño tiempo de retroceso si no hay target


    #region CORE
    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        currentSpeed = normalSpeed;
        initialGroundHeight = transform.position.y;
        InitializeDirection();
        initialSpawnPosition = transform.position;
        InitializeCheckpointSystem();
    }

    void Update()
    {
        if (!isAlive || hasReachedGoal) return;

        // 1) Sensores base
        CheckGrounded();

        // Ejecutar salto de trampa si está en ventana de gracia
        if (trapJumpTimer > 0f)
        {
            trapJumpTimer -= Time.deltaTime;

            // si tocamos suelo durante la ventana -> saltamos
            if (isGrounded)
            {
                float f = queuedTrapJumpForce > 0f ? queuedTrapJumpForce : midObstacleJumpForce;
                Debug.Log("[Tiny] Ejecutando salto por TrapWarning");
                TryJump(f);
                queuedTrapJumpForce = 0f;
                trapJumpTimer = 0f;
            }
        }

        CheckGoalProximity();
        DetectPowerUps();
        ScanForPlatforms();

        // 2) Entorno (se calcula SIEMPRE antes de decidir)
        DetectObstacles();
        DetectGaps();                // <--- MUY IMPORTANTE
        CheckPlatformsAndGoal();

        // 3) Navegación por Hint (no cortes sin dejar saltar gaps)
        if (movingToHint)
        {
            float dx = navTargetX - transform.position.x;
            float dirX = Mathf.Sign(dx);
            rb.velocity = new Vector2(dirX * currentSpeed, rb.velocity.y);

            // permite saltar huecos mientras navega al hint
            HandleGapJump();

            if (Mathf.Abs(dx) < 0.12f && isGrounded)
            {
                rb.velocity = new Vector2(0f, rb.velocity.y);
                movingToHint = false;
                jumpingViaHint = true;

                bool faceRightToJump = activeHint != null && activeHint.jumpTowardsRight;
                if (faceRightToJump != isFacingRight) ToggleDirection();
            }
            return; // durante la navegación no ejecutamos la IA normal
        }

        if (jumpingViaHint && activeHint != null)
        {
            float height = activeHint.landing.position.y - transform.position.y;

            if (height <= maxJumpHeight)
            {
                // Marcar que queremos saltar tan pronto toquemos el suelo
                hintJumpQueued = true;
            }

            jumpingViaHint = false;
        }

        if (hintJumpQueued && isGrounded && activeHint != null)
        {
            float height = activeHint.landing.position.y - transform.position.y;
            TryJump(CalculateJumpForce(height));
            awaitingHintLanding = true;
            hintJumpQueued = false;

            Debug.Log("Tiny ha ejecutado el salto hacia el Landing del Hint.");
        }

        if (awaitingHintLanding && activeHint != null)
        {
            float distToLanding = Vector2.Distance(transform.position, activeHint.landing.position);

            if (distToLanding < 0.5f && isGrounded)
            {
                // Ya llegó al Landing
                awaitingHintLanding = false;
                landingTimer = postLandingDelay;
                Debug.Log("Tiny aterrizó en el Landing del Hint. Preparando reanudación...");
            }
        }

        if (landingTimer > 0f)
        {
            landingTimer -= Time.deltaTime;
            rb.velocity = new Vector2(0f, rb.velocity.y); // Mantiene posición

            if (landingTimer <= 0f)
            {
                activeHint = null; // Limpieza completa del hint
                ResumeMovementAfterStop(); // Reanuda movimiento con velocidad normal
                Debug.Log("Reanudando navegación tras Landing.");
            }

            return; // Durante el delay, no ejecutar IA normal
        }

        // 4) Espera por obstáculo
        if (isWaitingAfterObstacle)
        {
            obstacleStopTimer -= Time.deltaTime;
            rb.velocity = new Vector2(0f, rb.velocity.y);

            if (obstacleStopTimer <= 0f)
            {
                isWaitingAfterObstacle = false;
                ToggleDirection();           // gira
                shouldJumpGap = false;       // limpia estado de gap
                rb.velocity = new Vector2(currentDirection.x * currentSpeed, rb.velocity.y);
                rb.WakeUp();

                if (!PlanAlternateRouteWithHint())
                {
                    isSearchingAlternateRoute = true; // (si usas el fallback)
                    retreatDirX = isFacingRight ? 1f : -1f;
                    retreatTarget = FindRetreatPoint(retreatDirX);
                    retreatFallbackTimer = retreatTarget.HasValue ? 0f : 1.0f;
                }
            }
            return; // no seguimos con IA normal durante la espera
        }

        // 5) IA normal
        if (!isStunned)
        {
            if (ShouldPrioritizePowerUp())
            {
                if (ShouldJumpForPowerUp() && isGrounded)
                {
                    Vector2 targetPlatform = platformPositions
                        .OrderBy(p => Vector2.Distance(p, currentTargetPowerUp.transform.position)).First();
                    float heightDiff = targetPlatform.y - transform.position.y;
                    TryJump(CalculateJumpForce(heightDiff));
                }
                else HandlePowerUpBehavior();
            }
            else if (shouldJumpToPlatform)
            {
                // el salto ya se ejecutó en CheckPlatformsAndGoal()
            }
            else HandleMovement();
        }

        // 6) Gaps y recolecciones (por si no estás en movingToHint)
        HandleGapJump();
        CheckPowerUpCollection();

        // 7) Visual
        HandleSpriteRotation();

        //if (goal != null)
        //{
        //    Debug.DrawLine(transform.position, goal.position, goalIsElevated ? Color.red : Color.yellow);
        //}

        ////Debug.Log($"shouldJumpToPlatform: {shouldJumpToPlatform} | isGrounded: {isGrounded}");

        //if (goal != null)
        //{
        //    // Círculo de distancia de llegada
        //    Debug.DrawLine(goal.position - Vector3.right * goalReachedDistance,
        //                   goal.position + Vector3.right * goalReachedDistance,
        //                   hasReachedGoal ? Color.green : Color.yellow);

        //    // Círculo de distancia de frenado
        //    Debug.DrawLine(goal.position - Vector3.right * goalSlowDownDistance,
        //                   goal.position + Vector3.right * goalSlowDownDistance,
        //                   Color.blue);
        //}
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
        // Si lo haces manual, cancela navegación guiada
        movingToHint = false;
        jumpingViaHint = false;

        currentDirection *= -1;
        isFacingRight = !isFacingRight;
        UpdateSpriteRotation();

        // sincroniza física inmediatamente
        if (isAlive)
        {
            rb.velocity = new Vector2(currentDirection.x * currentSpeed, rb.velocity.y);
            rb.WakeUp();
        }

        Debug.Log($"Dirección cambiada a: {(isFacingRight ? "Derecha" : "Izquierda")}");


        /*
        //Forzar sincronización física inmediata
        if (isGrounded && isAlive)
        {
            float dirX = currentDirection.x;
            rb.velocity = new Vector2(dirX * normalSpeed, rb.velocity.y);
            rb.WakeUp(); // asegura que el cuerpo vuelva a simular
        }

        Debug.Log($"Dirección cambiada a: {(isFacingRight ? "Derecha" : "Izquierda")}");
        */
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

    private void ResumeMovementAfterStop()
    {
        if (!isAlive) return;

        rb.velocity = new Vector2(currentDirection.x * currentSpeed, rb.velocity.y);
        rb.WakeUp();
    }

    // --- NAV HINT MOVEMENT ---
    private void HandleHintNavigation()
    {
        float dx = navTargetX - transform.position.x;
        float dirX = Mathf.Sign(dx);
        rb.velocity = new Vector2(dirX * currentSpeed, rb.velocity.y);

        if (Mathf.Abs(dx) < 0.12f && isGrounded)
        {
            rb.velocity = new Vector2(0f, rb.velocity.y);
            movingToHint = false;
            jumpingViaHint = true;

            bool faceRightToJump = activeHint != null && activeHint.jumpTowardsRight;
            if (faceRightToJump != isFacingRight) ToggleDirection();
        }
    }

    private void HandleHintJump()
    {
        float height = activeHint.landing.position.y - transform.position.y;
        if (height <= maxJumpHeight && isGrounded)
        {
            TryJump(CalculateJumpForce(height));
        }

        jumpingViaHint = false;
        activeHint = null;
    }

    // --- OBSTÁCULO WAIT ---
    private void HandleObstacleWait()
    {
        obstacleStopTimer -= Time.deltaTime;
        rb.velocity = new Vector2(0f, rb.velocity.y);

        if (obstacleStopTimer <= 0f)
        {
            isWaitingAfterObstacle = false;
            ToggleDirection();
            shouldJumpGap = false;
            ResumeMovementAfterStop();

            if (!PlanAlternateRouteWithHint())
            {
                isSearchingAlternateRoute = true;
                retreatDirX = isFacingRight ? 1f : -1f;
                retreatTarget = FindRetreatPoint(retreatDirX);
                retreatFallbackTimer = retreatTarget.HasValue ? 0f : 1.0f;
            }
        }
    }

    // --- DEBUG ---
    private void DrawDebugHelpers()
    {
        if (goal == null) return;
        Debug.DrawLine(transform.position, goal.position, goalIsElevated ? Color.red : Color.yellow);
    }
    #endregion MOVEMENT

    #region PLATFORM DETECTION
    private void ScanForPlatforms()
    {
        platformPositions.Clear();

        // DETECCIÓN MEJORADA - Siempre detectar plataformas si hay power-ups interesantes
        bool interestingPowerUpNearby = detectedPowerUps.Any(p =>
            p != null && p.CanInteract &&
            Vector2.Distance(transform.position, p.transform.position) < 5f);

        float referenceHeight = Mathf.Max(transform.position.y, initialGroundHeight);
        goalIsElevated = goal != null && goal.position.y > referenceHeight + minHeightToJump;

        // SIEMPRE escanear plataformas si hay power-ups interesantes o meta elevada
        if (!goalIsElevated && !interestingPowerUpNearby)
        {
            //Debug.Log("Meta NO elevada. Altura meta: " + goal.position.y + " vs Referencia: " + (referenceHeight + minHeightToJump));
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
        // No detectar durante navegación/cola de salto por hint
        if (movingToHint || jumpingViaHint || hintJumpQueued || awaitingHintLanding) return;

        // Invisibilidad: ignora todo
        if (isInvisible) return;

        Vector2 direction = isFacingRight ? Vector2.right : Vector2.left;
        Vector2 origin = (Vector2)transform.position + direction * 0.5f;
        /*
        // ============================
        // PASO 1) Detección de TRAMPAS
        // ============================
        // Caja MÁS ALTA y un poco MÁS BAJA respecto al centro para atrapar spikes enterrados.
        Vector2 trapBoxSize = new Vector2(obstacleOverlapSize.x, obstacleOverlapSize.y + 0.6f);
        Vector2 trapBoxCenter = origin + direction * (obstacleDetectionRange * 0.5f) + Vector2.down * 0.25f;

        // Capa combinada por si algún pico quedó en Default.
        int trapLayerMask = interactableLayer | LayerMask.GetMask("Default");

        Collider2D[] trapHits = Physics2D.OverlapBoxAll(
            trapBoxCenter,
            trapBoxSize,
            0f,
            trapLayerMask
        );

        // Debug visual de la caja de trampas
        DrawWireBox(trapBoxCenter, trapBoxSize, Color.cyan);

        foreach (var h in trapHits)
        {
            // Buscar TrapController en el mismo objeto o en el padre
            TrapController trap = h.GetComponent<TrapController>();
            if (trap == null && h.transform.parent != null)
                trap = h.transform.parent.GetComponent<TrapController>();

            if (trap != null)
            {
                // NO descartes triggers aquí: los spikes usan trigger (KillZone)
                bool trapActive = trap.IsTrapActive();
                Debug.Log($"Trap detectada: {h.name}, Activa={trapActive}");

                if (trapActive && isGrounded && !isStunned)
                {
                    Debug.Log($"Tiny salta por trampa activa: {h.name}");
                    TryJump(midObstacleJumpForce * 1.2f); // pequeño boost por seguridad
                    break; // evita múltiples saltos en un mismo frame
                }
            }
            Debug.Log($"TrapHit: {h.name} | layer={LayerMask.LayerToName(h.gameObject.layer)} | trigger={h.isTrigger}");
        }
        */
        // =========================================
        // PASO 2) Obstáculos normales / midObstacle
        // =========================================
        Collider2D[] hits = Physics2D.OverlapBoxAll(
            origin + direction * (obstacleDetectionRange * 0.5f),
            obstacleOverlapSize,
            0f,
            interactableLayer
        );

        bool obstacleFound = false;

        foreach (Collider2D hit in hits)
        {
            // Para OBSTÁCULOS normales y MID, sí filtramos triggers:
            if (hit.isTrigger) continue;

            Interactable interactable = hit.GetComponent<Interactable>();
            if (interactable == null) continue;

            if (interactable.interactableType == Interactable.InteractableType.Obstacle)
            {
                obstacleFound = true;
                break;
            }
            else if (interactable.interactableType == Interactable.InteractableType.MidObstacle && isGrounded)
            {
                Debug.Log("¡Saltando MidObstacle!");
                TryJump(midObstacleJumpForce);
            }
        }

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

        // Línea de debug del rango horizontal original
        Debug.DrawLine(origin, origin + direction * obstacleDetectionRange, obstacleFound ? Color.red : Color.green);
    }

    // Helper para dibujar una caja en escena (debug visual)
    private void DrawWireBox(Vector2 center, Vector2 size, Color color)
    {
        Vector2 half = size * 0.5f;
        Vector2 a = center + new Vector2(-half.x, -half.y);
        Vector2 b = center + new Vector2(-half.x, half.y);
        Vector2 c = center + new Vector2(half.x, half.y);
        Vector2 d = center + new Vector2(half.x, -half.y);

        Debug.DrawLine(a, b, color);
        Debug.DrawLine(b, c, color);
        Debug.DrawLine(c, d, color);
        Debug.DrawLine(d, a, color);
    }

    private bool PlanAlternateRouteWithHint()
    {
        // Queremos saltar hacia el lado del obstáculo (opuesto a la dirección de retreat).
        // Si acabas de bloquearte mirando a la derecha, tras el Toggle estarás mirando a la IZQUIERDA:
        // queremos un hint que SALTE hacia la DERECHA.
        bool wantJumpRight = !isFacingRight; // tras ToggleDirection()

        var hints = FindObjectsByType<JumpHints>(FindObjectsSortMode.None);
        if (hints == null || hints.Length == 0) return false;

        JumpHints best = null;
        float bestScore = float.MaxValue;

        foreach (var h in hints)
        {
            if (h == null || h.landing == null) continue;
            if (h.jumpTowardsRight != wantJumpRight) continue;

            // Queremos un hint que esté "detrás" respecto a donde estaba el obstáculo.
            // Si ahora miras a la izquierda (retreat), el hint debe estar a tu IZQUIERDA (x menor).
            bool hintIsBehind = isFacingRight ? (h.transform.position.x > transform.position.x)
                                              : (h.transform.position.x < transform.position.x);
            if (!hintIsBehind) continue;

            float dx = Mathf.Abs(h.transform.position.x - transform.position.x);
            float dy = Mathf.Abs(h.landing.position.y - transform.position.y);
            // usa una heurística simple: prioriza cercano en X y con altura razonable
            float score = dx + dy * 0.5f;

            if (score < bestScore)
            {
                bestScore = score;
                best = h;
            }
        }

        if (best == null) return false;

        activeHint = best;
        navTargetX = activeHint.transform.position.x;
        movingToHint = true;
        jumpingViaHint = false;

        // Asegura orientación para acercarte al hint
        bool shouldFaceRight = navTargetX > transform.position.x;
        if (shouldFaceRight != isFacingRight) ToggleDirection();

        Debug.Log($"[Hints] Plan: ir a hint {activeHint.name}, saltar hacia {(activeHint.jumpTowardsRight ? "derecha" : "izquierda")}");
        return true;
    }

    // === BUSCAR PUNTO SEGURO DETRÁS ===
    private Vector2? FindRetreatPoint(float dirX)
    {
        // dirX = +1 derecha, -1 izquierda (ya fijada al iniciar el retreat)
        Vector2 dirVec = new Vector2(dirX, 0f);

        float step = 0.5f;       // pasos de escaneo
        float maxDistance = 8f;  // hasta dónde retroceder como máximo
        float probeHeight = 1.5f;
        float maxDown = 3f;

        Vector2 start = transform.position;

        for (float d = 1f; d <= maxDistance; d += step)
        {
            // Escanear HACIA DONDE VAMOS A RETROCEDER
            Vector2 probePos = start + dirVec * d;

            // 1) Suelo bajo el punto probado
            RaycastHit2D down = Physics2D.Raycast(
                probePos + Vector2.up * probeHeight,
                Vector2.down,
                maxDown,
                groundLayer
            );
            if (down.collider == null) continue;

            // 2) Evitar bordes: debe haber suelo un poco adelante y un poco atrás del punto
            bool groundAhead = Physics2D.Raycast(
                probePos + dirVec * 0.4f + Vector2.up * 0.1f,
                Vector2.down, 1f, groundLayer
            );
            bool groundBehind = Physics2D.Raycast(
                probePos - dirVec * 0.4f + Vector2.up * 0.1f,
                Vector2.down, 1f, groundLayer
            );
            if (!groundAhead || !groundBehind) continue;

            // Punto válido
            Debug.DrawLine(start, down.point, Color.cyan, 1f);
            return down.point + Vector2.up * 0.1f;
        }

        return null; // no se encontró nada
    }

    public bool IsInvisible() => isInvisible;
    public float GetMidObstacleJumpForce() => midObstacleJumpForce;

    public void QueueTrapJump(float force)
    {
        // si está aturdido o ya muerto, ignora
        if (!isAlive || isStunned) return;

        // acumulamos el mayor force pedido y reiniciamos ventana
        queuedTrapJumpForce = Mathf.Max(queuedTrapJumpForce, force);
        trapJumpTimer = trapJumpGraceWindow;
        Debug.Log($"[Tiny] Trap jump encolado. Fuerza = {queuedTrapJumpForce}, ventana = {trapJumpTimer:0.00}s");
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
        if (isInvincible)
        {
            Debug.Log("Tiny es invencible: ignora efecto de FlySwatter");
            return;
        }

        Debug.Log($"FlySwatter aplicado! Poder: {power}. Inversión de dirección.");
        isReversed = true;

        float effectDuration = GetEffectDuration(power);
        Invoke("ResetDirection", effectDuration);
    }

    public void ApplyBatEffect(float power)
    {
        if (isInvincible)
        {
            Debug.Log("Tiny es invencible: ignora efecto de Bate");
            return;
        }

        // target = slowSpeed/normalSpeed (cuánto queremos bajar como mínimo)
        float targetFactor = slowSpeed / normalSpeed; // e.g. 1.5/3 = 0.5
        speedDebuffFactor = Mathf.Lerp(1f, targetFactor, power); // 1 -> target según power
        currentSpeed = normalSpeed * speedDebuffFactor * goalProximityFactor;

        Debug.Log($"Factor bate: {speedDebuffFactor:F2} | currentSpeed: {currentSpeed:F2}");

        float effectDuration = GetEffectDuration(power);
        CancelInvoke(nameof(ResetSpeed));              // evita stacking
        Invoke(nameof(ResetSpeed), effectDuration);

        if (power >= 0.8f)
        {
            Debug.Log("Golpe crítico con bate!");
            Die();
        }
    }

    public void ApplyWrenchEffect(float power)
    {
        if (isInvincible)
        {
            Debug.Log("Tiny es invencible: ignora efecto de Wrench");
            return;
        }

        Debug.Log($"Wrench aplicado! Poder: {power}. Aturdimiento.");
        isStunned = true;
        rb.velocity = Vector2.zero;
        rb.Sleep(); // pausa el cuerpo físico mientras está aturdido

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
        speedDebuffFactor = 1f;
        currentSpeed = normalSpeed * goalProximityFactor; // respeta freno por meta
        Debug.Log("Velocidad de Tiny restaurada a " + currentSpeed);
    }

    void EndStun()
    {
        Debug.Log("Tiny ya no está aturdido");
        isStunned = false;
        rb.WakeUp();
    }
    #endregion WEAPON EFFECTS

    #region POWERUPS
    private void DetectPowerUps()
    {
        detectedPowerUps.Clear();

        Collider2D[] powerUpHits = Physics2D.OverlapCircleAll(
            transform.position,
            powerUpDetectionRange,
            powerUpLayer
        );

        foreach (Collider2D hit in powerUpHits)
        {
            Interactable powerUp = hit.GetComponent<Interactable>();
            if (powerUp != null && powerUp.powerUpType != PowerUpType.None && powerUp.CanInteract)
            {
                detectedPowerUps.Add(powerUp);
            }
        }

        // Debug visual
        foreach (Interactable powerUp in detectedPowerUps)
        {
            Debug.DrawLine(transform.position, powerUp.transform.position, Color.magenta);
        }
    }

    private void HandlePowerUpBehavior()
    {
        if (currentTargetPowerUp == null || !ShouldPrioritizePowerUp()) return;

        Vector2 directionToPowerUp = (currentTargetPowerUp.transform.position - transform.position).normalized;
        bool shouldMoveRight = directionToPowerUp.x > 0;

        if (shouldMoveRight != isFacingRight)
        {
            ToggleDirection();
        }

        // VERIFICAR SI EL POWER-UP ESTÁ ELEVADO Y NECESITA SALTO
        float heightDifference = currentTargetPowerUp.transform.position.y - transform.position.y;

        if (heightDifference > minHeightToJump && heightDifference <= maxJumpHeight && isGrounded)
        {
            // Saltar hacia el power-up elevado
            TryJump(CalculateJumpForce(heightDifference));
            Debug.Log("Saltando para alcanzar power-up elevado");
        }

        Debug.DrawLine(transform.position, currentTargetPowerUp.transform.position, Color.cyan);
    }

    private bool ShouldJumpForPowerUp()
    {
        if (currentTargetPowerUp == null) return false;

        // Calcular si el power-up está en una plataforma
        float heightDifference = currentTargetPowerUp.transform.position.y - transform.position.y;
        bool isPowerUpElevated = heightDifference > minHeightToJump;

        // Verificar si hay una plataforma que lleve al power-up
        if (isPowerUpElevated)
        {
            foreach (var platform in platformPositions)
            {
                float platformHeight = platform.y - transform.position.y;
                float horizontalDistance = Mathf.Abs(platform.x - transform.position.x);

                if (platformHeight > minHeightToJump && platformHeight <= maxJumpHeight &&
                    horizontalDistance < 3f)
                {
                    return true;
                }
            }
        }

        return false;
    }

    private void CheckPowerUpCollection()
    {
        if (currentTargetPowerUp == null) return;

        float distance = Vector2.Distance(transform.position, currentTargetPowerUp.transform.position);
        if (distance < 0.8f) // Aumenta la distancia de colección
        {
            CollectPowerUp(currentTargetPowerUp);
            currentTargetPowerUp = null;
        }
    }

    private void CollectPowerUp(Interactable powerUp)
    {
        if (!powerUp.CanInteract) return;

        powerUp.MarkAsUsed();
        powerUp.gameObject.SetActive(false); // Se oculta visualmente

        // Aplicar el efecto antes de destruir
        ApplyPowerUp(powerUp);

        // Destruir con un pequeño retraso
        StartCoroutine(DestroyAfterDelay(powerUp.gameObject, 0.2f));

        Debug.Log("¡Power-up obtenido: " + powerUp.powerUpType + "!");
    }

    private void ApplyPowerUp(Interactable powerUp)
    {
        switch (powerUp.powerUpType)
        {
            case Interactable.PowerUpType.SpeedBoost:
                StartCoroutine(SpeedBoostEffect(powerUp.powerUpDuration, powerUp.speedMultiplier));
                break;
            case Interactable.PowerUpType.Invulnerability:
                if (invincibilityRoutine != null) StopCoroutine(InvulnerabilityEffect(0));
                StartCoroutine(InvulnerabilityEffect(powerUp.powerUpDuration));
                break;
            case Interactable.PowerUpType.Invisibility:
                StartCoroutine(InvisibilityEffect(powerUp.powerUpDuration));
                break;
        }
        Debug.Log("¡Power-up obtenido: " + powerUp.powerUpType + "!");
    }

    private IEnumerator DestroyAfterDelay(GameObject obj, float delay)
    {
        yield return new WaitForSeconds(delay);
        if (obj != null)
        {
            Destroy(obj);
            Debug.Log("Power-up destruido tras aplicar efecto");
        }
    }

    private IEnumerator SpeedBoostEffect(float duration, float multiplier)
    {
        Debug.Log("¡SpeedBoost activado! Duración: " + duration + "s");
        float originalSpeed = normalSpeed;
        normalSpeed *= multiplier; // Cambiar normalSpeed, no currentSpeed
        currentSpeed = normalSpeed; // Actualizar currentSpeed también

        // Efecto visual opcional (cambiar color)
        SpriteRenderer spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        Color originalColor = spriteRenderer.color;
        spriteRenderer.color = Color.yellow;

        yield return new WaitForSeconds(duration);

        normalSpeed = originalSpeed;
        currentSpeed = normalSpeed;
        spriteRenderer.color = originalColor;
        Debug.Log("SpeedBoost terminado");
    }

    private IEnumerator InvulnerabilityEffect(float duration)
    {
        // Debug inicial
        Debug.Log($"Invincibilidad activada por {duration} s");

        isInvincible = true;

        // Visual
        SpriteRenderer spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        Color originalColor = spriteRenderer != null ? spriteRenderer.color : Color.white;

        // Si quieres que NO se afecte por timeScale=0, usa Time.unscaledTime y WaitForSecondsRealtime
        float endTime = Time.time + duration;
        bool toggle = false;

        try
        {
            while (Time.time < endTime)
            {
                // Debug opcional: muestra tiempo restante real
                // Debug.Log($"Invincible remaining: {(endTime - Time.time):F2}s");

                // Parpadeo
                if (spriteRenderer != null)
                {
                    toggle = !toggle;
                    spriteRenderer.color = toggle ? Color.yellow : originalColor;
                }

                // Espera fija entre flashes (tiempo escalado)
                yield return new WaitForSeconds(0.15f);
            }
        }
        finally
        {
            // Restaurar siempre, incluso si la corrutina se detiene/lanza excepción
            if (spriteRenderer != null)
                spriteRenderer.color = originalColor;

            isInvincible = false;
            Debug.Log("Invincibilidad terminada");
        }
    }

    private IEnumerator InvisibilityEffect(float duration)
    {
        Debug.Log("Invisibility activada");

        // 1. Activar estado
        isInvisible = true;

        // 2. Efecto visual
        SpriteRenderer spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        Color originalColor = spriteRenderer.color;
        spriteRenderer.color = new Color(originalColor.r, originalColor.g, originalColor.b, 0.4f);

        // 3. Ignorar físicamente obstáculos y mid obstacles actuales en escena
        Collider2D myCollider = GetComponent<Collider2D>();
        Interactable[] interactables = FindObjectsByType<Interactable>(FindObjectsSortMode.None);
        List<Collider2D> ignoredColliders = new List<Collider2D>();

        foreach (var interactable in interactables)
        {
            if (interactable == null) continue;

            // Solo los tipos de obstáculo físicos
            if (interactable.interactableType == Interactable.InteractableType.Obstacle ||
                interactable.interactableType == Interactable.InteractableType.MidObstacle)
            {
                Collider2D obstacleCol = interactable.GetComponent<Collider2D>();
                if (obstacleCol != null)
                {
                    Physics2D.IgnoreCollision(myCollider, obstacleCol, true);
                    ignoredColliders.Add(obstacleCol);
                }
            }
        }

        Debug.Log("Tiny ahora puede atravesar obstáculos y mid obstacles");

        // 4. Esperar la duración del efecto
        float timer = 0f;
        while (timer < duration)
        {
            timer += Time.deltaTime;
            yield return null;
        }

        // 5. Restaurar colisiones
        foreach (var obstacleCol in ignoredColliders)
        {
            if (obstacleCol != null)
            {
                Physics2D.IgnoreCollision(myCollider, obstacleCol, false);
            }
        }

        // 6. Restaurar opacidad
        spriteRenderer.color = originalColor;
        isInvisible = false;

        Debug.Log("Invisibility terminada: Tiny vuelve a colisionar con los obstáculos");
    }

    public bool IsInvincible()
    {
        return isInvincible;
    }
    #endregion POWERUPS

    #region PRIORITY SYSTEMS
    private bool ShouldPrioritizePowerUp()
    {
        if (detectedPowerUps.Count == 0) return false;
        if (goal == null) return true;

        // Calcular distancia a la meta
        float distanceToGoal = Vector2.Distance(transform.position, goal.position);

        // Encontrar power-up más cercano
        float minPowerUpDistance = Mathf.Infinity;
        currentTargetPowerUp = null;

        foreach (Interactable powerUp in detectedPowerUps)
        {
            if (!powerUp.CanInteract) continue;

            float dist = Vector2.Distance(transform.position, powerUp.transform.position);
            if (dist < minPowerUpDistance)
            {
                minPowerUpDistance = dist;
                currentTargetPowerUp = powerUp;
            }
        }

        if (currentTargetPowerUp == null) return false;

        // Priorizar si está suficientemente cerca y/o la meta está lejos
        return minPowerUpDistance < Mathf.Min(distanceToGoal * 0.4f, 3f);
    }
    #endregion PRIORITY SYSTEMS

    #region CHECK POINT
    private void InitializeCheckpointSystem()
    {
        // Prioridad 1: CheckpointManager
        if (CheckPointManager.Instance != null)
        {
            Vector3 savedPosition = CheckPointManager.Instance.GetRespawnPosition();
            if (savedPosition != Vector3.zero)
            {
                transform.position = savedPosition;
                FindAndSetCurrentCheckpointByPosition(savedPosition);
            }
            else
            {
                FindInitialCheckpoint();
            }
        }
        else
        {
            FindInitialCheckpoint();
        }
    }
    /*
    private void FindCurrentCheckpointInScene()
    {
        // Buscar el checkpoint que coincida con la posición guardada
        CheckPoint[] allCheckpoints = FindObjectsByType<CheckPoint>(FindObjectsSortMode.None);
        Vector3 savedPosition = CheckPointManager.Instance.currentCheckpoint.position;

        foreach (CheckPoint checkpoint in allCheckpoints)
        {
            if (Vector3.Distance(checkpoint.transform.position, savedPosition) < 0.5f)
            {
                currentCheckpoint = checkpoint;
                break;
            }
        }
    }
    */
    private void FindInitialCheckpoint()
    {
        CheckPoint[] allCheckpoints = FindObjectsByType<CheckPoint>(FindObjectsSortMode.None);
        CheckPoint initialCheckpoint = null;
        CheckPoint fallbackCheckpoint = null;

        foreach (CheckPoint checkpoint in allCheckpoints)
        {
            // Prioridad: checkpoint marcado como inicial
            if (checkpoint.isInitialCheckpoint)
            {
                initialCheckpoint = checkpoint;
                break;
            }

            // Fallback: primer checkpoint con orden 0
            if (fallbackCheckpoint == null && checkpoint.checkpointOrder == 0)
            {
                fallbackCheckpoint = checkpoint;
            }
        }

        currentCheckpoint = initialCheckpoint ?? fallbackCheckpoint ??
                           (allCheckpoints.Length > 0 ? allCheckpoints[0] : null);

        //Debug.Log($"Checkpoint inicial asignado: {currentCheckpoint?.gameObject.name ?? "Ninguno"}");

        //Debug.Log("Checkpoint inicial: " + (currentCheckpoint != null ? currentCheckpoint.name : "Posición inicial"));
    }

    private void FindAndSetCurrentCheckpointByPosition(Vector3 position)
    {
        CheckPoint[] allCheckpoints = FindObjectsByType<CheckPoint>(FindObjectsSortMode.None);
        float closestDistance = Mathf.Infinity;
        CheckPoint closestCheckpoint = null;

        foreach (CheckPoint checkpoint in allCheckpoints)
        {
            float distance = Vector3.Distance(checkpoint.transform.position, position);
            if (distance < closestDistance && distance < 2f) // Radio de 2 unidades
            {
                closestDistance = distance;
                closestCheckpoint = checkpoint;
            }
        }

        if (closestCheckpoint != null)
        {
            currentCheckpoint = closestCheckpoint;
            //Debug.Log($"Checkpoint encontrado por posición: {closestCheckpoint.gameObject.name}");
        }
    }


    public void SetCurrentCheckpoint(CheckPoint newCheckpoint)
    {
        if (newCheckpoint != null && newCheckpoint != currentCheckpoint)
        {
            currentCheckpoint = newCheckpoint;

            // Registrar en el CheckpointManager
            if (CheckPointManager.Instance != null)
            {
                CheckPointManager.Instance.RegisterCheckpoint(newCheckpoint);
            }
        }
        //Debug.Log("Checkpoint activado: " + newCheckpoint.name);
    }

    public void RespawnAtCheckpoint()
    {
        StartCoroutine(RespawnRoutine());
    }

    private System.Collections.IEnumerator RespawnRoutine()
    {
        // Pequeño delay para efectos
        yield return new WaitForSeconds(0.5f);

        Vector3 respawnPos = GetRespawnPosition();
        transform.position = respawnPos;
        ResetTinyState();

        Debug.Log($"Tiny reapareció en: {respawnPos}");
    }

    private Vector3 GetRespawnPosition()
    {
        if (CheckPointManager.Instance != null)
        {
            Vector3 managerPos = CheckPointManager.Instance.GetRespawnPosition();
            if (managerPos != Vector3.zero) return managerPos;
        }

        if (currentCheckpoint != null)
            return currentCheckpoint.GetRespawnPosition();

        return initialSpawnPosition;
    }

    /*
    public void RespawnAtCheckpoint()
    {
        Vector3 respawnPos;

        // PRIORIDAD 1: Usar CheckpointManager si existe
        if (CheckPointManager.Instance != null &&
            CheckPointManager.Instance.currentCheckpoint.position != Vector3.zero)
        {
            respawnPos = CheckPointManager.Instance.GetRespawnPosition();
        }
        // PRIORIDAD 2: Usar checkpoint local
        else if (currentCheckpoint != null)
        {
            respawnPos = currentCheckpoint.GetRespawnPosition();
        }
        // PRIORIDAD 3: Posición inicial
        else
        {
            respawnPos = initialSpawnPosition;
        }

        transform.position = respawnPos;
        ResetTinyState();
    }
    */
    private void ResetTinyState()
    {
        // Resetear todas las variables de estado
        isAlive = true;
        isStunned = false;
        isReversed = false;
        currentSpeed = normalSpeed;
        rb.velocity = Vector2.zero;

        // Resetear dirección inicial
        InitializeDirection();
    }
    #endregion CHECK POINT

    #region DIE
    public void Die()
    {
        if (!isAlive) return;

        Debug.Log("Tiny ha muerto!");
        isAlive = false;

        StartCoroutine(DeathSequence());
    }

    private IEnumerator DeathSequence()
    {
        // --- Preparación ---
        // Desactivar collider y movimiento
        CapsuleCollider2D capsule = GetComponent<CapsuleCollider2D>();
        if (capsule != null)
            capsule.enabled = false;

        // Asegurarse de que no tenga velocidad previa
        rb.velocity = Vector2.zero;
        rb.angularVelocity = 0f;

        // Liberar rotación en Z para animación libre
        rb.constraints = RigidbodyConstraints2D.None;

        // --- Animación de "muerte" ---
        // Pequeño salto hacia arriba
        rb.AddForce(Vector2.up * 8f, ForceMode2D.Impulse);

        // Aplicar un torque aleatorio (giro hacia un lado)
        float randomDirection = Random.value > 0.5f ? 1f : -1f;
        rb.AddTorque(randomDirection * 200f);

        // Esperar mientras "cae fuera de pantalla"
        yield return new WaitForSeconds(2f);

        // --- Restaurar estado ---
        rb.velocity = Vector2.zero;
        rb.angularVelocity = 0f;

        // Restaurar freeze de rotación
        rb.constraints = RigidbodyConstraints2D.FreezeRotation;

        // Reset de rotación visual
        transform.rotation = Quaternion.identity;

        // Restaurar collider
        if (capsule != null)
            capsule.enabled = true;

        // Respawnear en checkpoint
        RespawnAtCheckpoint();

        Debug.Log("Tiny reapareció tras morir.");

        // Tiny vuelve a estar vivo
        isAlive = true;
    }

    public bool IsAlive()
    {
        return isAlive;
    }
    #endregion DIE

    private void CheckGoalProximity()
    {
        if (goal == null || hasReachedGoal) return;

        float distanceToGoal = Vector2.Distance(transform.position, goal.position);

        if (distanceToGoal <= goalReachedDistance)
        {
            hasReachedGoal = true;
            rb.velocity = Vector2.zero;
            Debug.Log("¡Tiny llegó a la meta!");
        }
        else if (distanceToGoal <= goalSlowDownDistance)
        {
            float speedFactor = Mathf.Clamp01(distanceToGoal / goalSlowDownDistance);
            goalProximityFactor = speedFactor;   // SOLO factor de meta
        }
        else
        {
            goalProximityFactor = 1f;
        }

        // Recalcular la velocidad efectiva combinando factores
        currentSpeed = normalSpeed * speedDebuffFactor * goalProximityFactor;
    }

    private void OnDrawGizmosSelected()
    {
        // Dibujar rango de detección de power-ups
        Gizmos.color = Color.magenta;
        Gizmos.DrawWireSphere(transform.position, powerUpDetectionRange);

        // Dibujar línea al power-up objetivo
        if (currentTargetPowerUp != null)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(transform.position, currentTargetPowerUp.transform.position);
        }
    }
}

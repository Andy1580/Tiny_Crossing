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

    [Header("Checkpoint System")]
    private CheckPoint currentCheckpoint;
    private Vector3 initialSpawnPosition;

    [Header("PowerUp Detection")]
    [SerializeField] private float powerUpDetectionRange = 3f;
    [SerializeField] private LayerMask powerUpLayer;
    private List<Interactable> detectedPowerUps = new List<Interactable>();
    private Interactable currentTargetPowerUp = null;

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
        initialSpawnPosition = transform.position;
        InitializeCheckpointSystem();
    }

    void Update()
    {
        if (!isAlive || hasReachedGoal) return; // No hacer nada si llegó a la meta

        CheckGrounded();
        DetectPowerUps();
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
            if (ShouldPrioritizePowerUp())
            {
                if (ShouldJumpForPowerUp() && isGrounded)
                {
                    // Saltar hacia plataforma con power-up
                    Vector2 targetPlatform = platformPositions.OrderBy(p =>
                        Vector2.Distance(p, currentTargetPowerUp.transform.position)).First();
                    float heightDiff = targetPlatform.y - transform.position.y;
                    TryJump(CalculateJumpForce(heightDiff));
                    Debug.Log("Saltando hacia plataforma con power-up");
                }
                else
                {
                    HandlePowerUpBehavior();
                }
            }
            else if (shouldJumpToPlatform)
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

        CheckPowerUpCollection();
        HandleSpriteRotation();
        HandleGapJump();

        if (goal != null)
        {
            Debug.DrawLine(transform.position, goal.position, goalIsElevated ? Color.red : Color.yellow);
        }

        //Debug.Log($"shouldJumpToPlatform: {shouldJumpToPlatform} | isGrounded: {isGrounded}");

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
        powerUp.gameObject.SetActive(false); // Desactivar el objeto

        ApplyPowerUp(powerUp);
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
                StartCoroutine(InvulnerabilityEffect(powerUp.powerUpDuration));
                break;
            case Interactable.PowerUpType.Invisibility:
                StartCoroutine(InvisibilityEffect(powerUp.powerUpDuration));
                break;
        }
        Debug.Log("¡Power-up obtenido: " + powerUp.powerUpType + "!");
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
        Debug.Log("¡Invulnerabilidad activada!");
        yield return new WaitForSeconds(duration);
        Debug.Log("Invulnerabilidad terminada");
    }

    private IEnumerator InvisibilityEffect(float duration)
    {
        Debug.Log("¡Invisibilidad activada!");
        yield return new WaitForSeconds(duration);
        Debug.Log("Invisibilidad terminada");
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

        Debug.Log($"Checkpoint inicial asignado: {currentCheckpoint?.gameObject.name ?? "Ninguno"}");

        Debug.Log("Checkpoint inicial: " + (currentCheckpoint != null ? currentCheckpoint.name : "Posición inicial"));
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
            Debug.Log($"Checkpoint encontrado por posición: {closestCheckpoint.gameObject.name}");
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
        Debug.Log("Checkpoint activado: " + newCheckpoint.name);
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

        // Debug de distancia
        //Debug.Log($"Distancia a meta: {distanceToGoal}");

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

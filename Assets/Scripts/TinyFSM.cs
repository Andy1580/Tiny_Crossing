using UnityEngine;

public class TinyFSM : MonoBehaviour
{
    private enum State
    {
        Idle,
        Walking,
        Jumping,
        Falling
    }

    private State currentState;

    [Header("Movimiento")]
    public float moveSpeed = 4f;
    public float jumpForce = 15f;
    [SerializeField] private int moveDirection = 1;
    public bool shouldJumpGap = false;

    [Header("Detección")]
    public float groundCheckDistance = 0.7f;
    public LayerMask groundLayer;
    

    [Header("Objetivo")]
    public Transform goal;
    public float goalReachedThreshold = 1f;

    private Rigidbody2D rb;
    private Animator animator;

    private bool isGrounded;
    private bool hasReachedGoal = false;

    // Para salto adaptativo y atascos
    private float stuckTimer = 0f;
    private float stuckCheckInterval = 0.3f;
    private float stuckThreshold = 0.05f;
    private Vector2 lastCheckedPosition;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        animator = GetComponent<Animator>();
    }

    private void Start()
    {
        currentState = State.Walking;
        rb.gravityScale = 3;
    }

    private void Update()
    {
        if (hasReachedGoal)
        {
            rb.velocity = Vector2.zero;
            return;
        }

        CheckGoalReached();
        UpdateState();
        HandleState();
        CheckStuckLogic();
    }

    private void UpdateState()
    {
        isGrounded = CheckGround();

        if (isGrounded && !hasReachedGoal)
        {
            currentState = State.Walking;
            return;
        }

        currentState = rb.velocity.y > 1.5f ? State.Jumping : State.Falling;
    }

    private void HandleState()
    {
        if (currentState == State.Idle) return;

        switch (currentState)
        {
            case State.Walking:
                HandleWalking();
                break;
            case State.Jumping:
                HandleJumping();
                break;
            case State.Falling:
                HandleFalling();
                break;
        }
    }

    private void HandleWalking()
    {
        float speedToUse = moveSpeed;

        // Si debe prepararse para un salto (por trigger o plataforma), baja velocidad
        if (shouldJumpGap || ShouldJumpForPlatform())
        {
            speedToUse = Mathf.Min(moveSpeed, 2f);
        }

        rb.velocity = new Vector2(speedToUse * moveDirection, rb.velocity.y);
        transform.localScale = new Vector3(Mathf.Sign(moveDirection), 1f, 1f);

        // 1. Saltar si hay un trigger de hueco
        if (shouldJumpGap)
        {
            Debug.Log("¡Saltando hueco marcado por trigger!");
            shouldJumpGap = false;
            Jump();
            return;
        }

        // 2. Saltar si hay plataforma
        if (ShouldJumpForPlatform())
        {
            Debug.Log("Salto por plataforma elevada.");
            Jump();
            return;
        }

        // 3. Caer si no hay suelo
        if (!IsGroundAhead())
        {
            currentState = State.Falling;
        }
    }

    private void Jump()
    {
        if (!isGrounded) return;

        currentState = State.Jumping;
        rb.velocity = new Vector2(rb.velocity.x, jumpForce);
        animator?.SetTrigger("Jump");
    }

    private void HandleJumping()
    {
        if (rb.velocity.y < 0)
        {
            currentState = State.Falling;
        }
    }

    private void HandleFalling()
    {
        if (isGrounded)
        {
            currentState = State.Walking;
        }
    }

    private void CheckGoalReached()
    {
        if (goal == null || hasReachedGoal) return;

        float dist = Vector2.Distance(transform.position, goal.position);

        if (dist <= goalReachedThreshold)
        {
            Debug.Log("Tiny llegó a la meta.");
            currentState = State.Idle;
            rb.velocity = Vector2.zero;
            rb.gravityScale = 20;
            hasReachedGoal = true;
        }
    }

    private bool CheckGround()
    {
        Vector2 boxSize = new Vector2(0.8f, 0.1f); // ancho ajustado al personaje
        Vector2 boxCenter = (Vector2)transform.position + Vector2.down * 0.6f;

        return Physics2D.BoxCast(boxCenter, boxSize, 0f, Vector2.down, 0.1f, groundLayer);
    }

    private bool IsGroundAhead()
    {
        Vector2 offset = new Vector2(moveDirection * 0.5f, 0f);
        return Physics2D.Raycast(transform.position + (Vector3)offset, Vector2.down, groundCheckDistance + 0.1f, groundLayer);
    }

    private bool ShouldJumpForPlatform()
    {
        // 1. Verificar si la meta está directamente en frente y al nivel del suelo
        if (goal != null)
        {
            float dx = goal.position.x - transform.position.x;
            float dy = goal.position.y - transform.position.y;

            bool goalInFront = Mathf.Sign(dx) == moveDirection;
            bool goalClose = Mathf.Abs(dx) <= 3.5f; // rango horizontal
            bool goalIsLow = dy <= 0.5f;             // está al nivel o más bajo

            if (goalInFront && goalClose && goalIsLow)
            {
                Debug.Log("Meta al frente, cerca y a nivel bajo. No saltar plataforma.");
                return false;
            }
        }

        // 2. Detectar plataformas elevadas al frente y por encima de Tiny
        Vector2 center = new Vector2(transform.position.x + moveDirection * 0.8f, transform.position.y + 1.2f);
        Vector2 size = new Vector2(1.2f, 1.2f);

        Collider2D[] hits = Physics2D.OverlapBoxAll(center, size, 0f, groundLayer);

        foreach (var hit in hits)
        {
            float heightDiff = hit.transform.position.y - transform.position.y;

            if (heightDiff > 0.3f)
            {
                Debug.Log("Plataforma detectada. Saltar.");
                return true;
            }
        }

        return false;
    }

    private void CheckStuckLogic()
    {
        stuckTimer += Time.deltaTime;

        if (stuckTimer >= stuckCheckInterval)
        {
            float moved = Vector2.Distance(transform.position, lastCheckedPosition);

            if (moved < stuckThreshold && isGrounded)
            {
                Debug.LogWarning("Tiny atascado. Saltito de desatasco.");
                rb.velocity = new Vector2(moveSpeed * moveDirection, jumpForce * 0.5f);
            }

            lastCheckedPosition = transform.position;
            stuckTimer = 0f;
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("GapTrigger"))
        {
            Debug.Log("Trigger de hueco detectado. Preparando salto.");
            shouldJumpGap = true;
        }

        if (other.CompareTag("Goal"))
        {
            Debug.Log("Tiny llegó a la meta (trigger).");
            hasReachedGoal = true;
            rb.velocity = Vector2.zero;
            rb.gravityScale = 20;
            currentState = State.Idle;
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (!Application.isPlaying || goal == null) return;

        Gizmos.color = Color.yellow;

        float dx = goal.position.x - transform.position.x;
        float dy = goal.position.y - transform.position.y;

        Gizmos.DrawLine(transform.position, goal.position);

        Vector2 platformBoxCenter = new Vector2(transform.position.x + moveDirection * 0.8f, transform.position.y + 1.2f);
        Vector2 platformBoxSize = new Vector2(1.2f, 1.2f);
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireCube(platformBoxCenter, platformBoxSize);

        Vector2 gapCheckOrigin = transform.position + new Vector3(moveDirection * 1.2f, 0f);
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(gapCheckOrigin, 0.3f);
    }
}

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
    public float moveSpeed = 2f;
    public float jumpForce = 6f;
    [SerializeField] private int moveDirection = 1; // 1 = derecha, -1 = izquierda

    [Header("Detección")]
    public float groundCheckDistance = 0.7f;
    public LayerMask groundLayer;

    [Header("Objetivo")]
    public Transform goal;
    public float goalReachedThreshold = 1f; // Más permisivo

    private Rigidbody2D rb;
    private Animator animator;

    private bool isGrounded;

    // Desatasco
    private float stuckTimer = 0f;
    private float stuckCheckInterval = 0.3f;
    private float stuckThreshold = 0.05f;
    private Vector2 lastCheckedPosition;

    public bool hasReachedGoal = false;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        animator = GetComponent<Animator>();
    }

    private void Start()
    {
        currentState = State.Walking;
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

    #region ESTADOS
    private void UpdateState()
    {
        isGrounded = CheckGround();

        if (isGrounded && !hasReachedGoal)
        {
            currentState = State.Walking;
            return;
        }

        if (rb.velocity.y > 1.5f)
            currentState = State.Jumping;
        else
            currentState = State.Falling;
    }

    private void HandleState()
    {
        if (currentState == State.Idle)
            return;

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
    #endregion

    #region MOVIMIENTO
    private void MoveHorizontally()
    {
        if(hasReachedGoal) return;

        rb.velocity = new Vector2(moveSpeed * moveDirection, rb.velocity.y);
        transform.localScale = new Vector3(Mathf.Sign(moveDirection), 1f, 1f);
    }

    private void HandleWalking()
    {
        MoveHorizontally();

        if (goal != null)
        {
            float dx = goal.position.x - transform.position.x;
            float dy = goal.position.y - transform.position.y;

            // Si la meta está adelante y al mismo nivel o más abajo => NO saltar
            if (Mathf.Sign(dx) == moveDirection && dy <= 1.0f)
            {
                Debug.Log("Meta detectada al frente. Priorizando avance.");
                return;
            }
        }

        // Evaluar plataformas arriba solo si la meta no está al frente
        Collider2D[] above = CheckPlatformsAbove(1.5f, 1.2f);
        if (above.Length > 0 && isGrounded)
        {
            Debug.Log("Evaluando salto por plataforma elevada detectada.");
            Jump();
            return;
        }

        // Verifica si hay suelo al frente
        Vector2 forwardOffset = Vector3.right * moveDirection * 0.5f;
        bool forwardGround = Physics2D.Raycast(transform.position + (Vector3)forwardOffset, Vector2.down, groundCheckDistance + 0.1f, groundLayer);

        if (!forwardGround)
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
            currentState = State.Falling;
    }

    private void HandleFalling()
    {
        if (isGrounded)
        {
            currentState = State.Walking;
            MoveHorizontally();
        }
    }
    #endregion

    #region META
    private void CheckGoalReached()
    {
        if (goal == null || hasReachedGoal) return;

        float distToGoal = Vector2.Distance(transform.position, goal.position);

        if (distToGoal <= goalReachedThreshold)
        {
            Debug.Log("Tiny llegó a la meta.");
            currentState = State.Idle;
            rb.velocity = Vector2.zero;
            hasReachedGoal = true;
        }
    }
    #endregion

    #region DETECCIÓN
    private bool CheckGround()
    {
        Vector2 center = transform.position;
        float rayOffset = 0.3f;

        bool centerRay = Physics2D.Raycast(center, Vector2.down, groundCheckDistance, groundLayer);
        bool leftRay = Physics2D.Raycast(center + Vector2.left * rayOffset, Vector2.down, groundCheckDistance, groundLayer);
        bool rightRay = Physics2D.Raycast(center + Vector2.right * rayOffset, Vector2.down, groundCheckDistance, groundLayer);

        return centerRay || leftRay || rightRay;
    }

    private Collider2D[] CheckPlatformsAbove(float height, float width)
    {
        Vector2 center = new Vector2(transform.position.x, transform.position.y + height / 2f);
        Vector2 size = new Vector2(width, height);
        return Physics2D.OverlapBoxAll(center, size, 0f, groundLayer);
    }

    private void OnDrawGizmosSelected()
    {
        Vector2 center = new Vector2(transform.position.x, transform.position.y + 1.5f / 2f);
        Vector2 size = new Vector2(1.2f, 1.5f);
        Gizmos.color = Color.magenta;
        Gizmos.DrawWireCube(center, size);
    }
    #endregion

    #region RECUPERACIÓN
    private void CheckStuckLogic()
    {
        stuckTimer += Time.deltaTime;

        if (stuckTimer >= stuckCheckInterval)
        {
            float movedDistance = Vector2.Distance(transform.position, lastCheckedPosition);

            if (movedDistance < stuckThreshold)
            {
                Debug.LogWarning("Tiny detectó atasco. Evaluando redirección.");

                if (goal != null)
                {
                    float deltaX = goal.position.x - transform.position.x;
                    moveDirection = deltaX >= 0 ? 1 : -1;
                    rb.velocity = new Vector2(moveSpeed * moveDirection, rb.velocity.y);
                    Debug.Log($"Redirigiendo hacia la meta. Dirección: {moveDirection}");
                }
            }

            lastCheckedPosition = transform.position;
            stuckTimer = 0f;
        }
    }
    #endregion
}

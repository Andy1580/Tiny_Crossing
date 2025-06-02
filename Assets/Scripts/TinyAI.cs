using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TinyAI : MonoBehaviour
{
    [Header("Referencias")]
    public Pathfinder pathfinder;
    public Transform goal;
    public GridManager gridManager;

    [Header("Parámetros de movimiento")]
    public float moveSpeed = 2f;
    public float jumpForce = 6f;
    public float nodeReachThreshold = 0.1f;
    public float jumpHeightTolerance = 0.2f; // Cuánto más alto debe estar el nodo para considerar un salto

    private List<Node> currentPath;
    private int currentNodeIndex = 0;

    private Rigidbody2D rb;
    private Animator animator;

    private bool isJumping = false;
    private bool isMoving = false;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        animator = GetComponent<Animator>();
    }

    private void Start()
    {
        StartCoroutine(PlanAndMove());
    }

    private IEnumerator PlanAndMove()
    {
        yield return new WaitUntil(() => IsGrounded());

        currentPath = pathfinder.FindPath(transform.position, goal.position);
        pathfinder.DebugDrawPath(currentPath, Color.green);

        if (currentPath == null || currentPath.Count == 0)
        {
            Debug.LogWarning("Tiny no encontró camino.");
            yield break;
        }

        currentNodeIndex = 0;
        isMoving = true;
    }

    private void Update()
    {
        if (!isMoving || currentPath == null || currentNodeIndex >= currentPath.Count)
            return;

        if (isJumping)
            return; // Si está saltando, no interferir, dejar que termine el salto

        MoveTowardsNextNode();
    }

    private void MoveTowardsNextNode()
    {
        if (currentPath == null || currentNodeIndex >= currentPath.Count)
            return;

        Node targetNode = currentPath[currentNodeIndex];
        Vector2 direction = (targetNode.worldPosition - transform.position).normalized;

        float verticalDifference = targetNode.worldPosition.y - transform.position.y;
        float horizontalDifference = targetNode.worldPosition.x - transform.position.x;

        // Decidir si saltar, caminar o caer
        if (verticalDifference > jumpHeightTolerance && Mathf.Abs(horizontalDifference) < 1.5f && IsGrounded() && !isJumping)
        {
            JumpTowards(direction);
        }
        else if (verticalDifference < -jumpHeightTolerance)
        {
            // Si el siguiente nodo está debajo, simplemente caer, no saltar
            WalkTowards(direction);
        }
        else if (IsGrounded() && !isJumping)
        {
            // Camino normal
            WalkTowards(direction);
        }

        // Verificar si llegó al nodo objetivo
        float distance = Vector2.Distance(transform.position, targetNode.worldPosition);
        if (distance <= nodeReachThreshold)
        {
            currentNodeIndex++;

            if (currentNodeIndex >= currentPath.Count)
            {
                Debug.Log("Tiny llegó a la meta.");
                isMoving = false;
            }
        }
    }

    private void WalkTowards(Vector2 direction)
    {
        rb.velocity = new Vector2(direction.x * moveSpeed, rb.velocity.y);
        animator?.SetBool("isJumping", false);

        // Flip Sprite
        if (direction.x != 0)
            transform.localScale = new Vector3(Mathf.Sign(direction.x), 1f, 1f);
    }

    private void JumpTowards(Vector2 direction)
    {
        isJumping = true;
        animator?.SetTrigger("Jump");

        rb.velocity = new Vector2(direction.x * moveSpeed, jumpForce);
    }

    private bool IsGrounded()
    {
        Vector2 center = transform.position;
        float rayDistance = 0.5f;
        float rayOffset = 0.3f; // Qué tan separados los rayos laterales

        bool centerHit = Physics2D.Raycast(center, Vector2.down, rayDistance, gridManager.groundLayer);
        bool leftHit = Physics2D.Raycast(center + Vector2.left * rayOffset, Vector2.down, rayDistance, gridManager.groundLayer);
        bool rightHit = Physics2D.Raycast(center + Vector2.right * rayOffset, Vector2.down, rayDistance, gridManager.groundLayer);

        return centerHit || leftHit || rightHit;
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        foreach (var contact in collision.contacts)
        {
            if (contact.normal.y > 0.5f)
            {
                isJumping = false;
                animator?.SetBool("isJumping", false);
                break;
            }
        }
    }
}

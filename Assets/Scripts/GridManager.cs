using UnityEngine;

public class GridManager : MonoBehaviour
{
    [Header("Grid Settings")]
    public int width = 20;
    public int height = 10;
    public float cellSize = 1f;

    [Header("World Origin (bottom-left of grid)")]
    public Vector3 originPosition;

    [Header("Walkable Detection")]
    public LayerMask groundLayer;
    public float detectionRadius = 0.45f;

    private Node[,] grid;

    public Node[,] Grid => grid;

    private void Awake()
    {
        GenerateGrid();
    }

    private void GenerateGrid()
    {
        grid = new Node[width, height];

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                Vector3 worldPos = originPosition + new Vector3(x * cellSize, y * cellSize, 0f);
                Vector2 checkBelow = new Vector2(worldPos.x, worldPos.y - (cellSize / 2f));

                bool hasGroundBelow = Physics2D.OverlapCircle(checkBelow, detectionRadius, groundLayer);

                NodeType nodeType = hasGroundBelow ? NodeType.Ground : NodeType.Air;

                grid[x, y] = new Node(new Vector2Int(x, y), worldPos, nodeType);

                //Debug.Log($"Node ({x},{y}) - Tipo: {nodeType}");
            }
        }
    }

    public void SetNodeBlocked(Vector3 worldPosition, bool blocked)
    {
        Node node = GetNodeFromWorldPosition(worldPosition);

        if (node == null) return;

        if (blocked)
        {
            node.type = NodeType.Blocked;
        }
        else
        {
            // Recalcular si el nodo debería ser Ground o Air
            Vector2 checkBelow = new Vector2(node.worldPosition.x, node.worldPosition.y - (cellSize / 2f));
            bool hasGroundBelow = Physics2D.OverlapCircle(checkBelow, detectionRadius, groundLayer);

            node.type = hasGroundBelow ? NodeType.Ground : NodeType.Air;
        }
    }

    public void SetNodesBlockedByCollider(Collider2D collider, bool blocked)
    {
        Bounds bounds = collider.bounds;

        int minX = Mathf.FloorToInt((bounds.min.x - originPosition.x) / cellSize);
        int maxX = Mathf.FloorToInt((bounds.max.x - originPosition.x) / cellSize);
        int minY = Mathf.FloorToInt((bounds.min.y - originPosition.y) / cellSize);
        int maxY = Mathf.FloorToInt((bounds.max.y - originPosition.y) / cellSize);

        for (int x = minX; x <= maxX; x++)
        {
            for (int y = minY; y <= maxY; y++)
            {
                if (x < 0 || x >= width || y < 0 || y >= height)
                    continue;

                Node node = grid[x, y];

                // Verificamos que realmente esté dentro del collider
                if (collider.OverlapPoint(node.worldPosition))
                {
                    if (blocked)
                    {
                        node.type = NodeType.Blocked;
                    }
                    else
                    {
                        Vector2 checkBelow = new Vector2(node.worldPosition.x, node.worldPosition.y - (cellSize / 2f));
                        bool hasGroundBelow = Physics2D.OverlapCircle(checkBelow, detectionRadius, groundLayer);
                        node.type = hasGroundBelow ? NodeType.Ground : NodeType.Air;
                    }

                    Debug.Log($"Marcando nodo ({x},{y}) como {(blocked ? "Blocked" : "Restaurado")}");
                }
            }
        }
    }

    public Node GetNodeFromWorldPosition(Vector3 worldPosition)
    {
        int x = Mathf.RoundToInt((worldPosition.x - originPosition.x) / cellSize);
        int y = Mathf.RoundToInt((worldPosition.y - originPosition.y) / cellSize);

        x = Mathf.Clamp(x, 0, width - 1);
        y = Mathf.Clamp(y, 0, height - 1);

        Node node = grid[x, y];
        Debug.Log($"WorldPos: {worldPosition} maps to Node ({x},{y}) - Type: {node.type}");
        return node;
    }

    private void OnDrawGizmos()
    {
        if (grid == null) return;

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                Node node = grid[x, y];

                switch (node.type)
                {
                    case NodeType.Ground:
                        Gizmos.color = Color.green;
                        break;
                    case NodeType.Air:
                        Gizmos.color = Color.cyan;
                        break;
                    case NodeType.Blocked:
                        Gizmos.color = Color.red;
                        break;
                }

                Gizmos.DrawCube(node.worldPosition, Vector3.one * (cellSize * 0.9f));
            }
        }
    }
}

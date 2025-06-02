using UnityEngine;

public enum NodeType
{
    Ground,     // Caminable
    Air,        // Espacio vacío
    Blocked     // Obstáculo
}

public class Node
{
    public Vector2Int gridPosition;
    public Vector3 worldPosition;
    public NodeType type;
    public bool isWalkable => type != NodeType.Blocked;

    public Node(Vector2Int gridPos, Vector3 worldPos, NodeType nodeType)
    {
        gridPosition = gridPos;
        worldPosition = worldPos;
        type = nodeType;
    }
}

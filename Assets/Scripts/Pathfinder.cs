using System.Collections.Generic;
using UnityEngine;

public class Pathfinder : MonoBehaviour
{
    [SerializeField] private GridManager gridManager;

    private List<Node> finalPath;

    [Header("Movimiento Vertical")]
    public int maxJumpHeight = 2; // Cuántos nodos puede saltar hacia arriba
    public int maxFallDistance = 4; // Cuántos nodos puede caer hacia abajo

    public List<Node> FindPath(Vector3 startWorldPos, Vector3 goalWorldPos)
    {
        Node startNode = gridManager.GetNodeFromWorldPosition(startWorldPos);
        Node goalNode = gridManager.GetNodeFromWorldPosition(goalWorldPos);

        if (startNode == null || goalNode == null || !goalNode.isWalkable)
        {
            Debug.LogWarning("No se pudo iniciar pathfinding. Nodo inválido.");
            return null;
        }

        Dictionary<Node, PathNodeInfo> allNodes = new Dictionary<Node, PathNodeInfo>();
        List<PathNodeInfo> openList = new List<PathNodeInfo>();
        HashSet<Node> closedSet = new HashSet<Node>();

        PathNodeInfo startInfo = new PathNodeInfo(startNode)
        {
            gCost = 0,
            hCost = GetHeuristic(startNode, goalNode)
        };

        openList.Add(startInfo);
        allNodes[startNode] = startInfo;

        while (openList.Count > 0)
        {
            PathNodeInfo current = GetLowestFCost(openList);

            if (current.node == goalNode)
            {
                finalPath = ReconstructPath(current);
                return finalPath;
            }

            openList.Remove(current);
            closedSet.Add(current.node);

            foreach (Node neighbor in GetNeighbours(current.node))
            {
                if (!neighbor.isWalkable || closedSet.Contains(neighbor))
                    continue;

                float tentativeG = current.gCost + GetDistance(current.node, neighbor);

                if (!allNodes.ContainsKey(neighbor))
                {
                    PathNodeInfo info = new PathNodeInfo(neighbor)
                    {
                        parent = current,
                        gCost = tentativeG,
                        hCost = GetHeuristic(neighbor, goalNode)
                    };

                    allNodes[neighbor] = info;
                    openList.Add(info);
                }
                else if (tentativeG < allNodes[neighbor].gCost)
                {
                    PathNodeInfo info = allNodes[neighbor];
                    info.gCost = tentativeG;
                    info.parent = current;
                }
            }
        }

        Debug.LogWarning("No se encontró un camino.");
        Debug.LogWarning($"StartNode Walkable: {startNode?.isWalkable}, GoalNode Walkable: {goalNode?.isWalkable}");
        Debug.Log($"Start: {startNode?.gridPosition}, Goal: {goalNode?.gridPosition}");
        return null;
    }

    private float GetHeuristic(Node a, Node b)
    {
        return Vector2.Distance(a.worldPosition, b.worldPosition);
    }

    private float GetDistance(Node a, Node b)
    {
        return Vector2.Distance(a.worldPosition, b.worldPosition);
    }

    private PathNodeInfo GetLowestFCost(List<PathNodeInfo> list)
    {
        PathNodeInfo best = list[0];
        foreach (var item in list)
        {
            if (item.FCost < best.FCost || (item.FCost == best.FCost && item.hCost < best.hCost))
                best = item;
        }
        return best;
    }

    private List<Node> GetNeighbours(Node node)
    {
        List<Node> neighbours = new List<Node>();

        // Movimiento horizontal normal (caminar)
        TryAddNeighbour(node.gridPosition.x + 1, node.gridPosition.y, neighbours);
        TryAddNeighbour(node.gridPosition.x - 1, node.gridPosition.y, neighbours);

        // Saltos verticales (subir)
        for (int i = 1; i <= maxJumpHeight; i++)
        {
            TryAddNeighbour(node.gridPosition.x, node.gridPosition.y + i, neighbours);
        }

        // Caídas verticales (bajar)
        for (int i = 1; i <= maxFallDistance; i++)
        {
            TryAddNeighbour(node.gridPosition.x, node.gridPosition.y - i, neighbours);
        }

        // Saltos en diagonal (cruzar huecos)
        TryJumpOverGap(node, neighbours);

        return neighbours;
    }

    private void TryAddNeighbour(int x, int y, List<Node> neighbours)
    {
        if (IsInGrid(x, y))
        {
            Node candidate = gridManager.Grid[x, y];
            if (candidate.type == NodeType.Ground)
            {
                neighbours.Add(candidate);
            }
        }
    }

    private void TryJumpOverGap(Node node, List<Node> neighbours)
    {
        int maxJumpDistance = 2; // cuántas celdas puede saltar horizontalmente

        for (int dx = -maxJumpDistance; dx <= maxJumpDistance; dx++)
        {
            if (dx == 0) continue;

            for (int dy = 0; dy <= maxJumpHeight; dy++)
            {
                int checkX = node.gridPosition.x + dx;
                int checkY = node.gridPosition.y + dy;

                if (IsInGrid(checkX, checkY))
                {
                    Node jumpCandidate = gridManager.Grid[checkX, checkY];
                    if (jumpCandidate.type == NodeType.Ground)
                    {
                        neighbours.Add(jumpCandidate);
                        break;
                    }
                }
            }
        }
    }

    private bool IsInGrid(int x, int y)
    {
        return x >= 0 && x < gridManager.Grid.GetLength(0) &&
               y >= 0 && y < gridManager.Grid.GetLength(1);
    }

    private List<Node> ReconstructPath(PathNodeInfo endNode)
    {
        List<Node> path = new List<Node>();
        PathNodeInfo current = endNode;

        while (current != null)
        {
            path.Add(current.node);
            current = current.parent;
        }

        path.Reverse();
        return path;
    }

    public void DebugDrawPath(List<Node> path, Color color)
    {
        if (path == null) return;

        for (int i = 0; i < path.Count - 1; i++)
        {
            Debug.DrawLine(path[i].worldPosition, path[i + 1].worldPosition, color, 5f);
        }
    }
}

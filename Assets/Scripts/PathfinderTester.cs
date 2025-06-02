using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PathfinderTester : MonoBehaviour
{
    public Pathfinder pathfinder;
    public Transform startPoint;
    public Transform goalPoint;

    private void Start()
    {
        List<Node> path = pathfinder.FindPath(startPoint.position, goalPoint.position);
        pathfinder.DebugDrawPath(path, Color.yellow);
    }
}

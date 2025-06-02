using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PathNodeInfo
{
    public Node node;
    public PathNodeInfo parent;
    public float gCost;
    public float hCost;
    public float FCost => gCost + hCost;

    public PathNodeInfo(Node node)
    {
        this.node = node;
    }
}

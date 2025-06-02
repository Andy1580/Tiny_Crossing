using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ObstacleAffectNode : MonoBehaviour
{

    private void OnTriggerEnter2D(Collider2D other)
    {
        PathNode node = other.GetComponent<PathNode>();
        if (node != null)
            node.isBlocked = true;
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        PathNode node = other.GetComponent<PathNode>();
        if (node != null)
            node.isBlocked = false;
    }
}

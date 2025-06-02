using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class ObstacleMarker : MonoBehaviour
{
    private GridManager gridManager;

    private void Start()
    {
        gridManager = Object.FindFirstObjectByType<GridManager>();
    }

    //private void Update()
    //{
    //    if (gridManager != null)
    //    {
    //        gridManager.SetNodesBlockedByCollider(GetComponent<Collider2D>(), true);
    //    }
    //}

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("NodeDetector"))
        {
            gridManager.SetNodesBlockedByCollider(GetComponent<Collider2D>(), true);
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (other.CompareTag("NodeDetector"))
        {
            gridManager.SetNodesBlockedByCollider(GetComponent<Collider2D>(), false);
        }
    }
}

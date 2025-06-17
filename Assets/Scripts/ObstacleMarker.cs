using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class ObstacleMarker : MonoBehaviour
{
    private GridManager gridManager;
    private Collider2D col;

    private void Awake()
    {
        col = GetComponent<Collider2D>();
    }

    private void Start()
    {
        gridManager = Object.FindFirstObjectByType<GridManager>();
    }

    private void Update()
    {
        if (gridManager != null)
        {
            gridManager.SetNodesBlockedByCollider(col, true);
        }
    }
}

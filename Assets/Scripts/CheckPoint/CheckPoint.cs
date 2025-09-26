using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CheckPoint : MonoBehaviour
{
    [Header("Checkpoint Settings")]
    [SerializeField] public bool isInitialCheckpoint = false;
    [SerializeField] public int checkpointOrder = 0; // Para orden lógico
    private bool hasBeenActivated = false;

    private void Start()
    {
        // Auto-activar checkpoint inicial
        if (isInitialCheckpoint)
        {
            ActivateCheckpoint();
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player") && !hasBeenActivated)
        {
            ActivateCheckpoint();
        }
    }

    private void ActivateCheckpoint()
    {
        hasBeenActivated = true;

        TinyController tiny = FindObjectOfType<TinyController>();
        if (tiny != null)
        {
            tiny.SetCurrentCheckpoint(this);
        }

        Debug.Log($"Checkpoint activado: {gameObject.name} (Orden: {checkpointOrder})");
    }

    public Vector3 GetRespawnPosition()
    {
        return transform.position + Vector3.up * 0.5f; // Pequeño offset para evitar stuck
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = isInitialCheckpoint ? Color.green :
                      hasBeenActivated ? Color.blue : Color.yellow;
        Gizmos.DrawWireCube(transform.position, new Vector3(1, 1, 0));
        Gizmos.color = Color.white;
        GUI.Label(new Rect(transform.position.x, transform.position.y + 1, 100, 20),
                 $"{checkpointOrder}:{gameObject.name}");
    }
}

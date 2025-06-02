using UnityEngine;

public class PathNode : MonoBehaviour
{
    [Header("Estado del nodo")]
    public bool isBlocked = false;

    private void OnTriggerEnter2D(Collider2D other)
    {
        // Si un obstáculo entra al nodo, lo marcamos como bloqueado
        if (other.CompareTag("Obstacle"))
        {
            isBlocked = true;
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        // Si un obstáculo sale del nodo, lo marcamos como libre
        if (other.CompareTag("Obstacle"))
        {
            isBlocked = false;
        }
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = isBlocked ? Color.red : Color.green;
        Gizmos.DrawSphere(transform.position, 0.2f);

#if UNITY_EDITOR
        UnityEditor.Handles.Label(transform.position + Vector3.up * 0.3f, gameObject.name);
#endif
    }
}

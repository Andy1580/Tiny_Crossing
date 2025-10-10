using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class JumpHints : MonoBehaviour
{
    [Tooltip("Punto donde Tiny debe aterrizar. Suele estar sobre la plataforma destino.")]
    public Transform landing;

    [Tooltip("Dirección en la que Tiny debe saltar desde este hint.")]
    public bool jumpTowardsRight = true; // true = saltar hacia la derecha, false = izquierda

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawSphere(transform.position, 0.07f);

        if (landing != null)
        {
            Gizmos.DrawLine(transform.position, landing.position);
            Gizmos.DrawSphere(landing.position, 0.07f);
        }

        // Flecha de dirección
        Vector3 dir = jumpTowardsRight ? Vector3.right : Vector3.left;
        Gizmos.color = Color.blue;
        Gizmos.DrawLine(transform.position, transform.position + dir * 0.5f);
    }
}

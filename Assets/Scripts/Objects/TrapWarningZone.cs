using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TrapWarningZone : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private TrapController trap;   // arrástralo en el Inspector

    [Header("Jump")]
    [Tooltip("Multiplicador sobre la fuerza de salto de MidObstacle")]
    [SerializeField] private float jumpForceMultiplier = 1.1f;

    private void Reset()
    {
        GetComponent<Collider2D>().isTrigger = true;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        // ¿Es Tiny?
        TinyController tiny = other.GetComponent<TinyController>();
        if (tiny == null) return;

        // Si Tiny es invisible, no hace falta saltar trampas.
        if (tiny.IsInvisible()) return;

        bool trapWillHurt = trap != null && trap.IsActiveOrRising();
        if (trapWillHurt)
        {
            float force = tiny.GetMidObstacleJumpForce() * jumpForceMultiplier;
            Debug.Log("[TrapWarning] Aviso a Tiny: saltar trampa");
            tiny.QueueTrapJump(force);
        }
    }
}

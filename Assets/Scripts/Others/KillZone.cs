using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class KillZone : MonoBehaviour
{
    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            TinyController tiny = other.GetComponent<TinyController>();
            if (tiny != null && tiny.IsAlive())
            {
                tiny.Die(); // Llama al método de muerte
            }
        }
    }
}

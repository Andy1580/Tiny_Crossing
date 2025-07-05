using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TinyController : MonoBehaviour
{
    [Header("Weapon Position Reference")]
    public Transform weaponAnchorPoint; // Objeto hijo donde se posicionarán las armas

    [Header("Collider Settings")]
    public Collider2D bodyCollider; // Collider para detectar armas

    // Movimiento básico de Tiny (para pruebas)
    void Update()
    {
        // Movimiento de prueba - será reemplazado por IA
        float move = Input.GetAxis("Horizontal") * Time.deltaTime * 3f;
        transform.Translate(move, 0, 0);
    }
}

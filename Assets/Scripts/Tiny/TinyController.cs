using UnityEngine;

public class TinyController : MonoBehaviour
{
    [Header("Weapon Position Reference")]
    public Transform weaponAnchorPoint;

    //[Header("Collider Settings")]
    //public Collider2D bodyCollider; // Collider para detectar armas

    [Header("Movement Settings")]
    public float normalSpeed = 3f;
    public float slowSpeed = 1f;
    public float stunDuration = 2f;

    private float currentSpeed;
    private bool isStunned = false;
    private bool isReversed = false;
    private bool isAlive = true;

    void Start()
    {
        currentSpeed = normalSpeed;
    }

    void Update()
    {
        if (!isAlive) return;

        // Movimiento básico de Tiny
        if (!isStunned)
        {
            float move = currentSpeed * Time.deltaTime;
            if (isReversed) move *= -1;
            transform.Translate(move, 0, 0);
        }
    }

    private float GetEffectDuration(float power)
    {
        if (power < 0.4f) return 1f;       // Rango bajo
        if (power < 0.8f) return 2.5f;     // Rango medio
        return 4f;                         // Rango alto
    }

    // ===== EFECTOS DE ARMAS =====
    public void ApplyFlySwatterEffect(float power)
    {
        Debug.Log($"FlySwatter aplicado! Poder: {power}");
        isReversed = true;

        // Duración basada en rango de poder
        float effectDuration = GetEffectDuration(power);
        Invoke("ResetDirection", effectDuration);
    }

    public void ApplyBatEffect(float power)
    {
        Debug.Log($"Bat aplicado! Poder: {power}");
        currentSpeed = Mathf.Lerp(normalSpeed, slowSpeed, power);

        float effectDuration = GetEffectDuration(power);
        Invoke("ResetSpeed", effectDuration);

        // Muerte solo en rango alto (0.8-1)
        if (power >= 0.8f) Die();
    }

    public void ApplyWrenchEffect(float power)
    {
        Debug.Log($"Wrench aplicado! Poder: {power}");
        isStunned = true;

        // Duración basada en rango de poder
        float effectDuration = GetEffectDuration(power);
        Invoke("EndStun", effectDuration);

        // Muerte solo en rango alto (0.8-1)
        if (power >= 0.8f) Die();
    }

    void ResetDirection()
    {
        Debug.Log("Dirección de Tiny restaurada");
        isReversed = false;
    }

    void ResetSpeed()
    {
        Debug.Log("Velocidad de Tiny restaurada");
        currentSpeed = normalSpeed;
    }

    void EndStun()
    {
        Debug.Log("Tiny ya no está aturdido");
        isStunned = false;
    }

    void Die()
    {
        Debug.Log("Tiny ha muerto!");
        isAlive = false;
        // Aquí tu lógica para reiniciar nivel o mostrar Game Over
    }
}

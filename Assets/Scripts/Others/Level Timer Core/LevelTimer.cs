using UnityEngine;
using System;

public class LevelTimer : MonoBehaviour
{
    [Header("Configuración del temporizador")]
    [Tooltip("Tiempo total del nivel en segundos (por ejemplo, 120 = 2 minutos)")]
    [SerializeField] private float totalTime = 120f;

    [Tooltip("¿Iniciar automáticamente al comenzar el nivel?")]
    [SerializeField] private bool autoStart = true;

    private float remainingTime;
    private bool isRunning = false;

    // Evento para cuando el tiempo se termina
    public event Action OnTimerEnd;

    void Start()
    {
        if (autoStart)
        {
            StartTimer();
        }
    }

    void Update()
    {
        if (!isRunning) return;

        remainingTime -= Time.deltaTime;

        if (remainingTime <= 0)
        {
            remainingTime = 0;
            isRunning = false;

            Debug.Log("¡Tiempo agotado! El jugador gana.");
            OnTimerEnd?.Invoke();
        }
    }

    // === MÉTODOS PÚBLICOS ===

    public void StartTimer()
    {
        remainingTime = totalTime;
        isRunning = true;
    }

    public void StopTimer()
    {
        isRunning = false;
    }

    public void ResumeTimer()
    {
        isRunning = true;
    }

    public void RestartTimer()
    {
        remainingTime = totalTime;
        isRunning = true;
    }

    public float GetRemainingTime()
    {
        return remainingTime;
    }

    public bool IsRunning()
    {
        return isRunning;
    }
}

using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class TinySpeechBubble : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private GameObject bubbleRoot;          // Contenedor de la viñeta (Tiny Speech Bubble)
    [SerializeField] private TMP_Text textLabel;             // Texto dentro del globo (TMP)
    [SerializeField] private Animator animator;              // Animator del globo

    [Header("Animator Triggers")]
    [SerializeField] private string triggerIn = "In";       // Trigger de entrada
    [SerializeField] private string triggerOut = "Out";      // Trigger de salida

    [Header("Timings")]
    [Tooltip("Segundos que la viñeta permanece visible por defecto")]
    [SerializeField] private float defaultVisibleSeconds = 5f;

    [Header("Scheduler (Ambient)")]
    [SerializeField] private bool autoStartLine = true;     // muestra línea de inicio automáticamente
    [SerializeField] private float startLineDelay = 1.2f;   // delay antes de la línea de inicio
    [SerializeField] private bool autoAmbient = true;       // activa el loop de ambientales

    [Tooltip("Ventana de espera entre frases aleatorias (min, max)")]
    [SerializeField] private Vector2 idleWindowSeconds = new Vector2(10f, 15f);

    [Header("Lines: Inicio / Aleatorias / Armas / Victoria")]
    [TextArea] public List<string> startLines = new List<string>();
    [TextArea] public List<string> ambientLines = new List<string>();

    [Header("Armas")]
    [TextArea] public List<string> flySwatterLines = new List<string>();
    [TextArea] public List<string> batLines = new List<string>();
    [TextArea] public List<string> wrenchLines = new List<string>();

    [Header("Victoria")]
    [TextArea] public List<string> victoryLines = new List<string>();

    private Coroutine ambientLoopCo;
    private bool ambientPaused = false;

    // Estado
    public bool IsVisible { get; private set; }
    private Coroutine autoHideCo;
    private System.Random rng;

    void Awake()
    {
        rng = new System.Random();

        // Si no asignaste por Inspector, intenta resolver automáticamente
        if (bubbleRoot == null) bubbleRoot = this.gameObject;
        if (animator == null) animator = GetComponent<Animator>();
        if (textLabel == null) textLabel = GetComponentInChildren<TMP_Text>(true);

        // Asegura que arranque oculto a nivel Animator (tu animación se encarga de la escala/canvas group)
        IsVisible = false;
    }

    private void Start()
    {
        // Si ya estabas llamando ShowStartLine() manualmente en tu versión, 
        // puedes desactivar autoStartLine en el Inspector para evitar duplicados.
        if (autoStartLine)
            StartCoroutine(StartLineThenAmbient());
        else if (autoAmbient)
            StartAmbient();  // arranca directo el loop de ambientales
    }

    // =============== API PÚBLICA ===============

    public void Show(string message, float? visibleSeconds = null)
    {
        if (string.IsNullOrEmpty(message)) return;

        // Asigna texto
        if (textLabel != null) textLabel.text = message;

        // Cancela cualquier hide pendiente
        if (autoHideCo != null)
        {
            StopCoroutine(autoHideCo);
            autoHideCo = null;
        }

        // Activa el contenedor (por si el primer frame de la animación oculta componentes)
        if (bubbleRoot != null && !bubbleRoot.activeSelf)
            bubbleRoot.SetActive(true);

        // Dispara animación de entrada
        if (animator != null)
        {
            animator.ResetTrigger(triggerOut);
            animator.SetTrigger(triggerIn);
        }

        IsVisible = true;

        // Programar auto-ocultado si se solicita (o usar default)
        float secs = visibleSeconds.HasValue ? visibleSeconds.Value : defaultVisibleSeconds;
        if (secs > 0f)
            autoHideCo = StartCoroutine(AutoHideAfter(secs));
    }

    public void Hide()
    {
        // Cancela any autohide
        if (autoHideCo != null)
        {
            StopCoroutine(autoHideCo);
            autoHideCo = null;
        }

        // Dispara animación de salida
        if (animator != null)
        {
            animator.ResetTrigger(triggerIn);
            animator.SetTrigger(triggerOut);
        }

        IsVisible = false;
    }

    public void ShowStartLine()
    {
        string line = PickRandom(startLines);
        if (!string.IsNullOrEmpty(line)) Show(line);
    }

    public void ShowRandomAmbient()
    {
        string line = PickRandom(ambientLines);
        if (!string.IsNullOrEmpty(line)) Show(line);
    }

    public void ShowVictoryLine()
    {
        StopAmbient();
        string line = PickRandom(victoryLines);
        if (!string.IsNullOrEmpty(line)) Show(line);
    }

    public void InterruptWithWeapon(Interactable.WeaponType weaponType)
    {
        // El “interrupt” es simplemente: mostrar inmediatamente una línea de arma (reinicia contador)
        string line = null;
        switch (weaponType)
        {
            case Interactable.WeaponType.FlySwatter: line = PickRandom(flySwatterLines); break;
            case Interactable.WeaponType.Bat: line = PickRandom(batLines); break;
            case Interactable.WeaponType.Wrench: line = PickRandom(wrenchLines); break;
        }

        if (!string.IsNullOrEmpty(line))
        {
            Show(line); // esto cancela el autohide anterior y reinicia
        }
    }

    // =============== Helpers ===============

    // Muestra línea de inicio, espera a que cierre, y arranca ambientales
    private IEnumerator StartLineThenAmbient()
    {
        yield return new WaitForSeconds(startLineDelay);
        ShowStartLine();
        yield return new WaitForSeconds(GetDefaultVisibleSeconds());

        if (autoAmbient)
            StartAmbient();
    }

    // --- Control del loop de ambientales ---
    public void StartAmbient()
    {
        StopAmbient();
        ambientPaused = false;
        ambientLoopCo = StartCoroutine(AmbientLoop());
    }

    public void StopAmbient()
    {
        if (ambientLoopCo != null)
        {
            StopCoroutine(ambientLoopCo);
            ambientLoopCo = null;
        }
    }

    public void PauseAmbient() { ambientPaused = true; }
    public void ResumeAmbient() { ambientPaused = false; }

    // Loop: espera ventana aleatoria y muestra una frase ambiental
    private IEnumerator AmbientLoop()
    {
        while (true)
        {
            // Espera a que no haya viñeta visible ni pausa
            while (IsVisible || ambientPaused)
                yield return null;

            // Espera idle aleatoria (se reinicia si entra cualquier otra frase)
            float waitTarget = GetRandomIdleWait();
            float t = 0f;
            while (t < waitTarget)
            {
                if (IsVisible || ambientPaused)  // si se mostró otra cosa, reinicia la espera
                {
                    // Espera a que vuelva a quedar libre
                    while (IsVisible || ambientPaused) yield return null;
                    t = 0f; // reinicia el conteo
                }
                else
                {
                    t += Time.deltaTime;
                    yield return null;
                }
            }

            // Muestra una ambiental
            ShowRandomAmbient();

            // Espera a que se oculte para volver a iterar
            while (IsVisible) yield return null;
        }
    }

    private IEnumerator AutoHideAfter(float seconds)
    {
        yield return new WaitForSeconds(seconds);
        Hide();
        autoHideCo = null;
    }

    private string PickRandom(List<string> pool)
    {
        if (pool == null || pool.Count == 0) return null;
        int i = rng.Next(0, pool.Count);
        return pool[i];
    }

    // Accesores útiles para el “scheduler” que implementaremos en la sub-tarea 2
    public float GetRandomIdleWait() => Random.Range(idleWindowSeconds.x, idleWindowSeconds.y);
    public float GetDefaultVisibleSeconds() => defaultVisibleSeconds;
}

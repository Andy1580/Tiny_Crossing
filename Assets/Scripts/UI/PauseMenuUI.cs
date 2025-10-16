using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class PauseMenuUI : MonoBehaviour
{
    [Header("Referencias UI")]
    [SerializeField] private GameObject pausePanel;
    [SerializeField] private CanvasGroup panelGroup;
    [SerializeField] private Button resumeButton;
    [SerializeField] private Button restartButton;
    [SerializeField] private Button mainMenuButton;

    [Header("Animación de Fade")]
    [SerializeField] private float fadeDuration = 0.5f;

    [Header("Estado del juego")]
    [SerializeField] private LevelManager levelManager; // referencia opcional al LevelManager
    private bool isGameOver = false; // bandera interna

    private bool isPaused = false;
    private bool isTransitioning = false;

    private void Start()
    {
        if (pausePanel != null)
        {
            pausePanel.SetActive(false);
            Debug.Log("[PauseMenu] Panel inicial desactivado correctamente.");
        }

        if (panelGroup != null)
        {
            panelGroup.alpha = 0f;
            panelGroup.interactable = false;
            panelGroup.blocksRaycasts = false;
        }

        if (resumeButton != null) resumeButton.onClick.AddListener(ResumeGame);
        if (restartButton != null) restartButton.onClick.AddListener(RestartLevel);
        if (mainMenuButton != null) mainMenuButton.onClick.AddListener(ReturnToMainMenu);

        // Buscar LevelManager automáticamente si no está asignado
        if (levelManager == null)
            levelManager = GameObject.FindFirstObjectByType<LevelManager>();
    }

    private void Update()
    {
        if (levelManager != null && levelManager.IsGameOver)
        {
            if (!isGameOver)
            {
                Debug.Log("[PauseMenu] Juego finalizado, pausa deshabilitada.");
                if (pausePanel.activeSelf)
                {
                    Debug.Log("[PauseMenu] Ocultando menú porque el juego terminó.");
                    StartCoroutine(FadeOutPanel());
                }
                isGameOver = true;
            }
            return; // bloquear pausa si el juego terminó
        }

        // Debug de pulsación
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            Debug.Log("ESC presionado");
            if (!isPaused) PauseGame();
            else ResumeGame();
        }
    }

    private void PauseGame()
    {
        Debug.Log(" Juego pausado");
        isPaused = true;
        Time.timeScale = 0f;
        if (pausePanel != null) pausePanel.SetActive(true);
        StartCoroutine(FadeInPanel());
    }

    private void ResumeGame()
    {
        Debug.Log(" Juego reanudado");
        if (!isPaused) return;
        StartCoroutine(FadeOutPanel());
    }

    private IEnumerator FadeInPanel()
    {
        isTransitioning = true;
        if (panelGroup == null) yield break;

        panelGroup.alpha = 0f;
        panelGroup.interactable = true;
        panelGroup.blocksRaycasts = true;

        float t = 0f;
        while (t < fadeDuration)
        {
            t += Time.unscaledDeltaTime;
            panelGroup.alpha = Mathf.Lerp(0f, 1f, t / fadeDuration);
            yield return null;
        }

        panelGroup.alpha = 1f;
        isTransitioning = false;
        Debug.Log(" Fade in terminado");
    }

    private IEnumerator FadeOutPanel()
    {
        isTransitioning = true;
        if (panelGroup == null) yield break;

        float t = 0f;
        while (t < fadeDuration)
        {
            t += Time.unscaledDeltaTime;
            panelGroup.alpha = Mathf.Lerp(1f, 0f, t / fadeDuration);
            yield return null;
        }

        panelGroup.alpha = 0f;
        panelGroup.interactable = false;
        panelGroup.blocksRaycasts = false;

        if (pausePanel != null)
            pausePanel.SetActive(false);

        Time.timeScale = 1f;
        isPaused = false;
        isTransitioning = false;
        Debug.Log(" Fade out terminado");
    }

    private void RestartLevel()
    {
        Debug.Log(" Reiniciando nivel...");
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    private void ReturnToMainMenu()
    {
        Debug.Log(" Volviendo al menú principal...");
        Time.timeScale = 1f;
        SceneManager.LoadScene("MainMenu");
    }
}

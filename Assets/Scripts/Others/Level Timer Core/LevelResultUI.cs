using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class LevelResultUI : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private CanvasGroup panelGroup;
    [SerializeField] private TextMeshProUGUI resultText;

    [Header("Buttons")]
    [SerializeField] private Button retryButton;
    [SerializeField] private Button nextLevelButton;
    [SerializeField] private Button mainMenuButton;

    [Header("Fade Settings")]
    [SerializeField] private float fadeDuration = 1.5f;
    [SerializeField] private float buttonFadeDelay = 0.3f; // Tiempo entre botones
    [SerializeField] private float buttonFadeDuration = 0.6f;

    private bool isShowing = false;

    private string[] levelOrder = { "Nivel1", "Nivel2", "Nivel3" };

    private void Awake()
    {
        if (panelGroup != null)
        {
            panelGroup.alpha = 0f;
            panelGroup.interactable = false;
            panelGroup.blocksRaycasts = false;
        }

        if (retryButton != null)
        {
            retryButton.onClick.RemoveAllListeners();
            retryButton.onClick.AddListener(OnRetry);
        }

        if (nextLevelButton != null)
        {
            nextLevelButton.onClick.RemoveAllListeners();
            nextLevelButton.onClick.AddListener(OnNextLevel);
        }

        if (mainMenuButton != null)
        {
            mainMenuButton.onClick.RemoveAllListeners();
            mainMenuButton.onClick.AddListener(OnMainMenu);
        }
    }

    private void Start()
    {
        CheckIfLastLevel();
        InitializeButtonsInvisible();
    }

    private void InitializeButtonsInvisible()
    {
        // Asegura que todos los botones empiecen invisibles desde el inicio
        Button[] buttons = { retryButton, nextLevelButton, mainMenuButton };

        foreach (var button in buttons)
        {
            if (button == null) continue;

            CanvasGroup group = button.GetComponent<CanvasGroup>();
            if (group == null)
                group = button.gameObject.AddComponent<CanvasGroup>();

            group.alpha = 0f;
            group.interactable = false;
            group.blocksRaycasts = false;

            // También reseteamos posición base (por si vienen desplazados de una escena anterior)
            RectTransform rect = button.GetComponent<RectTransform>();
            rect.anchoredPosition = rect.anchoredPosition; // Mantiene la posición pero evita que quede offscreen
        }
    }

    private void CheckIfLastLevel()
    {
        string currentScene = SceneManager.GetActiveScene().name;
        if (nextLevelButton != null)
        {
            bool isLast = currentScene == levelOrder[levelOrder.Length - 1];
            nextLevelButton.gameObject.SetActive(!isLast);
        }
    }

    public void Show(string message, Color color)
    {
        if (isShowing) return;
        isShowing = true;

        if (resultText != null)
        {
            resultText.text = message;
            resultText.color = color;
        }

        StartCoroutine(FadeIn());
    }

    private IEnumerator FadeIn()
    {
        panelGroup.blocksRaycasts = true;
        panelGroup.interactable = true;

        float elapsed = 0f;
        while (elapsed < fadeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            panelGroup.alpha = Mathf.Lerp(0f, 1f, elapsed / fadeDuration);
            yield return null;
        }

        panelGroup.alpha = 1f;

        // Llamamos la animación de botones
        StartCoroutine(AnimateButtons());
    }

    private IEnumerator AnimateButtons()
    {
        Button[] buttons = GetActiveButtons();

        // Antes de iniciar, ocultamos todos los botones completamente
        foreach (var button in buttons)
        {
            if (button == null) continue;

            CanvasGroup group = button.GetComponent<CanvasGroup>();
            if (group == null)
                group = button.gameObject.AddComponent<CanvasGroup>();

            group.alpha = 0f;
            group.interactable = false;
            group.blocksRaycasts = false;

            RectTransform rect = button.GetComponent<RectTransform>();
            rect.anchoredPosition += Vector2.down * 25f; // posición inicial más baja
        }

        // Luego, mostramos cada botón uno por uno con fade y desplazamiento
        foreach (var button in buttons)
        {
            if (button == null) continue;

            CanvasGroup group = button.GetComponent<CanvasGroup>();
            RectTransform rect = button.GetComponent<RectTransform>();

            Vector3 startPos = rect.anchoredPosition;
            Vector3 endPos = startPos - Vector3.down * 25f;

            float t = 0f;
            while (t < buttonFadeDuration)
            {
                t += Time.unscaledDeltaTime;
                float normalized = Mathf.Clamp01(t / buttonFadeDuration);

                group.alpha = Mathf.Lerp(0f, 1f, normalized);
                rect.anchoredPosition = Vector3.Lerp(startPos, endPos, normalized);

                yield return null;
            }

            group.alpha = 1f;
            rect.anchoredPosition = endPos;
            group.interactable = true;
            group.blocksRaycasts = true;

            // Esperamos antes de mostrar el siguiente botón
            yield return new WaitForSecondsRealtime(buttonFadeDelay);
        }
    }

    private Button[] GetActiveButtons()
    {
        // Devuelve solo los botones activos en la escena
        return new Button[]
        {
            retryButton,
            nextLevelButton != null && nextLevelButton.gameObject.activeSelf ? nextLevelButton : null,
            mainMenuButton
        };
    }

    private IEnumerator FadeOut()
    {
        float elapsed = 0f;
        while (elapsed < fadeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            panelGroup.alpha = Mathf.Lerp(1f, 0f, elapsed / fadeDuration);
            yield return null;
        }

        panelGroup.alpha = 0f;
        panelGroup.blocksRaycasts = false;
        panelGroup.interactable = false;
        isShowing = false;
    }

    public void Hide()
    {
        StartCoroutine(FadeOut());
    }

    // --- BOTONES ---
    private void OnRetry()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    private void OnNextLevel()
    {
        Time.timeScale = 1f;
        string currentScene = SceneManager.GetActiveScene().name;
        int index = System.Array.IndexOf(levelOrder, currentScene);

        if (index >= 0 && index < levelOrder.Length - 1)
        {
            string nextScene = levelOrder[index + 1];
            SceneManager.LoadScene(nextScene);
        }
        else
        {
            Debug.Log("Último nivel alcanzado. No hay siguiente nivel.");
            SceneManager.LoadScene("MainMenu");
        }
    }

    private void OnMainMenu()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene("MainMenu");
    }
}

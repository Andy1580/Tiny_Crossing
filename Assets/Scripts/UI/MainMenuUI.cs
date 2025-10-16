using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

public class MainMenuUI : MonoBehaviour
{
    [Header("Referencias UI")]
    [SerializeField] private CanvasGroup panelGroup;
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private Button playButton;
    [SerializeField] private Button levelSelectButton;
    [SerializeField] private Button controlsButton;
    [SerializeField] private Button exitButton;

    [Header("Animación")]
    [SerializeField] private float fadeDuration = 1.2f;
    [SerializeField] private float buttonFadeDuration = 0.5f;
    [SerializeField] private float buttonFadeDelay = 0.2f;

    private void Start()
    {
        InitializeInvisible();
        AssignButtonActions();
        StartCoroutine(ShowMenuSequence());
    }

    private void InitializeInvisible()
    {
        if (panelGroup != null)
        {
            panelGroup.alpha = 0f;
            panelGroup.interactable = false;
            panelGroup.blocksRaycasts = false;
        }

        Button[] buttons = { playButton, levelSelectButton, controlsButton, exitButton };
        foreach (var button in buttons)
        {
            if (button == null) continue;
            CanvasGroup group = button.GetComponent<CanvasGroup>();
            if (group == null) group = button.gameObject.AddComponent<CanvasGroup>();

            group.alpha = 0f;
            group.interactable = false;
            group.blocksRaycasts = false;
        }

        if (titleText != null)
            titleText.alpha = 0f;
    }

    private void AssignButtonActions()
    {
        if (playButton != null) playButton.onClick.AddListener(() => LoadScene("Scenes/Nivel1"));
        if (levelSelectButton != null) levelSelectButton.onClick.AddListener(OpenLevelSelect);
        if (controlsButton != null) controlsButton.onClick.AddListener(OpenControls);
        if (exitButton != null) exitButton.onClick.AddListener(ExitGame);
    }

    private IEnumerator ShowMenuSequence()
    {
        // Fade general del panel
        float t = 0f;
        while (t < fadeDuration)
        {
            t += Time.unscaledDeltaTime;
            panelGroup.alpha = Mathf.Lerp(0f, 1f, t / fadeDuration);
            yield return null;
        }
        panelGroup.interactable = true;
        panelGroup.blocksRaycasts = true;

        // Fade del título
        if (titleText != null)
        {
            t = 0f;
            while (t < 0.6f)
            {
                t += Time.unscaledDeltaTime;
                titleText.alpha = Mathf.Lerp(0f, 1f, t / 0.6f);
                yield return null;
            }
        }

        // Fade escalonado de botones
        Button[] buttons = { playButton, levelSelectButton, controlsButton, exitButton };
        foreach (var button in buttons)
        {
            if (button == null) continue;
            CanvasGroup group = button.GetComponent<CanvasGroup>();
            RectTransform rect = button.GetComponent<RectTransform>();

            Vector2 startPos = rect.anchoredPosition - Vector2.up * 30f;
            Vector2 endPos = rect.anchoredPosition;
            rect.anchoredPosition = startPos;

            t = 0f;
            while (t < buttonFadeDuration)
            {
                t += Time.unscaledDeltaTime;
                float normalized = Mathf.Clamp01(t / buttonFadeDuration);
                group.alpha = Mathf.Lerp(0f, 1f, normalized);
                rect.anchoredPosition = Vector2.Lerp(startPos, endPos, normalized);
                yield return null;
            }

            group.alpha = 1f;
            rect.anchoredPosition = endPos;
            group.interactable = true;
            group.blocksRaycasts = true;

            yield return new WaitForSecondsRealtime(buttonFadeDelay);
        }
    }

    private void LoadScene(string sceneName)
    {
        StartCoroutine(LoadSceneWithFade(sceneName));
    }

    private IEnumerator LoadSceneWithFade(string sceneName)
    {
        panelGroup.interactable = false;

        float t = 0f;
        while (t < 1f)
        {
            t += Time.unscaledDeltaTime;
            panelGroup.alpha = Mathf.Lerp(1f, 0f, t / 1f);
            yield return null;
        }

        SceneManager.LoadScene(sceneName);
    }

    private void OpenLevelSelect()
    {
        Debug.Log("Abrir menú de selección de nivel (pendiente Fase 7.3)");
        // Aquí luego se puede cambiar a otro panel o escena.
    }

    private void OpenControls()
    {
        Debug.Log("Mostrar ventana de controles (opcional)");
        // Podrías mostrar un panel temporal o una mini-popup aquí.
    }

    private void ExitGame()
    {
        Debug.Log("Saliendo del juego...");
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}

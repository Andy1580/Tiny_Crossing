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

    [Header("Paneles adicionales")]
    [SerializeField] private GameObject mainMenuPanel;
    [SerializeField] private GameObject levelSelectPanel;
    [SerializeField] private GameObject controlsPanel;

    [Header("Botones de selección de nivel")]
    [SerializeField] private Button buttonLevel1;
    [SerializeField] private Button buttonLevel2;
    [SerializeField] private Button buttonLevel3;
    [SerializeField] private Button buttonCloseLevelSelect;

    [Header("Animación")]
    [SerializeField] private float fadeDuration = 1.2f;
    [SerializeField] private float buttonFadeDuration = 0.5f;
    [SerializeField] private float buttonFadeDelay = 0.2f;

    private bool isLevelSelectOpen = false;
    private bool isControlsOpen = false;

    [Header("Scene Names")]
    [SerializeField] private string level1Scene = "Nivel1";
    [SerializeField] private string level2Scene = "Nivel2";
    [SerializeField] private string level3Scene = "Nivel3";

    private void Start()
    {
        InitializeInvisible();
        AssignButtonActions();
        StartCoroutine(ShowMenuSequence());

        // Asegurar estado inicial
        ShowMainMenu();
        if (controlsPanel != null) controlsPanel.SetActive(false);
    }

    // === Inicialización visual ===
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
        if (controlsButton != null) controlsButton.onClick.AddListener(ToggleControlsPanel);
        if (exitButton != null) exitButton.onClick.AddListener(ExitGame);

        if (buttonCloseLevelSelect != null) buttonCloseLevelSelect.onClick.AddListener(CloseLevelSelect);

        if (buttonLevel1 != null) buttonLevel1.onClick.AddListener(() => LoadScene(level1Scene));
        if (buttonLevel2 != null) buttonLevel2.onClick.AddListener(() => LoadScene(level2Scene));
        if (buttonLevel3 != null) buttonLevel3.onClick.AddListener(() => LoadScene(level3Scene));
    }

    // === Animación de aparición del menú principal ===
    private IEnumerator ShowMenuSequence()
    {
        float t = 0f;
        while (t < fadeDuration)
        {
            t += Time.unscaledDeltaTime;
            panelGroup.alpha = Mathf.Lerp(0f, 1f, t / fadeDuration);
            yield return null;
        }
        panelGroup.interactable = true;
        panelGroup.blocksRaycasts = true;

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

    // === Nueva lógica de paneles ===
    public void OpenLevelSelect()
    {
        SetPanelActive(mainMenuPanel, false);
        SetPanelActive(levelSelectPanel, true);
    }

    public void CloseLevelSelect()
    {
        SetPanelActive(levelSelectPanel, false);
        SetPanelActive(mainMenuPanel, true);
    }

    private void ToggleControlsPanel()
    {
        if (controlsPanel == null) return;

        isControlsOpen = !isControlsOpen;
        controlsPanel.SetActive(isControlsOpen);

        // Cerrar panel de nivel si está abierto
        if (isLevelSelectOpen && levelSelectPanel != null)
        {
            levelSelectPanel.SetActive(false);
            isLevelSelectOpen = false;
        }
    }

    public void ShowMainMenu()
    {
        SetPanelActive(mainMenuPanel, true);
        SetPanelActive(levelSelectPanel, false);
    }

    private void SetPanelActive(GameObject panel, bool active)
    {
        if (!panel) return;
        panel.SetActive(active);

        var cg = panel.GetComponent<CanvasGroup>();
        if (cg)
        {
            cg.alpha = active ? 1f : 0f;
            cg.interactable = active;
            cg.blocksRaycasts = active;
        }
    }

    // === Carga de escenas con fade ===
    private void LoadScene(string sceneName)
    {
        Time.timeScale = 1f;
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

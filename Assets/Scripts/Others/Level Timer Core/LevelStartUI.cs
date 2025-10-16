using System.Collections;
using TMPro;
using UnityEngine;

public class LevelStartUI : MonoBehaviour
{
    [Header("Referencias")]
    [SerializeField] private TextMeshProUGUI readyText;
    [SerializeField] private LevelTimer levelTimer;
    [SerializeField] private LevelTimerUI levelTimerUi;
    [SerializeField] private TinyController tiny;

    [Header("Configuración")]
    [SerializeField] private float readyDuration = 1.5f;
    [SerializeField] private float goDuration = 1.0f;

    private void Start()
    {
        if (tiny == null)
            tiny = GameObject.FindFirstObjectByType<TinyController>();

        StartCoroutine(StartSequence());
    }

    private IEnumerator StartSequence()
    {
        // Congela el juego mientras se muestra la intro
        Time.timeScale = 0f;

        if (tiny != null)
            tiny.enabled = false;

        if (levelTimer != null)
            levelTimer.StopTimer();

        //if (levelTimerUi != null)
        //    levelTimerUi.TimerText.gameObject.SetActive(false);

        // Mostrar READY
        readyText.text = "READY...";
        readyText.color = Color.yellow;
        readyText.alpha = 1f;
        yield return new WaitForSecondsRealtime(readyDuration);

        // Mostrar GO!
        readyText.text = "GO!";
        readyText.color = Color.green;
        yield return new WaitForSecondsRealtime(goDuration);

        // Ocultar texto
        readyText.text = "";

        // Reanudar el juego
        Time.timeScale = 1f;

        if (tiny != null)
            tiny.enabled = true;

        if (levelTimer != null)
        {
            levelTimer.enabled = true;
            yield return new WaitForSeconds(0.2f);
            levelTimer.StartTimer();
        }

        //if (levelTimer != null && !levelTimerUi.TimerText.gameObject.activeSelf)
        //    levelTimerUi.TimerText.gameObject.SetActive(true);
        //yield return new WaitForSecondsRealtime(0.5f);
        //levelTimer.StartTimer();
    }
}

using UnityEngine;
using UnityEngine.SceneManagement;

public class LevelManager : MonoBehaviour
{
    [Header("Referencias")]
    [SerializeField] private LevelTimer levelTimer;
    [SerializeField] private TinyController tiny;
    [SerializeField] private LevelResultUI resultPanel;

    private bool gameEnded = false;

    public bool IsGameOver => gameEnded;

    private void Start()
    {
        if (levelTimer != null)
        {
            levelTimer.OnTimerEnd += OnTimeOver;
        }

        if (tiny == null)
            tiny = GameObject.FindFirstObjectByType<TinyController>();

        if (tiny != null && !tiny.enabled)
            tiny.enabled = true;
    }

    private void OnDestroy()
    {
        if (levelTimer != null)
        {
            levelTimer.OnTimerEnd -= OnTimeOver;
        }
    }

    private void OnTimeOver()
    {
        if (gameEnded) return;
        gameEnded = true;

        if (tiny != null)
            tiny.enabled = false;

        Time.timeScale = 0f;

        if (resultPanel != null)
            resultPanel.Show("You Win!", Color.yellow);
        Cursor.visible = true;

        Debug.Log("¡Jugador gana! El tiempo se agotó.");
    }

    // =====================
    //   MÉTODOS DE CONTROL
    // =====================

    public void RestartLevel()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    public void StopLevel()
    {
        if (levelTimer != null)
            levelTimer.StopTimer();

        if (tiny != null)
            tiny.enabled = false;

        gameEnded = true;
        Debug.Log("Nivel detenido manualmente.");
    }

    public void ResumeLevel()
    {
        if (levelTimer != null)
            levelTimer.ResumeTimer();

        if (tiny != null)
            tiny.enabled = true;

        Time.timeScale = 1f;
        Debug.Log("Nivel reanudado.");
    }

    public void PlayerWinsByTime()
    {
        if (gameEnded) return;
        OnTimeOver();
    }

    public void PlayerWinsByTinyDeath()
    {
        if (gameEnded) return;

        gameEnded = true;
        if (levelTimer != null)
            levelTimer.StopTimer();

        Debug.Log("¡Jugador gana! Tiny ha muerto antes del tiempo.");
    }

    public void TinyWins()
    {
        if (gameEnded) return;
        gameEnded = true;

        if (levelTimer != null)
            levelTimer.StopTimer();

        if (tiny != null)
            tiny.enabled = false;

        Time.timeScale = 0f;

        if (resultPanel != null)
            resultPanel.Show("Tiny Win!", Color.cyan);
        Cursor.visible = true;

        Debug.Log("¡Tiny gana! Alcanzó la meta antes del tiempo.");
    }
}

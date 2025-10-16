using TMPro;
using UnityEngine;

public class LevelTimerUI : MonoBehaviour
{
    [Header("Referencias")]
    [SerializeField] private LevelTimer levelTimer;  // Arrastra aquí tu LevelTimer desde la escena
    [SerializeField] private TextMeshProUGUI timerText;

    private void Update()
    {
        if (levelTimer == null || timerText == null) return;

        float time = levelTimer.GetRemainingTime();
        int minutes = Mathf.FloorToInt(time / 60);
        int seconds = Mathf.FloorToInt(time % 60);

        timerText.text = $"{minutes:00}:{seconds:00}";
    }

    public TextMeshProUGUI TimerText { get { return timerText; } }
}

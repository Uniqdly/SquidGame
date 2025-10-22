using UnityEngine;
using TMPro;
using UnityEngine.SceneManagement;

public class LevelTimer : MonoBehaviour
{
    [Header("UI")]
    public TextMeshProUGUI timerText;

    [Header("Settings")]
    public float totalTime = 180f; // 3 минуты
    public Color normalColor = Color.green;
    public Color warningColor = Color.red;
    public float warningTime = 30f; // когда меньше 30 секунд Ч цвет станет красным

    [Header("References")]
    public PositionBasedRLGController gameController;

    private float timeRemaining;
    private bool isRunning = true;
    private bool finished = false;

    void Start()
    {
        timeRemaining = totalTime;
        UpdateTimerDisplay();
    }

    void Update()
    {
        if (!isRunning || finished) return;

        timeRemaining -= Time.deltaTime;
        if (timeRemaining < 0f)
        {
            timeRemaining = 0f;
            isRunning = false;
            OnTimeOver();
        }

        UpdateTimerDisplay();
    }

    void UpdateTimerDisplay()
    {
        int minutes = Mathf.FloorToInt(timeRemaining / 60f);
        int seconds = Mathf.FloorToInt(timeRemaining % 60f);

        if (timerText != null)
        {
            timerText.text = $"{minutes:00}:{seconds:00}";
            timerText.color = (timeRemaining <= warningTime) ? warningColor : normalColor;
        }
    }

    public void OnFinishReached()
    {
        if (finished) return;
        finished = true;
        isRunning = false;
        Debug.Log("[Timer] Player finished before time expired!");
        LoadNextLevel();
    }

    void OnTimeOver()
    {
        Debug.Log("[Timer] Time is up!");
        if (gameController != null && !gameController.IsPlayerDead())
        {
            gameController.ForceDeath(); // убиваем игрока, если он жив
        }
    }

    void LoadNextLevel()
    {
        // «агружаем следующую сцену по индексу
        int currentIndex = SceneManager.GetActiveScene().buildIndex;
        if (SceneManager.sceneCountInBuildSettings > currentIndex + 1)
        {
            SceneManager.LoadScene(currentIndex + 1);
        }
        else
        {
            Debug.Log("[Timer] No next level found Ч restarting current one.");
            SceneManager.LoadScene(currentIndex);
        }
    }
}

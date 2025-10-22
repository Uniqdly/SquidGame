using UnityEngine;
using TMPro;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
public class GameTimer : MonoBehaviour
{
    [Header("Time settings")]
    [Tooltip("Total countdown time in seconds (3 minutes = 180)")]
    public float totalTime = 180f;
    [Tooltip("Start the timer automatically on scene start")]
    public bool startOnAwake = true;

    [Header("UI")]
    public TextMeshProUGUI timerText;
    [Tooltip("Color when time > warningTime")]
    public Color normalColor = Color.white;
    [Tooltip("Color when time <= warningTime")]
    public Color warningColor = Color.red;
    [Tooltip("Threshold (seconds) when timer changes color / plays warning")]
    public float warningTime = 30f;

    [Header("Audio (optional)")]
    public AudioClip tickClip;
    public float tickVolume = 0.6f;
    public bool playTick = true;
    public AudioClip endClip;
    public float endVolume = 1f;

    [Header("References")]
    [Tooltip("Reference to the main game controller (optional). If set, we'll call ForceDeath() on timeout.")]
    public PositionBasedRLGController rlgController;

    [Tooltip("Fallback: if rlgController is null and playerObj provided, we'll deactivate it on death.")]
    public GameObject playerObj;

    [Header("Next level")]
    [Tooltip("Optional: scene name to load when player finished before time. If empty, will try next build index.")]
    public string nextSceneName = "";

    [Header("Debug")]
    public bool debugLogs = true;

    // internal
    float remainingTime;
    bool running = false;
    bool finished = false;
    float tickAccumulator = 0f;
    AudioSource localAudio;

    void Awake()
    {
        remainingTime = Mathf.Max(0f, totalTime);

        // prepare audio source for ticks/end if needed
        if ((tickClip != null || endClip != null) && GetComponent<AudioSource>() == null)
        {
            localAudio = gameObject.AddComponent<AudioSource>();
            localAudio.playOnAwake = false;
            localAudio.spatialBlend = 0f; // UI 2D sound by default
        }
        else
        {
            localAudio = GetComponent<AudioSource>();
        }

        if (startOnAwake)
        {
            StartTimer();
        }
        else
        {
            UpdateTimerDisplayImmediate();
        }
    }

    void StartTimer()
    {
        remainingTime = Mathf.Max(0f, totalTime);
        running = true;
        finished = false;
        tickAccumulator = 0f;
        UpdateTimerDisplayImmediate();
        if (debugLogs) Debug.Log($"[GameTimer] Timer started: {remainingTime:F1}s");
    }

    public void StartTimerPublic() => StartTimer();
    public void StopTimerPublic() { running = false; }
    public void ResetTimerPublic() { remainingTime = Mathf.Max(0f, totalTime); UpdateTimerDisplayImmediate(); }

    void Update()
    {
        if (!running || finished) return;

        float dt = Time.deltaTime;
        remainingTime -= dt;
        if (remainingTime < 0f) remainingTime = 0f;

        // ticking audio each whole second (simple implementation)
        if (playTick && tickClip != null)
        {
            tickAccumulator += dt;
            if (tickAccumulator >= 1f)
            {
                int ticks = (int)tickAccumulator;
                tickAccumulator -= ticks;
                // play tick once for each passed second (usually 1)
                for (int i = 0; i < ticks; i++) PlayTick();
            }
        }

        UpdateTimerDisplayImmediate();

        if (remainingTime <= 0f)
        {
            running = false;
            OnTimeExpired();
        }
    }

    void PlayTick()
    {
        if (localAudio == null || tickClip == null) return;
        localAudio.PlayOneShot(tickClip, Mathf.Clamp01(tickVolume));
    }

    void PlayEndSound()
    {
        if (localAudio == null || endClip == null) return;
        localAudio.PlayOneShot(endClip, Mathf.Clamp01(endVolume));
    }

    void UpdateTimerDisplayImmediate()
    {
        if (timerText != null)
        {
            int minutes = Mathf.FloorToInt(remainingTime / 60f);
            int seconds = Mathf.FloorToInt(remainingTime % 60f);
            timerText.text = $"{minutes:00}:{seconds:00}";
            timerText.color = (remainingTime <= warningTime) ? warningColor : normalColor;
        }
    }

    // Внешний метод — вызывается, когда игрок пересёк финиш
    // Можно вызывать из PositionBasedRLGController.OnFinishCrossed()
    public void OnFinishReached()
    {
        if (finished)
        {
            if (debugLogs) Debug.Log("[GameTimer] OnFinishReached called but already finished.");
            return;
        }

        finished = true;
        running = false;
        if (debugLogs) Debug.Log("[GameTimer] Finish reached before time expired — loading next level.");
        // короткая задержка чтобы дать игроку почувствовать победу
        Invoke(nameof(LoadNextLevel), 1.0f);
    }

    // Вызывается локально, когда таймер умирает
    void OnTimeExpired()
    {
        if (debugLogs) Debug.Log("[GameTimer] Time expired.");

        // play end sound if any
        PlayEndSound();

        // если rlgController установлен и игрок ещё не мёртв => вызываем ForceDeath (реализуй ForceDeath публичным в контроллере)
        if (rlgController != null)
        {
            // если у контроллера есть метод IsPlayerDead, проверим
            bool controllerDead = false;
            try
            {
                controllerDead = rlgController.IsPlayerDead();
            }
            catch { controllerDead = false; }

            if (!controllerDead)
            {
                // ожидаем 0.5s чтобы дать проиграть end sound
                Invoke(nameof(CallControllerForceDeath), 0.5f);
                return;
            }
        }

        // fallback: если нет контроллера или он уже помечен как dead — просто деактивируем игрока
        if (playerObj != null)
        {
            playerObj.SetActive(false);
            Invoke(nameof(RestartLevel), 2f);
        }
        else
        {
            Invoke(nameof(RestartLevel), 2f);
        }
    }

    void CallControllerForceDeath()
    {
        if (rlgController != null)
        {
            // предполагаем, что у тебя есть публичный метод ForceDeath()
            rlgController.ForceDeath();
            // как запасной вариант, если ForceDeath не существует (редко), можно попробовать PlayGunshotAndDie через reflection, но лучше иметь публичный метод
        }
        else
        {
            if (playerObj != null) playerObj.SetActive(false);
            Invoke(nameof(RestartLevel), 2f);
        }
    }

    void LoadNextLevel()
    {
        if (!string.IsNullOrEmpty(nextSceneName))
        {
            // Попробуем загрузить сцену по имени
            if (Application.CanStreamedLevelBeLoaded(nextSceneName))
            {
                SceneManager.LoadScene(nextSceneName);
                return;
            }
            else
            {
                if (debugLogs) Debug.LogWarning($"[GameTimer] Next scene '{nextSceneName}' not in build settings or cannot be loaded. Falling back to build index.");
            }
        }

        int currentIndex = SceneManager.GetActiveScene().buildIndex;
        if (SceneManager.sceneCountInBuildSettings > currentIndex + 1)
        {
            SceneManager.LoadScene(currentIndex + 1);
        }
        else
        {
            if (debugLogs) Debug.Log("[GameTimer] No next scene found in build settings. Reloading current scene.");
            SceneManager.LoadScene(currentIndex);
        }
    }

    void RestartLevel()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    // expose info to other scripts if needed
    public float GetRemainingTime() => Mathf.Max(0f, remainingTime);
    public bool IsRunning() => running;
    public bool IsFinished() => finished;
}

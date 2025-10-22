using UnityEngine;
using TMPro;
using UnityEngine.SceneManagement;

public class RedLightGreenLightController : MonoBehaviour
{
    [Header("References")]
    public Transform playerRoot; // XR Origin или Main Camera
    public Transform finishTransform;
    public TextMeshProUGUI statusText;
    public TextMeshProUGUI timerText;

    [Header("Gameplay")]
    public float moveThreshold = 0.06f; // подбирайте
    public float finishDistance = 1.5f;
    public int smoothingFrames = 5;

    // internal state
    Vector3[] posBuffer;
    int posIndex = 0;
    bool isGreen = true;
    float phaseTimer = 0f;
    bool gameActive = true;

    // NEW: track whether player already crossed start and whether finished
    bool hasCrossedStart = false;
    bool hasFinished = false;

    void Start()
    {
        if (playerRoot == null && Camera.main != null) playerRoot = Camera.main.transform;
        posBuffer = new Vector3[Mathf.Max(1, smoothingFrames)];
        for (int i = 0; i < posBuffer.Length; i++) posBuffer[i] = playerRoot.position;
        UpdateUI();
    }

    void Update()
    {
        if (!gameActive) return;

        // smoothing
        posBuffer[posIndex] = playerRoot.position;
        posIndex = (posIndex + 1) % posBuffer.Length;

        Vector3 avg = Vector3.zero;
        for (int i = 0; i < posBuffer.Length; i++) avg += posBuffer[i];
        avg /= posBuffer.Length;

        Vector3 last = posBuffer[(posIndex - 1 + posBuffer.Length) % posBuffer.Length];
        Vector3 avgFlat = new Vector3(avg.x, 0f, avg.z);
        Vector3 lastFlat = new Vector3(last.x, 0f, last.z);
        float moved = Vector3.Distance(avgFlat, lastFlat);

        // NEW: провер€ем движение только если игрок уже стартовал и ещЄ не финишировал
        if (!isGreen && hasCrossedStart && !hasFinished && moved > moveThreshold)
        {
            OnPlayerDetected();
        }

        // проверка финиша (если используете дистанцию)
        if (!hasFinished && finishTransform != null)
        {
            float dist = Vector3.Distance(playerRoot.position, finishTransform.position);
            if (dist <= finishDistance)
            {
                // отмечаем финиш и вызываем обработчик
                hasFinished = true;
                OnPlayerWin();
            }
        }

        if (phaseTimer > 0f)
        {
            phaseTimer -= Time.deltaTime;
            if (timerText) timerText.text = $"Phase time: {phaseTimer:F1}s";
        }
        else if (timerText) timerText.text = "";
    }

    public void SetGreen(bool green, float timer = 0f)
    {
        isGreen = green;
        phaseTimer = timer;
        UpdateUI();
    }

    void UpdateUI()
    {
        if (statusText)
        {
            statusText.text = isGreen ? "GREEN Ч Move!" : "RED Ч Stop!";
            statusText.color = isGreen ? Color.green : Color.red;
        }
    }

    public void OnPlayerDetected()
    {
        if (!gameActive) return;
        gameActive = false;
        if (statusText) statusText.text = "Detected! You Lose";
        Debug.Log("[RLG] Player detected on RED.");
        // здесь можно вызвать анимацию смерти, проиграть звук и т.д.
        Invoke(nameof(Restart), 2f);
    }

    public void OnPlayerWin()
    {
        if (!gameActive) return;
        gameActive = false;
        if (statusText) statusText.text = "You Win!";
        Debug.Log("[RLG] Player reached finish.");
        Invoke(nameof(Restart), 2f);
    }

    void Restart()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    // NEW: публичные методы дл€ триггеров
    public void NotifyStartCrossed()
    {
        hasCrossedStart = true;
        Debug.Log("[RLG] Player crossed START line");
    }

    public void NotifyFinishCrossed()
    {
        hasFinished = true;
        Debug.Log("[RLG] Player crossed FINISH line");
        OnPlayerWin();
    }

    // Optional: reset start flag (если хотите, чтобы игрок мог откатитьс€ назад)
    public void ResetStartFlag()
    {
        hasCrossedStart = false;
        Debug.Log("[RLG] Start flag reset");
    }

    // ƒл€ отладки: показать текущее состо€ние
    public string GetStateDebug()
    {
        return $"isGreen={isGreen}, hasCrossedStart={hasCrossedStart}, hasFinished={hasFinished}";
    }
}

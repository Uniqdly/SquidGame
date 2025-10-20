using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

[DisallowMultipleComponent]
public class PositionBasedRLGController : MonoBehaviour
{
    [Header("Gunshot / Distant death")]
    [Tooltip("Clip с выстрелом, проигрывается из gunshotOrigin")]
    public AudioClip gunshotClip;
    [Tooltip("Позиция, откуда слышен выстрел (размести далеко от игрока, напр. за финишем)")]
    public Transform gunshotOrigin;
    [Tooltip("Громкость выстрела (0..1)")]
    [Range(0f, 1f)]
    public float gunshotVolume = 1f;
    [Tooltip("Задержка перед смертью после выстрела (сек)")]
    public float gunshotToDeathDelay = 0.8f;
    [Tooltip("Максимальная дистанция, на которой слышен звук (для AudioSource)")]
    public float gunshotMaxDistance = 80f;
    [Tooltip("Минимальная дистанция в которой звук будет на full volume")]
    public float gunshotMinDistance = 8f;

    [Header("References")]
    public Transform playerRoot;               // XR Origin or camera transform
    public Transform startLineTransform;
    public Transform finishLineTransform;
    public Transform dollTransform;
    public Light directionalLight;

    [Header("UI (optional)")]
    public TextMeshProUGUI statusText;
    public TextMeshProUGUI debugText;

    [Header("Gameplay times")]
    public float startDelay = 1.5f;
    public float greenDuration = 7f;
    public float redDuration = 4f;

    [Header("Movement detection")]
    public int smoothingFrames = 5;
    public float moveThreshold = 0.06f; // meters (horizontal)
    public bool ignoreVertical = true;  // compare only XZ

    [Header("Start/Finish rectangle (fallback)")]
    public bool useRectFallback = true;
    public float startRectHalfWidth = 6f;
    public float startRectDepth = 0.5f;
    public float finishRectHalfWidth = 6f;
    public float finishRectDepth = 0.5f;

    [Header("Light & doll")]
    public Color greenColor = new Color(0.7f, 1f, 0.7f);
    public Color redColor = new Color(1f, 0.6f, 0.6f);
    public float greenIntensity = 1.2f;
    public float redIntensity = 0.8f;
    public float lightBlendDuration = 0.5f;
    public float dollTurnSpeed = 6f;
    public float dollBackYaw = 180f;

    [Header("Death / Ragdoll")]
    [Tooltip("Компоненты, которые следует отключить при смерти (CharacterController, locomotion scripts и т.п.)")]
    public Behaviour[] disableOnDeath;
    [Tooltip("Если у игрока есть Rigidbody — можно добавить его сюда и убрать isKinematic = true на смерть, чтобы включить физическое падение")]
    public Rigidbody playerRigidbody;
    [Tooltip("Если true — проиграть звук смерти")]
    public AudioClip deathClip;
    [Tooltip("AudioSource для deathClip (если null, будет попытка найти на этом объекте)")]
    public AudioSource audioSource;
    public float deathRestartDelay = 2f;

    [Header("Movement logging")]
    public bool enableMovementLogging = true;
    [Tooltip("период логирования в секундах")]
    public float logInterval = 0.5f;
    [Tooltip("Хранить логи в памяти (можно выгрузить позже)")]
    public bool storeMovementLog = false;

    [Header("Debug")]
    public bool debugLogs = true;

    // internal
    Vector3[] posBuffer;
    int posIndex = 0;
    bool isGreen = true;
    float phaseTimer = 0f;

    bool hasCrossedStart = false;
    bool hasFinished = false;
    bool playerDead = false;

    Vector3 startPos;
    Vector3 startNormal;
    float lastStartSide = 0f;

    Vector3 finishPos;
    Vector3 finishNormal;

    Quaternion dollForwardRot;
    Quaternion dollBackRot;

    Coroutine lightBlendCoroutine;

    // logging internals
    float logTimer = 0f;
    List<string> movementLog = new List<string>();

    void Awake()
    {
        if (directionalLight == null) directionalLight = RenderSettings.sun;
        posBuffer = new Vector3[Mathf.Max(1, smoothingFrames)];
    }

    void Start()
    {
        if (playerRoot == null && Camera.main != null) playerRoot = Camera.main.transform;

        Vector3 initial = (playerRoot != null) ? playerRoot.position : Vector3.zero;
        for (int i = 0; i < posBuffer.Length; i++) posBuffer[i] = initial;

        if (startLineTransform != null)
        {
            startPos = startLineTransform.position;
            startNormal = (startLineTransform.forward.magnitude > 0.001f) ? startLineTransform.forward.normalized : Vector3.forward;
            if (playerRoot != null) lastStartSide = Mathf.Sign(Vector3.Dot(playerRoot.position - startPos, startNormal));
            if (debugLogs) Debug.Log($"[PBL] startPos={startPos}, startNormal={startNormal}, lastSide={lastStartSide}");
        }

        if (finishLineTransform != null)
        {
            finishPos = finishLineTransform.position;
            finishNormal = (finishLineTransform.forward.magnitude > 0.001f) ? finishLineTransform.forward.normalized : Vector3.forward;
            if (debugLogs) Debug.Log($"[PBL] finishPos={finishPos}, finishNormal={finishNormal}");
        }

        if (dollTransform != null)
        {
            dollForwardRot = dollTransform.rotation;
            dollBackRot = dollForwardRot * Quaternion.Euler(0f, dollBackYaw, 0f);
        }

        // prepare audioSource default
        if (audioSource == null && deathClip != null)
        {
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
                audioSource.playOnAwake = false;
            }
        }

        StartCoroutine(GameLoop());
    }
    void PlayGunshotAndDie()
    {
        if (playerDead) return;
        playerDead = true;

        if (debugLogs) Debug.Log("[PBL] Player detected on RED — playing distant gunshot!");

        // Создаем временный источник звука в точке gunshotOrigin
        if (gunshotClip != null && gunshotOrigin != null)
        {
            GameObject gunshotObj = new GameObject("GunshotAudio");
            gunshotObj.transform.position = gunshotOrigin.position;
            AudioSource shotSource = gunshotObj.AddComponent<AudioSource>();
            shotSource.spatialBlend = 1f; // 3D-звук
            shotSource.rolloffMode = AudioRolloffMode.Logarithmic;
            shotSource.minDistance = gunshotMinDistance;
            shotSource.maxDistance = gunshotMaxDistance;
            shotSource.volume = gunshotVolume;
            shotSource.PlayOneShot(gunshotClip);

            Destroy(gunshotObj, gunshotClip.length + 1f);
        }
        else
        {
            Debug.LogWarning("[PBL] Gunshot clip or origin not assigned!");
        }

        // Отложенный вызов смерти
        StartCoroutine(GunshotDeathDelay());
    }

    // Вспомогательная корутина
    IEnumerator GunshotDeathDelay()
    {
        yield return new WaitForSeconds(gunshotToDeathDelay);
        DoDeath();
    }
    void Update()
    {
        Update_DebugInputs();

        if (playerRoot == null) return;
        if (playerDead) return;

        // START detection (plane + rect fallback)
        if (!hasCrossedStart && startLineTransform != null)
        {
            float currSide = Mathf.Sign(Vector3.Dot(playerRoot.position - startPos, startNormal));
            if (lastStartSide <= 0f && currSide > 0f)
            {
                OnStartCrossed("plane");
                hasCrossedStart = true;
            }
            lastStartSide = currSide;

            if (!hasCrossedStart && useRectFallback)
            {
                if (IsInsideRect(playerRoot.position, startLineTransform, startRectHalfWidth, startRectDepth))
                {
                    OnStartCrossed("rectFallback");
                    hasCrossedStart = true;
                }
            }
        }

        // FINISH detection
        if (!hasFinished && finishLineTransform != null)
        {
            float currF = Mathf.Sign(Vector3.Dot(playerRoot.position - finishPos, finishNormal));
            if (currF > 0f)
            {
                OnFinishCrossed("plane");
                hasFinished = true;
            }
            else if (useRectFallback && IsInsideRect(playerRoot.position, finishLineTransform, finishRectHalfWidth, finishRectDepth))
            {
                OnFinishCrossed("rectFallback");
                hasFinished = true;
            }
        }

        // Movement smoothing & detection
        posBuffer[posIndex] = playerRoot.position;
        posIndex = (posIndex + 1) % posBuffer.Length;

        Vector3 avg = Vector3.zero;
        for (int i = 0; i < posBuffer.Length; i++) avg += posBuffer[i];
        avg /= posBuffer.Length;

        Vector3 last = posBuffer[(posIndex - 1 + posBuffer.Length) % posBuffer.Length];
        Vector3 a = ignoreVertical ? new Vector3(avg.x, 0f, avg.z) : avg;
        Vector3 l = ignoreVertical ? new Vector3(last.x, 0f, last.z) : last;
        float moved = Vector3.Distance(a, l);

        // logging movement (periodic)
        if (enableMovementLogging)
        {
            logTimer -= Time.deltaTime;
            if (logTimer <= 0f)
            {
                string s = $"[MoveLog] t={Time.time:F2} moved={moved:F4} isGreen={isGreen} crossedStart={hasCrossedStart} finished={hasFinished}";
                Debug.Log(s);
                if (storeMovementLog) movementLog.Add(s);
                logTimer = Mathf.Max(0.01f, logInterval);
            }
        }

        // Only kill when RED & started & not finished
        if (!isGreen && hasCrossedStart && !hasFinished)
        {
            if (moved > moveThreshold)
            {
                if (debugLogs) Debug.Log($"[PBL] Movement detected on RED. moved={moved:F3}, threshold={moveThreshold}");
                PlayGunshotAndDie();
            }

        }

        // UI debug text
        if (debugText != null)
        {
            debugText.text = $"green={isGreen}\ncrossedStart={hasCrossedStart}\nfinished={hasFinished}\nmoved={moved:F3}";
        }

        // Doll smoothing rotation
        if (dollTransform != null)
        {
            Quaternion target = isGreen ? dollForwardRot : dollBackRot;
            dollTransform.rotation = Quaternion.Slerp(dollTransform.rotation, target, Time.deltaTime * dollTurnSpeed);
        }
    }
    // ---- DEBUG BLOCK START ----
    // Вставь этот код внутрь класса (PositionBasedRLGController), например внизу файла.

    [Header("DEBUG Helpers")]
    public bool debugVerbose = true;
    public KeyCode debugForceStartKey = KeyCode.F1;   // вручную отметить старт
    public KeyCode debugForceFinishKey = KeyCode.F2;  // вручную отметить финиш
    public KeyCode debugPrintStateKey = KeyCode.F3;   // печать позиции/дота

    // Печать текущих значений для диагностики
    void DebugPrintState()
    {
        if (playerRoot == null)
        {
            Debug.LogWarning("[DBG] playerRoot == null");
            return;
        }
        string s = $"[DBG] playerPos={playerRoot.position.ToString("F3")}";
        if (startLineTransform != null)
        {
            s += $", startPos={startPos.ToString("F3")}, startForward={startNormal.ToString("F3")}";
            float dot = Vector3.Dot(playerRoot.position - startPos, startNormal);
            s += $", dot={dot:F4}, lastStartSide={lastStartSide}";
        }
        if (finishLineTransform != null)
        {
            float dotF = Vector3.Dot(playerRoot.position - finishPos, finishNormal);
            s += $", finishPos={finishPos.ToString("F3")}, finishDot={dotF:F4}";
        }
        s += $", hasCrossedStart={hasCrossedStart}, hasFinished={hasFinished}, isGreen={isGreen}";
        Debug.Log(s);
    }

    // Метод для ручного вызова проверки (можно вызвать из инспектора)
    [ContextMenu("Run Manual Start/Finish Check")]
    public void ManualCheckStartFinish()
    {
        if (startLineTransform != null)
        {
            float currSide = Mathf.Sign(Vector3.Dot(playerRoot.position - startPos, startNormal));
            Debug.Log($"[DBG] Manual start check currSide={currSide}, lastStartSide={lastStartSide}");
            if (lastStartSide <= 0f && currSide > 0f)
            {
                OnStartCrossed("manual_check_plane");
                hasCrossedStart = true;
            }
            else if (useRectFallback && IsInsideRect(playerRoot.position, startLineTransform, startRectHalfWidth, startRectDepth))
            {
                OnStartCrossed("manual_check_rect");
                hasCrossedStart = true;
            }
        }
        if (finishLineTransform != null)
        {
            float currF = Mathf.Sign(Vector3.Dot(playerRoot.position - finishPos, finishNormal));
            Debug.Log($"[DBG] Manual finish check currF={currF}");
            if (currF > 0f || (useRectFallback && IsInsideRect(playerRoot.position, finishLineTransform, finishRectHalfWidth, finishRectDepth)))
            {
                OnFinishCrossed("manual_check");
                hasFinished = true;
            }
        }
    }

    void Update_DebugInputs()
    {
        if (!debugVerbose) return;

        if (Input.GetKeyDown(debugForceStartKey))
        {
            Debug.Log("[DBG] ForceStart pressed");
            hasCrossedStart = true;
            OnStartCrossed("forced_key");
        }
        if (Input.GetKeyDown(debugForceFinishKey))
        {
            Debug.Log("[DBG] ForceFinish pressed");
            hasFinished = true;
            OnFinishCrossed("forced_key");
        }
        if (Input.GetKeyDown(debugPrintStateKey))
        {
            DebugPrintState();
        }
    }
    // ---- DEBUG BLOCK END ----

    IEnumerator GameLoop()
    {
        yield return new WaitForSeconds(startDelay);

        while (!playerDead && !hasFinished)
        {
            // GREEN
            SetGreen(true, greenDuration);
            yield return new WaitForSeconds(greenDuration);

            // RED
            SetGreen(false, redDuration);
            yield return new WaitForSeconds(redDuration);
        }
    }

    public void SetGreen(bool green, float timer)
    {
        isGreen = green;
        phaseTimer = timer;
        if (statusText != null)
        {
            statusText.text = isGreen ? "GREEN — Move!" : "RED — Stop!";
            statusText.color = isGreen ? Color.green : Color.red;
        }

        if (directionalLight != null)
        {
            if (lightBlendCoroutine != null) StopCoroutine(lightBlendCoroutine);
            Color targetColor = green ? greenColor : redColor;
            float targetIntensity = green ? greenIntensity : redIntensity;
            lightBlendCoroutine = StartCoroutine(BlendLight(targetColor, targetIntensity, lightBlendDuration));
        }

        if (debugLogs) Debug.Log($"[PBL] SetGreen({green})");
    }

    IEnumerator BlendLight(Color targetColor, float targetIntensity, float duration)
    {
        if (directionalLight == null) yield break;
        Color s = directionalLight.color;
        float si = directionalLight.intensity;
        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            float f = Mathf.Clamp01(t / duration);
            directionalLight.color = Color.Lerp(s, targetColor, f);
            directionalLight.intensity = Mathf.Lerp(si, targetIntensity, f);
            yield return null;
        }
        directionalLight.color = targetColor;
        directionalLight.intensity = targetIntensity;
        lightBlendCoroutine = null;
    }

    // Death action: disables specified components, optionally toggles rigidbody, plays sound, shows UI and restarts scene
    void DoDeath()
    {
        if (debugLogs) Debug.Log("[PBL] Player dead.");

        // Отключаем управление
        if (disableOnDeath != null)
        {
            foreach (var b in disableOnDeath)
            {
                if (b != null) b.enabled = false;
            }
        }

        // Падение игрока
        if (playerRigidbody != null)
        {
            playerRigidbody.isKinematic = false;
            playerRigidbody.AddForce(Vector3.down * 2f, ForceMode.Impulse);
        }

        // Звук смерти
        if (deathClip != null && audioSource != null)
        {
            audioSource.PlayOneShot(deathClip);
        }

        if (statusText != null)
            statusText.text = "Detected! You Lose";

        StartCoroutine(RestartAfterDelay(deathRestartDelay));
    }

    IEnumerator RestartAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        // optional: dump movement log to console
        if (storeMovementLog && movementLog != null && movementLog.Count > 0)
        {
            Debug.Log($"[PBL] Movement log entries: {movementLog.Count}");
            for (int i = 0; i < movementLog.Count; i++)
            {
                Debug.Log(movementLog[i]);
            }
        }
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    void OnStartCrossed(string reason)
    {
        if (debugLogs) Debug.Log($"[PBL] Start crossed ({reason})");
        if (statusText != null) statusText.text = "Started!";
        hasCrossedStart = true;
    }

    void OnFinishCrossed(string reason)
    {
        if (debugLogs) Debug.Log($"[PBL] Finish crossed ({reason})");
        if (statusText != null) statusText.text = "Finished!";
        hasFinished = true;
        StartCoroutine(WinAndRestart());
    }

    IEnumerator WinAndRestart()
    {
        if (statusText != null) statusText.text = "You Win!";
        yield return new WaitForSeconds(2f);
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    bool IsInsideRect(Vector3 worldPoint, Transform rectTransform, float halfWidth, float depth)
    {
        Vector3 local = rectTransform.InverseTransformPoint(worldPoint);
        bool insideX = Mathf.Abs(local.x) <= halfWidth;
        bool insideZ = Mathf.Abs(local.z) <= depth * 0.5f;
        return insideX && insideZ;
    }

    // public helper: force death from other scripts / buttons
    public void ForceDeath()
    {
        DoDeath();
    }

    // public helper: reset movement log
    public void ClearMovementLog()
    {
        if (movementLog != null) movementLog.Clear();
    }
}

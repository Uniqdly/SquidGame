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

    [Header("Movement detection (player position smoothing)")]
    public int smoothingFrames = 5;
    public float moveThreshold = 0.06f; // meters (for playerRoot positional movement)
    public bool ignoreVertical = true;  // compare only XZ for playerRoot movement

    [Header("Controller-based movement (optional)")]
    [Tooltip("Если true — движения контроллеров будут толкать игрока в пространстве")]
    public bool useControllerMovement = true;
    [Tooltip("Правый контроллер (можно указать только один из контроллеров)")]
    public Transform rightController;
    [Tooltip("Левый контроллер (можно указать только один из контроллеров)")]
    public Transform leftController;
    [Tooltip("Чувствительность: какая доля перемещения контроллера переносится на игрока")]
    public float controllerMoveFactor = 1.0f;
    [Tooltip("Порог перемещения контроллера (в метрах) чтобы начать двигать игрока")]
    public float controllerMoveThreshold = 0.01f;
    [Tooltip("Если true — учитывать вертикальную компоненту контроллера при движении игрока")]
    public bool controllerAffectsY = true;

    [Header("Start/Finish rectangle (fallback)")]
    public bool useRectFallback = true;
    public float startRectHalfWidth = 6f;
    public float startRectDepth = 0.5f;
    public float finishRectHalfWidth = 6f;
    public float finishRectDepth = 0.5f;
    public bool HasFinished { get { return hasFinished; } }

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
    public bool playerDead = false;

    Vector3 startPos;
    Vector3 startNormal;
    float lastStartSide = 0f;

    Vector3 finishPos;
    Vector3 finishNormal;
    float lastFinishSide = 0f;

    Quaternion dollForwardRot;
    Quaternion dollBackRot;

    Coroutine lightBlendCoroutine;

    // controller smoothing buffer
    Vector3[] ctrlBuffer;
    int ctrlIndex = 0;

    // logging internals
    float logTimer = 0f;
    List<string> movementLog = new List<string>();

    void Awake()
    {
        if (directionalLight == null) directionalLight = RenderSettings.sun;
        posBuffer = new Vector3[Mathf.Max(1, smoothingFrames)];
        ctrlBuffer = new Vector3[Mathf.Max(1, smoothingFrames)];
    }
    public bool IsPlayerDead() { return playerDead; }
    void Start()
    {
        if (playerRoot == null && Camera.main != null) playerRoot = Camera.main.transform;

        Vector3 initial = (playerRoot != null) ? playerRoot.position : Vector3.zero;
        for (int i = 0; i < posBuffer.Length; i++) posBuffer[i] = initial;

        Vector3 ctrlInit = GetControllersAveragePosition();
        for (int i = 0; i < ctrlBuffer.Length; i++) ctrlBuffer[i] = ctrlInit;

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
            // initialize lastFinishSide analogous to start
            if (playerRoot != null) lastFinishSide = Mathf.Sign(Vector3.Dot(playerRoot.position - finishPos, finishNormal));
            if (debugLogs) Debug.Log($"[PBL] finishPos={finishPos}, finishNormal={finishNormal}, lastFinishSide={lastFinishSide}");
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

        // FINISH detection — use sign-change logic (safer) + rect fallback
        if (!hasFinished && finishLineTransform != null)
        {
            float currF = Mathf.Sign(Vector3.Dot(playerRoot.position - finishPos, finishNormal));
            if (lastFinishSide <= 0f && currF > 0f)
            {
                OnFinishCrossed("plane");
                hasFinished = true;
            }
            lastFinishSide = currF;

            if (!hasFinished && useRectFallback && IsInsideRect(playerRoot.position, finishLineTransform, finishRectHalfWidth, finishRectDepth))
            {
                OnFinishCrossed("rectFallback");
                hasFinished = true;
            }
        }

        // Movement smoothing & detection (playerRoot)
        posBuffer[posIndex] = playerRoot.position;
        posIndex = (posIndex + 1) % posBuffer.Length;

        Vector3 avg = Vector3.zero;
        for (int i = 0; i < posBuffer.Length; i++) avg += posBuffer[i];
        avg /= posBuffer.Length;

        Vector3 last = posBuffer[(posIndex - 1 + posBuffer.Length) % posBuffer.Length];
        Vector3 a = ignoreVertical ? new Vector3(avg.x, 0f, avg.z) : avg;
        Vector3 l = ignoreVertical ? new Vector3(last.x, 0f, last.z) : last;
        float movedPlayer = Vector3.Distance(a, l);

        // Controller smoothing & detection
        Vector3 ctrlAvg = Vector3.zero;
        Vector3 currCtrl = GetControllersAveragePosition();
        ctrlBuffer[ctrlIndex] = currCtrl;
        ctrlIndex = (ctrlIndex + 1) % ctrlBuffer.Length;
        for (int i = 0; i < ctrlBuffer.Length; i++) ctrlAvg += ctrlBuffer[i];
        ctrlAvg /= ctrlBuffer.Length;

        Vector3 prevCtrl = ctrlBuffer[(ctrlIndex - 1 + ctrlBuffer.Length) % ctrlBuffer.Length];
        Vector3 ctrlA = ignoreVertical ? new Vector3(ctrlAvg.x, 0f, ctrlAvg.z) : ctrlAvg;
        Vector3 ctrlL = ignoreVertical ? new Vector3(prevCtrl.x, 0f, prevCtrl.z) : prevCtrl;
        Vector3 ctrlDelta = ctrlAvg - prevCtrl;
        float ctrlDeltaMag = ctrlDelta.magnitude;

        // If using controller movement — map controller delta into player movement (in camera/gaze space)
        if (useControllerMovement && (rightController != null || leftController != null))
        {
            if (ctrlDeltaMag >= controllerMoveThreshold)
            {
                // Map controller world delta into camera local space, then build movement in world aligned with camera forward/right/up
                Transform cam = Camera.main != null ? Camera.main.transform : playerRoot;
                Vector3 localDelta = cam.InverseTransformVector(ctrlDelta); // controller delta in camera local space
                Vector3 moveWorld = Vector3.zero;
                // forward/backwards (local Z) -> camera forward
                moveWorld += cam.forward * localDelta.z;
                // lateral (local X)
                moveWorld += cam.right * localDelta.x;
                // vertical: allow optional mapping to world up
                if (controllerAffectsY)
                    moveWorld += cam.up * localDelta.y;

                // apply factor and translate playerRoot (preserve Y if you don't want vertical)
                Vector3 apply = moveWorld * controllerMoveFactor;
                // If you want to keep player on ground, zero Y unless controllerAffectsY
                if (!controllerAffectsY) apply.y = 0f;

                playerRoot.position += apply;
                // also update posBuffer immediate to avoid false big "movedPlayer" detection next frame
                posBuffer[posIndex] = playerRoot.position;
            }
        }

        // Combined moved to consider for RED detection: take the max of player-root motion and controller delta magnitude (projected if ignoreVertical)
        float movedForDetection = movedPlayer;
        if (ctrlDeltaMag > movedForDetection) movedForDetection = ctrlDeltaMag;

        // logging movement (periodic)
        if (enableMovementLogging)
        {
            logTimer -= Time.deltaTime;
            if (logTimer <= 0f)
            {
                string s = $"[MoveLog] t={Time.time:F2} movedPlayer={movedPlayer:F4} ctrlDelta={ctrlDeltaMag:F4} isGreen={isGreen} crossedStart={hasCrossedStart} finished={hasFinished}";
                if (debugLogs) Debug.Log(s);
                if (storeMovementLog) movementLog.Add(s);
                logTimer = Mathf.Max(0.01f, logInterval);
            }
        }

        // Only kill when RED & started & not finished
        if (!isGreen && hasCrossedStart && !hasFinished)
        {
            if (movedForDetection > moveThreshold)
            {
                if (debugLogs) Debug.Log($"[PBL] Movement detected on RED. movedForDetection={movedForDetection:F3}, threshold={moveThreshold}");
                PlayGunshotAndDie();
            }
        }

        // UI debug text
        if (debugText != null)
        {
            debugText.text = $"green={isGreen}\ncrossedStart={hasCrossedStart}\nfinished={hasFinished}\nplayerMoved={movedPlayer:F3}\nctrlDelta={ctrlDeltaMag:F3}";
        }

        // Doll smoothing rotation
        if (dollTransform != null)
        {
            Quaternion target = isGreen ? dollForwardRot : dollBackRot;
            dollTransform.rotation = Quaternion.Slerp(dollTransform.rotation, target, Time.deltaTime * dollTurnSpeed);
        }
    }

    // ---- DEBUG BLOCK START ----
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
            s += $", finishPos={finishPos.ToString("F3")}, finishDot={dotF:F4}, lastFinishSide={lastFinishSide}";
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
            Debug.Log($"[DBG] Manual finish check currF={currF}, lastFinishSide={lastFinishSide}");
            if (lastFinishSide <= 0f && currF > 0f)
            {
                OnFinishCrossed("manual_check_plane");
                hasFinished = true;
            }
            else if (useRectFallback && IsInsideRect(playerRoot.position, finishLineTransform, finishRectHalfWidth, finishRectDepth))
            {
                OnFinishCrossed("manual_check_rect");
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

    public void OnFinishCrossed(string reason)
    {
        if (debugLogs) Debug.Log($"[PBL] Finish crossed ({reason})");
        if (statusText != null) statusText.text = "Finished!";
        hasFinished = true;

        var gt = FindObjectOfType<GameTimer>();
        if (gt != null)
        {
            gt.OnFinishReached();
        }

    }

    // helper: return averaged controller world position (right+left if present)
    Vector3 GetControllersAveragePosition()
    {
        Vector3 sum = Vector3.zero;
        int cnt = 0;
        if (rightController != null) { sum += rightController.position; cnt++; }
        if (leftController != null) { sum += leftController.position; cnt++; }
        if (cnt == 0) return playerRoot != null ? playerRoot.position : Vector3.zero;
        return sum / cnt;
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

using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit; // дл€ безопасной деактивации XR перед загрузкой

[DisallowMultipleComponent]
public class PositionBasedRLGController : MonoBehaviour
{
    [Header("Gunshot (distant death)")]
    public AudioClip gunshotClip;
    public Transform gunshotOrigin;
    [Range(0f, 1f)] public float gunshotVolume = 1f;
    public float gunshotToDeathDelay = 0.8f;
    public float gunshotMaxDistance = 80f;
    public float gunshotMinDistance = 8f;

    [Header("References")]
    public Transform playerRoot;      // XR origin or camera
    public Transform startLineTransform;
    public Transform finishLineTransform;
    public Transform dollTransform;
    public Light directionalLight;

    [Header("Gameplay timings")]
    public float startDelay = 1.5f;
    public float greenDuration = 7f;
    public float redDuration = 4f;
    [Tooltip("—екунд до перезапуска сцены после смерти")]
    public float deathRestartDelay = 2f;
    [Tooltip("«адержка перед загрузкой следующей сцены при пересечении финиша")]
    public float finishLoadDelay = 0.15f;

    [Header("Movement detection")]
    public int smoothingFrames = 5;
    public float moveThreshold = 0.06f;
    public bool ignoreVertical = true;

    [Header("Controller movement (optional)")]
    public bool useControllerMovement = true;
    public Transform rightController;
    public Transform leftController;
    public float controllerMoveFactor = 1.0f;
    public float controllerMoveThreshold = 0.01f;
    public bool controllerAffectsY = true;

    [Header("Start/Finish rect fallback")]
    public bool useRectFallback = true;
    public float startRectHalfWidth = 6f;
    public float startRectDepth = 0.5f;
    public float finishRectHalfWidth = 6f;
    public float finishRectDepth = 0.5f;
    public bool HasFinished => hasFinished;

    [Header("Light & doll")]
    public Color greenColor = new Color(0.7f, 1f, 0.7f);
    public Color redColor = new Color(1f, 0.6f, 0.6f);
    public float greenIntensity = 1.2f;
    public float redIntensity = 0.8f;
    public float lightBlendDuration = 0.5f;
    [Tooltip("ƒлительность полного поворота куклы в секундах (измен€й тут).")]
    public float dollTurnDuration = 0.5f; // <- измен€й эту переменную дл€ настройки времени поворота
    public float dollBackYaw = 180f;

    [Header("Doll audio (play when doll turns)")]
    public AudioClip dollTurnToPlayerClip;   // sound for GREEN
    public AudioClip dollTurnAwayClip;       // sound for RED
    public AudioSource dollAudioSource;      // will try to find on dollTransform if null
    [Tooltip("≈сли true Ч звук куклы будет loop-итьс€ в текущем состо€нии")]
    public bool dollLoopTurnSound = false;

    [Header("Misc")]
    public bool debugLogs = true;

    // internals
    Vector3[] posBuffer;
    int posIndex;
    bool isGreen = true;
    float phaseTimer;

    bool hasCrossedStart;
    bool hasFinished;
    bool playerDead;

    Vector3 startPos;
    Vector3 startNormal;
    float lastStartSide;

    Vector3 finishPos;
    Vector3 finishNormal;
    float lastFinishSide;

    Quaternion dollForwardRot;
    Quaternion dollBackRot;

    Coroutine lightBlendCoroutine;

    // controller smoothing
    Vector3[] ctrlBuffer;
    int ctrlIndex;

    void Awake()
    {
        if (directionalLight == null) directionalLight = RenderSettings.sun;
        posBuffer = new Vector3[Mathf.Max(1, smoothingFrames)];
        ctrlBuffer = new Vector3[Mathf.Max(1, smoothingFrames)];
    }

    void Start()
    {
        if (playerRoot == null && Camera.main != null) playerRoot = Camera.main.transform;

        Vector3 initial = playerRoot != null ? playerRoot.position : Vector3.zero;
        for (int i = 0; i < posBuffer.Length; i++) posBuffer[i] = initial;

        Vector3 ctrlInit = GetControllersAveragePosition();
        for (int i = 0; i < ctrlBuffer.Length; i++) ctrlBuffer[i] = ctrlInit;

        if (startLineTransform != null)
        {
            startPos = startLineTransform.position;
            startNormal = startLineTransform.forward.sqrMagnitude > 0.0001f ? startLineTransform.forward.normalized : Vector3.forward;
            if (playerRoot != null) lastStartSide = Mathf.Sign(Vector3.Dot(playerRoot.position - startPos, startNormal));
        }

        if (finishLineTransform != null)
        {
            finishPos = finishLineTransform.position;
            finishNormal = finishLineTransform.forward.sqrMagnitude > 0.0001f ? finishLineTransform.forward.normalized : Vector3.forward;
            if (playerRoot != null) lastFinishSide = Mathf.Sign(Vector3.Dot(playerRoot.position - finishPos, finishNormal));
        }

        if (dollTransform != null)
        {
            dollForwardRot = dollTransform.rotation;
            dollBackRot = dollForwardRot * Quaternion.Euler(0f, dollBackYaw, 0f);
        }

        if (dollAudioSource == null && dollTransform != null)
        {
            dollAudioSource = dollTransform.GetComponent<AudioSource>();
            if (dollAudioSource == null)
            {
                dollAudioSource = dollTransform.gameObject.AddComponent<AudioSource>();
                dollAudioSource.playOnAwake = false;
                dollAudioSource.spatialBlend = 1f;
            }
        }

        // начальное состо€ние: свет зелЄный и (если включено) loop-озвучка куклы
        isGreen = true;
        SetGreen(true, greenDuration);

        StartCoroutine(GameLoop());
    }

    void Update()
    {
        if (playerRoot == null || playerDead) return;

        // START detection
        if (!hasCrossedStart && startLineTransform != null)
        {
            float currSide = Mathf.Sign(Vector3.Dot(playerRoot.position - startPos, startNormal));
            if (lastStartSide <= 0f && currSide > 0f) { OnStartCrossed(); hasCrossedStart = true; }
            lastStartSide = currSide;
            if (!hasCrossedStart && useRectFallback && IsInsideRect(playerRoot.position, startLineTransform, startRectHalfWidth, startRectDepth))
            {
                OnStartCrossed(); hasCrossedStart = true;
            }
        }

        // FINISH detection
        if (!hasFinished && finishLineTransform != null)
        {
            float currF = Mathf.Sign(Vector3.Dot(playerRoot.position - finishPos, finishNormal));
            if (lastFinishSide <= 0f && currF > 0f) { OnFinishCrossed(); hasFinished = true; }
            lastFinishSide = currF;
            if (!hasFinished && useRectFallback && IsInsideRect(playerRoot.position, finishLineTransform, finishRectHalfWidth, finishRectDepth))
            {
                OnFinishCrossed(); hasFinished = true;
            }
        }

        // Movement smoothing (playerRoot)
        posBuffer[posIndex] = playerRoot.position;
        posIndex = (posIndex + 1) % posBuffer.Length;
        Vector3 avg = Vector3.zero;
        for (int i = 0; i < posBuffer.Length; i++) avg += posBuffer[i];
        avg /= posBuffer.Length;
        Vector3 last = posBuffer[(posIndex - 1 + posBuffer.Length) % posBuffer.Length];
        Vector3 a = ignoreVertical ? new Vector3(avg.x, 0f, avg.z) : avg;
        Vector3 l = ignoreVertical ? new Vector3(last.x, 0f, last.z) : last;
        float movedPlayer = Vector3.Distance(a, l);

        // Controller smoothing & movement
        Vector3 currCtrl = GetControllersAveragePosition();
        ctrlBuffer[ctrlIndex] = currCtrl;
        ctrlIndex = (ctrlIndex + 1) % ctrlBuffer.Length;
        Vector3 ctrlAvg = Vector3.zero;
        for (int i = 0; i < ctrlBuffer.Length; i++) ctrlAvg += ctrlBuffer[i];
        ctrlAvg /= ctrlBuffer.Length;
        Vector3 prevCtrl = ctrlBuffer[(ctrlIndex - 1 + ctrlBuffer.Length) % ctrlBuffer.Length];
        Vector3 ctrlDelta = ctrlAvg - prevCtrl;
        float ctrlDeltaMag = (ignoreVertical ? new Vector3(ctrlDelta.x, 0f, ctrlDelta.z).magnitude : ctrlDelta.magnitude);

        if (useControllerMovement && (rightController != null || leftController != null))
        {
            if (ctrlDeltaMag >= controllerMoveThreshold)
            {
                Transform cam = Camera.main != null ? Camera.main.transform : playerRoot;
                Vector3 localDelta = cam.InverseTransformVector(ctrlDelta);
                Vector3 moveWorld = cam.forward * localDelta.z + cam.right * localDelta.x;
                if (controllerAffectsY) moveWorld += cam.up * localDelta.y;
                Vector3 apply = moveWorld * controllerMoveFactor;
                if (!controllerAffectsY) apply.y = 0f;
                playerRoot.position += apply;
                posBuffer[posIndex] = playerRoot.position;
            }
        }

        float movedForDetection = Mathf.Max(movedPlayer, ctrlDeltaMag);

        // Kill condition: RED & started & not finished
        if (!isGreen && hasCrossedStart && !hasFinished)
        {
            if (movedForDetection > moveThreshold)
            {
                if (debugLogs) Debug.Log($"[PBL] Movement detected on RED -> gunshot");
                PlayGunshotAndDie();
            }
        }

        // Doll rotation smoothing (use dollTurnDuration)
        if (dollTransform != null)
        {
            Quaternion target = isGreen ? dollForwardRot : dollBackRot;
            float frac = Mathf.Clamp01(Time.deltaTime / Mathf.Max(0.0001f, dollTurnDuration));
            dollTransform.rotation = Quaternion.Slerp(dollTransform.rotation, target, frac);
        }
    }

    IEnumerator GameLoop()
    {
        yield return new WaitForSeconds(startDelay);
        while (!playerDead && !hasFinished)
        {
            SetGreen(true, greenDuration);
            yield return new WaitForSeconds(greenDuration);
            SetGreen(false, redDuration);
            yield return new WaitForSeconds(redDuration);
        }
    }

    public void SetGreen(bool green, float timer)
    {
        if (isGreen == green) { phaseTimer = timer; return; }
        bool previous = isGreen;
        isGreen = green;
        phaseTimer = timer;

        // light blending
        if (directionalLight != null)
        {
            if (lightBlendCoroutine != null) StopCoroutine(lightBlendCoroutine);
            Color targetColor = green ? greenColor : redColor;
            float targetIntensity = green ? greenIntensity : redIntensity;
            lightBlendCoroutine = StartCoroutine(BlendLight(targetColor, targetIntensity, lightBlendDuration));
        }

        // doll audio: loop or one-shot
        if (dollAudioSource != null)
        {
            if (dollLoopTurnSound)
            {
                AudioClip clip = isGreen ? dollTurnToPlayerClip : dollTurnAwayClip;
                if (clip != null)
                {
                    if (dollAudioSource.clip != clip || !dollAudioSource.isPlaying)
                    {
                        dollAudioSource.clip = clip;
                        dollAudioSource.loop = true;
                        dollAudioSource.Play();
                    }
                }
                else
                {
                    dollAudioSource.Stop();
                    dollAudioSource.clip = null;
                    dollAudioSource.loop = false;
                }
            }
            else
            {
                if (isGreen && dollTurnToPlayerClip != null) dollAudioSource.PlayOneShot(dollTurnToPlayerClip);
                else if (!isGreen && dollTurnAwayClip != null) dollAudioSource.PlayOneShot(dollTurnAwayClip);
            }
        }

        if (debugLogs) Debug.Log($"[PBL] SetGreen({isGreen}) (prev={previous})");
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

    void PlayGunshotAndDie()
    {
        if (playerDead) return;
        playerDead = true;

        // play gunshot in world
        if (gunshotClip != null && gunshotOrigin != null)
        {
            GameObject gunshotObj = new GameObject("GunshotAudio");
            gunshotObj.transform.position = gunshotOrigin.position;
            AudioSource shotSource = gunshotObj.AddComponent<AudioSource>();
            shotSource.spatialBlend = 1f;
            shotSource.rolloffMode = AudioRolloffMode.Logarithmic;
            shotSource.minDistance = gunshotMinDistance;
            shotSource.maxDistance = gunshotMaxDistance;
            shotSource.volume = gunshotVolume;
            shotSource.PlayOneShot(gunshotClip);
            Destroy(gunshotObj, gunshotClip.length + 1f);
        }
        else if (debugLogs) Debug.LogWarning("[PBL] Gunshot clip or origin not assigned!");

        // stop doll loop sound immediately (if any)
        if (dollAudioSource != null && dollAudioSource.loop)
        {
            dollAudioSource.Stop();
            dollAudioSource.clip = null;
            dollAudioSource.loop = false;
        }

        // after short delay, restart the scene
        StartCoroutine(RestartAfterDelay(deathRestartDelay));
    }

    IEnumerator RestartAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        if (debugLogs) Debug.Log("[PBL] Restarting scene due to death...");
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    void OnStartCrossed()
    {
        if (debugLogs) Debug.Log("[PBL] Start crossed");
        hasCrossedStart = true;
    }

    public void OnFinishCrossed()
    {
        if (debugLogs) Debug.Log("[PBL] Finish crossed");
        hasFinished = true;

        // start coroutine that safely loads the next scene
        StartCoroutine(LoadNextLevelCoroutine());
    }

    IEnumerator LoadNextLevelCoroutine()
    {
        // optional short wait (allows hit-sounds/animations to play)
        yield return new WaitForSeconds(finishLoadDelay);

        // stop doll loop if any
        if (dollAudioSource != null && dollAudioSource.loop)
        {
            dollAudioSource.Stop();
            dollAudioSource.clip = null;
            dollAudioSource.loop = false;
        }

        // try to gracefully deactivate XR manager and ray interactors to avoid NRE in package code
        var xrMgr = FindObjectOfType<XRInteractionManager>();
        if (xrMgr != null) xrMgr.gameObject.SetActive(false);

        var rayInteractors = FindObjectsOfType<XRRayInteractor>();
        foreach (var r in rayInteractors)
        {
            if (r != null && r.gameObject.activeSelf) r.gameObject.SetActive(false);
        }

        // give Unity a frame to complete deactivation
        yield return null;

        int currentIndex = SceneManager.GetActiveScene().buildIndex;
        int nextIndex = currentIndex + 1;
        if (nextIndex < SceneManager.sceneCountInBuildSettings)
        {
            if (debugLogs) Debug.Log($"[PBL] Loading next scene (index {nextIndex})");
            SceneManager.LoadScene(nextIndex);
        }
        else
        {
            Debug.Log("[PBL] Ёто был последний уровень Ч следующа€ сцена отсутствует в Build Settings.");
        }
    }

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

    // helpers
    public void ForceDeath()
    {
        PlayGunshotAndDie();
    }

    public bool IsPlayerDead() => playerDead;
}

using System.Collections.Generic;
using System.Reflection;
using UnityEditor.Timeline.Actions;
using UnityEngine;
using UnityEngine.SceneManagement;

[RequireComponent(typeof(StampContour))]
public class CutChecker : MonoBehaviour
{
    [Header("Needle / stamp")]
    public Transform needleTip;              // назначь в Inspector
    public LayerMask stampLayer;
    public float rayLength = 0.12f;

    [Header("Tolerance / timing")]
    public float tolerance = 0.02f;          // допустимая дистанция до точки
    public float maxNoContactTime = 0.6f;    // если игла не касается штампа дольше этого - consider miss
    public float minTimeBetweenSamples = 0.02f;

    [Header("Miss settings")]
    public int maxAllowedMisses = 3;         // на 3ем — исчезание и смерть
    public float missCooldown = 0.6f;        // минимальное время между засчитанными промахами
    public bool resetMissesOnSuccessfulHit = false;

    [Header("Visual feedback (darken)")]
    public Renderer candyRenderer;           // основной renderer карамели (назначить)
    [Range(0f, 1f)] public float darkOnFirstMiss = 0.25f;
    [Range(0f, 1f)] public float darkOnSecondMiss = 0.55f;
    [Range(0f, 1f)] public float darkOnThirdMiss = 0.95f;
    public float darkLerpSpeed = 6f;

    [Header("Fail / VFX")]
    public GameObject brokenCandyPrefab;
    public Transform candyRoot;
    public ParticleSystem breakParticles;
    public AudioClip breakSound;
    public AudioClip playerHitSound;
    public float playerKillDelay = 0.6f;

    [Header("Restart")]
    [Tooltip("Дополнительная задержка перед перезапуском сцены после death-handling")]
    public float restartDelay = 1.5f;

    // internal
    private StampContour contour;
    private bool[] visited;
    private float lastSampleTime = 0f;
    private bool active = false;

    private float lastContactTime = 0f;
    private bool failed = false;

    // miss tracking
    private int missCount = 0;
    private float lastMissTime = -999f;
    private float currentDarkness = 0f; // 0..1
    private Material candyMaterialInstance = null;

    void Awake()
    {
        contour = GetComponent<StampContour>();
        ResetVisited();
        PrepareCandyMaterial();
    }

    void PrepareCandyMaterial()
    {
        if (candyRenderer == null) return;
        candyMaterialInstance = new Material(candyRenderer.material);
        candyRenderer.material = candyMaterialInstance;
        // Try to initialize currentDarkness from color brightness if possible
        Color col = Color.white;
        if (candyMaterialInstance.HasProperty("_BaseColor"))
            col = candyMaterialInstance.GetColor("_BaseColor");
        else if (candyMaterialInstance.HasProperty("_Color"))
            col = candyMaterialInstance.GetColor("_Color");
        currentDarkness = 1f - col.grayscale;
    }

    public void ResetVisited()
    {
        if (contour == null) contour = GetComponent<StampContour>();
        int n = contour != null ? contour.Count : 0;
        visited = new bool[Mathf.Max(0, n)];
        lastContactTime = Time.time;
        failed = false;
        missCount = 0;
        lastMissTime = -999f;
        currentDarkness = 0f;
        ApplyDarknessImmediate(0f);
    }

    public void StartChecking()
    {
        ResetVisited();
        active = true;
        Debug.Log("CutChecker: StartChecking()");
    }

    public void StopChecking()
    {
        active = false;
        Debug.Log("CutChecker: StopChecking()");
    }

    void Update()
    {
        if (!active || failed) return;
        if (Time.time - lastSampleTime < minTimeBetweenSamples) return;
        lastSampleTime = Time.time;

        if (needleTip == null) return;

        Ray r = new Ray(needleTip.position, needleTip.forward);
        RaycastHit hit;
        if (Physics.Raycast(r, out hit, rayLength, stampLayer))
        {
            lastContactTime = Time.time;
            // IMPORTANT: use contour.transform for local conversion (contour points are in contour.local space)
            Vector3 localHit = contour != null ? contour.transform.InverseTransformPoint(hit.point) : transform.InverseTransformPoint(hit.point);
            ProcessHitLocal(localHit, hit.collider);
        }
        else
        {
            if (Time.time - lastContactTime > maxNoContactTime)
            {
                RegisterMiss("No contact timeout");
                lastContactTime = Time.time; // prevent re-register each frame
            }
        }

        UpdateDarknessLerp();
    }

    // public helper used by NeedleCollisionLogger fallback
    public void NotifyCookieContact()
    {
        RegisterMiss("Cookie sustained contact (logger)", true);
    }

    // --- PUBLIC API: ProcessWorldHit overloads ----------------
    // Called by NeedleCollisionLogger: prefer overload with collider
    public void ProcessWorldHit(Vector3 worldPoint, Collider hitCollider)
    {
        if (!active || failed) return;
        if (contour == null) contour = GetComponent<StampContour>();
        if (contour == null) { Debug.LogWarning("CutChecker.ProcessWorldHit: contour == null"); return; }

        // convert to contour-local
        Vector3 localHit = contour.transform != null ? contour.transform.InverseTransformPoint(worldPoint) : transform.InverseTransformPoint(worldPoint);

        // If we have a direct collider, try direct handle (this will check exact ContourPoint component)
        if (hitCollider != null)
        {
            bool handled = TryHandleDirectContourCollider(hitCollider);
            if (handled) return;
        }

        // also run the distance-based path check (fallback)
        ProcessHitLocal(localHit, hitCollider);
    }

    // Backward-compatible single-arg version (calls new overload)
    public void ProcessWorldHit(Vector3 worldPoint)
    {
        ProcessWorldHit(worldPoint, null);
    }
    // ------------------------------------------------------------------

    // Existing processing (distance-based)
    void ProcessHitLocal(Vector3 localHit, Collider hitCollider)
    {
        if (contour == null) { Debug.LogWarning("CutChecker: contour null"); return; }
        float dist;
        int idx = contour.GetClosestIndex(localHit, out dist);

        if (idx < 0)
        {
            TryHandleDirectContourCollider(hitCollider);
            return;
        }

        Debug.Log($"CutChecker: Closest idx {idx} dist {dist:F4} tol {tolerance:F4}");

        // check ContourPoint type if present
        Transform pointTransform = contour.contourPoints[idx];
        var cp = pointTransform.GetComponent<ContourPoint>();
        if (cp != null)
        {
            if (dist <= tolerance)
            {
                if (cp.pointType == ContourPoint.PointType.Main)
                {
                    // successful hit
                    if (!visited[idx])
                    {
                        visited[idx] = true;
                        contour.OnPointTouched(pointTransform);
                        cp.MarkTouchedAsMain();
                    }
                    if (resetMissesOnSuccessfulHit)
                    {
                        missCount = 0;
                        ApplyDarknessTarget(0f);
                    }
                    CheckCompletion();
                }
                else
                {
                    // forbidden point -> immediate miss (force)
                    Debug.LogWarning($"CutChecker: Hit forbidden point type {cp.pointType} -> MISS");
                    cp.MarkAsMissed();
                    RegisterMiss($"Hit forbidden point type {cp.pointType} idx={idx} dist={dist:F4}", true);
                }
            }
            else
            {
                // outside tolerance -> miss (normal)
                RegisterMiss($"OUT_OF_LINE dist={dist:F4}");
            }
        }
        else
        {
            // fallback: no ContourPoint component
            if (dist <= tolerance)
            {
                if (!visited[idx])
                {
                    visited[idx] = true;
                    contour.OnPointTouched(pointTransform);
                }
                if (resetMissesOnSuccessfulHit)
                {
                    missCount = 0;
                    ApplyDarknessTarget(0f);
                }
                CheckCompletion();
            }
            else
            {
                RegisterMiss($"OUT_OF_LINE dist={dist:F4}");
            }
        }
    }

    // возвращает true если обработал хит (и дальнейшая обработка не требуется)
    bool TryHandleDirectContourCollider(Collider hitCollider)
    {
        if (hitCollider == null) return false;

        // Попробуем найти ContourPoint компонент в родителях
        var contourPointComp = hitCollider.GetComponentInParent<ContourPoint>();
        if (contourPointComp != null)
        {
            Transform pt = contourPointComp.transform;
            var sc = contour;
            if (sc != null && sc.contourPoints.Contains(pt))
            {
                int idx = sc.contourPoints.IndexOf(pt);
                if (idx >= 0 && !visited[idx])
                {
                    // если это запрещённый тип — промах (force)
                    if (contourPointComp.pointType != ContourPoint.PointType.Main)
                    {
                        contourPointComp.MarkAsMissed();
                        RegisterMiss($"Direct hit forbidden pointType={contourPointComp.pointType}", true);
                        return true; // обработали — это промах
                    }

                    // основной контур — успешный хит
                    visited[idx] = true;
                    sc.OnPointTouched(pt);
                    contourPointComp.MarkTouchedAsMain();
                    if (resetMissesOnSuccessfulHit)
                    {
                        missCount = 0;
                        ApplyDarknessTarget(0f);
                    }
                    CheckCompletion();
                    return true; // обработали — успех
                }
                else if (idx >= 0 && visited[idx])
                {
                    // уже посещённая точка — считаем обработанной, не нужно дальше
                    return true;
                }
            }
        }

        // else: не принадлежит нашему списку контурных точек — не обработали
        return false;
    }


    // Register miss; if force==true ignore cooldown (used for forbidden-point direct hits)
    void RegisterMiss(string reason, bool force = false)
    {
        if (!force && Time.time - lastMissTime < missCooldown)
        {
            Debug.Log($"CutChecker: Miss ignored (cooldown) reason={reason}");
            return;
        }

        lastMissTime = Time.time;
        missCount++;
        Debug.LogWarning($"CutChecker: MISS #{missCount} reason={reason}");

        float target = 0f;
        if (missCount == 1) target = darkOnFirstMiss;
        else if (missCount == 2) target = darkOnSecondMiss;
        else target = darkOnThirdMiss;

        ApplyDarknessTarget(target);

        if (missCount >= maxAllowedMisses)
        {
            TriggerFinalFail();
        }
    }

    float targetDarkness = 0f;
    void ApplyDarknessTarget(float t)
    {
        targetDarkness = Mathf.Clamp01(t);
    }
    void ApplyDarknessImmediate(float t)
    {
        currentDarkness = Mathf.Clamp01(t);
        SetMaterialDarkness(currentDarkness);
    }
    void UpdateDarknessLerp()
    {
        if (candyMaterialInstance == null) return;
        if (Mathf.Approximately(currentDarkness, targetDarkness)) return;
        currentDarkness = Mathf.Lerp(currentDarkness, targetDarkness, 1f - Mathf.Exp(-darkLerpSpeed * Time.deltaTime));
        SetMaterialDarkness(currentDarkness);
    }

    void SetMaterialDarkness(float darkness01)
    {
        if (candyMaterialInstance == null) return;

        Color baseCol = Color.white;
        if (candyRenderer != null)
        {
            if (candyMaterialInstance.HasProperty("_BaseColor"))
                baseCol = candyMaterialInstance.GetColor("_BaseColor");
            else if (candyMaterialInstance.HasProperty("_Color"))
                baseCol = candyMaterialInstance.GetColor("_Color");
        }

        Color targetCol = Color.Lerp(baseCol, Color.black, darkness01);

        if (candyMaterialInstance.HasProperty("_BaseColor"))
            candyMaterialInstance.SetColor("_BaseColor", targetCol);
        if (candyMaterialInstance.HasProperty("_Color"))
            candyMaterialInstance.SetColor("_Color", targetCol);
        if (candyMaterialInstance.HasProperty("_EmissionColor"))
        {
            Color em = targetCol * 0.5f;
            candyMaterialInstance.SetColor("_EmissionColor", em);
            if (darkness01 > 0.01f) candyMaterialInstance.EnableKeyword("_EMISSION");
            else candyMaterialInstance.DisableKeyword("_EMISSION");
        }
    }

    void TriggerFinalFail()
    {
        Debug.LogError("CutChecker: FINAL FAIL (max misses) — breaking candy and killing player.");
        failed = true;
        active = false;

        if (breakParticles != null && candyRoot != null) Instantiate(breakParticles, candyRoot.position, Quaternion.identity);
        if (breakSound != null && candyRoot != null) AudioSource.PlayClipAtPoint(breakSound, candyRoot.position);

        if (candyRoot != null) candyRoot.gameObject.SetActive(false);

        if (brokenCandyPrefab != null && candyRoot != null)
        {
            Instantiate(brokenCandyPrefab, candyRoot.position, candyRoot.rotation);
        }

        // call HandlePlayerHit after playerKillDelay
        Invoke(nameof(HandlePlayerHit), playerKillDelay);
    }

    void HandlePlayerHit()
    {
        Debug.Log("CutChecker: Handling player hit (kill).");

        if (playerHitSound != null && Camera.main != null) AudioSource.PlayClipAtPoint(playerHitSound, Camera.main.transform.position);
        bool invoked = false;


        var monos = FindObjectsOfType<MonoBehaviour>();
        foreach (var mb in monos)
        {
            var t = mb.GetType();
            if (t.Name == "PlayerDeathHandler")
            {
                var method = t.GetMethod("OnPlayerKilled", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (method != null)
                {
                    method.Invoke(mb, null);
                    Debug.Log("CutChecker: Invoked PlayerDeathHandler.OnPlayerKilled via reflection.");
                    invoked = true;
                    break;
                }
            }
        }

        // Fallback: disable all XR interactors
        var interactors = FindObjectsOfType<UnityEngine.XR.Interaction.Toolkit.XRBaseInteractor>();
        foreach (var it in interactors) it.enabled = false;

        Debug.Log("CutChecker: PlayerDeathHandler not found - disabled interactors as fallback.");

        // Schedule scene restart after restartDelay seconds
        if (restartDelay >= 0f)
        {
            Debug.Log($"CutChecker: Restarting scene in {restartDelay} seconds...");
            // use Invoke to call RestartNow
            Invoke(nameof(RestartNow), restartDelay);
        }
    }

    void RestartNow()
    {
        // simple restart of the active scene
        var scene = SceneManager.GetActiveScene();
        SceneManager.LoadScene(scene.name);
    }

    void CheckCompletion()
    {
        if (visited == null || visited.Length == 0) return;
        int count = 0;
        for (int i = 0; i < visited.Length; i++) if (visited[i]) count++;
        float pct = (float)count / visited.Length;
        Debug.Log($"CutChecker: Progress {count}/{visited.Length} = {pct:P0}");
        if (pct >= 0.98f)
        {
            Success();
        }
    }

    void Success()
    {
        Debug.Log("CutChecker: SUCCESS! Figure cut.");
        active = false;
    }
}

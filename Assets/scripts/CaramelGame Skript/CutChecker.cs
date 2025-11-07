using UnityEngine;
using System.Collections.Generic;
using System.Reflection;

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
            Vector3 localHit = transform.InverseTransformPoint(hit.point);
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

    // --- NEW PUBLIC API used by NeedleCollisionLogger ----------------
    // NeedleCollisionLogger calls this with a world contact point (ClosestPoint)
    public void ProcessWorldHit(Vector3 worldPoint)
    {
        if (contour == null) contour = GetComponent<StampContour>();
        Vector3 localHit = transform.InverseTransformPoint(worldPoint);

        // try to find collider at that world point (small overlap)
        Collider hitCollider = null;
        Collider[] hits = Physics.OverlapSphere(worldPoint, 0.01f, stampLayer);
        if (hits != null && hits.Length > 0) hitCollider = hits[0];

        ProcessHitLocal(localHit, hitCollider);
    }
    // ------------------------------------------------------------------

    // Existing processing (unchanged behaviour)
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

        if (dist <= tolerance)
        {
            visited[idx] = true;
            contour.OnPointTouched(contour.contourPoints[idx]);
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

    void TryHandleDirectContourCollider(Collider hitCollider)
    {
        if (hitCollider == null) return;
        var pointTransform = hitCollider.transform;
        var sc = GetComponent<StampContour>();
        if (sc != null && sc.contourPoints.Contains(pointTransform))
        {
            int idx = sc.contourPoints.IndexOf(pointTransform);
            if (idx >= 0 && !visited[idx])
            {
                visited[idx] = true;
                sc.OnPointTouched(pointTransform);
                if (resetMissesOnSuccessfulHit)
                {
                    missCount = 0;
                    ApplyDarknessTarget(0f);
                }
                CheckCompletion();
                return;
            }
        }

        RegisterMiss("Hit non-contour collider");
    }

    void RegisterMiss(string reason)
    {
        if (Time.time - lastMissTime < missCooldown)
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

        if (breakParticles != null) Instantiate(breakParticles, candyRoot.position, Quaternion.identity);
        if (breakSound != null) AudioSource.PlayClipAtPoint(breakSound, candyRoot.position);

        if (candyRoot != null) candyRoot.gameObject.SetActive(false);

        if (brokenCandyPrefab != null && candyRoot != null)
        {
            Instantiate(brokenCandyPrefab, candyRoot.position, candyRoot.rotation);
        }

        Invoke(nameof(HandlePlayerHit), playerKillDelay);
    }

    void HandlePlayerHit()
    {
        Debug.Log("CutChecker: Handling player hit (kill).");

        if (playerHitSound != null) AudioSource.PlayClipAtPoint(playerHitSound, Camera.main.transform.position);

        // First try: find any MonoBehaviour named "PlayerDeathHandler" and call its OnPlayerKilled method via reflection
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

        if (invoked) return;

        // Fallback: disable all XR interactors
        var interactors = FindObjectsOfType<UnityEngine.XR.Interaction.Toolkit.XRBaseInteractor>();
        foreach (var it in interactors) it.enabled = false;

        Debug.Log("CutChecker: PlayerDeathHandler not found - disabled interactors as fallback.");
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

using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(StampContour))]
public class CutChecker : MonoBehaviour
{
    public Transform needleTip; // назначить в Inspector
    public LayerMask stampLayer;
    public float rayLength = 0.12f;

    [Header("Tolerance (локал. ед.)")]
    public float tolerance = 0.02f;

    [Header("Sampling")]
    public float minTimeBetweenSamples = 0.02f;
    private float lastSampleTime = 0f;

    private StampContour contour;
    private bool[] visited;
    private bool active = false;

    void Awake()
    {
        contour = GetComponent<StampContour>();
        ResetVisited();
    }

    public void ResetVisited()
    {
        if (contour == null) contour = GetComponent<StampContour>();
        int n = contour != null ? contour.Count : 0;
        visited = new bool[Mathf.Max(0, n)];
    }

    [ContextMenu("Start Checking (editor)")]
    public void StartChecking()
    {
        ResetVisited();
        active = true;
        Debug.Log("CutChecker: StartChecking()");
    }

    [ContextMenu("Stop Checking (editor)")]
    public void StopChecking()
    {
        active = false;
        Debug.Log("CutChecker: StopChecking()");
    }

    // ручной вызов для теста
    [ContextMenu("Force Sample Now")]
    public void ForceSample() { DoSample(); }

    void Update()
    {
        if (!active) return;
        if (Time.time - lastSampleTime < minTimeBetweenSamples) return;
        lastSampleTime = Time.time;

        DoSample();
    }

    void DoSample()
    {
        if (needleTip == null)
        {
            Debug.LogWarning("CutChecker: needleTip is null");
            return;
        }

        // рисуем отладочный луч
        Debug.DrawRay(needleTip.position, needleTip.forward * rayLength, Color.cyan, 0.5f);

        Ray r = new Ray(needleTip.position, needleTip.forward);
        RaycastHit hit;
        if (Physics.Raycast(r, out hit, rayLength, stampLayer))
        {
            Debug.Log($"CutChecker: Ray hit {hit.collider.name} at world {hit.point}");
            if (hit.collider.transform.IsChildOf(transform) || hit.collider.transform == transform)
            {
                Vector3 localHit = transform.InverseTransformPoint(hit.point);
                Debug.Log($"CutChecker: localHit {localHit}");
                ProcessHitLocal(localHit);
            }
            else
            {
                Debug.Log("CutChecker: Hit object is not this stamp (different parent).");
            }
        }
        else
        {
            Debug.Log("CutChecker: Raycast MISS");
        }
    }

    void ProcessHitLocal(Vector3 localHit)
    {
        if (contour == null) { Debug.LogWarning("CutChecker: contour null"); return; }
        float dist;
        int idx = contour.GetClosestIndex(localHit, out dist);
        Debug.Log($"CutChecker: Closest idx {idx} dist {dist:F4} tol {tolerance:F4}");
        if (idx < 0) return;

        if (dist <= tolerance)
        {
            Debug.Log($"CutChecker: HIT ON LINE index {idx}");
            // пометим как пройденную (для простоты тут)
            visited[idx] = true;
        }
        else
        {
            Debug.LogWarning($"CutChecker: OUT OF LINE (dist {dist:F4} > tol {tolerance:F4}) => FAIL");
            Fail();
        }
    }

    void Fail()
    {
        Debug.LogError("CutChecker: FAIL!");
        active = false;
    }

    void OnDrawGizmos()
    {
        if (contour == null) contour = GetComponent<StampContour>();
        if (contour == null || contour.contourPoints == null) return;
        for (int i = 0; i < contour.contourPoints.Count; i++)
        {
            Vector3 world = transform.TransformPoint(contour.contourPoints[i].localPosition);
            Gizmos.color = (visited != null && i < visited.Length && visited[i]) ? Color.green : Color.red;
            Gizmos.DrawSphere(world, 0.004f);
        }
    }
}

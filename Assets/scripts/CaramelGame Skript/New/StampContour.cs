using System.Collections.Generic;
using UnityEngine;

public class StampContour : MonoBehaviour
{
    [Header("Contour points")]
    public List<ContourPoint> allowedPoints = new List<ContourPoint>();
    public List<ContourPoint> forbiddenPoints = new List<ContourPoint>();

    [Header("Settings")]
    public int maxForbiddenTouches = 3;

    [Header("Debug")]
    public bool drawGizmos = true;
    public Color allowedColor = Color.green;
    public Color forbiddenColor = Color.red;

    // internal state
    private HashSet<ContourPoint> cutAllowed = new HashSet<ContourPoint>();
    private int forbiddenTouchCount = 0;
    private bool finished = false;

    // EVENTS (подписывается Candy / GameManager)
    public System.Action OnWin;
    public System.Action OnLose;

    public bool IsFinished
    {
        get { return finished; }
    }

    /// <summary>
    /// Вызывается, когда игла коснулась точки
    /// </summary>
    public void TouchPoint(ContourPoint point)
    {
        if (finished || point == null)
            return;

        if (point.pointType == ContourPoint.PointType.Allowed)
        {
            HandleAllowed(point);
        }
        else
        {
            HandleForbidden(point);
        }
    }

    void HandleAllowed(ContourPoint point)
    {
        if (cutAllowed.Contains(point))
            return;

        cutAllowed.Add(point);
        point.MarkAllowed();

        Debug.Log($"[StampContour] Allowed {cutAllowed.Count}/{allowedPoints.Count}");

        if (cutAllowed.Count >= allowedPoints.Count)
        {
            Win();
        }
    }

    void HandleForbidden(ContourPoint point)
    {
        forbiddenTouchCount++;
        point.MarkForbidden();

        Debug.LogWarning($"[StampContour] Forbidden touch {forbiddenTouchCount}/{maxForbiddenTouches}");

        if (forbiddenTouchCount >= maxForbiddenTouches)
        {
            Lose();
        }
    }

    void Win()
    {
        if (finished) return;
        finished = true;

        Debug.Log("[StampContour] WIN");
        if (OnWin != null)
            OnWin.Invoke();
    }

    void Lose()
    {
        if (finished) return;
        finished = true;

        Debug.Log("[StampContour] LOSE");
        if (OnLose != null)
            OnLose.Invoke();
    }

    void OnDrawGizmos()
    {
        if (!drawGizmos) return;

        Gizmos.color = Color.green;
        foreach (var p in allowedPoints)
        {
            if (p != null)
                Gizmos.DrawSphere(p.transform.position, 0.003f);
        }

        Gizmos.color = Color.red;
        foreach (var p in forbiddenPoints)
        {
            if (p != null)
                Gizmos.DrawCube(p.transform.position, Vector3.one * 0.004f);
        }
    }
}

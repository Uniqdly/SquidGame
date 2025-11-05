using UnityEngine;
using System.Collections.Generic;

[ExecuteAlways]
public class StampContour : MonoBehaviour
{
    public List<Transform> contourPoints = new List<Transform>();
    public bool drawGizmos = true;
    public bool closed = true;

    public int Count => contourPoints != null ? contourPoints.Count : 0;

    public Vector3 GetLocalPoint(int index)
    {
        if (contourPoints == null || contourPoints.Count == 0) return Vector3.zero;
        index = Mathf.Clamp(index, 0, contourPoints.Count - 1);
        return contourPoints[index].localPosition;
    }

    public int GetClosestIndex(Vector3 localPos, out float dist)
    {
        dist = float.MaxValue;
        int best = -1;
        if (contourPoints == null || contourPoints.Count == 0) return -1;
        for (int i = 0; i < contourPoints.Count; i++)
        {
            float d = Vector3.Distance(localPos, contourPoints[i].localPosition);
            if (d < dist) { dist = d; best = i; }
        }
        return best;
    }

    void OnDrawGizmos()
    {
        if (!drawGizmos || contourPoints == null || contourPoints.Count == 0) return;
        Gizmos.color = Color.yellow;
        for (int i = 0; i < contourPoints.Count; i++)
        {
            var a = transform.TransformPoint(contourPoints[i].localPosition);
            var b = transform.TransformPoint(contourPoints[(i + 1) % contourPoints.Count].localPosition);
            Gizmos.DrawSphere(a, 0.003f);
            if (i < contourPoints.Count - 1 || closed) Gizmos.DrawLine(a, b);
        }
    }
}

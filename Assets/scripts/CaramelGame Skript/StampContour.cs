using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

[ExecuteAlways]
public class StampContour : MonoBehaviour
{
    public List<Transform> contourPoints = new List<Transform>();
    public bool drawGizmos = true;
    public bool closed = true;

    // Цвета точек
    public Color defaultColor = Color.yellow;
    public Color touchedColor = Color.green;

    // Для проверки, какие точки уже окрашены
    private HashSet<Transform> touchedPoints = new HashSet<Transform>();

    public int Count => contourPoints != null ? contourPoints.Count : 0;

    private void Start()
    {
        // Убедимся, что каждая точка имеет необходимые компоненты
        foreach (var point in contourPoints)
        {
            if (point == null) continue;

            // Добавляем визуализацию (если её нет)
            var rend = point.GetComponent<Renderer>();
            if (rend == null)
            {
                var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                sphere.transform.SetParent(point);
                sphere.transform.localPosition = Vector3.zero;
                sphere.transform.localScale = Vector3.one * 0.005f;

                rend = sphere.GetComponent<Renderer>();
                rend.material = new Material(Shader.Find("Standard"));
                rend.material.color = defaultColor;

                DestroyImmediate(sphere.GetComponent<Collider>()); // убираем лишний коллайдер
            }

            // Добавляем триггер для столкновений с иглой
            var collider = point.GetComponent<SphereCollider>();
            if (collider == null)
            {
                collider = point.gameObject.AddComponent<SphereCollider>();
                collider.isTrigger = true;
                collider.radius = 0.005f;
            }

            // Добавляем обработчик коллизий
            if (point.GetComponent<ContourCollision>() == null)
            {
                var handler = point.gameObject.AddComponent<ContourCollision>();
                handler.contour = this;
            }
        }
    }

    public void OnPointTouched(Transform point)
    {
        if (point == null || touchedPoints.Contains(point)) return;

        // Попробуем найти ContourPoint на самом объекте точки (или в родителе)
        var cp = point.GetComponent<ContourPoint>() ?? point.GetComponentInChildren<ContourPoint>();
        if (cp == null)
        {
            // Если нет ContourPoint — fallback к старой логике (как раньше)
            var rendFallback = point.GetComponentInChildren<Renderer>();
            if (rendFallback != null) rendFallback.material.color = touchedColor;
            touchedPoints.Add(point);
            Debug.Log($"[StampContour] Point '{point.name}' touched (no ContourPoint component). ({touchedPoints.Count}/{Count})");
            return;
        }

        if (cp.pointType == ContourPoint.PointType.Main)
        {
            // Успешное попадание по основному контуру
            cp.MarkTouchedAsMain();

            var rend = point.GetComponentInChildren<Renderer>();
            if (rend != null) rend.material.color = touchedColor;

            touchedPoints.Add(point);
            Debug.Log($"[StampContour] Main point '{point.name}' touched. ({touchedPoints.Count}/{Count})");
        }
        else
        {
            // Это внутренняя или внешняя точка — промах
            cp.MarkAsMissed();
            Debug.LogWarning($"[StampContour] Forbidden point '{point.name}' touched (type={cp.pointType}). Registering miss.");

            // Пытаемся уведомить CutChecker на том же объекте или в родителях
            var checker = GetComponentInParent<CutChecker>();
            if (checker != null)
            {
                // используем явный метод уведомления для промаха (RegisterMiss с force = true)
                var mi = checker.GetType().GetMethod("RegisterMiss", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public);
                if (mi != null)
                {
                    // вызов RegisterMiss(reason, true) — если метод есть
                    try
                    {
                        mi.Invoke(checker, new object[] { $"Touched forbidden contour point {point.name}", true });
                    }
                    catch
                    {
                        // fallback — если не получилось с force, вызываем публичный NotifyCookieContact
                        var notify = checker.GetType().GetMethod("NotifyCookieContact", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                        if (notify != null) notify.Invoke(checker, null);
                    }
                }
                else
                {
                    // fallback: вызов публичного NotifyCookieContact
                    var notify = checker.GetType().GetMethod("NotifyCookieContact", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    if (notify != null) notify.Invoke(checker, null);
                }
            }
        }
    }


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

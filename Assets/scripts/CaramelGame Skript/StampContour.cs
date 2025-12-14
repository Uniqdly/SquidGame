using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;

[ExecuteAlways]
public class StampContour : MonoBehaviour
{
    public List<Transform> contourPoints = new List<Transform>();
    public bool drawGizmos = true;
    public bool closed = true;

    [Header("Colors")]
    public Color defaultColor = Color.yellow;
    public Color touchedColor = Color.green;


    [Header("Win settings")]
    public float winDelay = 1.2f;
    public AudioClip winSound;
    public ParticleSystem winParticles;

    // internal
    private HashSet<Transform> touchedPoints = new HashSet<Transform>();
    private bool winTriggered = false;

    public int Count => contourPoints != null ? contourPoints.Count : 0;

    private void Start()
    {
        winTriggered = false;
        touchedPoints.Clear();

        foreach (var point in contourPoints)
        {
            if (point == null) continue;

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

                DestroyImmediate(sphere.GetComponent<Collider>());
            }

            var collider = point.GetComponent<SphereCollider>();
            if (collider == null)
            {
                collider = point.gameObject.AddComponent<SphereCollider>();
                collider.isTrigger = true;
                collider.radius = 0.005f;
            }

            if (point.GetComponent<ContourCollision>() == null)
            {
                var handler = point.gameObject.AddComponent<ContourCollision>();
                handler.contour = this;
            }
        }
    }

    public void OnPointTouched(Transform point)
    {
        if (point == null || winTriggered) return;
        if (touchedPoints.Contains(point)) return;

        var cp = point.GetComponent<ContourPoint>() ?? point.GetComponentInChildren<ContourPoint>();

        if (cp != null && cp.pointType != ContourPoint.PointType.Main)
        {
            cp.MarkAsMissed();
            Debug.LogWarning($"[StampContour] Forbidden point '{point.name}' touched.");

            var checker = GetComponentInParent<CutChecker>();
            if (checker != null)
            {
                var notify = checker.GetType().GetMethod(
                    "NotifyCookieContact",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic
                );
                if (notify != null) notify.Invoke(checker, null);
            }
            return;
        }

        // SUCCESSFUL POINT
        if (cp != null)
            cp.MarkTouchedAsMain();

        var rend = point.GetComponentInChildren<Renderer>();
        if (rend != null)
            rend.material.color = touchedColor;

        touchedPoints.Add(point);

        Debug.Log($"[StampContour] Point touched {touchedPoints.Count}/{Count}");

        CheckWinCondition();
    }

    void CheckWinCondition()
    {
        if (winTriggered) return;
        if (touchedPoints.Count < Count) return;

        TriggerWin();
    }

    void TriggerWin()
    {
        winTriggered = true;
        Debug.Log("[StampContour] WIN! All contour points cut.");

        // disable CutChecker if exists
        var checker = GetComponentInParent<CutChecker>();
        if (checker != null)
            checker.enabled = false;

        // disable XR interactors
        var interactors = FindObjectsOfType<UnityEngine.XR.Interaction.Toolkit.XRBaseInteractor>();
        foreach (var it in interactors)
            it.enabled = false;

        // effects
        if (winParticles != null)
            Instantiate(winParticles, transform.position, Quaternion.identity);

        if (winSound != null && Camera.main != null)
            AudioSource.PlayClipAtPoint(winSound, Camera.main.transform.position);

        Invoke(nameof(LoadNextLevel), winDelay);
    }

    void LoadNextLevel()
    {
        int current = SceneManager.GetActiveScene().buildIndex;
        int next = current + 1;

        if (next < SceneManager.sceneCountInBuildSettings)
        {
            Debug.Log($"[StampContour] Loading next level ({next})");
            SceneManager.LoadScene(next);
        }
        else
        {
            Debug.Log("[StampContour] Last level completed!");
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

        if (contourPoints == null || contourPoints.Count == 0)
            return -1;

        for (int i = 0; i < contourPoints.Count; i++)
        {
            float d = Vector3.Distance(localPos, contourPoints[i].localPosition);
            if (d < dist)
            {
                dist = d;
                best = i;
            }
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
            if (i < contourPoints.Count - 1 || closed)
                Gizmos.DrawLine(a, b);
        }
    }
}
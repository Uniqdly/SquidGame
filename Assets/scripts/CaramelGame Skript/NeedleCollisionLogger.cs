using System.Collections.Generic;
using UnityEngine;
using System.Reflection;

[DisallowMultipleComponent]
public class NeedleCollisionLogger : MonoBehaviour
{
    [Header("Layers / identification")]
    public LayerMask contourLayer;           // слой контурных точек
    public LayerMask cookieLayer;            // слой тела печенья
    public string contourTag = "";           // опционально: тег контурных точек (если используешь)
    public string needleTag = "Needle";      // тег иглы, если нужно в других скриптах

    [Header("Distance & margin")]
    public float sampleRadius = 0.012f;      // радиус выборки colliders вокруг кончика
    public float contourPreferMargin = 0.004f;// если контур ближе на эту величину -> считаем контурным попаданием
    public float ambiguousMargin = 0.001f;   // если разница расстояний < this -> считаем спорной и игнорируем

    [Header("Debounce (frames)")]
    [Tooltip("Сколько последовательных кадров нужно, чтобы подтвердить 'контурное' касание")]
    public int contourConfirmFrames = 3;
    [Tooltip("Сколько последовательных кадров нужно, чтобы подтвердить 'cookie' (промах)")]
    public int cookieConfirmFrames = 4;
    [Tooltip("Макс скорость иглы в м/с при которой дебаунс действует. При очень большой скорости можно ослабить требования.")]
    public float maxNeedleSpeedForDebounce = 2.0f;

    [Header("Optional direct link")]
    public CutChecker debugCutChecker;       // если хочешь жестко привязать

    // internal counters
    int contourFrameCount = 0;
    int cookieFrameCount = 0;

    // last frame needle tip position for speed estimation
    Vector3 prevPos;
    float needleSpeed = 0f;

    void Start()
    {
        prevPos = transform.position;
    }

    void Update()
    {
        // speed
        needleSpeed = (transform.position - prevPos).magnitude / Mathf.Max(Time.deltaTime, 1e-6f);
        prevPos = transform.position;

        // gather colliders in radius
        Collider[] hits = Physics.OverlapSphere(transform.position, sampleRadius);
        // find nearest contour collider and nearest cookie collider (and distances)
        Collider bestContour = null;
        Collider bestCookie = null;
        float bestContourDist = float.MaxValue;
        float bestCookieDist = float.MaxValue;

        foreach (var c in hits)
        {
            if (c == null) continue;
            // ignore self-colliders
            if (c.transform.IsChildOf(transform)) continue;

            float d = Vector3.Distance(transform.position, c.ClosestPoint(transform.position));

            int bit = 1 << c.gameObject.layer;
            bool isContourByLayer = (bit & contourLayer.value) != 0;
            bool isCookieByLayer = (bit & cookieLayer.value) != 0;

            bool tagContour = false;
            if (!string.IsNullOrEmpty(contourTag))
            {
                // safe tag check (Unity returns "Untagged" if tag doesn't exist)
                try { tagContour = c.gameObject.tag == contourTag; } catch { tagContour = false; }
            }

            // also consider presence of ContourCollision component as strong signal
            bool hasContourComponent = c.GetComponentInParent<ContourCollision>() != null;

            bool isContour = isContourByLayer || tagContour || hasContourComponent;
            bool isCookie = isCookieByLayer;

            if (isContour)
            {
                if (d < bestContourDist)
                {
                    bestContourDist = d;
                    bestContour = c;
                }
            }
            else if (isCookie)
            {
                if (d < bestCookieDist)
                {
                    bestCookieDist = d;
                    bestCookie = c;
                }
            }
            else
            {
                // ignore other colliders
            }
        }

        // Decision logic:
        bool consideredContour = false;
        bool consideredCookie = false;

        if (bestContour != null && bestCookie == null)
        {
            consideredContour = true;
        }
        else if (bestCookie != null && bestContour == null)
        {
            consideredCookie = true;
        }
        else if (bestContour != null && bestCookie != null)
        {
            float diff = bestCookieDist - bestContourDist; // positive -> contour closer
            if (diff > contourPreferMargin)
            {
                consideredContour = true;
            }
            else if (diff < -contourPreferMargin)
            {
                consideredCookie = true;
            }
            else
            {
                // ambiguous - both at similar distances
                consideredContour = false;
                consideredCookie = false;
            }
        }
        else
        {
            // none found
            consideredContour = false;
            consideredCookie = false;
        }

        // If needle is moving very fast, we may want to reduce required confirmation (or skip)
        bool useDebounce = needleSpeed <= maxNeedleSpeedForDebounce;

        // update counters
        if (consideredContour)
        {
            contourFrameCount++;
            cookieFrameCount = 0;
        }
        else if (consideredCookie)
        {
            cookieFrameCount++;
            contourFrameCount = 0;
        }
        else
        {
            // ambiguous / none: slowly decay counts (avoid immediate transitions)
            contourFrameCount = Mathf.Max(0, contourFrameCount - 1);
            cookieFrameCount = Mathf.Max(0, cookieFrameCount - 1);
        }

        // decide to fire event
        if ((!useDebounce && consideredContour) || (useDebounce && contourFrameCount >= contourConfirmFrames))
        {
            // confirmed contour hit - call CutChecker.ProcessWorldHit once and reset
            if (bestContour != null)
            {
                Vector3 contactPoint = bestContour.ClosestPoint(transform.position);
                CallProcessWorldHit(contactPoint, bestContour);
                // reset counters to avoid multiple repeats
                contourFrameCount = 0;
                cookieFrameCount = 0;
            }
        }
        else if ((!useDebounce && consideredCookie) || (useDebounce && cookieFrameCount >= cookieConfirmFrames))
        {
            // confirmed cookie miss - notify CutChecker about sustained cookie contact
            CallNotifyCookieContact(bestCookie);
            contourFrameCount = 0;
            cookieFrameCount = 0;
        }

#if UNITY_EDITOR
        DebugDrawDebugMarkers(bestContour, bestCookie, bestContourDist, bestCookieDist);
#endif
    }

    void CallProcessWorldHit(Vector3 worldPoint, Collider contourCollider)
    {
        // try direct CutChecker from debug field, then parent of collider
        CutChecker cc = debugCutChecker;
        if (cc == null && contourCollider != null) cc = contourCollider.GetComponentInParent<CutChecker>();
        if (cc == null)
        {
            // try to find any CutChecker on parents of this NeedleTip (maybe single active)
            cc = GetComponentInParent<CutChecker>();
        }

        if (cc != null)
        {
            // use the overload that includes the collider
            cc.ProcessWorldHit(worldPoint, contourCollider);
            Debug.Log($"NeedleCollisionLogger: ProcessWorldHit @ {worldPoint} (collider={contourCollider?.name})");
            return;
        }

        Debug.LogWarning("NeedleCollisionLogger: No CutChecker found to process contour hit.");
    }

    void CallNotifyCookieContact(Collider cookieCollider)
    {
        CutChecker cc = debugCutChecker;
        if (cc == null && cookieCollider != null) cc = cookieCollider.GetComponentInParent<CutChecker>();
        if (cc == null) cc = GetComponentInParent<CutChecker>();

        if (cc != null)
        {
            // try call public NotifyCookieContact if exists, else try Invoke 'RegisterMiss' or 'ProcessWorldHit' fallback
            MethodInfo mi = cc.GetType().GetMethod("NotifyCookieContact", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (mi != null)
            {
                mi.Invoke(cc, null);
                Debug.Log("NeedleCollisionLogger: NotifyCookieContact invoked on CutChecker.");
                return;
            }

            // fallback: try ProcessWorldHit with a point on cookie collider (this will probably count as miss in CutChecker)
            if (cookieCollider != null)
            {
                Vector3 pt = cookieCollider.ClosestPoint(transform.position);
                MethodInfo pm = cc.GetType().GetMethod("ProcessWorldHit", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, new System.Type[] { typeof(Vector3) }, null);
                if (pm != null)
                {
                    pm.Invoke(cc, new object[] { pt });
                    Debug.Log("NeedleCollisionLogger: Fallback ProcessWorldHit invoked on CutChecker for cookie contact.");
                    return;
                }

                // try overload ProcessWorldHit(Vector3, Collider)
                MethodInfo pm2 = cc.GetType().GetMethod("ProcessWorldHit", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, new System.Type[] { typeof(Vector3), typeof(Collider) }, null);
                if (pm2 != null)
                {
                    pm2.Invoke(cc, new object[] { pt, cookieCollider });
                    Debug.Log("NeedleCollisionLogger: Fallback ProcessWorldHit(world, collider) invoked on CutChecker for cookie contact.");
                    return;
                }
            }

            Debug.LogWarning("NeedleCollisionLogger: No suitable method on CutChecker for cookie contact.");
            return;
        }

        Debug.LogWarning("NeedleCollisionLogger: No CutChecker found to notify cookie contact.");
    }

    // debug drawing helper
    void DebugDrawDebugMarkers(Collider contourC, Collider cookieC, float contourD, float cookieD)
    {
        if (contourC != null)
        {
            Debug.DrawLine(transform.position, contourC.ClosestPoint(transform.position), Color.green);
        }
        if (cookieC != null)
        {
            Debug.DrawLine(transform.position, cookieC.ClosestPoint(transform.position), Color.red);
        }
        // draw sample sphere
        Debug.DrawRay(transform.position, transform.up * 0.005f, Color.cyan);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, sampleRadius);
    }
}

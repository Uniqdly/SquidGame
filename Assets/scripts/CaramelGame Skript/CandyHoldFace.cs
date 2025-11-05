using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

[RequireComponent(typeof(XRGrabInteractable))]
public class CandyHoldFace : MonoBehaviour
{
    XRGrabInteractable grab;
    Transform cam;
    bool isHeld = false;

    [Header("Позиционирование перед лицом")]
    public float distance = 0.38f;
    public float verticalOffset = -0.12f;
    public float smoothSpeed = 12f;

    [Header("Ориентация штампа")]
    [Tooltip("Если штамп у модели находится сверху (локальная +Y) — включи.")]
    public bool stampIsOnTop = true;
    [Tooltip("Если штамп смотрит в противоположную сторону, включи (переворачивает по правой оси).")]
    public bool invertStampRight = false;

    [Header("Поведение")]
    public bool makeKinematicWhileHeld = true;
    public bool forceVerticalUp = true;

    // internal
    bool prevTrackPosition = true;
    bool prevTrackRotation = true;
    bool trackPropsAvailable = false;

    void Awake()
    {
        grab = GetComponent<XRGrabInteractable>();
        cam = Camera.main != null ? Camera.main.transform : null;

        grab.selectEntered.AddListener(OnGrab);
        grab.selectExited.AddListener(OnRelease);

        var tPos = grab.GetType().GetProperty("trackPosition");
        var tRot = grab.GetType().GetProperty("trackRotation");
        if (tPos != null && tRot != null) trackPropsAvailable = true;
    }

    void OnDestroy()
    {
        if (grab != null)
        {
            grab.selectEntered.RemoveListener(OnGrab);
            grab.selectExited.RemoveListener(OnRelease);
        }
    }

    private void OnGrab(SelectEnterEventArgs args)
    {
        // disable internal tracking if possible
        if (trackPropsAvailable)
        {
            var trackPosProp = grab.GetType().GetProperty("trackPosition");
            var trackRotProp = grab.GetType().GetProperty("trackRotation");
            if (trackPosProp != null)
            {
                prevTrackPosition = (bool)trackPosProp.GetValue(grab, null);
                trackPosProp.SetValue(grab, false, null);
            }
            if (trackRotProp != null)
            {
                prevTrackRotation = (bool)trackRotProp.GetValue(grab, null);
                trackRotProp.SetValue(grab, false, null);
            }
        }

        isHeld = true;
        if (makeKinematicWhileHeld)
        {
            var rb = GetComponent<Rigidbody>();
            if (rb != null) rb.isKinematic = true;
        }
    }

    private void OnRelease(SelectExitEventArgs args)
    {
        if (trackPropsAvailable)
        {
            var trackPosProp = grab.GetType().GetProperty("trackPosition");
            var trackRotProp = grab.GetType().GetProperty("trackRotation");
            if (trackPosProp != null) trackPosProp.SetValue(grab, prevTrackPosition, null);
            if (trackRotProp != null) trackRotProp.SetValue(grab, prevTrackRotation, null);
        }

        isHeld = false;
        if (makeKinematicWhileHeld)
        {
            var rb = GetComponent<Rigidbody>();
            if (rb != null) rb.isKinematic = false;
        }
    }

    void Update()
    {
        if (!isHeld || cam == null) return;

        Vector3 targetPos = cam.position + cam.forward * distance + cam.up * verticalOffset;
        Vector3 forwardToCamera = (cam.position - targetPos).normalized;
        Vector3 upVector = forceVerticalUp ? Vector3.up : cam.up;

        Quaternion targetRot;

        if (stampIsOnTop)
        {
            // Мы хотим, чтобы локальная +Y смотрела в сторону игрока (forwardToCamera).
            // Для этого используем Quaternion.LookRotation(forward, up) таким образом:
            // задаём forward как правая вектор камеры (или -правая, если invert).
            Vector3 stampForward = invertStampRight ? -cam.right : cam.right;

            // LookRotation: первый аргумент -> локальная +Z, второй -> локальная +Y.
            // Мы ставим локальную +Y = forwardToCamera, локальную +Z = stampForward.
            targetRot = Quaternion.LookRotation(stampForward, forwardToCamera);
        }
        else
        {
            // Обычное поведение: лицевая сторона (локальная +Z) смотрит на камеру
            targetRot = Quaternion.LookRotation(forwardToCamera, upVector);
        }

        transform.position = Vector3.Lerp(transform.position, targetPos, 1f - Mathf.Exp(-smoothSpeed * Time.deltaTime));
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, 1f - Mathf.Exp(-smoothSpeed * Time.deltaTime));
    }
}

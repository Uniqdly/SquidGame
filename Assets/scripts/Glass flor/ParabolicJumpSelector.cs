using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;

[DisallowMultipleComponent]
public class ParabolicJumpSelector : MonoBehaviour
{
    [Header("Targets selection")]
    public Transform leftController;
    public Transform rightController;

    [Header("Jump parameters")]
    public float jumpDuration = 0.6f;
    public float arcHeight = 1.0f;
    public float maxSelectDistance = 6f;
    public LayerMask glassLayer = ~0;

    [Header("Input")]
    public bool useGripButton = true;

    [Header("Player reference")]
    public Transform playerRoot;
    public Rigidbody playerRigidbody;

    [Header("Landing")]
    public float landingYOffset = 0.05f;

    // internals
    GlassPiece highlighted = null;
    RaycastHit? selectedHit = null; // ← НОВОЕ
    bool busy = false;

    // state для edge detection (чтобы не дёргало при удержании)
    bool prevGripLeft = false;
    bool prevGripRight = false;

    void Awake()
    {
        if (playerRoot == null) playerRoot = transform;
        if (playerRigidbody == null && playerRoot != null) playerRigidbody = playerRoot.GetComponent<Rigidbody>();
    }

    void Update()
    {
        if (busy) return;

        bool found = TryUpdateHighlightFromController(rightController) || TryUpdateHighlightFromController(leftController);

        if (useGripButton && highlighted != null)
        {
            bool gripPressed = GripPressedThisFrame();
            if (gripPressed)
            {
                StartCoroutine(DoParabolicJumpTo(highlighted.transform));
            }
        }
    }

    bool TryUpdateHighlightFromController(Transform ctrl)
    {
        if (ctrl == null) return false;

        Ray r = new Ray(ctrl.position, ctrl.forward);
        if (Physics.Raycast(r, out RaycastHit hit, maxSelectDistance, glassLayer, QueryTriggerInteraction.Ignore))
        {
            var gp = hit.collider.GetComponentInParent<GlassPiece>();
            if (gp != null && !gp.IsBroken())
            {
                SetHighlight(gp, hit);
                return true;
            }
        }
        ClearHighlight();
        return false;
    }

    void SetHighlight(GlassPiece gp, RaycastHit hit)
    {
        if (highlighted == gp) return;
        if (highlighted != null) highlighted.Highlight(false);
        highlighted = gp;
        selectedHit = hit;
        if (highlighted != null) highlighted.Highlight(true);
    }

    void ClearHighlight()
    {
        if (highlighted != null)
        {
            highlighted.Highlight(false);
            highlighted = null;
        }
        selectedHit = null;
    }

    bool GripPressedThisFrame()
    {
        var devices = new List<InputDevice>();
        InputDevices.GetDevicesWithCharacteristics(InputDeviceCharacteristics.Controller, devices);

        bool anyPressed = false;
        bool leftPressed = false, rightPressed = false;

        foreach (var d in devices)
        {
            bool isLeft = d.characteristics.HasFlag(InputDeviceCharacteristics.Left);
            bool isRight = d.characteristics.HasFlag(InputDeviceCharacteristics.Right);

            if (d.TryGetFeatureValue(CommonUsages.gripButton, out bool val) && val)
            {
                if (isLeft) leftPressed = true;
                else if (isRight) rightPressed = true;
                else anyPressed = true; // unknown hand → считаем как общий
            }
        }

        bool result = false;
        if (leftPressed && !prevGripLeft) result = true;
        if (rightPressed && !prevGripRight) result = true;
        if (anyPressed && !prevGripLeft && !prevGripRight) result = true;

        prevGripLeft = leftPressed;
        prevGripRight = rightPressed;

        return result;
    }

    IEnumerator DoParabolicJumpTo(Transform target)
    {
        if (busy) yield break;
        if (target == null) yield break;
        if (highlighted == null) { busy = false; yield break; }

        busy = true;
        ClearHighlight();

        Vector3 startPos = playerRoot.position;
        Vector3 endPos = startPos;

        if (selectedHit.HasValue)
        {
            RaycastHit hit = selectedHit.Value;

            Renderer[] rends = highlighted.GetComponentsInChildren<Renderer>(true);
            if (rends.Length > 0)
            {
                Bounds b = rends[0].bounds;
                for (int i = 1; i < rends.Length; i++)
                    b.Encapsulate(rends[i].bounds);

                Vector3 projectedCenter = b.center - Vector3.Dot(b.center - hit.point, hit.normal) * hit.normal;
                endPos = projectedCenter + hit.normal * landingYOffset;
            }
            else
            {
                endPos = hit.point + hit.normal * landingYOffset;
            }
        }
        else
        {
            Renderer rend = target.GetComponent<Renderer>();
            if (rend != null)
                endPos = new Vector3(rend.bounds.center.x, rend.bounds.min.y + landingYOffset, rend.bounds.center.z);
            else
                endPos = new Vector3(target.position.x, target.position.y + landingYOffset, target.position.z);
        }

        bool hadRb = playerRigidbody != null;
        if (hadRb)
        {
            playerRigidbody.velocity = Vector3.zero;
            playerRigidbody.isKinematic = true;
        }

        float t = 0f;
        while (t < jumpDuration)
        {
            t += Time.deltaTime;
            float f = Mathf.Clamp01(t / jumpDuration);

            Vector3 horiz = Vector3.Lerp(startPos, endPos, f);
            float arc = 4f * arcHeight * f * (1f - f);
            float y = Mathf.Lerp(startPos.y, endPos.y, f) + arc;
            playerRoot.position = new Vector3(horiz.x, y, horiz.z);

            yield return null;
        }

        playerRoot.position = endPos;

        if (hadRb)
        {
            playerRigidbody.isKinematic = false;
            playerRigidbody.velocity = Vector3.zero;
        }

        // Обработка плитки — используем highlighted, т.к. он актуален
        if (highlighted != null)
        {
            if (highlighted.isBreakable)
                highlighted.Break();
        }

        busy = false;
    }
}
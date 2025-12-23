using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR;
using XR = UnityEngine.XR;

[DisallowMultipleComponent]
public class ParabolicJumpOnGrab : MonoBehaviour
{
    [Header("Controllers (ray origin)")]
    public Transform rightController;
    public Transform leftController;

    [Header("Selection / raycast")]
    public LayerMask glassLayer = ~0;
    public float maxSelectDistance = 6f;

    [Header("Jump parameters")]
    public float jumpDuration = 0.6f;
    public float arcHeight = 1.0f;
    public bool requireGrounded = false;
    public float groundCheckDistance = 0.25f;
    public LayerMask groundMask = ~0;

    [Header("Jump Cooldown")]
    public float jumpCooldown = 1.0f;
    float lastJumpTime = -999f;

    [Header("Player (to move)")]
    public Transform playerRoot;
    public Rigidbody playerRigidbody;

    [Header("Landing / pause")]
    public float landingYOffset = 0.05f;
    public float landingPause = 0.18f;

    [Header("Highlighting")]
    public bool enableHighlight = true;

    [Header("Input")]
    public InputActionProperty jumpAction;

    GlassPiece highlighted = null;
    RaycastHit? lastHit = null;
    bool busy = false;
    bool prevJump = false;

    void Awake()
    {
        if (playerRoot == null) playerRoot = transform;
        if (playerRigidbody == null && playerRoot != null) playerRigidbody = playerRoot.GetComponent<Rigidbody>();
        if (jumpAction.action != null) jumpAction.action.Enable();
    }

    void Update()
    {
        if (busy) return;

        bool found = false;
        if (rightController != null) found = TrySelectFromController(rightController);
        if (!found && leftController != null) found = TrySelectFromController(leftController);
        if (!found) ClearHighlight();

        bool jumpPressed = IsJumpTriggered();
        if (jumpPressed && !prevJump)
        {
            if (highlighted != null)
            {
                if (Time.time - lastJumpTime < jumpCooldown)
                {
                    float wait = jumpCooldown - (Time.time - lastJumpTime);
                    Debug.Log($"[PJ] Jump on cooldown: wait {wait:F2}s");
                }
                else if (requireGrounded && !IsGrounded())
                {
                    Debug.Log("[PJ] Grounded check failed, not jumping");
                }
                else
                {
                    lastJumpTime = Time.time;
                    StartCoroutine(ParabolicJumpTo(highlighted));
                }
            }
            else
            {
                Debug.Log("[PJ] Jump pressed but nothing highlighted");
            }
        }

        prevJump = jumpPressed;
    }

    bool TrySelectFromController(Transform ctrl)
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
        return false;
    }

    void SetHighlight(GlassPiece gp, RaycastHit hit)
    {
        if (!enableHighlight) return;
        if (highlighted == gp) return;
        if (highlighted != null) highlighted.Highlight(false);
        highlighted = gp;
        lastHit = hit;
        if (highlighted != null) highlighted.Highlight(true);
    }

    void ClearHighlight()
    {
        if (!enableHighlight) return;
        if (highlighted != null)
        {
            highlighted.Highlight(false);
            highlighted = null;
        }
        lastHit = null;
    }

    bool IsJumpTriggered()
    {
        if (jumpAction.action != null)
        {
            var act = jumpAction.action;
            try { if (act.WasPressedThisFrame()) return true; } catch { }
            if (act.triggered) return true;
        }

        if (Keyboard.current?.spaceKey.wasPressedThisFrame == true) return true;
        if (Input.GetKeyDown(KeyCode.Space)) return true;
        if (Gamepad.current?.buttonSouth.wasPressedThisFrame == true) return true;

        var devices = new List<XR.InputDevice>();
        XR.InputDevices.GetDevicesWithCharacteristics(XR.InputDeviceCharacteristics.Controller, devices);
        if (devices.Count == 0)
        {
            var left = XR.InputDevices.GetDeviceAtXRNode(XR.XRNode.LeftHand);
            var right = XR.InputDevices.GetDeviceAtXRNode(XR.XRNode.RightHand);
            if (left.isValid) devices.Add(left);
            if (right.isValid) devices.Add(right);
        }

        foreach (var d in devices)
        {
            if (d.TryGetFeatureValue(XR.CommonUsages.primaryButton, out bool p) && p) return true;
            if (d.TryGetFeatureValue(XR.CommonUsages.secondaryButton, out bool s) && s) return true;
            if (d.TryGetFeatureValue(XR.CommonUsages.gripButton, out bool gbtn) && gbtn) return true;
            if (d.TryGetFeatureValue(XR.CommonUsages.triggerButton, out bool tbtn) && tbtn) return true;
            if (d.TryGetFeatureValue(XR.CommonUsages.grip, out float g) && g > 0.55f) return true;
            if (d.TryGetFeatureValue(XR.CommonUsages.trigger, out float tr) && tr > 0.55f) return true;
        }

        return false;
    }

    bool IsGrounded()
    {
        int mask = groundMask.value == 0 ? ~0 : groundMask.value;
        return Physics.Raycast(playerRoot.position, Vector3.down, groundCheckDistance, mask);
    }

    Transform GetNearestController()
    {
        Transform best = null;
        float bestDist = float.MaxValue;
        if (rightController != null)
        {
            float d = Vector3.Distance(rightController.position, playerRoot.position);
            if (d < bestDist) { bestDist = d; best = rightController; }
        }
        if (leftController != null)
        {
            float d = Vector3.Distance(leftController.position, playerRoot.position);
            if (d < bestDist) { best = leftController; }
        }
        return best;
    }

    IEnumerator ParabolicJumpTo(GlassPiece targetPiece)
    {
        if (busy || targetPiece == null) { busy = false; yield break; }
        busy = true;
        ClearHighlight();

        Vector3 startPos = playerRoot.position;
        Vector3 endPos = startPos;

        Transform nearestCtrl = GetNearestController();
        RaycastHit hit = lastHit ?? new RaycastHit();
        bool hasValidHit = lastHit.HasValue;

        if (!hasValidHit && nearestCtrl != null)
        {
            Ray ray = new Ray(nearestCtrl.position, nearestCtrl.forward);
            if (Physics.Raycast(ray, out RaycastHit newHit, maxSelectDistance, glassLayer, QueryTriggerInteraction.Ignore))
            {
                var gp = newHit.collider.GetComponentInParent<GlassPiece>();
                if (gp == targetPiece)
                {
                    hit = newHit;
                    hasValidHit = true;
                }
            }
        }

        if (hasValidHit)
        {
            Vector3 surfacePoint = hit.point;
            Vector3 normal = hit.normal;

            if (targetPiece.landingPoint != null)
            {
                Vector3 projected = targetPiece.landingPoint.position - Vector3.Dot(targetPiece.landingPoint.position - surfacePoint, normal) * normal;
                endPos = projected + normal * landingYOffset;
            }
            else
            {
                endPos = surfacePoint + normal * landingYOffset;
            }
        }
        else
        {
            Renderer rend = targetPiece.GetComponent<Renderer>();
            if (rend != null)
            {
                Bounds b = rend.bounds;
                endPos = new Vector3(b.center.x, b.min.y + landingYOffset, b.center.z);
            }
            else
            {
                endPos = targetPiece.transform.position;
                endPos.y += landingYOffset;
            }
        }

        bool hadRb = playerRigidbody != null;
        CollisionDetectionMode prevMode = hadRb ? playerRigidbody.collisionDetectionMode : CollisionDetectionMode.Discrete;
        if (hadRb)
        {
            playerRigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
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

        if (landingPause > 0f) yield return new WaitForSeconds(landingPause);

        if (hadRb)
        {
            yield return new WaitForFixedUpdate();
            if (playerRigidbody != null)
            {
                playerRigidbody.velocity = Vector3.zero;
                playerRigidbody.angularVelocity = Vector3.zero;
                playerRigidbody.Sleep();
                playerRigidbody.collisionDetectionMode = prevMode;
                playerRigidbody.isKinematic = false;
                playerRigidbody.velocity = Vector3.zero;
                playerRigidbody.angularVelocity = Vector3.zero;
            }
        }

        var finish = targetPiece.GetComponent<FinishPlatform>();
        if (finish == null) finish = targetPiece.GetComponentInParent<FinishPlatform>();
        if (finish != null)
        {
            finish.OnPlayerArrive();
        }
        else if (targetPiece.isBreakable)
        {
            targetPiece.Break();
        }

        busy = false;
    }
}
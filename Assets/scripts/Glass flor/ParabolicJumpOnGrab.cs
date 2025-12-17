using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;    // New Input System
using UnityEngine.XR;            // XR fallback

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
    public float jumpCooldown = 1.0f;   // задержка между прыжками (сек)
    float lastJumpTime = -999f;

    [Header("Player (to move)")]
    public Transform playerRoot;
    public Rigidbody playerRigidbody;

    [Header("Landing / pause")]
    [Tooltip("Вертикальный оффсет от поверхности при посадке (м)")]
    public float landingYOffset = 0.05f;
    [Tooltip("Время в секундах, которое игрок будет замирать на центре платформы после приземления")]
    public float landingPause = 0.18f;

    [Header("Highlighting")]
    public bool enableHighlight = true;

    [Header("Input (assign Input Action here)")]
    [Tooltip("Input Action that should trigger jump. Bind it to keyboard Space and controller button(s).")]
    public InputActionProperty jumpAction;

    // internals
    GlassPiece highlighted = null;
    bool busy = false;
    bool prevJump = false;

    void Awake()
    {
        if (playerRoot == null) playerRoot = transform;
        if (playerRigidbody == null && playerRoot != null) playerRigidbody = playerRoot.GetComponent<Rigidbody>();

        // enable action if provided
        if (jumpAction != null && jumpAction.action != null)
        {
            try { jumpAction.action.Enable(); } catch { }
        }
    }

    void Update()
    {
        if (busy) return;

        // selection from controllers
        bool found = false;
        if (rightController != null) found = TrySelectFromController(rightController);
        if (!found && leftController != null) found = TrySelectFromController(leftController);
        if (!found) ClearHighlight();

        // input: priority to InputAction (action-based), then keyboard/gamepad, then XR legacy
        bool jumpPressed = IsJumpTriggered();

        // rising edge: pressed now, wasn't pressed prev frame
        if (jumpPressed && !prevJump)
        {
            if (highlighted != null)
            {
                // проверяем cooldown между прыжками
                if (Time.time - lastJumpTime < jumpCooldown)
                {
                    float wait = jumpCooldown - (Time.time - lastJumpTime);
                    Debug.Log($"[PJ] Jump on cooldown: wait {wait:F2}s");
                }
                else if (requireGrounded && !IsGrounded())
                {
                    prevJump = jumpPressed; // сохраняем текущее состояние
                    Debug.Log("[PJ] Grounded check failed, not jumping");
                }
                else
                {
                    lastJumpTime = Time.time;
                    StartCoroutine(ParabolicJumpTo(highlighted.transform));
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
                SetHighlight(gp);
                return true;
            }
        }
        return false;
    }

    void SetHighlight(GlassPiece gp)
    {
        if (!enableHighlight) return;
        if (highlighted == gp) return;
        if (highlighted != null) highlighted.Highlight(false);
        highlighted = gp;
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
    }

    bool IsJumpTriggered()
    {
        // 1) Action-based (preferred) — supports multiple bindings (keyboard + controller)
        if (jumpAction != null && jumpAction.action != null)
        {
            var act = jumpAction.action;
            // Use .WasPerformedThisFrame or .triggered depending on action type
            // WasPressedThisFrame exists for Button interactions; Use triggered as generic
            try
            {
                if (act.WasPressedThisFrame()) return true; // good for button actions
            }
            catch { }
            if (act.triggered) return true;
        }

        // 2) Keyboard fallback (Editor test)
        if (Keyboard.current != null)
        {
            if (Keyboard.current.spaceKey.wasPressedThisFrame) return true;
        }
        else
        {
            if (Input.GetKeyDown(KeyCode.Space)) return true;
        }

        // 3) Gamepad fallback (optional)
        if (Gamepad.current != null)
        {
            if (Gamepad.current.buttonSouth.wasPressedThisFrame) return true;
        }

        // 4) Legacy XR InputDevices fallback (if controllers present)
        var devices = new List<UnityEngine.XR.InputDevice>();
        UnityEngine.XR.InputDevices.GetDevicesWithCharacteristics(UnityEngine.XR.InputDeviceCharacteristics.Controller, devices);
        if (devices.Count == 0)
        {
            // also try XRNode query
            var left = UnityEngine.XR.InputDevices.GetDeviceAtXRNode(UnityEngine.XR.XRNode.LeftHand);
            var right = UnityEngine.XR.InputDevices.GetDeviceAtXRNode(UnityEngine.XR.XRNode.RightHand);
            if (left.isValid) devices.Add(left);
            if (right.isValid) devices.Add(right);
        }

        foreach (var d in devices)
        {
            // check boolean usages first
            if (d.TryGetFeatureValue(UnityEngine.XR.CommonUsages.primaryButton, out bool p) && p) return true;
            if (d.TryGetFeatureValue(UnityEngine.XR.CommonUsages.secondaryButton, out bool s) && s) return true;
            if (d.TryGetFeatureValue(UnityEngine.XR.CommonUsages.gripButton, out bool gbtn) && gbtn) return true;
            if (d.TryGetFeatureValue(UnityEngine.XR.CommonUsages.triggerButton, out bool tbtn) && tbtn) return true;

            // analog
            if (d.TryGetFeatureValue(UnityEngine.XR.CommonUsages.grip, out float g) && g > 0.55f) return true;
            if (d.TryGetFeatureValue(UnityEngine.XR.CommonUsages.trigger, out float tr) && tr > 0.55f) return true;
        }

        return false;
    }

    bool IsGrounded()
    {
        int mask = groundMask.value == 0 ? ~0 : groundMask.value;
        return Physics.Raycast(playerRoot.position, Vector3.down, groundCheckDistance, mask);
    }

    IEnumerator ParabolicJumpTo(Transform target)
    {
        if (busy) yield break;
        if (target == null) yield break;

        busy = true;
        ClearHighlight();

        Vector3 startPos = playerRoot.position;

        // --- вычисляем корректную целевую позицию (центр поверхности target) ---
        Vector3 center = target.position;
        bool foundBounds = false;

        // 1) попробуем Renderer.bounds (обычно лучше для визуального центра)
        var rend = target.GetComponentInParent<Renderer>();
        if (rend != null)
        {
            center = rend.bounds.center;
            foundBounds = true;
        }
        else
        {
            // 2) если Renderer нет — попробуем Collider.bounds (например у плитки есть коллайдер)
            var colComp = target.GetComponentInParent<Collider>();
            if (colComp != null)
            {
                center = colComp.bounds.center;
                foundBounds = true;
            }
            else
            {
                // 3) если и этого нет — попытаемся собрать все renderers в родителях/детях и усреднить центры
                var rends = target.GetComponentsInParent<Renderer>();
                if (rends != null && rends.Length > 0)
                {
                    Bounds b = rends[0].bounds;
                    for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);
                    center = b.center;
                    foundBounds = true;
                }
            }
        }

        // endPos: по горизонтали — центр поверхности, по вертикали — оставляем высоту игрока (не занижаем голову)
        Vector3 endPos = new Vector3(center.x, startPos.y, center.z);

        // --- сохранение и переключение Rigidbody (как раньше) ---
        bool hadRb = playerRigidbody != null;
        CollisionDetectionMode prevMode = CollisionDetectionMode.Discrete;
        if (hadRb)
        {
            prevMode = playerRigidbody.collisionDetectionMode;
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

        // финальная позиция — немного выше поверхности, чтобы избежать пересечения
        float surfaceY = float.MinValue;
        if (foundBounds)
        {
            var finalRend = target.GetComponentInParent<Renderer>();
            if (finalRend != null)
                surfaceY = finalRend.bounds.max.y;
            else
            {
                var finalCol = target.GetComponentInParent<Collider>();
                if (finalCol != null)
                    surfaceY = finalCol.bounds.max.y;
            }
        }

        Vector3 finalPos;
        if (surfaceY != float.MinValue)
            finalPos = new Vector3(center.x, surfaceY + landingYOffset, center.z);
        else
            finalPos = new Vector3(endPos.x, endPos.y + landingYOffset, endPos.z);

        // сразу ставим позицию — игрок оказывается в центре платформы
        playerRoot.position = finalPos;

        // --- ПАУЗА: игрок замирает на центре платформы на landingPause секунд ---
        if (landingPause > 0f)
        {
            yield return new WaitForSeconds(landingPause);
        }

        // --- затем ждём один физический шаг и стабилизируем Rigidbody, чтобы избежать подпрыгов ---
        if (hadRb)
        {
            yield return new WaitForFixedUpdate();

            playerRigidbody.velocity = Vector3.zero;
            playerRigidbody.angularVelocity = Vector3.zero;
            playerRigidbody.Sleep();

            playerRigidbody.collisionDetectionMode = prevMode;
            playerRigidbody.isKinematic = false;

            playerRigidbody.velocity = Vector3.zero;
            playerRigidbody.angularVelocity = Vector3.zero;
        }

        // обработка по типу платформы (если это FinishPlatform или GlassPiece)
        var finish = target.GetComponentInParent<FinishPlatform>();
        if (finish != null)
        {
            Debug.Log("[PJ] Landed on FinishPlatform: " + finish.name);
            finish.OnPlayerArrive();
        }
        else
        {
            var gp = target.GetComponentInParent<GlassPiece>();
            if (gp != null && gp.isBreakable)
            {
                Debug.Log("[PJ] Landed on fragile " + gp.name + " — calling Break()");
                gp.Break();
            }
        }

        busy = false;
    }
}

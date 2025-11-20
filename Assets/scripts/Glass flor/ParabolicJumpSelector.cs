using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;

[DisallowMultipleComponent]
public class ParabolicJumpSelector : MonoBehaviour
{
    [Header("Targets selection")]
    [Tooltip("»спользуемый контроллер дл€ прицеливани€ (левый или правый). ≈сли оба заданы Ч оба работают")]
    public Transform leftController;
    public Transform rightController;

    [Header("Jump parameters")]
    public float jumpDuration = 0.6f;
    public float arcHeight = 1.0f;
    public float maxSelectDistance = 6f; // макс дистанци€ выбора плитки
    public LayerMask glassLayer = ~0; // слой, на котором расположены стекл€нные плитки

    [Header("Input")]
    [Tooltip("—читывать gripButton (grab) дл€ запуска прыжка")]
    public bool useGripButton = true;

    [Header("Player reference")]
    [Tooltip("Transform XR Origin (тот объект, который будет перемещатьс€)")]
    public Transform playerRoot;
    [Tooltip("Optional Rigidbody on player; will be made kinematic during jump")]
    public Rigidbody playerRigidbody;

    // internal
    GlassPiece highlighted = null;
    GlassPiece lastHighlighted = null;
    bool busy = false;

    void Awake()
    {
        if (playerRoot == null) playerRoot = transform;
        if (playerRigidbody == null && playerRoot != null) playerRigidbody = playerRoot.GetComponent<Rigidbody>();
    }

    void Update()
    {
        if (busy) return;

        // 1) провер€ем оба контроллера на наведЄнный объект (приоритет Ч правый, затем левый)
        bool found = TryUpdateHighlightFromController(rightController) || TryUpdateHighlightFromController(leftController);

        // 2) если нажали grab (grip), то начинаем прыжок к выделенной плитке
        if (highlighted != null && useGripButton && GripPressedThisFrame())
        {
            StartCoroutine(DoParabolicJumpTo(highlighted.transform));
        }
    }

    bool TryUpdateHighlightFromController(Transform ctrl)
    {
        if (ctrl == null) return false;

        Ray r = new Ray(ctrl.position, ctrl.forward);
        if (Physics.Raycast(r, out RaycastHit hit, maxSelectDistance, glassLayer, QueryTriggerInteraction.Ignore))
        {
            var gp = hit.collider.GetComponentInParent<GlassPiece>();
            if (gp != null)
            {
                SetHighlight(gp);
                return true;
            }
        }

        // если ничего не найдено Ч снимем подсветку (только если текущим был этот контроллер)
        ClearHighlight();
        return false;
    }

    void SetHighlight(GlassPiece gp)
    {
        if (highlighted == gp) return;
        if (highlighted != null) highlighted.Highlight(false);
        highlighted = gp;
        if (highlighted != null) highlighted.Highlight(true);
    }

    void ClearHighlight()
    {
        if (highlighted != null) { highlighted.Highlight(false); highlighted = null; }
    }

    bool GripPressedThisFrame()
    {
        // провер€ем все контроллеры Ч если любой контроллер только что нажал grip
        var devices = new List<InputDevice>();
        InputDevices.GetDevicesWithCharacteristics(InputDeviceCharacteristics.Controller, devices);

        foreach (var d in devices)
        {
            if (d.TryGetFeatureValue(CommonUsages.gripButton, out bool val) && val)
            {
                // важно: это не WasPressedThisFrame, но дл€ простоты Ч достаточно
                // (если нужна точность по кадрам Ч нужно хранить предыдущее состо€ние)
                return true;
            }
        }
        return false;
    }

    IEnumerator DoParabolicJumpTo(Transform target)
    {
        if (busy) yield break;
        if (target == null) yield break;

        busy = true;
        // снимем подсветку
        ClearHighlight();

        Vector3 startPos = playerRoot.position;
        Vector3 endPos = target.position;

        // optionally align heights (если XR pivot Ч на высоте головы, можно корректировать y)
        // здесь предполагаем pivot у playerRoot на "ступн€х", поэтому используем endPos.y = startPos.y
        endPos = new Vector3(endPos.x, startPos.y, endPos.z);

        // отключаем физику, если есть
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

        // убедимс€ в финальной позиции
        playerRoot.position = endPos;

        // restore rb
        if (hadRb)
        {
            playerRigidbody.isKinematic = false;
            playerRigidbody.velocity = Vector3.zero;
        }

        // при приземлении Ч вызов Break() если плитка ломка€
        var gp = target.GetComponentInParent<GlassPiece>();
        if (gp != null)
        {
            if (gp.isBreakable)
                gp.Break();
            else
            {
                // safe feedback (можно добавить звук)
            }
        }

        busy = false;
    }
}

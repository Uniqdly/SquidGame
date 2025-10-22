using UnityEngine;
using UnityEngine.XR;
using System.Collections.Generic;

/// <summary>
/// ������� �������� �����, ����� ������������ ������� �� ������������.
/// �������� � XR InputDevices (������������ Device Simulator), � ����� � �������� Space / LMB.
/// ������������� ������� �� ������, ����������� ������� (XR Origin) ��� ��������� manager.
/// </summary>
[DisallowMultipleComponent]
public class TriggerMove : MonoBehaviour
{
    [Header("References")]
    [Tooltip("���������, ������� ����� ������������ (XR Origin / PlayerRoot)")]
    public Transform playerRoot;

    [Header("Movement")]
    public float baseSpeed = 2.0f;         // �/� ��� ������ ������� ��������
    public bool useCharacterController = true;
    public CharacterController characterController; // optional (if null and useCharacterController true, will try to get from playerRoot)
    public bool ignoreY = true;            // ��������� ������ �� XZ, ��������� ���������

    [Header("Input / fallback")]
    [Tooltip("���� true, Space ��� LMB ����� �������� ��� ������� � Editor")]
    public bool allowKeyboardFallback = true;
    public KeyCode fallbackKey = KeyCode.Space;
    public bool fallbackMouseButton = true; // LMB

    [Header("Smoothing")]
    [Tooltip("������� ���������/���������� (0 - ���������, >0 - �����������)")]
    public float acceleration = 10f;

    [Header("Debug")]
    public bool debugLogs = false;

    // internal
    Vector3 currentVelocity = Vector3.zero;
    float currentForwardSpeed = 0f;

    // cache XR devices
    List<InputDevice> leftDevices = new List<InputDevice>();
    List<InputDevice> rightDevices = new List<InputDevice>();

    void Awake()
    {
        if (playerRoot == null)
        {
            playerRoot = this.transform;
            Debug.LogWarning("[TriggerMove] playerRoot not set � using object transform");
        }

        if (useCharacterController && characterController == null && playerRoot != null)
        {
            characterController = playerRoot.GetComponent<CharacterController>();
            if (characterController == null)
            {
                // �� ������ ������������� � ������� �� ���������� ������������
                if (debugLogs) Debug.Log("[TriggerMove] CharacterController not found on playerRoot. Falling back to Transform.Translate.");
            }
        }
    }

    void Update()
    {
        // 1) ��������� �������� ��������� � XR ���������
        float triggerValue = ReadXRTriggerValue();

        // 2) fallback: ���������� / ����
        if (allowKeyboardFallback)
        {
            if (Input.GetKey(fallbackKey)) triggerValue = Mathf.Max(triggerValue, 1f);
            if (fallbackMouseButton && Input.GetMouseButton(0)) triggerValue = Mathf.Max(triggerValue, 1f);
        }

        // 3) ������� �������� �� �������� (0..baseSpeed)
        float targetSpeed = Mathf.Clamp01(triggerValue) * baseSpeed;

        // smooth toward target speed
        if (acceleration > 0f)
            currentForwardSpeed = Mathf.MoveTowards(currentForwardSpeed, targetSpeed, acceleration * Time.deltaTime);
        else
            currentForwardSpeed = targetSpeed;

        // 4) ��������� velocity vector
        Vector3 forward = playerRoot.forward;
        if (ignoreY) { forward.y = 0f; if (forward.sqrMagnitude < 1e-6f) forward = Vector3.forward; forward.Normalize(); }

        Vector3 move = forward * currentForwardSpeed;

        // 5) ��������� ��������
        if (characterController != null && useCharacterController)
        {
            // CharacterController.Move ��������� �������� � ������ (������ * Time.deltaTime)
            Vector3 moveFrame = move * Time.deltaTime;
            characterController.Move(moveFrame);
        }
        else
        {
            // transform.Translate world space
            playerRoot.Translate(move * Time.deltaTime, Space.World);
        }

        // optional debug
        if (debugLogs && triggerValue > 0f)
        {
            Debug.Log($"[TriggerMove] trigger={triggerValue:F3}, speed={currentForwardSpeed:F3}");
        }
    }

    float ReadXRTriggerValue()
    {
        float maxVal = 0f;

        // left hand
        InputDevices.GetDevicesAtXRNode(XRNode.LeftHand, leftDevices);
        foreach (var d in leftDevices)
        {
            if (d.TryGetFeatureValue(CommonUsages.trigger, out float v))
            {
                if (v > maxVal) maxVal = v;
            }
            // also support squeeze/grip if you want:
            // if (d.TryGetFeatureValue(CommonUsages.grip, out float g)) maxVal = Mathf.Max(maxVal, g);
        }

        // right hand
        InputDevices.GetDevicesAtXRNode(XRNode.RightHand, rightDevices);
        foreach (var d in rightDevices)
        {
            if (d.TryGetFeatureValue(CommonUsages.trigger, out float v))
            {
                if (v > maxVal) maxVal = v;
            }
        }

        return maxVal;
    }

    // helper: expose current speed
    public float GetCurrentSpeed() => currentForwardSpeed;
}

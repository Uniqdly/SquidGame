using UnityEngine;
using UnityEngine.XR;

[RequireComponent(typeof(Rigidbody))]
public class JumpController : MonoBehaviour
{
    public PlayerGroundController groundController; // ссылка на твой скрипт ground check
    public float jumpHeight = 1.0f; // желаемая высота прыжка (метры)
    public float jumpCooldown = 0.2f; // минимальное время между прыжками
    public bool useXRPrimaryButton = true; // поддержка XR input

    Rigidbody rb;
    float lastJumpTime = -10f;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        if (groundController == null) groundController = GetComponent<PlayerGroundController>();
    }

    void Update()
    {
        // тестовый ввод (клавиатура)
        if (Input.GetButtonDown("Jump"))
        {
            TryJump();
        }

        // XR input (простая поддержка primaryButton на любом устройстве)
        if (useXRPrimaryButton)
        {
            bool primaryPressed = false;
            var devices = new System.Collections.Generic.List<InputDevice>();
            InputDevices.GetDevicesWithCharacteristics(InputDeviceCharacteristics.Controller, devices);
            foreach (var d in devices)
            {
                if (d.TryGetFeatureValue(CommonUsages.primaryButton, out bool val) && val) { primaryPressed = true; break; }
            }
            if (primaryPressed) TryJump();
        }
    }

    void TryJump()
    {
        if (Time.time - lastJumpTime < jumpCooldown) return;
        if (groundController == null || !groundController.IsGrounded) return;

        float g = Mathf.Abs(Physics.gravity.y);
        float v = Mathf.Sqrt(2f * g * Mathf.Max(0.01f, jumpHeight)); // v = sqrt(2gh)
        Vector3 vel = rb.velocity;
        vel.y = v;
        rb.velocity = vel;

        lastJumpTime = Time.time;
    }
}

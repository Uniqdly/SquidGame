using UnityEngine;
using UnityEngine.XR;

[RequireComponent(typeof(Rigidbody))]
public class JumpController : MonoBehaviour
{
    public float jumpHeight = 1.2f;
    public float groundCheckDistance = 0.25f;
    public LayerMask groundMask;
    public float jumpCooldown = 0.2f;
    public bool useXRPrimaryButton = true;

    Rigidbody rb;
    float lastJumpTime = -10f;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
    }

    void Update()
    {
        bool jumpPressed = Input.GetButtonDown("Jump");

        if (useXRPrimaryButton)
        {
            var devices = new System.Collections.Generic.List<InputDevice>();
            InputDevices.GetDevicesWithCharacteristics(InputDeviceCharacteristics.Controller, devices);
            foreach (var d in devices)
            {
                if (d.TryGetFeatureValue(CommonUsages.primaryButton, out bool v) && v)
                {
                    jumpPressed = true;
                    break;
                }
            }
        }

        if (jumpPressed)
            TryJump();
    }

    bool IsGrounded()
    {
        LayerMask mask = groundMask.value == 0 ? (LayerMask)~0 : groundMask;
        return Physics.Raycast(transform.position, Vector3.down, groundCheckDistance, mask);

    }

    void TryJump()
    {
        if (Time.time - lastJumpTime < jumpCooldown) return;
        if (!IsGrounded()) return;

        float g = Mathf.Abs(Physics.gravity.y);
        float v = Mathf.Sqrt(2f * g * jumpHeight);

        Vector3 vel = rb.velocity;
        vel.y = v;
        rb.velocity = vel;

        lastJumpTime = Time.time;
    }
}

using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class PlayerRigidbodyGravity : MonoBehaviour
{
    public Transform headTransform;
    public LayerMask groundLayers = ~0;
    public float groundCheckRadius = 0.15f;
    public float groundOffset = 0.1f; // от центра вниз
    public float groundMaxDistance = 0.2f;
    private Rigidbody rb;
    private bool grounded;

    public float gravityScale = 1f;
    public float terminalVelocity = 50f;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.useGravity = false; // мы применяем вручную
        rb.constraints = RigidbodyConstraints.FreezeRotation; // не даём крутиться
        if (headTransform == null && Camera.main != null) headTransform = Camera.main.transform;
    }

    void FixedUpdate()
    {
        grounded = GroundCheck();
        if (!grounded)
        {
            // применяем гравитацию
            Vector3 v = rb.velocity;
            v += Physics.gravity * gravityScale * Time.fixedDeltaTime;
            if (v.y < -terminalVelocity) v.y = -terminalVelocity;
            rb.velocity = v;
        }
        else
        {
            // немного "прижимаем" игрока к земле
            if (rb.velocity.y < 0f)
            {
                Vector3 v = rb.velocity;
                v.y = Mathf.Max(v.y, -1f);
                rb.velocity = v;
            }
        }
    }

    bool GroundCheck()
    {
        Vector3 origin = transform.position + Vector3.up * groundOffset;
        return Physics.SphereCast(origin, groundCheckRadius, Vector3.down, out RaycastHit hit, groundMaxDistance, groundLayers, QueryTriggerInteraction.Ignore);
    }
}

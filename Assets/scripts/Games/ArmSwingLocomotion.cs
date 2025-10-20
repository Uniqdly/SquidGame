using System.Collections;
using UnityEngine;

/// <summary>
/// Arm Swing Locomotion for VR:
/// Attach to a manager object (or XR Origin). Provide leftHand/rightHand transforms and playerRoot (XR Origin).
/// Moves the player forward when hands make sufficient back->forward swings.
/// Uses CharacterController.Move when available, otherwise translates the playerRoot transform.
/// </summary>
[DisallowMultipleComponent]
public class ArmSwingLocomotion : MonoBehaviour
{
    [Header("References")]
    public Transform playerRoot;        // XR Origin root (the object to move)
    public Transform leftHand;          // left controller transform
    public Transform rightHand;         // right controller transform

    [Header("Core movement params")]
    public float impulseMultiplier = 1.5f;   // how strong each swing contributes to movement
    public float maxSpeed = 3.0f;            // clamp speed (m/s)
    public float damping = 5.0f;             // smoothing of velocity
    public bool useCharacterController = true;

    [Header("Swing detection")]
    public float swingVelocityThreshold = 0.4f;   // minimum hand velocity (m/s) along local backward to count as a swing
    public float perHandCooldown = 0.18f;         // seconds until the same hand can trigger again
    public float combineWindow = 0.35f;           // time window to combine left+right swings
    public bool requireBothHands = false;         // if true, require at least one swing from both hands in window
    public float minCombinedStrength = 0.5f;      // minimal combined strength to apply movement

    [Header("Gravity / Vertical")]
    public float gravity = -9.81f;         // simple gravity when using CharacterController
    public float stepOffset = 0.3f;        // if using CharacterController, set to usual value

    [Header("Controls (optional)")]
    public KeyCode toggleEnabledKey = KeyCode.None;
    public bool enabledByDefault = true;

    [Header("Debug")]
    public bool debugDraw = false;
    public bool debugLogs = false;

    // internal state
    Vector3 lastLeftPos;
    Vector3 lastRightPos;
    Vector3 lastPlayerPos;
    float lastUpdateTime;

    float leftCooldownTimer = 0f;
    float rightCooldownTimer = 0f;

    float leftLastSwingTime = -10f;
    float rightLastSwingTime = -10f;
    float leftLastStrength = 0f;
    float rightLastStrength = 0f;

    Vector3 currentVelocity = Vector3.zero;
    CharacterController cc;

    bool locomotionEnabled;

    void Awake()
    {
        locomotionEnabled = enabledByDefault;
        if (playerRoot == null && Camera.main != null)
            playerRoot = Camera.main.transform;

        if (useCharacterController && playerRoot != null)
        {
            cc = playerRoot.GetComponent<CharacterController>();
            if (cc == null)
            {
                // optional: add CharacterController automatically (comment out if undesired)
                cc = playerRoot.gameObject.AddComponent<CharacterController>();
                cc.stepOffset = stepOffset;
                cc.center = new Vector3(0f, 0.9f, 0f);
                cc.height = 1.8f;
            }
        }

        lastUpdateTime = Time.time;
        if (leftHand != null) lastLeftPos = leftHand.position;
        if (rightHand != null) lastRightPos = rightHand.position;
        if (playerRoot != null) lastPlayerPos = playerRoot.position;
    }

    void Update()
    {
        // toggle enable
        if (toggleEnabledKey != KeyCode.None && Input.GetKeyDown(toggleEnabledKey))
        {
            locomotionEnabled = !locomotionEnabled;
            if (debugLogs) Debug.Log("[ArmSwing] locomotionEnabled = " + locomotionEnabled);
        }

        float dt = Time.deltaTime;
        if (dt <= 0f) return;

        // update cooldowns
        leftCooldownTimer = Mathf.Max(0f, leftCooldownTimer - dt);
        rightCooldownTimer = Mathf.Max(0f, rightCooldownTimer - dt);

        // sample hand velocities
        Vector3 leftVel = Vector3.zero;
        Vector3 rightVel = Vector3.zero;
        if (leftHand != null) leftVel = (leftHand.position - lastLeftPos) / dt;
        if (rightHand != null) rightVel = (rightHand.position - lastRightPos) / dt;

        // Update last positions
        if (leftHand != null) lastLeftPos = leftHand.position;
        if (rightHand != null) lastRightPos = rightHand.position;

        // Determine player's forward (flat)
        Vector3 playerForward = (playerRoot != null) ? playerRoot.forward : Vector3.forward;
        playerForward.y = 0f;
        if (playerForward.sqrMagnitude < 1e-6f) playerForward = Vector3.forward;
        playerForward.Normalize();

        // Project velocities on -forward (i.e. backward motion). We count backward swing as trigger.
        float leftProj = Vector3.Dot(leftVel, -playerForward);   // positive if hand moving backward relative to forward
        float rightProj = Vector3.Dot(rightVel, -playerForward);

        // compute strength (clamped) — how much swing happened this frame
        float leftStrength = Mathf.Max(0f, leftProj - swingVelocityThreshold); // only values above threshold
        float rightStrength = Mathf.Max(0f, rightProj - swingVelocityThreshold);

        // if hand exceeds threshold and not in cooldown -> register swing
        if (leftStrength > 0f && leftCooldownTimer <= 0f)
        {
            leftLastSwingTime = Time.time;
            leftLastStrength = leftStrength;
            leftCooldownTimer = perHandCooldown;
            if (debugLogs) Debug.Log($"[ArmSwing] Left swing: strength={leftStrength:F3}");
        }

        if (rightStrength > 0f && rightCooldownTimer <= 0f)
        {
            rightLastSwingTime = Time.time;
            rightLastStrength = rightStrength;
            rightCooldownTimer = perHandCooldown;
            if (debugLogs) Debug.Log($"[ArmSwing] Right swing: strength={rightStrength:F3}");
        }

        // decide whether to apply movement impulse
        float combinedStrength = 0f;
        // combine swings if they occurred within combineWindow, or if requireBothHands==false accumulate both recent strengths weighted by recency
        if (requireBothHands)
        {
            // require one recent swing from both hands
            if (Time.time - leftLastSwingTime <= combineWindow && Time.time - rightLastSwingTime <= combineWindow)
                combinedStrength = leftLastStrength + rightLastStrength;
        }
        else
        {
            // sum recent swings (within combineWindow)
            if (Time.time - leftLastSwingTime <= combineWindow) combinedStrength += leftLastStrength;
            if (Time.time - rightLastSwingTime <= combineWindow) combinedStrength += rightLastStrength;
        }

        // apply movement only if locomotion enabled and combined strength high enough
        if (locomotionEnabled && combinedStrength >= minCombinedToApply)
        {
            // movement amount = impulseMultiplier * combinedStrength * dt (scale to m/s)
            float moveAmount = impulseMultiplier * combinedStrength;
            Vector3 moveVec = playerForward * moveAmount;

            // integrate into currentVelocity smoothly
            // we add instantaneous forward velocity (clamped)
            currentVelocity += moveVec;
            float horizontalSpeed = new Vector3(currentVelocity.x, 0f, currentVelocity.z).magnitude;
            if (horizontalSpeed > maxSpeed)
            {
                Vector3 horiz = new Vector3(currentVelocity.x, 0f, currentVelocity.z).normalized * maxSpeed;
                currentVelocity = new Vector3(horiz.x, currentVelocity.y, horiz.z);
            }

            // zero out last strengths so we don't repeatedly apply same swing
            leftLastStrength = 0f;
            rightLastStrength = 0f;
            leftLastSwingTime = -10f;
            rightLastSwingTime = -10f;

            if (debugLogs) Debug.Log($"[ArmSwing] Applying impulse moveAmount={moveAmount:F3}, combined={combinedStrength:F3}");
        }

        // apply damping to horizontal velocity
        Vector3 horizVel = new Vector3(currentVelocity.x, 0f, currentVelocity.z);
        horizVel = Vector3.Lerp(horizVel, Vector3.zero, Mathf.Clamp01(damping * dt));
        currentVelocity = new Vector3(horizVel.x, currentVelocity.y, horizVel.z);

        // apply gravity if using CharacterController
        if (cc != null)
        {
            currentVelocity.y += gravity * dt;
            Vector3 move = currentVelocity * dt;
            // CharacterController expects movement in world space
            cc.Move(move);
        }
        else
        {
            // simple transform move + gravity
            currentVelocity.y += gravity * dt;
            playerRoot.Translate(currentVelocity * dt, Space.World);
        }

        // Reset Y velocity if grounded (simple check)
        if (cc != null && cc.isGrounded)
        {
            currentVelocity.y = -0.1f;
        }

        // debug draw velocities / projections
        if (debugDraw && playerRoot != null)
        {
            Debug.DrawLine(playerRoot.position, playerRoot.position + playerForward, Color.green);
            if (leftHand != null) Debug.DrawLine(leftHand.position, leftHand.position + leftVel, Color.cyan);
            if (rightHand != null) Debug.DrawLine(rightHand.position, rightHand.position + rightVel, Color.magenta);
        }
    }

    // helper: minimal combined strength param exposed to inspector via property-like field
    [SerializeField, Tooltip("Minimal combined strength required to apply movement (sum of recent swings)")]
    float minCombinedToApply = 0.5f;

    // Optional: expose method to instantly add forward impulse (useful for jumps / power-ups)
    public void AddForwardImpulse(float amount)
    {
        currentVelocity += playerRoot.forward * amount;
        if (debugLogs) Debug.Log($"[ArmSwing] AddForwardImpulse {amount}");
    }
}

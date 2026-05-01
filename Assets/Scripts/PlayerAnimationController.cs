using UnityEngine;

public class PlayerAnimationController : MonoBehaviour
{
    [SerializeField] PlayerController player;
    [SerializeField] Animator animator;
    [SerializeField] CharacterController controller;

    static readonly int IsWalkingHash = Animator.StringToHash("isWalking");
    static readonly int BlendHash     = Animator.StringToHash("Blend");

    [Header("Blend Smoothing")]
    [Tooltip("How fast Blend ramps UP when starting/speeding up.")]
    [SerializeField] float accelSmooth = 10f;

    [Tooltip("How fast Blend ramps DOWN when stopping/slowing. " +
             "Set high (15-25) so feet don't keep shuffling after you stop.")]
    [SerializeField] float decelSmooth = 20f;

    [Tooltip("Below this speed the character is considered idle.")]
    [SerializeField] float moveThreshold = 0.1f;

    float currentBlend;

    void Update()
    {
        UpdateMovementAnimation();
    }

    void UpdateMovementAnimation()
    {
        Vector3 horizontalVelocity = controller.velocity;
        horizontalVelocity.y = 0f;

        float speed = horizontalVelocity.magnitude;
        bool isMoving = speed > moveThreshold;

        animator.SetBool(IsWalkingHash, isMoving);

        // Target blend value: 0 = idle, ~0.5 = walk, 1 = run.
        float targetBlend = isMoving
            ? Mathf.InverseLerp(0f, player.runSpeed, speed)
            : 0f;

        // Use different smoothing for accel vs decel.
        // Decelerating fast prevents the "sliding feet" look when stopping.
        float smooth = (targetBlend > currentBlend) ? accelSmooth : decelSmooth;
        currentBlend = Mathf.Lerp(currentBlend, targetBlend, Time.deltaTime * smooth);

        // Snap to zero when very small to avoid endless tiny lerp.
        if (!isMoving && currentBlend < 0.01f) currentBlend = 0f;

        animator.SetFloat(BlendHash, currentBlend);
    }
}
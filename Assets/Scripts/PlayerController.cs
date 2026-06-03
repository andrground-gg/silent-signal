using UnityEngine;

public class PlayerController : MonoBehaviour
{
    public float walkSpeed = 5f;
    public float runSpeed = 9f;
    public float gravity = -9.81f;
    public float jumpHeight = 2f;

    public float mouseSensitivity = 100f;
    public Transform playerCamera;

    public float bobFrequency = 8f;
    public float bobAmplitudeY = 0.06f;
    public float bobSmoothing = 10f;
    private float bobTimer = 0f;
    private Vector3 bobOffset = Vector3.zero;
    private Vector3 cameraRestPosition;

    public AudioSource footstepSource;
    public AudioClip grassClip;
    public AudioClip concreteClip;
    public float footstepMinPitch = 0.9f;
    public float footstepMaxPitch = 1.1f;
    [Header("Footstep Timing")]
    public float footstepMinDelay = 0.08f; // absolute minimum time between footsteps
    public float groundedDebounce = 0.12f;
    private float _groundedStableTimer = 0f;
    public float footstepResumeDelay = 0.12f;
    private bool _wasMovingOnGround = false;
    public float walkStepInterval = 0.5f;
    public float runStepInterval = 0.3f;
    private float _footstepTimer = 0f;
    private float _lastFootstepTime = -10f;

    public float landingVelocityThreshold = 1.2f; // minimum downward speed to consider as a landing
    private bool _wasGrounded = false;

    [Header("Ladder")]
    public LayerMask ladderLayer;
    public float ladderClimbSpeed = 3.5f;
    public float ladderAttachDot = 0.4f;
    public float ladderContactGrace = 0.1f;
    public float ladderJumpVertical = 4f;
    public float ladderJumpHorizontal = 3f;
    public float ladderReattachDelay = 0.2f;
    private bool _onLadder = false;
    private float _ladderContactTimer = 0f;
    private float _ladderReattachTimer = 0f;
    private Vector3 _ladderNormal = Vector3.forward;

    private CharacterController controller;
    private Vector3 velocity;
    private float groundedTimer = 0f;
    private float groundedBuffer = 0.1f;
    
    float xRotation = 0f;

    private bool _isRunning;

    public bool IsInspecting { get; set; }

    public LayerMask grassLayer;
    public LayerMask concreteLayer;

    void Start()
    {
        controller = GetComponent<CharacterController>();
        cameraRestPosition = playerCamera.localPosition;

        CursorManager.Lock();
    }

    void Update()
    {
        if (IsInspecting) return;
        UpdateLockState();
        UpdateRotation();
        UpdateMovement();
        UpdateHeadBob();
    }

    private void UpdateMovement()
    {
        if (_ladderReattachTimer > 0f)
        {
            _ladderReattachTimer -= Time.deltaTime;
            if (_ladderReattachTimer < 0f) _ladderReattachTimer = 0f;
        }

        if (_ladderContactTimer > 0f)
        {
            _ladderContactTimer -= Time.deltaTime;
            if (_ladderContactTimer < 0f) _ladderContactTimer = 0f;
        }

        bool touchingLadder = _ladderContactTimer > 0f;

        // Debounce raw grounded state to avoid twitching on slopes
        bool groundedRaw = controller.isGrounded;
        if (groundedRaw)
        {
            _groundedStableTimer += Time.deltaTime;
            if (_groundedStableTimer > groundedDebounce) _groundedStableTimer = groundedDebounce;
        }
        else
        {
            _groundedStableTimer -= Time.deltaTime;
            if (_groundedStableTimer < 0f) _groundedStableTimer = 0f;
        }

        bool grounded = _groundedStableTimer >= groundedDebounce * 0.5f;

        if (grounded && !_wasGrounded)
        {
            if (velocity.y < -landingVelocityThreshold)
            {
                PlayLandingSound();
                _footstepTimer = Mathf.Max(_footstepTimer, footstepResumeDelay);
            }
        }

        if (grounded)
        {
            groundedTimer = groundedBuffer;
            if (velocity.y < 0f)
                velocity.y = -0.5f;
        }
        else
        {
            groundedTimer -= Time.deltaTime;
        }

        float x = Input.GetAxis("Horizontal");
        float z = Input.GetAxis("Vertical");
        _isRunning = Input.GetButton("Sprint") && z > 0.1f;

        float currentSpeed = _isRunning ? runSpeed : walkSpeed;
        Vector3 move = transform.right * x + transform.forward * z;

        Vector3 moveDir = move.sqrMagnitude > 0.001f ? move.normalized : Vector3.zero;
        bool movingTowardLadder = touchingLadder
            && _ladderReattachTimer <= 0f
            && Vector3.Dot(moveDir, -_ladderNormal) > ladderAttachDot;

        if (movingTowardLadder)
        {
            _onLadder = true;
        }
        else if (!touchingLadder)
        {
            _onLadder = false;
        }

        if (_onLadder)
        {
            if (Input.GetButtonDown("Jump"))
            {
                _onLadder = false;
                _ladderReattachTimer = ladderReattachDelay;
                groundedTimer = 0f;
                _groundedStableTimer = 0f;
                // apply an immediate impulse away from the ladder
                Vector3 impulse = (_ladderNormal * ladderJumpHorizontal) + (Vector3.up * ladderJumpVertical);
                controller.Move(impulse * Time.deltaTime);
                // keep only the vertical component so horizontal momentum isn't retained
                velocity = Vector3.up * ladderJumpVertical;
                _wasMovingOnGround = false;
                _wasGrounded = false;
                return;
            }

            velocity = Vector3.up * ladderClimbSpeed;
            controller.Move(velocity * Time.deltaTime);
            _wasMovingOnGround = false;
            _wasGrounded = false;
            return;
        }

        // if (Input.GetButtonDown("Jump") && groundedTimer > 0f)
        // {
        //     velocity.y = Mathf.Sqrt(-jumpHeight * gravity);
        //     groundedTimer = 0f;
        // }

        velocity.y += gravity * Time.deltaTime;
        controller.Move((move * currentSpeed + velocity) * Time.deltaTime);

        bool isMovingOnGround = grounded && move.magnitude > 0.1f;

        // If movement just started, add a small resume delay to avoid an immediate footstep
        if (isMovingOnGround && !_wasMovingOnGround)
        {
            _footstepTimer = Mathf.Max(_footstepTimer, footstepResumeDelay);
        }

        UpdateFootsteps(isMovingOnGround);
        _wasMovingOnGround = isMovingOnGround;
        _wasGrounded = grounded;
    }

    private void UpdateRotation()
    {
        if (!CursorManager.IsLocked)
            return;
        
        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity * Time.deltaTime;
        float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity * Time.deltaTime;

        // Vertical rotation (camera only)
        xRotation -= mouseY;
        xRotation = Mathf.Clamp(xRotation, -90f, 90f);

        playerCamera.localRotation = Quaternion.Euler(xRotation, 0f, 0f);

        // Horizontal rotation (player body)
        transform.Rotate(Vector3.up * mouseX);
    }
    
    private void UpdateLockState()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            CursorManager.Unlock();
            return;
        }

        if (!CursorManager.IsLocked && !CursorManager.UIHasCursor && Input.GetMouseButtonDown(0))
            CursorManager.Lock();
    }

    private void UpdateHeadBob()
    {
        bool shouldBob = controller.isGrounded && controller.velocity.magnitude > 0.1f;

        if (shouldBob)
        {
            float speedMultiplier = _isRunning ? 2.25f : 1f;
            bobTimer += Time.deltaTime * bobFrequency * speedMultiplier;

            bobOffset = new Vector3(
                0f,
                Mathf.Sin(bobTimer) * bobAmplitudeY,
                0f
            );
        }
        else
        {
            bobTimer = 0f;
            bobOffset = Vector3.zero;
        }

        playerCamera.localPosition = Vector3.Lerp(
            playerCamera.localPosition,
            cameraRestPosition + bobOffset,
            Time.deltaTime * bobSmoothing
        );
    }

    private void UpdateFootsteps(bool isMovingOnGround)
    {
        if (!isMovingOnGround)
        {
            _footstepTimer = 0f;
            return;
        }

        _footstepTimer -= Time.deltaTime;
        if (_footstepTimer > 0f) return;

        _footstepTimer = _isRunning ? runStepInterval : walkStepInterval;

        Vector3 rayOrigin = transform.position + Vector3.up * 0.1f;
        if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, 3f))
        {
            AudioClip toPlay;
            int layer = hit.collider.gameObject.layer;
            if (IsInLayerMask(grassLayer, layer)) toPlay = grassClip;
            else if (IsInLayerMask(concreteLayer, layer)) toPlay = concreteClip;
            else toPlay = concreteClip;

            if (toPlay != null && footstepSource != null)
            {
                float timeSinceLast = Time.time - _lastFootstepTime;
                if (timeSinceLast < footstepMinDelay)
                {
                    _footstepTimer = footstepMinDelay - timeSinceLast;
                    return;
                }

                footstepSource.pitch = Random.Range(footstepMinPitch, footstepMaxPitch);
                footstepSource.PlayOneShot(toPlay);
                _lastFootstepTime = Time.time;
            }
        }
    }

    private void PlayLandingSound()
    {
        Vector3 rayOrigin = transform.position + Vector3.up * 0.1f;
        if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, 1.5f))
        {
            AudioClip toPlay;
            int layer = hit.collider.gameObject.layer;
            if (IsInLayerMask(grassLayer, layer)) toPlay = grassClip;
            else if (IsInLayerMask(concreteLayer, layer)) toPlay = concreteClip;
            else toPlay = concreteClip;

            if (toPlay != null && footstepSource != null)
            {
                float timeSinceLast = Time.time - _lastFootstepTime;
                if (timeSinceLast < footstepMinDelay) return;

                footstepSource.pitch = Random.Range(footstepMinPitch, footstepMaxPitch);
                footstepSource.PlayOneShot(toPlay);
                _lastFootstepTime = Time.time;
            }
        }
    }

    private bool IsInLayerMask(LayerMask mask, int layer)
    {
        return (mask.value & (1 << layer)) != 0;
    }

    private void OnControllerColliderHit(ControllerColliderHit hit)
    {
        if (!IsInLayerMask(ladderLayer, hit.gameObject.layer))
            return;

        _ladderNormal = hit.normal;
        _ladderContactTimer = ladderContactGrace;
    }
}
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

    private CharacterController controller;
    private Vector3 velocity;
    private float groundedTimer = 0f;
    private float groundedBuffer = 0.1f;
    
    float xRotation = 0f;

    private bool _isRunning;

    public LayerMask grassLayer;
    public LayerMask concreteLayer;

    void Start()
    {
        controller = GetComponent<CharacterController>();
        cameraRestPosition = playerCamera.localPosition;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void Update()
    {
        UpdateLockState();
        UpdateRotation();
        UpdateMovement();
        UpdateHeadBob();
    }

    private void UpdateMovement()
    {
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
        if (Cursor.lockState != CursorLockMode.Locked)
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
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            return;
        }

        if (Cursor.lockState != CursorLockMode.Locked && Input.GetMouseButtonDown(0))
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            return;
        }
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
}
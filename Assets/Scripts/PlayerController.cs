using UnityEngine;

public class PlayerController : MonoBehaviour
{
    public float speed = 5f;
    public float gravity = -9.81f;
    public float jumpHeight = 2f;

    public float mouseSensitivity = 100f;
    public Transform playerCamera;

    private CharacterController controller;
    private Vector3 velocity;
    private float groundedTimer = 0f;
    private float groundedBuffer = 0.1f;
    
    float xRotation = 0f;

    void Start()
    {
        controller = GetComponent<CharacterController>();

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void Update()
    {
        UpdateLockState();
        UpdateRotation();
        UpdateMovement();
    }

    private void UpdateMovement()
    {
        // Ground buffer
        if (controller.isGrounded)
        {
            groundedTimer = groundedBuffer;

            if (velocity.y < 0)
                velocity.y = -0.5f; 
        }
        else
        {
            groundedTimer -= Time.deltaTime;
        }

        float x = Input.GetAxis("Horizontal");
        float z = Input.GetAxis("Vertical");

        Vector3 move = transform.right * x + transform.forward * z;

        if (Input.GetButtonDown("Jump") && groundedTimer > 0f)
        {
            velocity.y = Mathf.Sqrt(-jumpHeight * gravity);
            groundedTimer = 0f;
        }

        velocity.y += gravity * Time.deltaTime;

        Vector3 finalMove = move * speed + velocity;
        controller.Move(finalMove * Time.deltaTime);
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
}
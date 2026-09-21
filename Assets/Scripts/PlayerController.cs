using UnityEngine;
using UnityEngine.InputSystem;
using SensingWorld;

[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    [Header("Input")]
    [SerializeField] private InputActionAsset inputActions;

    [Header("Move")]
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float gravity = -9.81f;
    [SerializeField] private float jumpHeight = 1.5f;

    [Header("Look (optional)")]
    [SerializeField] private Transform cameraTransform;
    [SerializeField] private float mouseSensitivity = 0.03f;

    [Header("Push")]
    [SerializeField] private float pushForce = 2f;
    [SerializeField] private SensingWorldEventBusHost eventBusHost;

    private CharacterController controller;
    private InputAction moveAction;
    private InputAction lookAction;
    private InputAction jumpAction;

    private Vector3 velocity;
    private float cameraPitch;
    private bool MouseLook = true;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();

        if (eventBusHost == null)
            eventBusHost = FindFirstObjectByType<SensingWorldEventBusHost>();

        var map = inputActions.FindActionMap("Player", throwIfNotFound: true);
        moveAction = map.FindAction("Move", throwIfNotFound: true);
        lookAction = map.FindAction("Look", throwIfNotFound: true);
        jumpAction = map.FindAction("Jump", throwIfNotFound: true);
    }

    private void OnEnable()
    {
        moveAction.Enable();
        lookAction.Enable();
        jumpAction.Enable();

        jumpAction.performed += OnJumpPerformed;
    }

    private void OnDisable()
    {
        jumpAction.performed -= OnJumpPerformed;

        moveAction.Disable();
        lookAction.Disable();
        jumpAction.Disable();
    }

    private void OnJumpPerformed(InputAction.CallbackContext ctx)
    {
        if (controller.isGrounded)
        {
            velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
        }
    }

    private void Update()
    {
        HandleMove();

        if (Keyboard.current[Key.Tab].wasPressedThisFrame)
            MouseLook = !MouseLook;
        if (MouseLook)
        {
            Cursor.visible = false;
            Cursor.lockState = CursorLockMode.Locked;
        }else
        {
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
        }

        HandleLook();
    }

    private void HandleMove()
    {
        Vector2 input = moveAction.ReadValue<Vector2>();
        Vector3 move = transform.right * input.x + transform.forward * input.y;
        controller.Move(move * moveSpeed * Time.deltaTime);

        if (controller.isGrounded && velocity.y < 0f)
        {
            velocity.y = -2f;
        }

        velocity.y += gravity * Time.deltaTime;
        controller.Move(velocity * Time.deltaTime);
    }

    private void HandleLook()
    {
        if (cameraTransform == null) return;
        if (!MouseLook) return;

        Vector2 lookInput = lookAction.ReadValue<Vector2>();
        float mouseX = lookInput.x * mouseSensitivity;
        float mouseY = lookInput.y * mouseSensitivity;

        transform.Rotate(Vector3.up * mouseX);

        cameraPitch -= mouseY;
        cameraPitch = Mathf.Clamp(cameraPitch, -80f, 80f);
        cameraTransform.localRotation = Quaternion.Euler(cameraPitch, 0f, 0f);
    }

    private void OnControllerColliderHit(ControllerColliderHit hit)
    {
        Rigidbody body = hit.collider.attachedRigidbody;

        SensingWorldEntity targetEntity =
            hit.collider.GetComponentInParent<SensingWorldEntity>();

        if (targetEntity != null &&
            targetEntity.Kind == SensingWorldEntityKind.NPC &&
            eventBusHost != null)
        {
            eventBusHost.Publish(new SensingWorldEvent(
                SensingWorldEventType.Hit,
                gameObject,
                targetEntity.gameObject,
                Time.time,
                hit.point,
                targetEntity.gameObject.tag,
                Vector3.Distance(transform.position, targetEntity.transform.position),
                0f,
                0f));
        }

        if (body == null || body.isKinematic) return;

        // 위/아래 방향으로는 밀지 않음 (바닥 밟았다고 아래로 미는 것 방지)
        if (hit.moveDirection.y < -0.3f) return;

        Vector3 pushDir = new Vector3(hit.moveDirection.x, 0f, hit.moveDirection.z);
        body.AddForce(pushDir * pushForce, ForceMode.Impulse);
    }
}

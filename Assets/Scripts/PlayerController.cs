using System.Collections.Generic;
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
    [SerializeField] GravityGrabber grabber;
    private InputAction moveAction;
    private InputAction lookAction;
    private InputAction jumpAction;
    private InputAction attackAction;

    private Vector3 velocity;
    private float cameraPitch;
    private bool MouseLook = true;
    private readonly HashSet<SensingWorldEntity> activeControllerContacts = new();
    private readonly HashSet<SensingWorldEntity> controllerContactsThisMove = new();

    public float TouchMaxPower = 1f;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();

        if (eventBusHost == null)
            eventBusHost = FindFirstObjectByType<SensingWorldEventBusHost>();
        if (grabber == null)
            grabber = GetComponent<GravityGrabber>();

        var map = inputActions.FindActionMap("Player", throwIfNotFound: true);
        moveAction = map.FindAction("Move", throwIfNotFound: true);
        lookAction = map.FindAction("Look", throwIfNotFound: true);
        jumpAction = map.FindAction("Jump", throwIfNotFound: true);
        attackAction = map.FindAction("Attack", throwIfNotFound: true);
    }

    private void OnEnable()
    {
        moveAction.Enable();
        lookAction.Enable();
        jumpAction.Enable();

        jumpAction.performed += OnJumpPerformed;
        attackAction.performed += OnAttackPerformed;
    }

    private void OnDisable()
    {
        jumpAction.performed -= OnJumpPerformed;

        moveAction.Disable();
        lookAction.Disable();
        jumpAction.Disable();

        activeControllerContacts.Clear();
        controllerContactsThisMove.Clear();
    }

    private void OnJumpPerformed(InputAction.CallbackContext ctx)
    {
        if (controller.isGrounded)
        {
            velocity.y = Mathf.Sqrt(jumpHeight * gameObject.transform.localScale.y * -2f * gravity);

            eventBusHost.Publish(new SensingWorldEvent(
                SensingWorldEventType.Jump, gameObject, gameObject, Time.time, gameObject.transform.position, gameObject.tag, 0,0,0
            ));
        }
    }
    private void OnAttackPerformed(InputAction.CallbackContext ctx)
    {
        if (grabber.isHold)
        {
            grabber.Throw();

            eventBusHost.Publish(
                new SensingWorldEvent(
                    SensingWorldEventType.Throw,
                    gameObject,
                    gameObject,
                    Time.time,
                    gameObject.transform.position,
                    gameObject.tag,
                    0f,
                    0f,
                    0f));
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
        controllerContactsThisMove.Clear();

        Vector2 input = moveAction.ReadValue<Vector2>();
        Vector3 move = transform.right * input.x + transform.forward * input.y;
        controller.Move(move * moveSpeed * gameObject.transform.localScale.x * Time.deltaTime);

        if (controller.isGrounded && velocity.y < 0f)
        {
            velocity.y = -2f;
        }

        velocity.y += gravity * Time.deltaTime;
        controller.Move(velocity * Time.deltaTime);

        activeControllerContacts.RemoveWhere(target =>
            target == null || !controllerContactsThisMove.Contains(target));
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
            float velocity = body != null
                ? controller.velocity.magnitude
                : 0f;

            // Debug.Log($"Body :  {body != null} / {hit.collider?.name} -  {hit.rigidbody?.name} - {velocity} / {controller.velocity.magnitude}");

            if (velocity > 0)
            {
                eventBusHost.Publish(new SensingWorldEvent(
                velocity > TouchMaxPower
                    ? SensingWorldEventType.Hit
                    : SensingWorldEventType.Touch,
                gameObject,
                targetEntity.gameObject,
                Time.time,
                hit.point,
                targetEntity.gameObject.tag,
                Vector3.Distance(
                    transform.position,
                    targetEntity.transform.position),
                0f,
                0f));   
            }
        }

        // 여기부터는 플레이어가 물체를 미는 처리

        GrabbableObject grabbable =
            hit.collider.GetComponentInParent<GrabbableObject>();

        if (grabbable != null && grabbable.IsHeld)
            return;

        if (body == null || body.isKinematic)
            return;

        if (hit.moveDirection.y < -0.3f)
            return;

        Vector3 pushDir = new Vector3(
            hit.moveDirection.x,
            0f,
            hit.moveDirection.z);

        body.AddForce(
            pushDir * pushForce,
            ForceMode.Impulse);
    }
}

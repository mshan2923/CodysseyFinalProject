using SensingWorld;
using UnityEngine;
using UnityEngine.AI;

public sealed class CatNpcAnimationDriver : MonoBehaviour
{
    private static readonly int FallHash = Animator.StringToHash("Fall");
    private static readonly int JumpHash = Animator.StringToHash("Jump");
    private static readonly int XHash = Animator.StringToHash("X");

    [Header("SensingWorld Events")]
    [SerializeField] private SensingWorldEventBusHost eventBusHost;

    [SerializeField] private Animator animator;
    [SerializeField] private NavMeshAgent navigationAgent;
    [SerializeField] private CharacterController characterController;
    [SerializeField] private JumpUpLinkTraversal jumpLinkTraversal;
    [SerializeField, Min(0f)] private float smoothTime = 0.1f;
    [SerializeField, Min(1f)] private float fullTurnAngle = 90f;
    [SerializeField, Min(0.001f)] private float minimumTurnSpeed = 0.05f;
    [Header("Direction Debug")]
    [SerializeField] private bool drawDirectionDebug = true;
    [SerializeField, Min(0.1f)] private float debugRayLength = 2f;

    private float currentTurnX;
    private float turnVelocity;

    private void Awake()
    {
        if (animator == null)
            animator = GetComponentInChildren<Animator>();

        if (navigationAgent == null)
            navigationAgent = GetComponent<NavMeshAgent>();

        if (characterController == null)
            characterController = GetComponent<CharacterController>();

        if (jumpLinkTraversal == null)
            jumpLinkTraversal = GetComponent<JumpUpLinkTraversal>();

        if (eventBusHost == null)
            eventBusHost = FindFirstObjectByType<SensingWorldEventBusHost>();

    }

    private void Update()
    {
        if (animator == null)
            return;

        Vector3 worldVelocity = navigationAgent != null
            ? navigationAgent.velocity
            : characterController != null
                ? characterController.velocity
                : Vector3.zero;

        Vector3 desiredWorldVelocity = navigationAgent != null
            ? navigationAgent.desiredVelocity
            : worldVelocity;
        desiredWorldVelocity.y = 0f;

        float targetTurnX = 0f;
        Vector3 desiredDirection = desiredWorldVelocity.normalized;

        {
            float absoluteAngle = Vector3.Angle(transform.forward, desiredDirection);
            float magnitude = Mathf.Clamp01(absoluteAngle / fullTurnAngle);
            float side = Vector3.Dot(transform.right, desiredDirection);
            float sign = side < 0f ? -1f : 1f;
            targetTurnX = magnitude * sign;
        }


        animator.SetFloat(XHash, targetTurnX);

        DrawDirectionDebug(desiredWorldVelocity, targetTurnX);
        // animator.SetFloat(SpeedMagnitudeHash, targetVelocity.magnitude);//NavigationToLocation Action에서

        if (jumpLinkTraversal == null || !jumpLinkTraversal.IsTraversing)
        {
            animator.SetBool(JumpHash, false);
            animator.SetBool(FallHash, false);
        }
    }

    public void OnJump()
    {
        eventBusHost.Publish(new SensingWorldEvent
            (
                SensingWorldEventType.Jump,
             gameObject, gameObject, Time.time, 
             gameObject.transform.position, 
             gameObject.tag, 
             0,0,0
            ));
    }

    private void DrawDirectionDebug(Vector3 desiredWorldVelocity, float targetTurnX)
    {
        if (!drawDirectionDebug)
            return;

        Vector3 origin = transform.position + Vector3.up * 0.05f;
        Vector3 desiredDirection = desiredWorldVelocity.sqrMagnitude > 0.0001f
            ? desiredWorldVelocity.normalized
            : Vector3.zero;

        Debug.DrawRay(origin, transform.forward * debugRayLength, Color.green);
        // Debug.DrawRay(origin, transform.right * debugRayLength, Color.blue);

        if (desiredDirection != Vector3.zero)
            Debug.DrawRay(origin, desiredDirection * debugRayLength, Color.cyan);

        // Debug.DrawRay(
        //     origin + Vector3.up * 0.05f,
        //     transform.right * (targetTurnX * debugRayLength),
        //     Color.red);
    }

    private void OnDrawGizmosSelected()
    {
        if (!drawDirectionDebug)
            return;

        Vector3 origin = transform.position + Vector3.up * 0.1f;
        Vector3 desiredDirection = navigationAgent != null
            ? navigationAgent.desiredVelocity
            : Vector3.zero;
        desiredDirection.y = 0f;
        float absoluteAngle = desiredDirection.sqrMagnitude > 0.0001f
            ? Vector3.Angle(transform.forward, desiredDirection.normalized)
            : 0f;

#if UNITY_EDITOR
        UnityEditor.Handles.Label(
            origin,
            $"Animator X: {currentTurnX:F2}\nTarget Angle: {absoluteAngle:F1}");
#endif
    }
}

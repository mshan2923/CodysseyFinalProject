using UnityEngine;
using UnityEngine.AI;

public sealed class CatNpcAnimationDriver : MonoBehaviour
{
    private static readonly int FallHash = Animator.StringToHash("Fall");
    private static readonly int JumpHash = Animator.StringToHash("Jump");
    private static readonly int SpeedMagnitudeHash = Animator.StringToHash("SpeedMagnitude");
    private static readonly int XHash = Animator.StringToHash("X");

    [SerializeField] private Animator animator;
    [SerializeField] private NavMeshAgent navigationAgent;
    [SerializeField] private CharacterController characterController;
    [SerializeField] private JumpUpLinkTraversal jumpLinkTraversal;
    [SerializeField] private float smoothTime = 0.1f;
    [SerializeField] private float turnAngleThreshold = 5f;
    [SerializeField] private float minimumTurnSpeed = 0.05f;

    private Vector2 currentVelocity;
    private bool isTurning;
    private float turnStartAngle;
    private float turnDirection;

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

        Vector3 localVelocity = transform.InverseTransformDirection(worldVelocity);
        float speed = navigationAgent != null ? navigationAgent.speed : 1f;

        if (speed <= 0.001f)
            speed = 1f;

        Vector2 targetVelocity = new Vector2(
            Mathf.Clamp(localVelocity.x / speed, -1f, 1f),
            Mathf.Clamp(localVelocity.z / speed, -1f, 1f));

        // Vector3 desiredWorldVelocity = navigationAgent != null
        //     ? navigationAgent.desiredVelocity
        //     : worldVelocity;
        // desiredWorldVelocity.y = 0f;

        // if (desiredWorldVelocity.sqrMagnitude > minimumTurnSpeed * minimumTurnSpeed)
        // {
        //     float signedAngle = Vector3.SignedAngle(
        //         transform.forward,
        //         desiredWorldVelocity.normalized,
        //         Vector3.up);

        //     float absoluteAngle = Mathf.Abs(signedAngle);

        //     if (absoluteAngle > turnAngleThreshold)
        //     {
        //         float currentDirection = Mathf.Sign(signedAngle);

        //         if (!isTurning || !Mathf.Approximately(turnDirection, currentDirection))
        //         {
        //             isTurning = true;
        //             turnStartAngle = absoluteAngle;
        //             turnDirection = currentDirection;
        //         }

        //         float turnProgress = 1f - Mathf.Clamp01(absoluteAngle / turnStartAngle);
        //         float turnPulse = 1f - Mathf.Abs(turnProgress * 2f - 1f);
        //         targetVelocity = new Vector2(turnDirection * turnPulse, 0f);
        //     }
        //     else
        //     {
        //         isTurning = false;
        //     }
        // }
        // else
        // {
        //     isTurning = false;
        // }

        // float lerpFactor = 1f - Mathf.Exp(-Time.deltaTime / Mathf.Max(0.001f, smoothTime));
        // currentVelocity = Vector2.Lerp(currentVelocity, targetVelocity, lerpFactor);

        animator.SetFloat(XHash, targetVelocity.x);
        // animator.SetFloat(SpeedMagnitudeHash, targetVelocity.magnitude);//NavigationToLocation Action에서

        if (jumpLinkTraversal == null || !jumpLinkTraversal.IsTraversing)
        {
            animator.SetBool(JumpHash, false);
            animator.SetBool(FallHash, false);
        }
    }

    public void OnJump()
    {
        Debug.Log("Try Jump");
    }
}

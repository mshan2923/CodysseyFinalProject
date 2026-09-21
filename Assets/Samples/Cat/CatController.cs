using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class CatController : MonoBehaviour
{
    [Header("이동")]
    public float moveSpeed = 4f;
    public float rotationSpeed = 10f; // 고양이가 이동 방향으로 도는 속도

    public float animSmoothTime = 0.1f;
    [Header("중력")]
    public float gravity = -20f;
    private float verticalVelocity;

    [Header("참조")]
    public Transform cameraTransform;
    public Animator animator;

    Vector2 currentAnimVelocity; // 애니메이터로 보낼 X,Z (블렌드트리용)
    Vector2 animVelocityRef;
    public CharacterController characterController;

    void Start()
    {
        if (cameraTransform == null && Camera.main != null)
            cameraTransform = Camera.main.transform;

        if (characterController == null)
             characterController = GetComponent<CharacterController>();

    }

    void Update()
    {
        // 1. 입력 (대각선 자동 지원: 두 축이 동시에 눌리면 정규화됨)
        float inputX = Input.GetAxisRaw("Horizontal"); // A/D
        float inputZ = Input.GetAxisRaw("Vertical");   // W/S
        Vector3 inputDir = new Vector3(inputX, 0f, inputZ).normalized;

        // 2. 카메라 기준으로 월드 방향 변환 (카메라가 자유 회전해도 이동은 카메라 정면 기준)
        Vector3 camForward = cameraTransform.forward;
        Vector3 camRight = cameraTransform.right;
        camForward.y = 0f;
        camRight.y = 0f;
        camForward.Normalize();
        camRight.Normalize();

        Vector3 moveDir = camForward * inputDir.z + camRight * inputDir.x;

        // 3. 실제 이동
        Vector3 velocity = moveDir * moveSpeed;

        if (characterController.isGrounded && verticalVelocity < 0)
            verticalVelocity = -2f; // 바닥에 붙어있게
        else
            verticalVelocity += gravity * Time.deltaTime;

        Vector3 fullVelocity = new Vector3(velocity.x, verticalVelocity, velocity.z);
        characterController.Move(fullVelocity * Time.deltaTime);

        // 4. 캐릭터가 이동 방향(카메라 정면과의 DOT 결과 반영)으로 부드럽게 회전
        if (moveDir.sqrMagnitude > 0.001f)
        {
            Quaternion targetRot = Quaternion.LookRotation(moveDir, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, rotationSpeed * Time.deltaTime);
        }

        // 5. 애니메이터용 X,Z 계산: 캐릭터 로컬 기준이 아니라
        //    "카메라 정면 대비 이동 방향"의 DOT으로 전진/횡이동 값을 뽑음 (스트레이 방식)
        float forwardDot = Vector3.Dot(moveDir, transform.forward); // 전진/후진
        float rightDot = Vector3.Dot(moveDir, transform.right);     // 좌/우

        Vector2 targetAnim = new Vector2(rightDot, forwardDot) * inputDir.magnitude;
        currentAnimVelocity = Vector2.SmoothDamp(currentAnimVelocity, targetAnim, ref animVelocityRef, animSmoothTime);

        animator.SetFloat("X", currentAnimVelocity.x);
        animator.SetFloat("Z", currentAnimVelocity.y);
    }
}
using UnityEngine;

public class CameraRotation : MonoBehaviour
{
    [SerializeField] private Transform character;

    [SerializeField] private float sensitivity = 3f;
    [SerializeField] private float rotationThreshold = 30f;
    [SerializeField] private float characterRotationSpeed = 360f;

    [SerializeField] private float minPitch = -60f;
    [SerializeField] private float maxPitch = 60f;

    private float pitch;
    private float yaw;

    void Start()
    {
        yaw = character.eulerAngles.y;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void Update()
    {
        float mouseX = Input.GetAxis("Mouse X");
        float mouseY = Input.GetAxis("Mouse Y");

        // 카메라 회전
        yaw += mouseX * sensitivity;

        pitch -= mouseY * sensitivity;
        pitch = Mathf.Clamp(pitch, minPitch, maxPitch);

        transform.rotation = Quaternion.Euler(
            pitch,
            yaw,
            0f
        );

        // 캐릭터와 카메라의 수평 각도 차이
        float angle = Mathf.DeltaAngle(
            character.eulerAngles.y,
            yaw
        );

        // 일정 각도를 넘었을 때만 캐릭터 회전
        if (Mathf.Abs(angle) > rotationThreshold)
        {
            float targetYaw = yaw;

            character.rotation = Quaternion.RotateTowards(
                character.rotation,
                Quaternion.Euler(0f, targetYaw, 0f),
                characterRotationSpeed * Time.deltaTime
            );
        }
    }
}
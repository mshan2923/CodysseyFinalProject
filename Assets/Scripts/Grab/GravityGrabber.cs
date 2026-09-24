using UnityEngine;
using UnityEngine.InputSystem;

namespace SensingWorld
{
    public sealed class GravityGrabber : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Camera viewCamera;
        [SerializeField] private Transform holdPoint;
        [SerializeField] private SensingWorldEventBusHost eventBusHost;
        [SerializeField] private Collider playerCollider; // 플레이어 CharacterController 콜라이더

        [Header("Grab")]
        [SerializeField] private float maxGrabDistance = 4f;
        [SerializeField] private float holdDistance = 2.5f;
        [SerializeField] private LayerMask grabbableLayers = ~0;
        [SerializeField] private Key grabKey = Key.E;
        [SerializeField] private float maxHoldDistanceBeforeDrop = 6f; // 이 거리보다 멀어지면 자동 드롭

        [Header("Physics Follow (PD control)")]
        [SerializeField] private float followSpring = 150f;   // 위치 오차에 대한 가속도 계수
        [SerializeField] private float followDamping = 15f;   // 속도 감쇠 계수 (진동 방지 핵심)
        [SerializeField] private float maxFollowAccel = 100f;

        private GrabbableObject heldObject;
        private GrabbableObject pointedObject;
        private GameObject lookedTarget;
        private Collider[] heldColliders;

        private bool restoreGravity;
        private CollisionDetectionMode restoreCollisionDetectionMode;
        private RigidbodyInterpolation restoreInterpolation;
        private float restoreSleepThreshold;

        private void Awake()
        {
            if (viewCamera == null)
                viewCamera = GetComponentInChildren<Camera>();

            if (holdPoint == null && viewCamera != null)
                holdPoint = viewCamera.transform;

            if (eventBusHost == null)
                eventBusHost = FindFirstObjectByType<SensingWorldEventBusHost>();
        }

        private void Update()
        {
            UpdateLookEvent();
            UpdatePointedOutline();

            if (Keyboard.current != null &&
                Keyboard.current[grabKey].wasPressedThisFrame)
            {
                if (heldObject == null)
                    TryGrab();
                else
                    Release();
            }
        }

        private void FixedUpdate()
        {
            if (heldObject == null)
                return;

            Rigidbody body = heldObject.Body;
            if (body == null)
                return;

            Vector3 targetPosition;

            if (viewCamera != null)
            {
                targetPosition =
                    viewCamera.transform.position +
                    viewCamera.transform.forward * holdDistance;
            }
            else if (holdPoint != null)
            {
                targetPosition = holdPoint.position;
            }
            else
            {
                return;
            }

            // 너무 멀어지면 (벽에 막혀서 못 따라오는 경우 등) 자동으로 놓기
            if (Vector3.Distance(body.position, targetPosition) > maxHoldDistanceBeforeDrop)
            {
                Release();
                return;
            }

            body.WakeUp();

            Vector3 positionError = targetPosition - body.position;

            // PD 제어: 스프링(오차) - 댐퍼(현재 속도) → 진동 없이 수렴
            Vector3 desiredAccel =
                positionError * followSpring - body.linearVelocity * followDamping;

            desiredAccel = Vector3.ClampMagnitude(desiredAccel, maxFollowAccel);

            body.AddForce(desiredAccel, ForceMode.Acceleration);
        }

        private void TryGrab()
        {
            GrabbableObject grabbable = FindGrabbableInView();

            if (grabbable == null || grabbable.Body == null)
                return;

            heldObject = grabbable;

            Rigidbody body = heldObject.Body;

            restoreGravity = body.useGravity;
            restoreCollisionDetectionMode = body.collisionDetectionMode;
            restoreInterpolation = body.interpolation;
            restoreSleepThreshold = body.sleepThreshold;

            body.useGravity = false;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.sleepThreshold = 0f; // 잡고 있는 동안 재우지 않음
            body.WakeUp();

            // 플레이어와의 충돌만 무시 — 벽/바닥과는 정상 충돌 유지
            if (playerCollider != null)
            {
                heldColliders = heldObject.GetComponentsInChildren<Collider>();
                foreach (var col in heldColliders)
                    Physics.IgnoreCollision(col, playerCollider, true);
            }

            heldObject.SetHeld(true);
            heldObject.SetOutlineVisible(true);

            PublishEvent(
                SensingWorldEventType.ObjectPickedUp,
                heldObject.gameObject);
        }

        private GrabbableObject FindGrabbableInView()
        {
            if (viewCamera == null)
                return null;

            Ray ray = new Ray(
                viewCamera.transform.position,
                viewCamera.transform.forward);

            if (!Physics.Raycast(
                    ray,
                    out RaycastHit hit,
                    maxGrabDistance,
                    grabbableLayers,
                    QueryTriggerInteraction.Ignore))
            {
                return null;
            }

            return hit.collider.GetComponentInParent<GrabbableObject>();
        }

        private GameObject FindLookTargetInView()
        {
            if (viewCamera == null)
                return null;

            Ray ray = new Ray(
                viewCamera.transform.position,
                viewCamera.transform.forward);

            if (!Physics.Raycast(
                    ray,
                    out RaycastHit hit,
                    maxGrabDistance,
                    grabbableLayers,
                    QueryTriggerInteraction.Ignore))
            {
                return null;
            }

            SensingWorldEntity entity =
                hit.collider.GetComponentInParent<SensingWorldEntity>();

            if (entity != null)
                return entity.gameObject;

            GrabbableObject grabbable =
                hit.collider.GetComponentInParent<GrabbableObject>();

            if (grabbable != null)
                return grabbable.gameObject;

            Transform current = hit.collider.transform;

            while (current != null)
            {
                if (current.CompareTag("Interaction") ||
                    current.CompareTag("NPC"))
                {
                    return current.gameObject;
                }

                current = current.parent;
            }

            return null;
        }

        private void UpdateLookEvent()
        {
            GameObject nextLookTarget = FindLookTargetInView();

            if (nextLookTarget == lookedTarget)
                return;

            if (lookedTarget != null)
                PublishEvent(
                    SensingWorldEventType.TargetLookEnded,
                    lookedTarget);

            lookedTarget = nextLookTarget;

            if (lookedTarget != null)
                PublishEvent(
                    SensingWorldEventType.TargetLookStarted,
                    lookedTarget);
        }

        private void UpdatePointedOutline()
        {
            GrabbableObject nextPointedObject =
                FindGrabbableInView();

            if (nextPointedObject == pointedObject)
                return;

            if (pointedObject != null)
            {
                pointedObject.SetOutlineVisible(
                    pointedObject == heldObject);
            }

            pointedObject = nextPointedObject;

            if (pointedObject != null)
                pointedObject.SetOutlineVisible(true);
        }

        private void Release()
        {
            if (heldObject == null)
                return;

            Rigidbody body = heldObject.Body;

            body.useGravity = restoreGravity;
            body.collisionDetectionMode = restoreCollisionDetectionMode;
            body.interpolation = restoreInterpolation;
            body.sleepThreshold = restoreSleepThreshold;

            body.WakeUp();

            if (playerCollider != null && heldColliders != null)
            {
                foreach (var col in heldColliders)
                {
                    if (col != null)
                        Physics.IgnoreCollision(col, playerCollider, false);
                }
            }
            heldColliders = null;

            heldObject.SetHeld(false);
            heldObject.SetOutlineVisible(
                heldObject == pointedObject);

            PublishEvent(
                SensingWorldEventType.ObjectDropped,
                heldObject.gameObject);

            heldObject = null;
        }

        private void PublishEvent(
            SensingWorldEventType type,
            GameObject target)
        {
            if (eventBusHost == null)
                return;

            Vector3 position =
                target != null
                    ? target.transform.position
                    : transform.position;

            string targetTag =
                target != null
                    ? target.tag
                    : string.Empty;

            eventBusHost.Publish(
                new SensingWorldEvent(
                    type,
                    gameObject,
                    target,
                    Time.time,
                    position,
                    targetTag,
                    0f,
                    0f,
                    0f));
        }

        private void OnDisable()
        {
            if (lookedTarget != null)
            {
                PublishEvent(
                    SensingWorldEventType.TargetLookEnded,
                    lookedTarget);

                lookedTarget = null;
            }

            if (pointedObject != null)
                pointedObject.SetOutlineVisible(false);

            Release();
        }

        private void OnDrawGizmosSelected()
        {
            if (viewCamera == null)
                return;

            Gizmos.color = Color.cyan;

            Gizmos.DrawRay(
                viewCamera.transform.position,
                viewCamera.transform.forward * maxGrabDistance);
        }
    }
}
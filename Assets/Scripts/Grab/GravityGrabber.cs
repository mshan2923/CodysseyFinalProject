using UnityEngine;
using UnityEngine.AI;
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

        [SerializeField] private float ThrowPower = 10f;

        private GrabbableObject heldObject;
        public bool isHold { get => heldObject != null;}
        private GrabbableObject pointedObject;
        private GameObject lookedTarget;
        private Collider[] heldColliders;

        private bool restoreGravity;
        private bool restoreIsKinematic;
        private CollisionDetectionMode restoreCollisionDetectionMode;
        private RigidbodyInterpolation restoreInterpolation;
        private float restoreSleepThreshold;

        private NavMeshAgent pendingNavMeshAgent;
        private GrabbableObject pendingNavMeshObject;

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
                    viewCamera.transform.forward * holdDistance * gameObject.transform.localScale.x;
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
            if (Vector3.Distance(body.position, targetPosition) > maxHoldDistanceBeforeDrop * gameObject.transform.localScale.x)
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
            restoreIsKinematic = body.isKinematic;
            restoreCollisionDetectionMode = body.collisionDetectionMode;
            restoreInterpolation = body.interpolation;
            restoreSleepThreshold = body.sleepThreshold;

            body.useGravity = false;
            // Kinematic Rigidbody에는 AddForce가 적용되지 않으므로,
            // 잡고 있는 동안에는 반드시 물리 Rigidbody로 전환한다.
            body.isKinematic = false;
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

            if (heldObject.transform.TryGetComponent(out NavMeshAgent agent))
            {
                if (agent.enabled)
                {
                    agent.isStopped = true;
                    agent.enabled = false;
                }
                body.isKinematic = false; // NavMeshAgent가 Rigidbody를 제어하지 않도록 전환
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
                    maxGrabDistance * gameObject.transform.localScale.x,
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
                    maxGrabDistance * gameObject.transform.localScale.x,
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

            bool hasNavMeshAgent = heldObject.transform.TryGetComponent(
                out NavMeshAgent agent);

            // NavMeshAgent가 있던 NPC는 놓는 즉시 Agent를 켜지 않는다.
            // 먼저 실제 물리 충돌을 받아 바닥/벽에 안착한 뒤 재활성화한다.
            if (hasNavMeshAgent)
            {
                body.isKinematic = false;
                pendingNavMeshAgent = agent;
                pendingNavMeshObject = heldObject;
                pendingNavMeshObject.CollisionEntered +=
                    OnPendingNavMeshObjectCollision;
            }
            else
            {
                body.isKinematic = restoreIsKinematic;
            }

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

        private void OnPendingNavMeshObjectCollision(Collision collision)
        {
            if (pendingNavMeshObject == null || pendingNavMeshAgent == null)
                return;

            Vector3 position = pendingNavMeshObject.transform.position;
            int areaMask = pendingNavMeshAgent.areaMask;

            if (!NavMesh.SamplePosition(position, out NavMeshHit navHit, 1.5f, areaMask))
                return;

            GrabbableObject grabbable = pendingNavMeshObject;
            NavMeshAgent agent = pendingNavMeshAgent;
            Rigidbody body = grabbable.Body;

            grabbable.CollisionEntered -= OnPendingNavMeshObjectCollision;
            pendingNavMeshObject = null;
            pendingNavMeshAgent = null;

            // 충돌 후 NavMesh 높이만 보정한 다음 Agent를 다시 맡긴다.
            Vector3 snappedPosition = grabbable.transform.position;
            snappedPosition.y = navHit.position.y;
            grabbable.transform.position = snappedPosition;

            agent.enabled = true;
            if (!agent.isOnNavMesh)
            {
                agent.enabled = false;
                return;
            }

            agent.isStopped = false;
            agent.ResetPath();
            body.isKinematic = true;
        }

        public void Throw()
        {
            if (heldObject == null)
                return;

            // Throw 경로에서도 같은 프레임에 heldObject를 비우고
            // 대상의 IsHeld 상태를 해제할 수 있도록 참조를 고정한다.
            GrabbableObject thrownObject = heldObject;

            Rigidbody body = heldObject.Body;

            body.useGravity = restoreGravity;
            body.collisionDetectionMode = restoreCollisionDetectionMode;
            body.interpolation = restoreInterpolation;
            body.sleepThreshold = restoreSleepThreshold;
            Vector3 throwDirection = viewCamera != null
                ? viewCamera.transform.forward
                : transform.forward;
            body.linearVelocity = throwDirection * ThrowPower;

            bool hasNavMeshAgent = heldObject.transform.TryGetComponent(
                out NavMeshAgent agent);

            // NavMeshAgent가 있던 NPC는 놓는 즉시 Agent를 켜지 않는다.
            // 먼저 실제 물리 충돌을 받아 바닥/벽에 안착한 뒤 재활성화한다.
            if (hasNavMeshAgent)
            {
                body.isKinematic = false;
                pendingNavMeshAgent = agent;
                pendingNavMeshObject = heldObject;
                pendingNavMeshObject.CollisionEntered +=
                    OnPendingNavMeshObjectCollision;
            }
            else
            {
                body.isKinematic = restoreIsKinematic;
            }

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

            thrownObject.SetHeld(false);
            thrownObject.SetOutlineVisible(
                thrownObject == pointedObject);

            PublishEvent(
                SensingWorldEventType.Throw,
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
                viewCamera.transform.forward * maxGrabDistance * gameObject.transform.localScale.x);
        }
    }
}

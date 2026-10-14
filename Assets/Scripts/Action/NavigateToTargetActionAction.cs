using System;
using Unity.Properties;
using UnityEngine;
using UnityEngine.AI;

namespace Unity.Behavior
{
    [Serializable, GeneratePropertyBag]
    [NodeDescription(
        name: "Navigate To Target Fixed",
        description: "Navigates a GameObject towards another GameObject using NavMeshAgent.",
        story: "[Agent] navigates to [Target] Can Cancel",
        category: "Action/Navigation",
        id: "5501dacdb29c38b60b4b917e5d7cb23b")]
    public partial class NavigateToTargetActionAction : Action
    {
        public enum TargetPositionMode
        {
            ClosestPointOnAnyCollider,
            ClosestPointOnTargetCollider,
            ExactTargetPosition
        }

        [SerializeReference] public BlackboardVariable<GameObject> Agent;
        [SerializeReference] public BlackboardVariable<GameObject> Target;
        [SerializeReference] public BlackboardVariable<float> Speed = new BlackboardVariable<float>(1.0f);
        [SerializeReference] public BlackboardVariable<float> DistanceThreshold = new BlackboardVariable<float>(0.2f);
        [SerializeReference] public BlackboardVariable<string> AnimatorSpeedParam = new BlackboardVariable<string>("SpeedMagnitude");
        [SerializeReference] public BlackboardVariable<float> SlowDownDistance = new BlackboardVariable<float>(1.0f);
        [SerializeReference] public BlackboardVariable<TargetPositionMode> TargetMode = new(TargetPositionMode.ClosestPointOnAnyCollider);
        [SerializeReference] public BlackboardVariable<bool> FailIfUnreachable = new(true);

        private const float k_TargetMoveThreshold = 0.001f;
        private const float k_NavMeshSampleDistance = 2.0f;

        private NavMeshAgent m_NavMeshAgent;
        private Animator m_Animator;

        private GameObject m_LastTarget;
        private Vector3 m_LastTargetPosition;
        private Vector3 m_Destination;

        private float m_OriginalSpeed = -1f;
        private float m_OriginalStoppingDistance = -1f;
        private float m_CurrentSpeed;



        protected override Status OnStart()
        {

            if (Agent == null || Agent.Value == null || Target == null || Target.Value == null)
            {
                 return Status.Failure;
            }

            Status status = Initialize();
            return status;
        }

        protected override Status OnUpdate()
        {
            if (Agent == null || Agent.Value == null || Target == null || Target.Value == null)
            {
                return Status.Failure;
            }

            if (m_NavMeshAgent != null && !m_NavMeshAgent.isOnNavMesh)
            {
                return Status.Failure;
            }

            bool targetChanged = m_LastTarget != Target.Value;
            bool targetMoved = Vector3.Distance(
                m_LastTargetPosition,
                Target.Value.transform.position) > k_TargetMoveThreshold;

            // 타겟이 바뀌면 이전 경로(계산 중인 것 포함)를 버리고 새로 시작
            if (targetChanged || targetMoved)
            {
                UpdateDestination(clearPath: targetChanged);
            }

            float distance = Vector3.Distance(Agent.Value.transform.position, m_Destination);
            if (distance <= DistanceThreshold.Value)
            {
                return Status.Success;
            }

            if (m_NavMeshAgent != null)
            {
                if (!m_NavMeshAgent.pathPending && !m_NavMeshAgent.hasPath)
                {
                    // 실제 목적지까지 거리가 남았는데 경로가 없다면
                    // 도착으로 처리하지 않는다.
                    Debug.LogWarning(
                        $"[NavigateToTarget][{Agent.Value.name} -> {Target.Value.name}] " +
                        $"경로 없음! 거리={distance:F2}, " +
                        $"pathStatus={m_NavMeshAgent.pathStatus}, " +
                        $"destination={m_NavMeshAgent.destination}, " +
                        $"targetDestination={m_Destination}");

                    return Status.Failure;
                }

                m_CurrentSpeed = m_NavMeshAgent.velocity.magnitude;
            }
            else
            {
                m_CurrentSpeed = MoveWithoutNavMesh(distance);
            }

            UpdateAnimatorSpeed();
            return Status.Running;
        }

        protected override void OnEnd()
        {

            UpdateAnimatorSpeed(0f);

            RestoreAgentSettings();

            m_NavMeshAgent = null;
            m_Animator = null;
            m_LastTarget = null;
            m_CurrentSpeed = 0f;
        }
        

        protected override void OnDeserialize()
        {
            if (Agent.Value == null)
                return;

            m_NavMeshAgent = Agent.Value.GetComponentInChildren<NavMeshAgent>();

            if (m_NavMeshAgent != null && m_NavMeshAgent.isOnNavMesh)
                m_NavMeshAgent.Warp(Agent.Value.transform.position);

            Initialize();
        }

        private Status Initialize()
        {
            if (Agent == null || Agent.Value == null || Target == null || Target.Value == null)
            {
                return Status.Failure;
            }

            m_LastTarget = Target.Value;
            m_LastTargetPosition = Target.Value.transform.position;
            m_NavMeshAgent = Agent.Value.GetComponentInChildren<NavMeshAgent>();
            m_Animator = Agent.Value.GetComponentInChildren<Animator>();

            m_Destination = GetNavigableTargetPosition();

            float distance = Vector3.Distance(Agent.Value.transform.position, m_Destination);
            if (distance <= DistanceThreshold.Value)
            {
                return Status.Success;
            }

            if (m_NavMeshAgent != null)
            {
                if (!m_NavMeshAgent.isOnNavMesh)
                {
                    return Status.Failure;
                }

                // 이미 백업돼 있으면 덮어쓰지 않음 (수정된 값을 원본으로 오인 방지)
                if (m_OriginalSpeed < 0f)
                {
                    m_OriginalSpeed = m_NavMeshAgent.speed;
                    m_OriginalStoppingDistance = m_NavMeshAgent.stoppingDistance;
                }

                m_NavMeshAgent.speed = Speed.Value;
                m_NavMeshAgent.stoppingDistance = 0f;
                // m_NavMeshAgent.isStopped = false;
                // m_NavMeshAgent.ResetPath();

                bool destinationAccepted =
                    m_NavMeshAgent.SetDestination(m_Destination);

                Debug.Log(
                    $"[NavigateToTarget] SetDestination={destinationAccepted}, " +
                    $"Agent={Agent.Value.name}, Target={Target.Value.name}, " +
                    $"Destination={m_Destination}");

            }
            else
            {

            }

            UpdateAnimatorSpeed(0f);
            return Status.Running;
        }

        private void UpdateDestination(bool clearPath)
        {
            if (Target.Value == null)
                return;

            m_LastTarget = Target.Value;
            m_LastTargetPosition = Target.Value.transform.position;
            m_Destination = GetNavigableTargetPosition();

            if (m_NavMeshAgent == null || !m_NavMeshAgent.isOnNavMesh)
            {
                return;
            }

            if (clearPath)
                m_NavMeshAgent.ResetPath();

            m_NavMeshAgent.isStopped = false;
            bool destinationAccepted =
                m_NavMeshAgent.SetDestination(m_Destination);

            Debug.Log(
                $"[NavigateToTarget] SetDestination={destinationAccepted}, " +
                $"Agent={Agent.Value.name}, Target={Target.Value.name}, " +
                $"Destination={m_Destination}");
        }

        // 경로 제거 + 관성 제거. 이동 중 속도가 남아 미끄러지는 걸 막는다.
        private void StopAgent()
        {
            if (m_NavMeshAgent == null)
                return;

            Debug.LogWarning(
                $"[NavigateToTarget] StopAgent 호출\n" +
                $"Agent={Agent.Value?.name}\n" +
                $"Target={Target.Value?.name}\n" +
                $"hasPath={m_NavMeshAgent.hasPath}\n" +
                $"pathStatus={m_NavMeshAgent.pathStatus}\n" +
                $"remainingDistance={m_NavMeshAgent.remainingDistance:F2}\n" +
                $"Stack:\n{Environment.StackTrace}");

            if (!m_NavMeshAgent.isOnNavMesh)
                return;

            m_NavMeshAgent.ResetPath();
            m_NavMeshAgent.velocity = Vector3.zero;
            m_NavMeshAgent.isStopped = false;
        }

        private void RestoreAgentSettings()
        {
            if (m_NavMeshAgent != null)
            {
                if (m_OriginalSpeed >= 0f)
                    m_NavMeshAgent.speed = m_OriginalSpeed;

                if (m_OriginalStoppingDistance >= 0f)
                    m_NavMeshAgent.stoppingDistance = m_OriginalStoppingDistance;
            }

            m_OriginalSpeed = -1f;
            m_OriginalStoppingDistance = -1f;
        }

        private Vector3 GetNavigableTargetPosition()
        {
            Vector3 targetPosition = GetTargetPosition();

            // Collider.ClosestPoint can return a point on a wall, raised object,
            // or another non-walkable surface. NavMeshAgent.SetDestination then
            // receives a destination outside the baked NavMesh and never gets a
            // usable path. Keep the target's intended location, but snap only
            // the final destination to this agent's NavMesh and area mask.
            if (m_NavMeshAgent != null && m_NavMeshAgent.isOnNavMesh &&
                NavMesh.SamplePosition(
                    targetPosition,
                    out NavMeshHit hit,
                    k_NavMeshSampleDistance,
                    m_NavMeshAgent.areaMask))
            {
                return hit.position;
            }

            return targetPosition;
        }

        private Vector3 GetTargetPosition()
        {
            Collider collider = null;

            switch (TargetMode.Value)
            {
                case TargetPositionMode.ClosestPointOnAnyCollider:
                    collider = Target.Value.GetComponentInChildren<Collider>();
                    break;
                case TargetPositionMode.ClosestPointOnTargetCollider:
                    collider = Target.Value.GetComponent<Collider>();
                    break;
            }

            if (collider != null && collider.enabled)
                return collider.ClosestPoint(Agent.Value.transform.position);

            return Target.Value.transform.position;
        }

        private float MoveWithoutNavMesh(float distance)
        {
            Vector3 current = Agent.Value.transform.position;
            Vector3 direction = m_Destination - current;

            if (direction.sqrMagnitude <= 0.0001f)
                return 0f;

            float currentSpeed = Speed.Value;

            if (SlowDownDistance.Value > 0f && distance < SlowDownDistance.Value)
                currentSpeed *= Mathf.Clamp01(distance / SlowDownDistance.Value);

            Agent.Value.transform.position = Vector3.MoveTowards(
                current, m_Destination, currentSpeed * Time.deltaTime);

            return currentSpeed;
        }

        private void UpdateAnimatorSpeed(float explicitSpeed = -1f)
        {
            if (m_Animator == null)
                return;

            m_Animator.SetFloat(
                AnimatorSpeedParam.Value,
                explicitSpeed >= 0f ? explicitSpeed : m_CurrentSpeed);
        }
        
    }
}

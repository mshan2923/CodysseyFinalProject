
using System;
using Unity.Properties;
using UnityEngine;
using UnityEngine.AI;

namespace Unity.Behavior
{
    [Serializable, GeneratePropertyBag]
    [NodeDescription(
        name: "Navigate To Target",
        description: "Navigates a GameObject to a specified position using NavMeshAgent.",
        story: "[Agent] navigates to [Target]",
        category: "Action/Navigation",
        id: "7bb7e54f99524da6eed2361c84315483")]
    public class NavigateToTargetAction : Action
    {
        [SerializeReference]
        public BlackboardVariable<GameObject> Agent;

        [SerializeReference]
        public BlackboardVariable<Vector3> Target;

        [SerializeReference]
        public BlackboardVariable<float> Speed =
            new BlackboardVariable<float>(1.0f);

        [SerializeReference]
        public BlackboardVariable<float> DistanceThreshold =
            new BlackboardVariable<float>(0.2f);

        [SerializeReference]
        public BlackboardVariable<string> AnimatorSpeedParam =
            new BlackboardVariable<string>("SpeedMagnitude");

        [SerializeReference]
        public BlackboardVariable<bool> FailIfUnreachable =
            new BlackboardVariable<bool>(true);

        private NavMeshAgent m_NavMeshAgent;
        private Animator m_Animator;

        private Vector3 m_LastTargetPosition;

        [CreateProperty]
        private float m_OriginalStoppingDistance = -1f;

        [CreateProperty]
        private float m_OriginalSpeed = -1f;

        private float m_CurrentSpeed;

        protected override Status OnStart()
        {
            if (Agent.Value == null)
            {
                LogFailure("No agent provided.");
                return Status.Failure;
            }

            return Initialize();
        }

        protected override Status OnUpdate()
        {
            if (Agent.Value == null)
            {
                return Status.Failure;
            }

            Vector3 agentPosition = Agent.Value.transform.position;
            Vector3 targetPosition = Target.Value;

            float distance = Vector3.Distance(
                agentPosition,
                targetPosition
            );


            bool targetChanged =
                m_LastTargetPosition != targetPosition;

            if (targetChanged)
            {
                m_LastTargetPosition = targetPosition;
            }

            bool destinationReached =
                distance <= DistanceThreshold.Value;

            if (destinationReached &&
                (m_NavMeshAgent == null ||
                 !m_NavMeshAgent.pathPending))
            {
                return Status.Success;
            }

            if (m_NavMeshAgent == null)
            {
                return Status.Failure;
            }

            if (!m_NavMeshAgent.isOnNavMesh)
            {
                UpdateAnimatorSpeed(0f);
                return Status.Running;
            }

            if (targetChanged || !m_NavMeshAgent.hasPath)
            {
                m_LastTargetPosition = targetPosition;

                bool result = m_NavMeshAgent.SetDestination(targetPosition);

                Debug.Log(
                    $"[NavigateToTarget] SetDestination={result}, " +
                    $"Target={targetPosition}"
                );
            }
            else if (!m_NavMeshAgent.pathPending &&
                     !m_NavMeshAgent.hasPath)
            {
                if (m_NavMeshAgent.pathStatus ==
                    NavMeshPathStatus.PathPartial ||
                    m_NavMeshAgent.pathStatus ==
                    NavMeshPathStatus.PathInvalid)
                {
                    return FailIfUnreachable.Value
                        ? Status.Failure
                        : Status.Success;
                }
            }

            UpdateAnimatorSpeed();

            return Status.Running;
        }

        protected override void OnEnd()
        {
            UpdateAnimatorSpeed(0f);

            if (m_NavMeshAgent != null)
            {
                if (m_NavMeshAgent.isOnNavMesh)
                {
                    m_NavMeshAgent.ResetPath();
                }

                m_NavMeshAgent.speed = m_OriginalSpeed;
                m_NavMeshAgent.stoppingDistance =
                    m_OriginalStoppingDistance;
            }

            m_NavMeshAgent = null;
            m_Animator = null;
        }

        protected override void OnDeserialize()
        {
            if (Agent.Value != null)
            {
                m_NavMeshAgent =
                    Agent.Value.GetComponentInChildren<NavMeshAgent>();

                if (m_NavMeshAgent != null)
                {
                    if (m_OriginalSpeed >= 0f)
                        m_NavMeshAgent.speed = m_OriginalSpeed;

                    if (m_OriginalStoppingDistance >= 0f)
                        m_NavMeshAgent.stoppingDistance =
                            m_OriginalStoppingDistance;

                    if (m_NavMeshAgent.isOnNavMesh)
                    {
                        m_NavMeshAgent.Warp(
                            Agent.Value.transform.position
                        );
                    }
                }

                Initialize();
            }
        }

        private Status Initialize()
        {
            m_LastTargetPosition = Target.Value;

            float distance = Vector3.Distance(
                Agent.Value.transform.position,
                Target.Value
            );

            if (distance <= DistanceThreshold.Value)
            {
                return Status.Success;
            }

            m_NavMeshAgent =
                Agent.Value.GetComponentInChildren<NavMeshAgent>();

            if (m_NavMeshAgent == null)
            {
                LogFailure("No NavMeshAgent found.");
                return Status.Failure;
            }

            if (m_NavMeshAgent.isOnNavMesh)
            {
                m_NavMeshAgent.ResetPath();
            }

            m_OriginalSpeed = m_NavMeshAgent.speed;
            m_NavMeshAgent.speed = Speed.Value;

            m_OriginalStoppingDistance =
                m_NavMeshAgent.stoppingDistance;

            m_NavMeshAgent.stoppingDistance =
                DistanceThreshold.Value;

            if (!m_NavMeshAgent.isOnNavMesh)
            {
                UpdateAnimatorSpeed(0f);
                return Status.Running;
            }

            m_NavMeshAgent.SetDestination(
                Target.Value
            );

            m_Animator =
                Agent.Value.GetComponentInChildren<Animator>();

            UpdateAnimatorSpeed(0f);

            return Status.Running;
        }

        private void UpdateAnimatorSpeed(float explicitSpeed = -1f)
        {
            if (m_Animator == null)
                return;

            float speed = explicitSpeed >= 0f
                ? explicitSpeed
                : m_NavMeshAgent != null
                    ? m_NavMeshAgent.velocity.magnitude
                    : m_CurrentSpeed;

            if (!string.IsNullOrEmpty(AnimatorSpeedParam.Value))
            {
                m_Animator.SetFloat(
                    AnimatorSpeedParam.Value,
                    speed
                );
            }
        }
    }
}

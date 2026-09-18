using System;
using Unity.Properties;
using UnityEngine;

namespace Unity.Behavior
{
    [Serializable, GeneratePropertyBag]
    [NodeDescription(
        name: "Smooth Look At",
        description: "Rotates the Agent toward the Target at a specified speed",
        story: "Look at [Target] with [Agent] at [RotationSpeed] degrees per second",
        category: "Action/Transform",
        id: "5758956fa14e2d9bbd8e032d8b9fe486")]
    public class SmoothLookAt : Action
    {
        [SerializeReference]
        public BlackboardVariable<GameObject> Agent;

        [SerializeReference]
        public BlackboardVariable<GameObject> Target;

        [SerializeReference]
        public BlackboardVariable<float> RotationSpeed;

        protected override Status OnStart()
        {
            if (Agent.Value == null)
            {
                LogFailure("No agent provided.");
                return Status.Failure;
            }

            if (Target.Value == null)
            {
                LogFailure("No target provided.");
                return Status.Failure;
            }

            if (RotationSpeed.Value <= 0f)
            {
                LogFailure("Rotation speed must be greater than 0.");
                return Status.Failure;
            }

            return Status.Running;
        }

        protected override Status OnUpdate()
        {
            Vector3 direction =
                Target.Value.transform.position -
                Agent.Value.transform.position;

            // 수평 방향만 사용
            direction.y = 0f;

            if (direction.sqrMagnitude <= 0.0001f)
            {
                return Status.Success;
            }

            Quaternion targetRotation =
                Quaternion.LookRotation(direction);

            Agent.Value.transform.rotation =
                Quaternion.RotateTowards(
                    Agent.Value.transform.rotation,
                    targetRotation,
                    RotationSpeed.Value * Time.deltaTime
                );

            float angle =
                Quaternion.Angle(
                    Agent.Value.transform.rotation,
                    targetRotation
                );

            return angle <= 0.1f
                ? Status.Success
                : Status.Running;
        }
    }
}
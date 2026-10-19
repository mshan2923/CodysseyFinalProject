using System;
using Unity.Properties;
using UnityEngine;
using UnityEngine.AI;

namespace Unity.Behavior
{
    [Serializable, GeneratePropertyBag]
    [NodeDescription(
        name: "Set Random Position",
        description: "Sets a random position around the Agent",
        story: "Set [TargetPosition] to a random position around [Agent] within [Radius]",
        category: "Action/Position",
        id: "b5125255057c64a7933676c848ca4bd1")]
    public class SetRandomPosition : Action
    {
        [Tooltip("Agent used as the center position.")]
        [SerializeReference]
        public BlackboardVariable<GameObject> Agent;

        [Tooltip("Maximum random position radius.")]
        [SerializeReference]
        public BlackboardVariable<float> Radius;

        [Tooltip("Random position output.")]
        [SerializeReference]
        public BlackboardVariable<Vector3> TargetPosition;

        [Tooltip("Raycast height.")]
        [SerializeReference]
        public BlackboardVariable<float> RayHeight = new(10f);

        [Tooltip("Raycast distance.")]
        [SerializeReference]
        public BlackboardVariable<float> RayDistance = new(20f);


        protected override Status OnStart()
        {
            if (Agent.Value == null)
            {
                LogFailure("No agent provided.");
                return Status.Failure;
            }

            if (Radius.Value <= 0)
            {
                LogFailure("Radius must be greater than 0.");
                return Status.Failure;
            }

            Vector2 randomOffset =
                UnityEngine.Random.insideUnitCircle * Radius.Value;

            Vector3 randomPosition =
                Agent.Value.transform.position +
                new Vector3(
                    randomOffset.x,
                    RayHeight.Value,
                    randomOffset.y
                );

            if (!Physics.Raycast(
                randomPosition,
                Vector3.down,
                out RaycastHit hit,
                RayDistance.Value))
            {
                return Status.Failure;
            }

            if (!NavMesh.SamplePosition(
                hit.point,
                out NavMeshHit navHit,
                2f,
                NavMesh.AllAreas))
            {
                return Status.Failure;
            }

            NavMeshPath path = new NavMeshPath();

            if (!NavMesh.CalculatePath(
                Agent.Value.transform.position,
                navHit.position,
                NavMesh.AllAreas,
                path))
            {
                return Status.Failure;
            }

            if (path.status != NavMeshPathStatus.PathComplete)
            {
                return Status.Failure;
            }

            TargetPosition.Value = navHit.position;
            return Status.Success;

        }
    }
}
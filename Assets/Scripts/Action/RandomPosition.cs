using System;
using Unity.Properties;
using UnityEngine;

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
                    0f,
                    randomOffset.y
                );

            TargetPosition.Value = randomPosition;


            return Status.Success;
        }
    }
}
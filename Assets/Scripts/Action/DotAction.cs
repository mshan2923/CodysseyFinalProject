using System;
using Unity.Properties;
using UnityEngine;

namespace Unity.Behavior
{
    [Serializable, GeneratePropertyBag]
    [NodeDescription(
        name: "Calculate Dot",
        description: "Calculates the dot product between Agent forward and Target direction",
        story: "Dot from [Agent] to [Target] and store it in [Result]",
        category: "Action/Math",
        id: "304d989e247100f6afae42c0665502d3")]
    public class CalculateDot : Action
    {
        [SerializeReference]
        public BlackboardVariable<GameObject> Agent;

        [SerializeReference]
        public BlackboardVariable<GameObject> Target;

        [SerializeReference]
        public BlackboardVariable<float> Result;

        protected override Status OnStart()
        {
            if (Agent.Value == null || Target.Value == null)
            {
                return Status.Failure;
            }

            Vector3 forward =
                Agent.Value.transform.forward;

            Vector3 toTarget =
                Target.Value.transform.position -
                Agent.Value.transform.position;

            Result.Value = Vector3.Dot(
                forward.normalized,
                toTarget.normalized
            );

            return Status.Success;
        }
    }
}

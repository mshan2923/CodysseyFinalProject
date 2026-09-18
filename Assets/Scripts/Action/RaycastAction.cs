using System;
using Unity.Behavior;
using UnityEngine;
using Action = Unity.Behavior.Action;
using Unity.Properties;

namespace Unity.Behavior
{
    [Serializable, GeneratePropertyBag]
    [NodeDescription(
        name: "Raycast",
        description: "Checks if a ray hits an object",
        story: "Raycast from [Agent] within [Distance] and store result in [Hit]",
        category: "Action/Physics",
        id: "4ffe8bbf9232196d5d06e85e991a7009")]
    public class RaycastAction : Action
    {
        [SerializeReference]
        public BlackboardVariable<GameObject> Agent;

        [SerializeReference]
        public BlackboardVariable<float> Distance;

        [SerializeReference]
        public BlackboardVariable<Transform> Hit;
        [SerializeReference]
        public BlackboardVariable<bool> IsDrawRay;

        [SerializeReference]
        public BlackboardVariable<int> laymask = new BlackboardVariable<int>(-1);

        protected override Status OnStart()
        {
            if (Agent.Value == null)
            {
                LogFailure("No agent provided.");
                return Status.Failure;
            }

            Vector3 origin =
                Agent.Value.transform.position;

            Vector3 direction =
                Agent.Value.transform.forward;


            if (Physics.Raycast(origin, direction, out var hit, Distance.Value, laymask.Value))
            {
                Hit.Value = hit.collider.gameObject.transform;
            }else
            {
                Hit.Value = null;
            }

            if (IsDrawRay)
            {
                Debug.DrawRay(
                    origin,
                    direction * Distance.Value,
                    Hit.Value == null
                        ? Color.red
                        : Color.green,
                    1f
                );   
            }


            return Hit.Value != null ? Status.Success : Status.Failure;
        }
    }
}
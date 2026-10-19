using System;
using Unity.Mathematics;
using Unity.Properties;
using UnityEngine;

namespace Unity.Behavior
{
        [Serializable, GeneratePropertyBag]
        [NodeDescription(
            name: "Find Closest In Grid",
            description: "Finds the closest GameObject in Grid",
            story: "Find [Target] closest to [Agent] in Grid Radius : [Radius]",
            category: "Action/Find",
            id: "")]
    public class FindClosestWithGrid : Action
    {
        [Tooltip("[Out Value] If a target is found, the field is assigned with it.")]
        [SerializeReference] public BlackboardVariable<GameObject> Target;
        [SerializeReference] public BlackboardVariable<GameObject> Agent;
        [SerializeReference] public BlackboardVariable<float> Radius;
        [SerializeReference] public BlackboardVariable<float> Distace;

        
        protected override Status OnStart()
        {
            if (Agent.Value == null)
            {
                LogFailure("No agent provided.");
                return Status.Failure;
            }

            Debug.Log(
                $"[FindClosestInGrid] " +
                $"Agent: {Agent.Value.name}, " +
                $"Radius: {Radius.Value}, " +
                $"Grid Count: {SpatialGrid.Count}, " +
                $"Cell Size: {SpatialGrid.CellSize}"
            );

            Transform spatialTarget =
                SpatialGrid.FindNearest(
                    Agent.Value.transform.position,
                    Radius.Value
                );

            if (spatialTarget != null)
            {
                Target.Value =
                    spatialTarget != null
                        ? spatialTarget.gameObject
                        : null;

                Distace.Value = math.distance(Target.Value.transform.position , Agent.Value.transform.position);
                return Status.Success;
            }else
            {
                return Status.Failure;
            }
        }
    }
}

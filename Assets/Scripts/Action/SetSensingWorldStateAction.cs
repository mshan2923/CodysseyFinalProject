using System;
using SensingWorld;
using Unity.Behavior;
using Unity.Properties;
using UnityEngine;
using Action = Unity.Behavior.Action;

[Serializable, GeneratePropertyBag]
[NodeDescription(
    name: "Set Sensing World State",
    story: "Set [Target] state to [State]",
    category: "Action/SensingWorld",
    id: "b2f1e8c4d6a94703a5c9e1f7284d6b10")]
public partial class SetSensingWorldStateAction : Action
{
    [SerializeReference] public BlackboardVariable<GameObject> Target;
    [SerializeReference] public BlackboardVariable<SensingWorldEntityState> State;

    protected override Status OnStart()
    {
        if (Target?.Value == null)
        {
            Debug.LogWarning("[SensingWorld] SetSensingWorldStateAction 대상이 없습니다.");
            return Status.Failure;
        }

        SensingWorldEntity entity = Target.Value.GetComponent<SensingWorldEntity>();
        if (entity == null)
        {
            Debug.LogWarning(
                $"[SensingWorld] {Target.Value.name}에 SensingWorldEntity가 없습니다.");
            return Status.Failure;
        }

        entity.SetState(State != null
            ? State.Value
            : SensingWorldEntityState.Unknown);

        return Status.Success;
    }
}

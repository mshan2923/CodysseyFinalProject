using System;
using Unity.Behavior;
using UnityEngine;
using Action = Unity.Behavior.Action;
using Unity.Properties;

[Serializable, GeneratePropertyBag]
[NodeDescription(name: "GetTargetResolverAction", story: "Get Target : [target]", category: "Action", id: "a3c4ecf864ce3b3b6b8df10e103e259b")]
public partial class GetTargetResolverAction : Action
{
    [SerializeReference] public BlackboardVariable<LlmTargetResolver> TargetResolver;
    [SerializeReference] public BlackboardVariable<GameObject> Target;

    protected override Status OnStart()
    {
        if (TargetResolver == null || TargetResolver.Value == null)
        {
            Debug.LogWarning("[GetTargetResolverAction] TargetResolver가 null입니다.");
            return Status.Failure;
        }
        Target.Value = TargetResolver.Value.TargetObject;
        return Status.Success;
    }



    protected override void OnEnd()
    {
    }
}


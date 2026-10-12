using System;
using Unity.Behavior;
using UnityEngine;
using Action = Unity.Behavior.Action;
using Unity.Properties;
using System.Collections.Generic;

[Serializable, GeneratePropertyBag]
[NodeDescription(name: "GetTargetResolverAction", story: "Get Target : [target] And Random Action", category: "Action", id: "a3c4ecf864ce3b3b6b8df10e103e259b")]
public partial class GetTargetResolverAction : Action
{
    [SerializeReference] public BlackboardVariable<LlmTargetResolver> TargetResolver;
    [SerializeReference] public BlackboardVariable<GameObject> Target;

    [SerializeReference] public BlackboardVariable<List<string>> ActionList;
    [SerializeReference] public BlackboardVariable<string> Action;


    protected override Status OnStart()
    {
        if (TargetResolver == null || TargetResolver.Value == null)
        {
            Debug.LogWarning("[GetTargetResolverAction] TargetResolver가 null입니다.");
            return Status.Failure;
        }
        Target.Value = TargetResolver.Value.TargetObject;

        if (ActionList == null || ActionList.Value == null || ActionList.Value.Count == 0)
        {
            Debug.LogWarning("[GetTargetResolverAction] ActionList가 null이거나 비어있습니다.");
            return Status.Failure;
        }

        if (string.IsNullOrEmpty(Action.Value))
        {
            int randomIndex = UnityEngine.Random.Range(0, ActionList.Value.Count);
            Action.Value = ActionList.Value[randomIndex];
        }
        //SensingWorldEventType

        return Status.Success;
    }



    protected override void OnEnd()
    {
    }
}


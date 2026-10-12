using System;
using Unity.Behavior;
using UnityEngine;
using Action = Unity.Behavior.Action;
using Unity.Properties;

[Serializable, GeneratePropertyBag]
[NodeDescription(name: "SetString", story: "[value] : [target]", category: "Action", id: "9c67c186d69ee740b964f4b6e58ce865")]
public partial class SetStringAction : Action
{
    [SerializeReference] public BlackboardVariable<string> Value;
    [SerializeReference] public BlackboardVariable<string> Target;

    protected override Status OnStart()
    {
        Value.Value = Target.Value;
        return Status.Success;
    }

}


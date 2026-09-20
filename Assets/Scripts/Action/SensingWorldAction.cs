using System;
using Unity.Behavior;
using UnityEngine;
using Action = Unity.Behavior.Action;
using Unity.Properties;
using SensingWorld;

[Serializable, GeneratePropertyBag]
[NodeDescription(name: "SensingWorldAction", story: "Get Sensing : [Result]", category: "Action", id: "a59c8caeaba609a51337c8d4d48d845b")]
public partial class SensingWorldAction : Action
{
    [SerializeReference] public BlackboardVariable<string> Result;

    protected override Status OnStart()
    {
        Result.Value =  SensingWorldState.Instance.BuildContext();
        return Status.Success;
    }

    // protected override Status OnUpdate()
    // {
    //     return Status.Success;
    // }

    // protected override void OnEnd()
    // {
    // }
}


using System;
using Unity.Behavior;
using UnityEngine;
using Action = Unity.Behavior.Action;
using Unity.Properties;

[Serializable, GeneratePropertyBag]
[NodeDescription(name: "Append", story: "[TextA] Append [TextB] = [Result]", category: "Action", id: "d354d3cdd4728a5d6096d57d68bfc7b4")]
public partial class AppendAction : Action
{
    [SerializeReference] public BlackboardVariable<string> TextA;
    [SerializeReference] public BlackboardVariable<string> TextB;
    [SerializeReference] public BlackboardVariable<string> Result;

    protected override Status OnStart()
    {
        Result.Value = String.Join(",", TextA.Value ,  TextB.Value);
                return Status.Success;
    }

    protected override Status OnUpdate()
    {
        return Status.Success;
    }

    protected override void OnEnd()
    {
    }
}


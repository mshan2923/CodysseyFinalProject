using System;
using System.Collections.Generic;
using Unity.Behavior;
using UnityEngine;
using Action = Unity.Behavior.Action;
using Unity.Properties;
using UnityEngine.UIElements;

[Serializable, GeneratePropertyBag]
[NodeDescription(name: "PickRandomAction", story: "Random in [list] => [Value]", category: "Action", id: "3f60d3c91265465ad2715d3b78553c5e")]
public partial class PickRandomAction : Action
{
    [SerializeReference] public BlackboardVariable<List<string>> List;

            [SerializeReference] public BlackboardVariable<string> Value;

    protected override Status OnStart()
    {

        int random = UnityEngine.Random.Range(0, List.Value.Count);
        Value.Value = List.Value[random];

        return Status.Success;
    }

}


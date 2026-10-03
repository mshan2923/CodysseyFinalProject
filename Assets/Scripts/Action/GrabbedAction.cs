using System;
using Unity.Behavior;
using UnityEngine;
using Action = Unity.Behavior.Action;
using Unity.Properties;
using SensingWorld;

[Serializable, GeneratePropertyBag]
[NodeDescription(name: "GrabbedAction", story: "IsGrab : [Self]", category: "Action", id: "bd23bbd9431520f30f96a7c8fa7fb4fb")]
public partial class GrabbedAction : Action
{
    [SerializeReference] public BlackboardVariable<GrabbableObject> Self;

    [SerializeReference] public BlackboardVariable<bool> IsGrabbed;

    protected override Status OnStart()
    {
        if (Self == null || Self.Value == null)
        {
            Debug.LogWarning("GrabbedAction: Self is not set or is null.");
            return Status.Failure;
        }

        if (!Self.Value.IsHeld)
        {
            return Status.Failure;
        }

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


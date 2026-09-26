using System;
using Unity.Behavior;
using UnityEngine;
using Action = Unity.Behavior.Action;
using Unity.Properties;
using SensingWorld;

[Serializable, GeneratePropertyBag]
[NodeDescription(name: "CallWorldEventAction", story: "Event : [EventType]", category: "Action", id: "64dd9d96b91ede2ed5af6e37a8cebc56")]
public partial class CallWorldEventAction : Action
{

    [SerializeReference]
    public BlackboardVariable<SensingWorldEventType> EventType;

    [SerializeReference]
    public BlackboardVariable<GameObject> self;
    [SerializeReference]
    public BlackboardVariable<GameObject> target;

    protected override Status OnStart()
    {
        var eventBusHost = SensingWorldEventBusHost.Instance;

                eventBusHost.Publish(new SensingWorldEvent(
                    EventType.Value,
                    self.Value,
                    target.Value,
                    Time.time,
                    self.Value.transform.position,
                    target.Value.tag,
                    0,0,0
                ));


        return Status.Success;
    }

}


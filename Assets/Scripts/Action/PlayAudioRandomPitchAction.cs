using System;
using Unity.Behavior;
using UnityEngine;
using Action = Unity.Behavior.Action;
using Unity.Properties;

[Serializable, GeneratePropertyBag]
[NodeDescription(
    name: "PlayAudioRandomPitch",
    story: "[Self] : [Audio]",
    category: "Action/Resource",
    id: "addf0b3cdc061287b217603a8d64a48e"
)]
public partial class PlayAudioRandomPitchAction : Action
{
    [SerializeReference] public BlackboardVariable<AudioSource> Self;
    [SerializeReference] public BlackboardVariable<AudioClip> Audio;

    [SerializeReference] public BlackboardVariable<float> minPitch = new BlackboardVariable<float>(0.8f);
    [SerializeReference] public BlackboardVariable<float> maxPitch = new BlackboardVariable<float>(1.2f);

    protected override Status OnStart()
    {
        if (Self.Value == null || Audio.Value == null)
            return Status.Failure;
        if (Self.Value.enabled == false)
            Self.Value.enabled = true;

        Self.Value.pitch = UnityEngine.Random.Range(minPitch.Value, maxPitch.Value);
        Self.Value.PlayOneShot(Audio.Value);

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
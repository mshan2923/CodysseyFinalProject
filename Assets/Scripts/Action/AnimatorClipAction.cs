using System;
using Unity.Behavior;
using UnityEngine;
using Unity.Properties;
using Action = Unity.Behavior.Action;

[Serializable, GeneratePropertyBag]
[NodeDescription(name: "Animator Clip", story: "Animation : [Clip]", category: "Action/Animation", id: "d5520fe53ec35696356c49d3f08ebd90")]
public partial class AnimatorClipAction : Action
{
    [SerializeReference] public BlackboardVariable<string> Clip;
    [SerializeReference] public BlackboardVariable<float> FadeTime = new BlackboardVariable<float>(0.1f);
    [SerializeReference] public BlackboardVariable<bool> WaitForEnd = new BlackboardVariable<bool>(true);

    private Animator _animator;
    private int _hash;

    protected override Status OnStart()
    {
        if (_animator == null)
            _animator = GameObject.GetComponentInChildren<Animator>();
        if (_animator == null)
            return Status.Failure;

        _hash = Animator.StringToHash(Clip.Value);
        if (!_animator.HasState(0, _hash))
        {
            Debug.LogWarning($"[AnimatorClipAction] State '{Clip.Value}' not found.", GameObject);
            return Status.Failure;
        }

        _animator.CrossFadeInFixedTime(_hash, FadeTime.Value);
        return WaitForEnd.Value ? Status.Running : Status.Success;
    }

    protected override Status OnUpdate()
    {
        // CrossFade는 다음 Animator 업데이트에 반영되므로, 전환 중이면 next 상태를 본다
        var info = _animator.IsInTransition(0)
            ? _animator.GetNextAnimatorStateInfo(0)
            : _animator.GetCurrentAnimatorStateInfo(0);

        if (info.shortNameHash != _hash)
            return Status.Running; // 아직 목표 상태로 진입 전

        return info.normalizedTime >= 1f ? Status.Success : Status.Running;
    }
}
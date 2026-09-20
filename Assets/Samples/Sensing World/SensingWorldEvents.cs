using Codyssey.Core.Events;
using UnityEngine;

namespace SensingWorld
{
    public enum SensingWorldEventType
    {
        TargetVisible,
        TargetLost,
        ObjectPickedUp,
        ObjectDropped,
        Hit,
        TargetLookStarted,
        TargetLookEnded,

        // TODO: NPC Health 시스템이 추가되면 실제 HP 변경 지점에서 발행한다.
        // HP 감소 원인을 공격으로 해석하지 않고, 이전 값과 현재 값을 사실로 기록한다.
        HPChanged
    }

    public enum SensingWorldEventCategory
    {
        PlayerAction,
        WorldEvent
    }

    public enum SensingWorldEventChannel
    {
        Vision,
        WorldEvent
    }

    public readonly struct SensingWorldEvent : IGameEvent
    {
        // HPChanged 연결 시 아래 이벤트 데이터에 이전 HP와 현재 HP를 추가하는 것을 고려한다.
        // 의미 판단은 이 구조체에서 하지 않고 Local LLM이 Hit와 시간 순서로 해석한다.
        public readonly SensingWorldEventType Type;
        public readonly SensingWorldEventCategory Category;
        public readonly SensingWorldEventChannel Channel;
        public readonly GameObject Actor;
        public readonly string ActorName;
        public readonly GameObject Target;
        public readonly string TargetName;
        public readonly string TargetTag;
        public readonly float Timestamp;
        public readonly Vector3 Position;
        public readonly float Distance;
        public readonly float FacingDot;
        public readonly float InteractionScore;

        public SensingWorldEvent(
            SensingWorldEventType type,
            GameObject actor,
            GameObject target,
            float timestamp,
            Vector3 position,
            string targetTag,
            float distance,
            float facingDot,
            float interactionScore,
            SensingWorldEventCategory? category = null,
            SensingWorldEventChannel? channel = null)
        {
            Type = type;
            Category = category ?? GetDefaultCategory(type);
            Channel = channel ?? GetDefaultChannel(type);
            Actor = actor;
            ActorName = GetDisplayName(actor, "None");
            Target = target;
            TargetName = GetDisplayName(target, "None");
            TargetTag = targetTag;
            Timestamp = timestamp;
            Position = position;
            Distance = distance;
            FacingDot = facingDot;
            InteractionScore = interactionScore;
        }

        private static string GetDisplayName(GameObject entityObject, string fallback)
        {
            if (entityObject == null)
                return fallback;

            SensingWorldEntity entity = entityObject.GetComponent<SensingWorldEntity>();
            if (entity != null && !string.IsNullOrWhiteSpace(entity.DisplayName))
                return entity.DisplayName;

            return entityObject.name;
        }

        private static SensingWorldEventCategory GetDefaultCategory(SensingWorldEventType type)
        {
            return type == SensingWorldEventType.ObjectPickedUp ||
                   type == SensingWorldEventType.ObjectDropped
                ? SensingWorldEventCategory.PlayerAction
                : SensingWorldEventCategory.WorldEvent;
        }

        private static SensingWorldEventChannel GetDefaultChannel(SensingWorldEventType type)
        {
            return type == SensingWorldEventType.TargetVisible ||
                   type == SensingWorldEventType.TargetLost ||
                   type == SensingWorldEventType.TargetLookStarted ||
                   type == SensingWorldEventType.TargetLookEnded
                ? SensingWorldEventChannel.Vision
                : SensingWorldEventChannel.WorldEvent;
        }
    }
}

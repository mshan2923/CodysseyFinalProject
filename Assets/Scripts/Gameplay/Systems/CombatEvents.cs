using Codyssey.Core.Events;
using Codyssey.Gameplay.Entities;

namespace Codyssey.Gameplay.Systems
{
    public enum RequestRejectionReason
    {
        InvalidDamage,
        AttackerNotFound,
        TargetNotFound
    }

    public readonly struct RequestRejected : IGameEvent
    {
        public readonly EntityId AttackerId;
        public readonly EntityId TargetId;
        public readonly RequestRejectionReason Reason;

        public RequestRejected(EntityId attackerId, EntityId targetId, RequestRejectionReason reason)
        {
            AttackerId = attackerId;
            TargetId = targetId;
            Reason = reason;
        }
    }

    public readonly struct AttackPerformed : IGameEvent
    {
        public readonly EntityId AttackerId;
        public readonly EntityId TargetId;

        public AttackPerformed(EntityId attackerId, EntityId targetId)
        {
            AttackerId = attackerId;
            TargetId = targetId;
        }
    }

    public readonly struct DamageApplied : IGameEvent
    {
        public readonly EntityId SourceId;
        public readonly EntityId TargetId;
        public readonly float Amount;
        public readonly float RemainingHealth;

        public DamageApplied(EntityId sourceId, EntityId targetId, float amount, float remainingHealth)
        {
            SourceId = sourceId;
            TargetId = targetId;
            Amount = amount;
            RemainingHealth = remainingHealth;
        }
    }
}

using System.Collections.Generic;
using Codyssey.Core.Commands;
using Codyssey.Core.Events;
using Codyssey.Gameplay.Entities;

namespace Codyssey.Gameplay.Systems
{
    /// <summary>Example gameplay logic: validates an attack, changes state, then publishes facts.</summary>
    public sealed class CombatSystem : IRequestHandler<AttackRequest>
    {
        private readonly GameEventBus eventBus;
        private readonly IReadOnlyDictionary<EntityId, GameEntity> entities;

        public CombatSystem(GameEventBus eventBus, IReadOnlyDictionary<EntityId, GameEntity> entities)
        {
            this.eventBus = eventBus;
            this.entities = entities;
        }

        public void Handle(AttackRequest request)
        {
            if (request.Damage <= 0)
            {
                Reject(request, RequestRejectionReason.InvalidDamage);
                return;
            }

            if (!entities.ContainsKey(request.AttackerId))
            {
                Reject(request, RequestRejectionReason.AttackerNotFound);
                return;
            }

            if (!entities.TryGetValue(request.TargetId, out var target))
            {
                Reject(request, RequestRejectionReason.TargetNotFound);
                return;
            }

            eventBus.Publish(new AttackPerformed(request.AttackerId, request.TargetId));
            var applied = target.ApplyDamage(request.Damage);
            eventBus.Publish(new DamageApplied(
                request.AttackerId,
                request.TargetId,
                applied,
                target.Health));
        }

        private void Reject(AttackRequest request, RequestRejectionReason reason)
        {
            eventBus.Publish(new RequestRejected(request.AttackerId, request.TargetId, reason));
        }
    }
}

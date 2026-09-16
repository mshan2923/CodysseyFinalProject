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
            if (request.Damage <= 0 ||
                !entities.ContainsKey(request.AttackerId) ||
                !entities.TryGetValue(request.TargetId, out var target))
                return;

            eventBus.Publish(new AttackPerformed(request.AttackerId, request.TargetId));
            var applied = target.ApplyDamage(request.Damage);
            eventBus.Publish(new DamageApplied(
                request.AttackerId,
                request.TargetId,
                applied,
                target.Health));
        }
    }
}

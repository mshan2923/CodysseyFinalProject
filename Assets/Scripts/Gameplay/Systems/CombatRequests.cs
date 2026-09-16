using Codyssey.Core.Commands;
using Codyssey.Gameplay.Entities;

namespace Codyssey.Gameplay.Systems
{
    public readonly struct AttackRequest : IRequest
    {
        public readonly EntityId AttackerId;
        public readonly EntityId TargetId;
        public readonly int Damage;

        public AttackRequest(EntityId attackerId, EntityId targetId, int damage)
        {
            AttackerId = attackerId;
            TargetId = targetId;
            Damage = damage;
        }
    }
}

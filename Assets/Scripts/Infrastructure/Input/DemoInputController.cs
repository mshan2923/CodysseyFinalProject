using Codyssey.Core.Commands;
using Codyssey.Gameplay.Entities;
using Codyssey.Gameplay.Systems;
using UnityEngine;
using GameplayEntityId = Codyssey.Gameplay.Entities.EntityId;

namespace Codyssey.Infrastructure.Input
{
    /// <summary>Unity input adapter only. It creates a request and knows no combat rules.</summary>
    public sealed class DemoInputController : MonoBehaviour
    {
        private RequestDispatcher dispatcher;
        private GameplayEntityId attackerId;
        private GameplayEntityId targetId;
        private float damage;

        public void Initialize(RequestDispatcher dispatcher, GameplayEntityId attackerId, GameplayEntityId targetId, float damage)
        {
            this.dispatcher = dispatcher;
            this.attackerId = attackerId;
            this.targetId = targetId;
            this.damage = damage;
        }

        private void Update()
        {
            var pressed = UnityEngine.Input.GetKeyDown(KeyCode.Space);
            if (!pressed)
                return;

            // Debug.Log($"dispatcher : {dispatcher != null} / Pressed / {dispatcher?.Count ?? 0}");
            if (dispatcher == null)
                return;

            var sent = dispatcher.Send(new AttackRequest(attackerId, targetId, damage));
            // Debug.Log($"AttackRequest sent: {sent}");
        }
    }
}

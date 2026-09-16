using System;
using Codyssey.Core.Events;
using Codyssey.Gameplay.Systems;
using UnityEngine;

namespace Codyssey.Infrastructure.Debugging
{
    /// <summary>Development-only presentation/debug subscriber.</summary>
    public sealed class EventDebugLogger : IDisposable
    {
        private readonly IDisposable damageSubscription;

        public EventDebugLogger(GameEventBus eventBus)
        {
            damageSubscription = eventBus.Subscribe<DamageApplied>(OnDamageApplied);
        }

        private static void OnDamageApplied(DamageApplied gameEvent)
        {
            Debug.Log($"[Event] DamageApplied: {gameEvent.SourceId} -> {gameEvent.TargetId}, " +
                      $"amount={gameEvent.Amount}, remainingHealth={gameEvent.RemainingHealth}");
        }

        public void Dispose() => damageSubscription.Dispose();
    }
}
